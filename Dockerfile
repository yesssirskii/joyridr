# Builds the API into a small runtime image. Railway (and the CI check) build this from the repo root.

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore on its own first, so Docker reuses the downloaded packages until the .csproj changes.
COPY joyridr/joyridr.csproj joyridr/
RUN dotnet restore joyridr/joyridr.csproj
COPY joyridr/ joyridr/
RUN dotnet publish joyridr/joyridr.csproj -c Release -o /app --no-restore

# ---- Run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Uploaded photos. On Railway a volume is mounted here so they survive redeploys.
# The image runs as root because Railway mounts volumes owned by root.
RUN mkdir -p wwwroot/uploads

# Railway handles HTTPS and forwards plain HTTP with X-Forwarded-* headers. Trusting them makes the API
# see the real scheme (https) and each user's real IP, which the login rate limit is keyed on.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
# Where to send the odd plain-HTTP request (Railway serves HTTPS on 443).
ENV ASPNETCORE_HTTPS_PORT=443

# Railway picks the port and passes it in PORT; 8080 when run anywhere else.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet joyridr.dll"]
