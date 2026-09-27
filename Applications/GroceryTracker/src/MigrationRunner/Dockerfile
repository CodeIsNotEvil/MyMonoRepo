# The SDK stage runs on the machine doing the build ($BUILDPLATFORM), even when the image is for
# another architecture. The output is portable IL started with `dotnet X.dll`, so one publish serves
# amd64 and arm64 alike. That keeps .NET out of QEMU emulation, where it is slow and prone to crash,
# when CI builds the arm64 image for the Pi (.github/workflows/grocerytracker-images.yml).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Sets the CINE.* root namespace. It sits outside src/, so without this the Razor components
# would compile into the unprefixed namespace and the build breaks on _Imports.razor.
COPY Directory.Build.props ./

COPY src/MigrationRunner/GroceryTracker.MigrationRunner.csproj src/MigrationRunner/
COPY src/Infrastructure/GroceryTracker.Infrastructure.csproj src/Infrastructure/
COPY src/Domain/GroceryTracker.Domain.csproj src/Domain/

RUN dotnet restore src/MigrationRunner/GroceryTracker.MigrationRunner.csproj

COPY src/ src/
# No apphost, for the same reason as in the API's Dockerfile: the entrypoint runs the portable dll.
RUN dotnet publish src/MigrationRunner/GroceryTracker.MigrationRunner.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

# Npgsql probes for this at connection time to negotiate GSSAPI encryption; the slim runtime image
# doesn't ship it, so every run logged a scary-looking "Cannot load library ... Error:" line even on
# success, as Npgsql silently fell back to normal password/SCRAM auth. Installing it removes the
# noise rather than leaving a red herring next to any *real* connection failure.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# MIT asks for the notice to travel with every copy, and an image is a copy.
COPY LICENSE /usr/share/licenses/grocerytracker/LICENSE

ENTRYPOINT ["dotnet", "GroceryTracker.MigrationRunner.dll"]
