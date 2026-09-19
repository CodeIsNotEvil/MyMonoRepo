# Multi-arch base images, so the same Dockerfile builds for an x86 dev box and an arm64 Pi.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: this layer only changes when a dependency changes, so the restore stays
# cached across ordinary code edits. That matters when building on a Pi.
COPY src/Api/GroceryTracker.Api.csproj src/Api/
COPY src/Application/GroceryTracker.Application.csproj src/Application/
COPY src/Infrastructure/GroceryTracker.Infrastructure.csproj src/Infrastructure/
COPY src/Contracts/GroceryTracker.Contracts.csproj src/Contracts/
COPY src/Domain/GroceryTracker.Domain.csproj src/Domain/
# The API references the Blazor client project (that's what lets it self-host the WASM app for `dotnet
# run`), so restore needs this csproj present too, or it silently fails to resolve the client's own
# packages and publish later dies looking for an assets file that restore never produced.
COPY src/UI/GroceryTracker.UI.Blazor/GroceryTracker.UI.Blazor.csproj src/UI/GroceryTracker.UI.Blazor/

RUN dotnet restore src/Api/GroceryTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/GroceryTracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# The runtime image ships neither wget nor curl, so the compose healthcheck below has nothing to
# call the /health endpoint with. curl is the smallest fix; installed and its apt cache dropped in
# the same layer so it doesn't linger in the image.
#
# libgssapi-krb5-2 is what Npgsql probes for to negotiate GSSAPI encryption on every connection;
# without it, each connection attempt logs a scary-looking "Cannot load library ... Error:" line
# even when the connection succeeds, which is a red herring next to any *real* connection failure.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5000 \
    DOTNET_gcServer=0

COPY --from=build /app/publish .

EXPOSE 5000
ENTRYPOINT ["dotnet", "GroceryTracker.Api.dll"]
