using Shop.Infrastructure;
using Shop.Services;

namespace Shop.Composition;

public static class Registrations
{
    public static void Register(ServiceRegistry registry)
    {
        registry.AddScoped<OrderRepository, OrderRepository>();
        registry.AddScoped<ReportService, ReportService>();
        registry.AddSingleton<PaymentService, PaymentService>();
    }
}
