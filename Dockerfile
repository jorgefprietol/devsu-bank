FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG SERVICE=Clients
WORKDIR /source
COPY Directory.Build.props .
COPY src/ src/
RUN dotnet restore src/${SERVICE}.Api/${SERVICE}.Api.csproj
RUN dotnet publish src/${SERVICE}.Api/${SERVICE}.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ARG SERVICE=Clients
ENV SERVICE_DLL=${SERVICE}.Api.dll
ENV ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$SERVICE_DLL\""]
