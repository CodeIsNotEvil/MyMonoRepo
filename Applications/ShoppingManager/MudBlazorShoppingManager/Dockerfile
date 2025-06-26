FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ShoppingManager/ShoppingManager.csproj", "ShoppingManager/"]
COPY ["ShoppingManager.Client/ShoppingManager.Client.csproj", "ShoppingManager.Client/"]
RUN dotnet restore "ShoppingManager/ShoppingManager.csproj"
COPY . .
WORKDIR "/src/ShoppingManager"
RUN dotnet build "ShoppingManager.csproj" -c $BUILD_CONFIGURATION -o /app/build


FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ShoppingManager.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT [ "dotnet", "ShoppingManager.dll" ]

# COPY ShoppingManager.sln ./
# COPY ./ShoppingManager/ShoppingManager.csproj ./ShoppingManager/
# COPY ./ShoppingManager.Client/ShoppingManager.Client.csproj ./ShoppingManager.Client/
# 
# RUN dotnet restore
# COPY . ./
# RUN dotnet build -c Release -o out
# 
# FROM nginx:alpine
# WORKDIR /app
# EXPOSE 80
# COPY nginx.conf /etc/nginx/nginx.conf
# COPY --from=build /app/out/wwwroot /usr/share/nginx/html