using Shop.Domain;

namespace Shop.Infrastructure;

public interface IDbExecutor
{
    Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null);
    Task<IReadOnlyList<Order>> QueryOrdersAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null);
}

public interface IPaymentGateway
{
    Task<bool> ChargeAsync(string customerId, decimal amount);
}

public interface IAuditLog
{
    Task WriteAsync(string message);
}
