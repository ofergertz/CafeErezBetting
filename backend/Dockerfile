# ── Dev stage ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev
WORKDIR /app
# Pre-restore NuGet packages into the image layer so the container
# does not need outbound internet access at runtime.
COPY ["src/CafeErezBetting.API/CafeErezBetting.API.csproj", "src/CafeErezBetting.API/"]
COPY ["src/CafeErezBetting.Core/CafeErezBetting.Core.csproj", "src/CafeErezBetting.Core/"]
COPY ["src/CafeErezBetting.Infrastructure/CafeErezBetting.Infrastructure.csproj", "src/CafeErezBetting.Infrastructure/"]
RUN dotnet restore "src/CafeErezBetting.API/CafeErezBetting.API.csproj"
EXPOSE 5000
# Chromium for Playwright scraper (WinnerScraperService, production only)
RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 \
    libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libgbm1 \
    libxkbcommon0 libpango-1.0-0 libcairo2 libasound2 && \
    rm -rf /var/lib/apt/lists/*
ENV PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium


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
# Chromium for Playwright scraper (WinnerScraperService, production only)
RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 \
    libxcomposite1 libxdamage1 libxfixes3 libxrandr2 libgbm1 \
    libxkbcommon0 libpango-1.0-0 libcairo2 libasound2 && \
    rm -rf /var/lib/apt/lists/*
ENV PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/chromium
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CafeErezBetting.API.dll"]
