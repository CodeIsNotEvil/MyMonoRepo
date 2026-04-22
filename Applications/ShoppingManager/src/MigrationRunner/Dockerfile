FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/MigrationRunner/ShoppingManager.MigrationRunner.csproj src/MigrationRunner/
COPY src/Infrastructure/ShoppingManager.Infrastructure.csproj src/Infrastructure/
COPY src/Domain/ShoppingManager.Domain.csproj src/Domain/

RUN dotnet restore src/MigrationRunner/ShoppingManager.MigrationRunner.csproj

COPY . .
WORKDIR /src/src/MigrationRunner
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShoppingManager.MigrationRunner.dll"]
