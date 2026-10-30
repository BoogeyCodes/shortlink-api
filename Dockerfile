# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ShortLink.sln ./
COPY src/ShortLink.Api/ShortLink.Api.csproj src/ShortLink.Api/
COPY tests/ShortLink.Api.Tests/ShortLink.Api.Tests.csproj tests/ShortLink.Api.Tests/
RUN dotnet restore

COPY . .
RUN dotnet test --no-restore --configuration Release
RUN dotnet publish src/ShortLink.Api -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/data/shortlink.db"
VOLUME /data
EXPOSE 8080

COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "ShortLink.Api.dll"]
