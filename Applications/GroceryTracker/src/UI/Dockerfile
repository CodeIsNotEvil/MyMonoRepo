# Publishes the Blazor WebAssembly app and serves the static output from nginx, which is what
# actually runs on the Pi. The BFF is reached through this same nginx, so the browser only ever
# talks to one origin.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/UI/GroceryTracker.UI.Blazor/GroceryTracker.UI.Blazor.csproj src/UI/GroceryTracker.UI.Blazor/
COPY src/Contracts/GroceryTracker.Contracts.csproj src/Contracts/
COPY src/Domain/GroceryTracker.Domain.csproj src/Domain/

RUN dotnet restore src/UI/GroceryTracker.UI.Blazor/GroceryTracker.UI.Blazor.csproj

COPY src/ src/

# Release publish is what generates service-worker-assets.js and the fingerprinted asset names the
# offline cache relies on; a Debug build would ship the no-op development service worker.
RUN dotnet publish src/UI/GroceryTracker.UI.Blazor/GroceryTracker.UI.Blazor.csproj \
      -c Release -o /app/publish --no-restore

FROM nginx:alpine
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY deploy/nginx/grocerytracker.container.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
