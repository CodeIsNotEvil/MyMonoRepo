# ShoppingManager

ShoppingManager is a .NET-based application designed to manage shopping activities efficiently. It follows a clean architecture with separate layers for API, Application, Domain, and Infrastructure.
The frontend is built using Blazor Progressive WebAssembly (PWA) to provide a responsive and interactive user experience.

## Project Structure

- **Api**: Contains the Web API project that serves as the entry point for clients.
- **Application**: Contains business logic and application services.
- **Domain**: Contains domain entities and business rules.
- **Infrastructure**: Contains data access and external service implementations.
- **UI**: Contains the Blazor Progressive WebAssembly project for the frontend.
- **Tests**: Contains unit tests for each layer of the application.

## Getting Started

1. Clone the repository.
2. Navigate to the ShoppingManager directory.
3. Restore dependencies: `dotnet restore`
4. Build the solution: `dotnet build`
5. Run the API project: `dotnet run --project src/Api/ShoppingManager.Api/ShoppingManager.Api.csproj`

## Running Tests

To run the unit tests, navigate to the ShoppingManager directory and execute:

```bash
dotnet test
```
