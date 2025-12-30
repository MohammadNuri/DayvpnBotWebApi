# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only project files first (to cache restore)
COPY ["DayvpnBotWebApi.csproj", "./"]
RUN dotnet restore "DayvpnBotWebApi.csproj"

# Copy remaining files and publish
COPY . .
RUN dotnet publish "DayvpnBotWebApi.csproj" -c Release -o /app/publish

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Set environment variable for correct HTTP port
ENV ASPNETCORE_URLS=http://+:80

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "DayvpnBotWebApi.dll"]
