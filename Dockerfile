# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ProcessadorDiagramas.APIGatewayService.sln ./
COPY src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj \
     src/ProcessadorDiagramas.APIGatewayService/

RUN dotnet restore src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj
RUN dotnet tool install --global dotnet-ef --version 8.0.0

ENV PATH="${PATH}:/root/.dotnet/tools"

COPY . .

RUN dotnet publish src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj \
    -c Release -o /app/publish --no-restore

RUN dotnet ef migrations bundle \
    --project src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj \
    --startup-project src/ProcessadorDiagramas.APIGatewayService/ProcessadorDiagramas.APIGatewayService.csproj \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output /app/efbundle

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
COPY --from=build /app/efbundle /app/efbundle

ENTRYPOINT ["dotnet", "ProcessadorDiagramas.APIGatewayService.dll"]
