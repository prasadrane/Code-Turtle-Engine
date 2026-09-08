namespace SampleRepo;

public interface IPaymentGateway
{
    Task<bool> ChargeAsync(int amountCents);
}
