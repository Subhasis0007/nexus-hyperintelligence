FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore layers
COPY ["src/Nexus.Models/Nexus.Models.csproj",   "src/Nexus.Models/"]
COPY ["src/Nexus.Crypto/Nexus.Crypto.csproj",   "src/Nexus.Crypto/"]
COPY ["src/Nexus.Core/Nexus.Core.csproj",       "src/Nexus.Core/"]
COPY ["src/Nexus.Agents/Nexus.Agents.csproj",   "src/Nexus.Agents/"]
COPY ["src/Nexus.API/Nexus.API.csproj",         "src/Nexus.API/"]
RUN dotnet restore "src/Nexus.API/Nexus.API.csproj"

# Build
COPY . .
WORKDIR "/src/src/Nexus.API"
RUN dotnet build "Nexus.API.csproj" -c Release -o /app/build --no-restore

# Publish
FROM build AS publish
RUN dotnet publish "Nexus.API.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "Nexus.API.dll"]
