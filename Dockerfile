#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NetworkHealthCheck.csproj", "."]
RUN dotnet restore "./NetworkHealthCheck.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "NetworkHealthCheck.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NetworkHealthCheck.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NetworkHealthCheck.dll"]

RUN apt-get update \
	&& apt-get install -y curl

HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=1 CMD curl --silent --fail http://localhost:80/health || exit 1