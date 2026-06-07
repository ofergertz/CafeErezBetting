# ── Dev stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev
WORKDIR /app
EXPOSE 5000

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/CafeErezBetting.API/CafeErezBetting.API.csproj", "src/CafeErezBetting.API/"]
COPY ["src/CafeErezBetting.Core/CafeErezBetting.Core.csproj", "src/CafeErezBetting.Core/"]
COPY ["src/CafeErezBetting.Infrastructure/CafeErezBetting.Infrastructure.csproj", "src/CafeErezBetting.Infrastructure/"]
RUN dotnet restore "src/CafeErezBetting.API/CafeErezBetting.API.csproj"
COPY . .
RUN dotnet build "src/CafeErezBetting.API/CafeErezBetting.API.csproj" -c Release -o /app/build

# ── Publish stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "src/CafeErezBetting.API/CafeErezBetting.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage (prod) ──────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS prod
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CafeErezBetting.API.dll"]
