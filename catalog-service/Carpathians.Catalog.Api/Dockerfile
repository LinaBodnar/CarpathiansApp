FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Carpathians.Catalog.Api/Carpathians.Catalog.Api.csproj", "Carpathians.Catalog.Api/"]
COPY ["Carpathians.Catalog.Bll/Carpathians.Catalog.Bll.csproj", "Carpathians.Catalog.Bll/"]
COPY ["Carpathians.Catalog.Dal/Carpathians.Catalog.Dal.csproj", "Carpathians.Catalog.Dal/"]
COPY ["Carpathians.Catalog.Domain/Carpathians.Catalog.Domain.csproj", "Carpathians.Catalog.Domain/"]

RUN dotnet restore "./Carpathians.Catalog.Api/Carpathians.Catalog.Api.csproj"
COPY . .
WORKDIR "/src/Carpathians.Catalog.Api"
RUN dotnet build "./Carpathians.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Carpathians.Catalog.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Carpathians.Catalog.Api.dll"]
