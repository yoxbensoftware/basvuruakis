FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/api/BasvuruAkis.Api.csproj apps/api/
RUN dotnet restore apps/api/BasvuruAkis.Api.csproj
COPY apps/api apps/api
RUN dotnet publish apps/api/BasvuruAkis.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "BasvuruAkis.Api.dll"]
