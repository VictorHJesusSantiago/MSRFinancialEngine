FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NuGet.Config .
COPY MSRFinancialEngine.sln .
COPY src/MSRFinancialEngine.Domain/MSRFinancialEngine.Domain.csproj src/MSRFinancialEngine.Domain/
COPY src/MSRFinancialEngine.Application/MSRFinancialEngine.Application.csproj src/MSRFinancialEngine.Application/
COPY src/MSRFinancialEngine.Infrastructure/MSRFinancialEngine.Infrastructure.csproj src/MSRFinancialEngine.Infrastructure/
COPY src/MSRFinancialEngine.Api/MSRFinancialEngine.Api.csproj src/MSRFinancialEngine.Api/
COPY tests/MSRFinancialEngine.Tests/MSRFinancialEngine.Tests.csproj tests/MSRFinancialEngine.Tests/
RUN dotnet restore src/MSRFinancialEngine.Api/MSRFinancialEngine.Api.csproj

COPY . .
RUN dotnet publish src/MSRFinancialEngine.Api/MSRFinancialEngine.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MSRFinancialEngine.Api.dll"]
