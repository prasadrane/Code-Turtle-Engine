using Shop.Domain;

namespace Shop.Services;

public sealed class ReportService
{
    private readonly IServiceProvider _services;

    public ReportService(IServiceProvider services) => _services = services;

    public async Task<decimal> TotalAboveAsync(string customerId, decimal threshold)
    {
        var orders = (OrderRepository)_services.GetService(typeof(OrderRepository))!; // BENCH: ServiceLocator
        var list = await orders.FindByCustomerAsync(customerId).ConfigureAwait(false);
        decimal total = 0;
        foreach (var o in list)
            if (o.Total > threshold) total += o.Total;
        return total;
    }
}
