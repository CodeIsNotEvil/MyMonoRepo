FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/MigrationRunner/GroceryTracker.MigrationRunner.csproj src/MigrationRunner/
COPY src/Infrastructure/GroceryTracker.Infrastructure.csproj src/Infrastructure/
COPY src/Domain/GroceryTracker.Domain.csproj src/Domain/

RUN dotnet restore src/MigrationRunner/GroceryTracker.MigrationRunner.csproj

COPY src/ src/
RUN dotnet publish src/MigrationRunner/GroceryTracker.MigrationRunner.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GroceryTracker.MigrationRunner.dll"]
