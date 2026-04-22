using ShoppingManager.Domain.Model;

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
    public Task<PaymentGroup> CreatePaymentGroupAsync(string name)
    {
        var paymentGroup = new PaymentGroup
        {
            Name = name
        };

        return Task.FromResult(paymentGroup);
    }

    public Task<PaymentGroup> GetPaymentGroupAsync(Guid paymentGroupId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }

    public Task<IEnumerable<PaymentGroup>> GetAllPaymentGroupsAsync()
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }

    public Task AddUserToGroupAsync(Guid paymentGroupId, Guid userId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }

    public Task DeletePaymentGroupAsync(Guid paymentGroupId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }
}
