namespace SampleRepo;

public class PaymentService
{
    private readonly IPaymentGateway _gateway;
    private readonly ILogger _logger;

    public PaymentService(IPaymentGateway gateway, ILogger logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<decimal> ProcessPaymentAsync(IReadOnlyList<int> amounts)
    {
        // Defect: missing ConfigureAwait(false).
        bool ok = await _gateway.ChargeAsync(amounts.Sum());

        // Defect: LINQ closure allocation (captured 'threshold').
        int threshold = 100;
        var large = amounts.Where(a => a > threshold).ToList();

        // Defect: implicit boxing of a value type.
        object boxed = large.Count;
        _logger.Log("processed " + boxed.ToString());

        // Defect: unawaited task (fire-and-forget).
        NotifyAsync();

        return ok ? large.Count : 0m;
    }

    private Task NotifyAsync() => Task.CompletedTask;
}
