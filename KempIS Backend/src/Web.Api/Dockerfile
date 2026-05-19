FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Playwright's official .NET image lags behind .NET 10, so we layer the
# Chromium OS dependencies onto the standard ASP.NET runtime image.
USER root
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    libgbm1 \
    libxss1 \
    libasound2t64 \
    libpango-1.0-0 \
    libcairo2 \
    libx11-xcb1 \
    fonts-liberation \
    fonts-noto-color-emoji \
    curl \
  && rm -rf /var/lib/apt/lists/*

# Persist the data-protection key ring outside the container layer so bearer
# tokens survive restarts. The directory is owned by APP_UID before USER drop.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
  && chown -R $APP_UID:$APP_UID /home/app

USER $APP_UID
EXPOSE 5000

# Kestrel binding is defined in appsettings.json (Kestrel:Endpoints:Http:Url).
# DOTNET_RUNNING_IN_CONTAINER toggles a couple of framework defaults that
# help in container environments (e.g. globalization invariant mode probes).
ENV DOTNET_RUNNING_IN_CONTAINER=true

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl --fail --silent http://localhost:5000/health || exit 1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Web.Api/Web.Api.csproj", "src/Web.Api/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/SharedKernel/SharedKernel.csproj", "src/SharedKernel/"]
RUN dotnet restore "./src/Web.Api/Web.Api.csproj"

COPY . .
WORKDIR "/src/src/Web.Api"
RUN dotnet build "./Web.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# ---------- publish ----------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Web.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- final ----------
FROM base AS final
WORKDIR /app
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "Web.Api.dll"]
