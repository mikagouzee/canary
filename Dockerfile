# STAGE 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["CloudNativeCanary.csproj", "./"]
RUN dotnet restore "CloudNativeCanary.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "CloudNativeCanary.csproj" -c Release -o /app/publish

# STAGE 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CloudNativeCanary.dll"]