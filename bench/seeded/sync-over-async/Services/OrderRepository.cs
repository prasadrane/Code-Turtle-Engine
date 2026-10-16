using Shop.Domain;
using Shop.Infrastructure;

namespace Shop.Services;

public sealed class OrderRepository
{
    private readonly IDbExecutor _db;

    public OrderRepository(IDbExecutor db) => _db = db;

    public async Task<IReadOnlyList<Order>> FindByCustomerAsync(string customerId)
    {
        var parameters = new Dictionary<string, object?> { ["id"] = customerId };
        return await _db.QueryOrdersAsync("SELECT * FROM Orders WHERE CustomerId = @id", parameters).ConfigureAwait(false);
    }

    public IReadOnlyList<Order> FindByCustomer(string customerId) =>
        FindByCustomerAsync(customerId).Result; // BENCH: SyncOverAsync

    public Task<int> DeleteAsync(int id) =>
        _db.ExecuteAsync("DELETE FROM Orders WHERE Id = @id", new Dictionary<string, object?> { ["id"] = id });
}
