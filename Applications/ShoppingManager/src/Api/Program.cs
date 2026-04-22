using Microsoft.EntityFrameworkCore;
using ShoppingManager.Infrastructure.Data;
using ShoppingManager.Infrastructure.Abstractions;
using ShoppingManager.Infrastructure.Repositories;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ShoppingManager.Infrastructure.Data.ShoppingManagerDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register infrastructure services
builder.Services.AddScoped<ShoppingManager.Infrastructure.Abstractions.IUnitOfWork, ShoppingManager.Infrastructure.Repositories.UnitOfWork>();

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

// Apply migrations at startup
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ShoppingManager.Infrastructure.Data.ShoppingManagerDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while migrating the database.");
}

if (app.Environment.IsDevelopment()) {
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazor");

app.UseAuthorization();

app.MapControllers();

app.Run();
