# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ProcessadorDiagramas.APIGatewayService.sln ./
COPY src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj \
     src/ProcessadorDiagramas.APIGatewayService/

RUN dotnet restore src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj

COPY . .

RUN dotnet publish src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj \
    -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ProcessadorDiagramas.APIGatewayService.dll"]
