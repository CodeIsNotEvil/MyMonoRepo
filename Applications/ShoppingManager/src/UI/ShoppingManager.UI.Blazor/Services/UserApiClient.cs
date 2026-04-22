using System.Net.Http.Json;

namespace ShoppingManager.UI.Blazor.Services;

public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class UserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public interface IUserApiClient
{
    Task<UserResponse> CreateUserAsync(CreateUserRequest request);
    Task<UserResponse> GetUserAsync(Guid id);
    Task<IEnumerable<UserResponse>> GetAllUsersAsync();
    Task<UserResponse> UpdateUserAsync(Guid id, UpdateUserRequest request);
    Task DeleteUserAsync(Guid id);
}

public class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;

    public UserApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        HttpResponseMessage? response = await _httpClient.PostAsJsonAsync("api/users", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<UserResponse> GetUserAsync(Guid id)
    {
        HttpResponseMessage? response = await _httpClient.GetAsync($"api/users/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<IEnumerable<UserResponse>> GetAllUsersAsync()
    {
        HttpResponseMessage? response = await _httpClient.GetAsync("api/users");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<UserResponse>>();
    }

    public async Task<UserResponse> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        HttpResponseMessage? response = await _httpClient.PutAsJsonAsync($"api/users/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        HttpResponseMessage? response = await _httpClient.DeleteAsync($"api/users/{id}");
        response.EnsureSuccessStatusCode();
    }
}
