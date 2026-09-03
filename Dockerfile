FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AlternateEarth.Server/AlternateEarth.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV Server__Urls=http://0.0.0.0:8080
ENV Server__DataDirectory=/data
VOLUME ["/data"]
EXPOSE 8080
ENTRYPOINT ["dotnet", "AlternateEarth.Server.dll"]
