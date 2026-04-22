using System.Net.Http.Json;
using System.Text;
using Newtonsoft.Json;
namespace ShoppingManager.UI.Blazor.Services;

public class CreatePaymentRequest
{
    public decimal Amount { get; set; }
    public Guid UserId { get; set; }
    public DateTime? Date { get; set; }
}

public class PaymentResponse
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public Guid UserId { get; set; }
}

public interface IPaymentApiClient
{
    Task<PaymentResponse> CreatePaymentAsync(CreatePaymentRequest request);
    Task<PaymentResponse> GetPaymentAsync(Guid id);
    Task<IEnumerable<PaymentResponse>> GetUserPaymentsAsync(Guid userId);
}

public class PaymentApiClient : IPaymentApiClient
{
    private readonly HttpClient _httpClient;

    public PaymentApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentResponse> CreatePaymentAsync(CreatePaymentRequest request)
    {
        string? json = JsonConvert.SerializeObject(request);
        HttpResponseMessage? response = await _httpClient.PostAsync("api/payments", new StringContent(json, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResponse>() ?? throw new Exception("Failed to deserialize user response");
    }

    public async Task<PaymentResponse> GetPaymentAsync(Guid id)
    {
        HttpResponseMessage? response = await _httpClient.GetAsync($"api/payments/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResponse>() ?? throw new Exception("Failed to deserialize user response");
    }

    public async Task<IEnumerable<PaymentResponse>> GetUserPaymentsAsync(Guid userId)
    {
        HttpResponseMessage? response = await _httpClient.GetAsync($"api/payments/user/{userId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<PaymentResponse>>() ?? throw new Exception("Failed to deserialize user response");
    }
}
