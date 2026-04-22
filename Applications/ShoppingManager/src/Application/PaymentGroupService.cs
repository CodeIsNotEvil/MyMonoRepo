using ShoppingManager.Domain.Model;
using ShoppingManager.Infrastructure.Abstractions;

namespace ShoppingManager.Application;

public interface IPaymentGroupService
{
    Task<PaymentGroup> CreatePaymentGroupAsync(string name);
    Task<PaymentGroup> GetPaymentGroupAsync(Guid paymentGroupId);
    Task<IEnumerable<PaymentGroup>> GetAllPaymentGroupsAsync();
    Task AddUserToGroupAsync(Guid paymentGroupId, Guid userId);
    Task DeletePaymentGroupAsync(Guid paymentGroupId);
}

public class PaymentGroupService : IPaymentGroupService
{
    private readonly IUnitOfWork _unitOfWork;

    public PaymentGroupService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<PaymentGroup> CreatePaymentGroupAsync(string name)
    {
        var paymentGroup = new PaymentGroup
        {
            Name = name
        };

        await _unitOfWork.PaymentGroups.AddAsync(paymentGroup);
        await _unitOfWork.SaveChangesAsync();

        return paymentGroup;
    }

    public async Task<PaymentGroup> GetPaymentGroupAsync(Guid paymentGroupId)
    {
        var paymentGroup = await _unitOfWork.PaymentGroups.GetByIdAsync(paymentGroupId);
        if (paymentGroup is null)
        {
            throw new KeyNotFoundException($"PaymentGroup with ID {paymentGroupId} not found.");
        }
        return paymentGroup;
    }

    public async Task<IEnumerable<PaymentGroup>> GetAllPaymentGroupsAsync()
    {
        return await _unitOfWork.PaymentGroups.GetAllAsync();
    }

    public async Task AddUserToGroupAsync(Guid paymentGroupId, Guid userId)
    {
        var paymentGroup = await _unitOfWork.PaymentGroups.GetByIdAsync(paymentGroupId);
        if (paymentGroup is null)
        {
            throw new KeyNotFoundException($"PaymentGroup with ID {paymentGroupId} not found.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }

        if (!paymentGroup.Users.Contains(user))
        {
            paymentGroup.Users.Add(user);
            await _unitOfWork.PaymentGroups.UpdateAsync(paymentGroup);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task DeletePaymentGroupAsync(Guid paymentGroupId)
    {
        await _unitOfWork.PaymentGroups.DeleteAsync(paymentGroupId);
        await _unitOfWork.SaveChangesAsync();
    }
}
