# ASP.NET runtime (production base)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Expose API port
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj để restore trước
COPY ["LetsLearn.API/LetsLearn.API.csproj", "LetsLearn.API/"]
COPY ["LetsLearn.Core/LetsLearn.Core.csproj", "LetsLearn.Core/"]
COPY ["LetsLearn.UseCases/LetsLearn.UseCases.csproj", "LetsLearn.UseCases/"]

RUN dotnet restore "LetsLearn.API/LetsLearn.API.csproj"

# Copy toàn code
COPY . .

# Build
RUN dotnet build "LetsLearn.API/LetsLearn.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "LetsLearn.API/LetsLearn.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "LetsLearn.API.dll"]
