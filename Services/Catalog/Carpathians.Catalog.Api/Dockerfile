# Збірка з кореня репозиторію (сервіс посилається на Platform.ServiceDefaults і спільні Protos/):
#   docker build -f Services/Catalog/Carpathians.Catalog.Api/Dockerfile -t carpathians/catalog-api .
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Services/Catalog/Carpathians.Catalog.Api/Carpathians.Catalog.Api.csproj", "Services/Catalog/Carpathians.Catalog.Api/"]
COPY ["Services/Catalog/Carpathians.Catalog.Bll/Carpathians.Catalog.Bll.csproj", "Services/Catalog/Carpathians.Catalog.Bll/"]
COPY ["Services/Catalog/Carpathians.Catalog.Dal/Carpathians.Catalog.Dal.csproj", "Services/Catalog/Carpathians.Catalog.Dal/"]
COPY ["Services/Catalog/Carpathians.Catalog.Domain/Carpathians.Catalog.Domain.csproj", "Services/Catalog/Carpathians.Catalog.Domain/"]
COPY ["Platform.ServiceDefaults/Carpathians.Platform.ServiceDefaults.csproj", "Platform.ServiceDefaults/"]

RUN dotnet restore "Services/Catalog/Carpathians.Catalog.Api/Carpathians.Catalog.Api.csproj"
COPY Services/Catalog/ Services/Catalog/
COPY Platform.ServiceDefaults/ Platform.ServiceDefaults/
COPY Protos/ Protos/
WORKDIR "/src/Services/Catalog/Carpathians.Catalog.Api"
RUN dotnet build "./Carpathians.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Carpathians.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Carpathians.Catalog.Api.dll"]
