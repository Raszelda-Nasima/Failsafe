# Multi-stage build: the SDK image (large, has compilers/tooling) builds
# the app, then only the compiled output gets copied into the much
# smaller ASP.NET runtime image — the final container doesn't carry the
# entire SDK around unnecessarily.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Failsafe.API/Failsafe.API.csproj
RUN dotnet publish src/Failsafe.API/Failsafe.API.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5171
# Overrides local dev's UseUrls behavior — inside a container, Kestrel
# should always bind to all interfaces on this port, since "localhost"
# inside a container means the container itself, not the host machine.
ENV ASPNETCORE_URLS=http://+:5171
ENTRYPOINT ["dotnet", "Failsafe.API.dll"]