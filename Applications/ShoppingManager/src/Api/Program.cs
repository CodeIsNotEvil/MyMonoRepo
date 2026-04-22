WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register application services
builder.Services.AddScoped<ShoppingManager.Application.IPaymentService, ShoppingManager.Application.PaymentService>();
builder.Services.AddScoped<ShoppingManager.Application.IPaymentGroupService, ShoppingManager.Application.PaymentGroupService>();
builder.Services.AddScoped<ShoppingManager.Application.IUserService, ShoppingManager.Application.UserService>();

// Add CORS for Blazor WebAssembly communication
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5174")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

WebApplication? app = builder.Build();


if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazor");

app.UseAuthorization();

app.MapControllers();

app.Run();
