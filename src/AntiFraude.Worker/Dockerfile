FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/AntiFraude.Worker/AntiFraude.Worker.csproj", "src/AntiFraude.Worker/"]
COPY ["src/AntiFraude.Application/AntiFraude.Application.csproj", "src/AntiFraude.Application/"]
COPY ["src/AntiFraude.Domain/AntiFraude.Domain.csproj", "src/AntiFraude.Domain/"]
COPY ["src/AntiFraude.Infrastructure/AntiFraude.Infrastructure.csproj", "src/AntiFraude.Infrastructure/"]
COPY ["src/AntiFraude.CrossCutting/AntiFraude.CrossCutting.csproj", "src/AntiFraude.CrossCutting/"]
RUN dotnet restore "src/AntiFraude.Worker/AntiFraude.Worker.csproj"

COPY . .
WORKDIR "/src/src/AntiFraude.Worker"
RUN dotnet build "AntiFraude.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AntiFraude.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AntiFraude.Worker.dll"]
