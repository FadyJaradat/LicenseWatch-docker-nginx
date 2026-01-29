# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LicenseWatch.slnx ./
COPY src/LicenseWatch.Core/LicenseWatch.Core.csproj src/LicenseWatch.Core/
COPY src/LicenseWatch.Infrastructure/LicenseWatch.Infrastructure.csproj src/LicenseWatch.Infrastructure/
COPY src/LicenseWatch.Web/LicenseWatch.Web.csproj src/LicenseWatch.Web/
RUN dotnet restore src/LicenseWatch.Web/LicenseWatch.Web.csproj

COPY . .
RUN dotnet publish src/LicenseWatch.Web/LicenseWatch.Web.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN mkdir -p /app/App_Data
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LicenseWatch.Web.dll"]
