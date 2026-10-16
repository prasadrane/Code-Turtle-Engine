using Shop.Domain;
using Shop.Infrastructure;

namespace Shop.Services;

public sealed class PaymentService
{
    private readonly IPaymentGateway _gateway;
    private readonly IAuditLog _audit;

    public PaymentService(IPaymentGateway gateway, IAuditLog audit)
    {
        _gateway = gateway;
        _audit = audit;
    }

    public async Task<bool> PayAsync(Order order)
    {
        var ok = await _gateway.ChargeAsync(order.CustomerId, order.Total); // BENCH: MissingConfigureAwait
        await _audit.WriteAsync($"Charged order {order.Id}").ConfigureAwait(false);
        return ok;
    }
}
