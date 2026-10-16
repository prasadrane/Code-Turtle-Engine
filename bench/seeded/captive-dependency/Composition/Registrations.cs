using Shop.Infrastructure;
using Shop.Services;

namespace Shop.Composition;

public static class Registrations
{
    public static void Register(ServiceRegistry registry)
    {
        registry.AddScoped<OrderRepository, OrderRepository>();
        registry.AddSingleton<ReportService, ReportService>(); // BENCH: CaptiveDependency
        registry.AddSingleton<PaymentService, PaymentService>();
    }
}
