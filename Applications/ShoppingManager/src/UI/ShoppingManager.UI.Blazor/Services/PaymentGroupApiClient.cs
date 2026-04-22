using System.Net.Http.Json;

namespace ShoppingManager.UI.Blazor.Services;

public class CreatePaymentGroupRequest
{
    public string Name { get; set; } = string.Empty;
}

public class AddUserToGroupRequest
{
    public Guid UserId { get; set; }
}

public class PaymentGroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Guid> UserIds { get; set; } = new List<Guid>();
    public ICollection<Guid> PaymentIds { get; set; } = new List<Guid>();
}

public interface IPaymentGroupApiClient
{
    Task<PaymentGroupResponse> CreatePaymentGroupAsync(CreatePaymentGroupRequest request);
    Task<PaymentGroupResponse> GetPaymentGroupAsync(Guid id);
    Task<IEnumerable<PaymentGroupResponse>> GetAllPaymentGroupsAsync();
    Task AddUserToGroupAsync(Guid paymentGroupId, AddUserToGroupRequest request);
    Task DeletePaymentGroupAsync(Guid id);
}

public class PaymentGroupApiClient : IPaymentGroupApiClient
{
    private readonly HttpClient _httpClient;

    public PaymentGroupApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentGroupResponse> CreatePaymentGroupAsync(CreatePaymentGroupRequest request)
    {
        HttpResponseMessage? response = await _httpClient.PostAsJsonAsync("api/paymentgroups", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentGroupResponse>() ?? throw new Exception("Failed to deserialize user response");
    }

    public async Task<PaymentGroupResponse> GetPaymentGroupAsync(Guid id)
    {
        HttpResponseMessage? response = await _httpClient.GetAsync($"api/paymentgroups/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentGroupResponse>() ?? throw new Exception("Failed to deserialize user response");
    }

    public async Task<IEnumerable<PaymentGroupResponse>> GetAllPaymentGroupsAsync()
    {
        HttpResponseMessage? response = await _httpClient.GetAsync("api/paymentgroups");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<PaymentGroupResponse>>() ?? throw new Exception("Failed to deserialize user response");
    }

    public async Task AddUserToGroupAsync(Guid paymentGroupId, AddUserToGroupRequest request)
    {
        HttpResponseMessage? response = await _httpClient.PostAsJsonAsync($"api/paymentgroups/{paymentGroupId}/users", request);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePaymentGroupAsync(Guid id)
    {
        HttpResponseMessage? response = await _httpClient.DeleteAsync($"api/paymentgroups/{id}");
        response.EnsureSuccessStatusCode();
    }
}
