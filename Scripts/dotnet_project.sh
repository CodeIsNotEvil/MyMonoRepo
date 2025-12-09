#!/bin/bash

echo "Getting all necessary .NET templates..."
dotnet new install bunit.template

echo "Creating a new .NET Web API project..."
dotnet new webapi --name "ShoppingManager.Api" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Api --use-controllers

echo "Creating a new .NET Class Library project for Application layer..."
dotnet new classlib --name "ShoppingManager.Application" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Application

echo "Creating a new .NET Class Library project for Domain layer..."
dotnet new classlib --name "ShoppingManager.Domain" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Domain

echo "Creating a new .NET Class Library project for Infrastructure layer..."
dotnet new classlib --name "ShoppingManager.Infrastructure" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Infrastructure

echo "Creating a new Blazor Auto project for Forntend..."
dotnet new blazorwasm --name "ShoppingManager.UI.Blazor" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/UI/ShoppingManager.UI.Blazor --pwa

echo "All projects have been created successfully."

echo "creating sln file"
dotnet new sln --name "ShoppingManager" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager

echo "Adding projects to the solution file..."
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Api/ShoppingManager.Api.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Application/ShoppingManager.Application.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Domain/ShoppingManager.Domain.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Infrastructure/ShoppingManager.Infrastructure.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj
echo "All projects have been added to the solution file successfully."

echo "Setting up project references..."
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Api/ShoppingManager.Api.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Application/ShoppingManager.Application.csproj
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Application/ShoppingManager.Application.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Domain/ShoppingManager.Domain.csproj
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Infrastructure/ShoppingManager.Infrastructure.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Domain/ShoppingManager.Domain.csproj
echo "Project references have been set up successfully."

echo "The .NET project structure has been set up and built successfully."

echo "Create test projects"
dotnet new xunit --name "ShoppingManager.Api.Tests" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Api.Tests
dotnet new xunit --name "ShoppingManager.Application.Tests" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Application.Tests
dotnet new xunit --name "ShoppingManager.Domain.Tests" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Domain.Tests
dotnet new xunit --name "ShoppingManager.Infrastructure.Tests" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Infrastructure.Tests
dotnet new bunit --framework xunit --name "ShoppingManager.UI.Blazor.Tests" --output ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/UI/ShoppingManager.UI.Blazor.Tests
echo "All test projects have been created successfully."

echo "Adding test projects to the solution file..."
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Api.Tests/ShoppingManager.Api.Tests.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Application.Tests/ShoppingManager.Application.Tests.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Domain.Tests/ShoppingManager.Domain.Tests.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Infrastructure.Tests/ShoppingManager.Infrastructure.Tests.csproj
dotnet sln ~/Repositories/MyMonoRepo/Applications/ShoppingManager/ShoppingManager.slnx add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/UI/ShoppingManager.UI.Blazor.Tests/ShoppingManager.UI.Blazor.Tests.csproj
echo "All test projects have been added to the solution file successfully."

echo "Setting up test project references..."
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Api.Tests/ShoppingManager.Api.Tests.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Api/ShoppingManager.Api.csproj
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Application.Tests/ShoppingManager.Application.Tests.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Application/ShoppingManager.Application.csproj
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Domain.Tests/ShoppingManager.Domain.Tests.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Domain/ShoppingManager.Domain.csproj
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/Infrastructure.Tests/ShoppingManager.Infrastructure.Tests.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/Infrastructure/ShoppingManager.Infrastructure.csproj
dotnet add ~/Repositories/MyMonoRepo/Applications/ShoppingManager/tests/UI/ShoppingManager.UI.Blazor.Tests/ShoppingManager.UI.Blazor.Tests.csproj reference ~/Repositories/MyMonoRepo/Applications/ShoppingManager/src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj
echo "Test project references have been set up successfully."

echo "the test project structure has been set up successfully."

cd ~/Repositories/MyMonoRepo/Applications/ShoppingManager
dotnet restore
dotnet format
dotnet build
echo "we have build and restored the solution successfully."

echo "Create a README.md file for the ShoppingManager project..."
cat <<EOL > ~/Repositories/MyMonoRepo/Applications/ShoppingManager/README.md
# ShoppingManager
\
ShoppingManager is a .NET-based application designed to manage shopping activities efficiently. It follows a clean architecture with separate layers for API, Application, Domain, and Infrastructure.
The frontend is built using Blazor Progressive WebAssembly (PWA) to provide a responsive and interactive user experience.
\
## Project Structure
\
- **Api**: Contains the Web API project that serves as the entry point for clients.
- **Application**: Contains business logic and application services.
- **Domain**: Contains domain entities and business rules.
- **Infrastructure**: Contains data access and external service implementations.
- **UI**: Contains the Blazor Progressive WebAssembly project for the frontend.
- **Tests**: Contains unit tests for each layer of the application.
\
## Getting Started
\
1. Clone the repository.
2. Navigate to the ShoppingManager directory.
3. Restore dependencies: \`dotnet restore\`
4. Build the solution: \`dotnet build\`
5. Run the API project: \`dotnet run --project src/Api/ShoppingManager.Api/ShoppingManager.Api.csproj\`
\
## Running Tests
\
To run the unit tests, navigate to the ShoppingManager directory and execute:
\
\`\`\`bash
dotnet test
\`\`\`
EOL
echo "README.md file has been created successfully."
