# Use the official Azure Functions .NET 8.0 isolated base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot

# Copy csproj and restore as distinct layers
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CopilotCloner.csproj", "."]
RUN dotnet restore "./CopilotCloner.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "./CopilotCloner.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./CopilotCloner.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .

# Install .NET SDK 8.0 and PAC CLI
RUN apt-get update && \
    apt-get install -y wget && \
    wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y dotnet-sdk-8.0 && \
    mkdir -p /opt/dotnet-tools && \
    dotnet tool install --tool-path /opt/dotnet-tools Microsoft.PowerApps.CLI.Tool && \
    ln -s /opt/dotnet-tools/pac /usr/local/bin/pac && \
    rm -rf /var/lib/apt/lists/*

# Set environment variables
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

# Expose the port (if necessary)
EXPOSE 80
