using GroceryTracker.UI.Blazor;
using GroceryTracker.UI.Blazor.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same origin in production: nginx serves these files and proxies /api to the BFF, so there is no
// CORS and no token to leak. Only the local dev server needs an explicit override.
var apiBaseAddress = builder.Configuration["ApiBaseUrl"] is { Length: > 0 } configured
  ? configured
  : builder.HostEnvironment.BaseAddress;

// Singleton rather than scoped: the sync engine outlives any one component and holds onto it.
builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

builder.Services.AddSingleton<LocalStore>();
builder.Services.AddSingleton<ConnectivityService>();
builder.Services.AddSingleton<SyncEngine>();
builder.Services.AddSingleton<GroceryDataService>();

await builder.Build().RunAsync();
