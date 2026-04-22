using ShoppingManager.Domain.Model;
using ShoppingManager.Infrastructure.Abstractions;

namespace ShoppingManager.Application;

public interface IPaymentService
{
    Task<Payment> CreatePaymentAsync(decimal amount, Guid userId, DateTime? date = null);
    Task<Payment> GetPaymentAsync(Guid paymentId);
    Task<IEnumerable<Payment>> GetUserPaymentsAsync(Guid userId);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Payment> CreatePaymentAsync(decimal amount, Guid userId, DateTime? date = null)
    {
        // Verify user exists
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }

        var payment = new Payment
        {
            Amount = amount,
            UserId = userId,
            Date = date ?? DateTime.UtcNow
        };

        await _unitOfWork.Payments.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        return payment;
    }

    public async Task<Payment> GetPaymentAsync(Guid paymentId)
    {
        var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
        if (payment is null)
        {
            throw new KeyNotFoundException($"Payment with ID {paymentId} not found.");
        }
        return payment;
    }

    public async Task<IEnumerable<Payment>> GetUserPaymentsAsync(Guid userId)
    {
        var payments = await _unitOfWork.Payments.GetAllAsync();
        return payments.Where(p => p.UserId == userId);
    }
}
