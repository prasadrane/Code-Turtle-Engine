using System.Globalization;
using Shop.Domain;

namespace Shop.Services;

public sealed class ReportService
{
    private readonly OrderRepository _orders;

    public ReportService(OrderRepository orders) => _orders = orders;

    public async Task<decimal> TotalAboveAsync(string customerId, decimal threshold)
    {
        var orders = await _orders.FindByCustomerAsync(customerId).ConfigureAwait(false);
        decimal total = 0;
        foreach (var o in orders)
            if (o.Total > threshold) total += o.Total;
        return total;
    }

    public string Describe(decimal total) =>
        string.Format(CultureInfo.InvariantCulture, "Total: {0}", total); // BENCH: BoxingAllocation
}
