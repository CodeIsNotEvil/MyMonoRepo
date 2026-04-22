using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ShoppingManager.UI.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls
builder.Services.AddScoped(sp => 
{
    var baseAddress = new Uri("http://localhost:5078"); // API base address
    return new HttpClient { BaseAddress = baseAddress };
});

// Register API client services
builder.Services.AddScoped<ShoppingManager.UI.Blazor.Services.IPaymentApiClient, ShoppingManager.UI.Blazor.Services.PaymentApiClient>();
builder.Services.AddScoped<ShoppingManager.UI.Blazor.Services.IPaymentGroupApiClient, ShoppingManager.UI.Blazor.Services.PaymentGroupApiClient>();
builder.Services.AddScoped<ShoppingManager.UI.Blazor.Services.IUserApiClient, ShoppingManager.UI.Blazor.Services.UserApiClient>();

await builder.Build().RunAsync();
