using Shop.Domain;
using Shop.Infrastructure;

namespace Shop.Services;

public sealed class OrderRepository
{
    private readonly IDbExecutor _db;

    public OrderRepository(IDbExecutor db) => _db = db;

    public async Task<IReadOnlyList<Order>> FindByCustomerAsync(string customerId)
    {
        var sql = "SELECT * FROM Orders WHERE CustomerId = '" + customerId + "'"; // BENCH: SqlInjection
        return await _db.QueryOrdersAsync(sql).ConfigureAwait(false);
    }

    public Task<int> DeleteAsync(int id) =>
        _db.ExecuteAsync("DELETE FROM Orders WHERE Id = @id", new Dictionary<string, object?> { ["id"] = id });
}
