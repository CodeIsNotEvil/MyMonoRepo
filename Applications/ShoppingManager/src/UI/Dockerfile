FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

COPY src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj
COPY src/Domain/ShoppingManager.Domain.csproj src/Domain/ShoppingManager.Domain.csproj

RUN dotnet restore src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj

COPY . .
WORKDIR /src
RUN dotnet build src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj -c Release

EXPOSE 5173
ENTRYPOINT ["dotnet", "run", "-c", "Release", "--project", "src/UI/ShoppingManager.UI.Blazor/ShoppingManager.UI.Blazor.csproj", "--urls", "http://+:5173"]
