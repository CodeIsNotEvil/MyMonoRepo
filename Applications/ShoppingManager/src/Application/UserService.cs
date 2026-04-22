using ShoppingManager.Domain.Model;

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
    public Task<User> CreateUserAsync(string name, string email)
    {
        var user = new User
        {
            Name = name,
            Email = email
        };

        return Task.FromResult(user);
    }

    public Task<User> GetUserAsync(Guid userId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }

    public Task<IEnumerable<User>> GetAllUsersAsync()
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }

    public Task<User> UpdateUserAsync(Guid userId, string name, string email)
    {
        var user = new User
        {
            Id = userId,
            Name = name,
            Email = email
        };

        return Task.FromResult(user);
    }

    public Task DeleteUserAsync(Guid userId)
    {
        // This will be implemented with repository pattern
        throw new NotImplementedException();
    }
}
