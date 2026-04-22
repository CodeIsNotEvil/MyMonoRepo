using ShoppingManager.Domain.Model;

namespace ShoppingManager.Application;

public interface IPaymentService
{
    Task<Payment> CreatePaymentAsync(decimal amount, Guid userId, DateTime? date = null);
    Task<Payment> GetPaymentAsync(Guid paymentId);
    Task<IEnumerable<Payment>> GetUserPaymentsAsync(Guid userId);
}

public class PaymentService : IPaymentService
{
    public Task<Payment> CreatePaymentAsync(decimal amount, Guid userId, DateTime? date = null)
    {
        var payment = new Payment
        {
            Amount = amount,
            UserId = userId,
            Date = date ?? DateTime.UtcNow
        };

        return Task.FromResult(payment);
    }

    public Task<Payment> GetPaymentAsync(Guid paymentId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }

    public Task<IEnumerable<Payment>> GetUserPaymentsAsync(Guid userId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }
}
