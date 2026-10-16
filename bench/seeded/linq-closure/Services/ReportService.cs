using Shop.Domain;

namespace Shop.Services;

public sealed class ReportService
{
    private readonly OrderRepository _orders;

    public ReportService(OrderRepository orders) => _orders = orders;

    public async Task<decimal> TotalAboveAsync(string customerId, decimal threshold)
    {
        var orders = await _orders.FindByCustomerAsync(customerId).ConfigureAwait(false);
        var minimum = threshold;
        return orders.Where(o => o.Total > minimum).Sum(o => o.Total); // BENCH: LinqClosureCapture
    }
}
