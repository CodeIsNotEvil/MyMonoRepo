FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Api/ShoppingManager.Api.csproj src/Api/
COPY src/Application/ShoppingManager.Application.csproj src/Application/
COPY src/Infrastructure/ShoppingManager.Infrastructure.csproj src/Infrastructure/
COPY src/Domain/ShoppingManager.Domain.csproj src/Domain/

RUN dotnet restore src/Api/ShoppingManager.Api.csproj

COPY . .
WORKDIR /src/src/Api
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "ShoppingManager.Api.dll"]
