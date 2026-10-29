FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Shared/MiniBank.Shared.Domain/MiniBank.Shared.Domain.csproj", "src/Shared/MiniBank.Shared.Domain/"]
COPY ["src/Shared/MiniBank.Shared.Contracts/MiniBank.Shared.Contracts.csproj", "src/Shared/MiniBank.Shared.Contracts/"]
COPY ["src/Shared/MiniBank.Shared.Infrastructure/MiniBank.Shared.Infrastructure.csproj", "src/Shared/MiniBank.Shared.Infrastructure/"]
COPY ["src/Gateway/MiniBank.ApiGateway/MiniBank.ApiGateway.csproj", "src/Gateway/MiniBank.ApiGateway/"]

RUN dotnet restore "src/Gateway/MiniBank.ApiGateway/MiniBank.ApiGateway.csproj"

COPY . .
WORKDIR "/src/src/Gateway/MiniBank.ApiGateway"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MiniBank.ApiGateway.dll"]
