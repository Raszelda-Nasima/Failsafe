FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Failsafe.Web/Failsafe.Web.csproj
RUN dotnet publish src/Failsafe.Web/Failsafe.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 5056
ENV ASPNETCORE_URLS=http://+:5056
ENTRYPOINT ["dotnet", "Failsafe.Web.dll"]