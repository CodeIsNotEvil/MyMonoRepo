using ShoppingManager.Domain.Model;
using ShoppingManager.Infrastructure.Abstractions;

namespace ShoppingManager.Application;

public interface IUserService
{
    Task<User> CreateUserAsync(string name, string email);
    Task<User> GetUserAsync(Guid userId);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> UpdateUserAsync(Guid userId, string name, string email);
    Task DeleteUserAsync(Guid userId);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<User> CreateUserAsync(string name, string email)
    {
        var user = new User
        {
            Name = name,
            Email = email
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return user;
    }

    public async Task<User> GetUserAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }
        return user;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _unitOfWork.Users.GetAllAsync();
    }

    public async Task<User> UpdateUserAsync(Guid userId, string name, string email)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user is null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found.");
        }

        user.Name = name;
        user.Email = email;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return user;
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        await _unitOfWork.Users.DeleteAsync(userId);
        await _unitOfWork.SaveChangesAsync();
    }
}
