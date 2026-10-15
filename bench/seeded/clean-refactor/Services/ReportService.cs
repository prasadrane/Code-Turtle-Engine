using Shop.Domain;

namespace Shop.Services;

public sealed class ReportService
{
    private readonly OrderRepository _orders;

    public ReportService(OrderRepository orders) => _orders = orders;

    public async Task<decimal> TotalAboveAsync(string customerId, decimal threshold)
    {
        var orders = await _orders.FindByCustomerAsync(customerId).ConfigureAwait(false);
        decimal sum = 0;
        foreach (var order in orders)
            if (order.Total > threshold) sum += order.Total;
        return sum;
    }
}
