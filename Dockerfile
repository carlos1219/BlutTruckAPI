# syntax=docker/dockerfile:1

# 1) Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BlutTruck.sln", "./"]
COPY ["BlutTruck/BlutTruck.csproj", "BlutTruck/"]
RUN dotnet restore "BlutTruck.sln"

COPY . .
RUN dotnet publish "BlutTruck.sln" -c Release -o /app/publish --no-restore

# 2) Runtime stage: solo lo publicado, sin privilegios de root
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# La imagen aspnet 8 incluye el usuario sin privilegios "app" (UID 1654)
USER app

EXPOSE 3000
ENTRYPOINT ["dotnet", "BlutTruck.dll"]
