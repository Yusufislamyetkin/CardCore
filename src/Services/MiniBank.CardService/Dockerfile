FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
EXPOSE 5003

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]

COPY ["src/Shared/MiniBank.Shared.Domain/MiniBank.Shared.Domain.csproj", "src/Shared/MiniBank.Shared.Domain/"]
COPY ["src/Shared/MiniBank.Shared.Contracts/MiniBank.Shared.Contracts.csproj", "src/Shared/MiniBank.Shared.Contracts/"]
COPY ["src/Shared/MiniBank.Shared.Infrastructure/MiniBank.Shared.Infrastructure.csproj", "src/Shared/MiniBank.Shared.Infrastructure/"]
COPY ["src/Services/MiniBank.CardService/MiniBank.CardService.csproj", "src/Services/MiniBank.CardService/"]

RUN dotnet restore "src/Services/MiniBank.CardService/MiniBank.CardService.csproj"

COPY . .
WORKDIR "/src/src/Services/MiniBank.CardService"
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MiniBank.CardService.dll"]
