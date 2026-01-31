FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG VERSION=0.0.0
ARG VERSION_SUFFIX=
WORKDIR /src

COPY ["MailGatekeeper.Api/MailGatekeeper.Api.csproj", "MailGatekeeper.Api/"]
RUN dotnet restore "MailGatekeeper.Api/MailGatekeeper.Api.csproj"

COPY . .
WORKDIR "/src/MailGatekeeper.Api"
RUN dotnet publish "MailGatekeeper.Api.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false \
    /p:FileVersion=${VERSION} \
    /p:VersionPrefix=${VERSION} \
    /p:VersionSuffix=${VERSION_SUFFIX}

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MailGatekeeper.dll"]
