FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/AntiFraude.Api/AntiFraude.Api.csproj", "src/AntiFraude.Api/"]
COPY ["src/AntiFraude.Application/AntiFraude.Application.csproj", "src/AntiFraude.Application/"]
COPY ["src/AntiFraude.Domain/AntiFraude.Domain.csproj", "src/AntiFraude.Domain/"]
COPY ["src/AntiFraude.Infrastructure/AntiFraude.Infrastructure.csproj", "src/AntiFraude.Infrastructure/"]
COPY ["src/AntiFraude.CrossCutting/AntiFraude.CrossCutting.csproj", "src/AntiFraude.CrossCutting/"]
RUN dotnet restore "src/AntiFraude.Api/AntiFraude.Api.csproj"

COPY . .
WORKDIR "/src/src/AntiFraude.Api"
RUN dotnet build "AntiFraude.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AntiFraude.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AntiFraude.Api.dll"]
