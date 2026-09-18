# Multi-arch base images, so the same Dockerfile builds for an x86 dev box and an arm64 Pi.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: this layer only changes when a dependency changes, so the restore stays
# cached across ordinary code edits. That matters when building on a Pi.
COPY src/Bff/GroceryTracker.Bff.csproj src/Bff/
COPY src/Application/GroceryTracker.Application.csproj src/Application/
COPY src/Infrastructure/GroceryTracker.Infrastructure.csproj src/Infrastructure/
COPY src/Contracts/GroceryTracker.Contracts.csproj src/Contracts/
COPY src/Domain/GroceryTracker.Domain.csproj src/Domain/

RUN dotnet restore src/Bff/GroceryTracker.Bff.csproj

COPY src/ src/
RUN dotnet publish src/Bff/GroceryTracker.Bff.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000 \
    DOTNET_gcServer=0

COPY --from=build /app/publish .

EXPOSE 5000
ENTRYPOINT ["dotnet", "GroceryTracker.Bff.dll"]
