# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY src/Azure.Tenant.Automation.csproj ./
RUN dotnet restore Azure.Tenant.Automation.csproj

# Copy source code and build
COPY src/ ./
RUN dotnet build Azure.Tenant.Automation.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish Azure.Tenant.Automation.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create a non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser && \
    chown -R appuser:appuser /app
USER appuser

# Set entrypoint
ENTRYPOINT ["dotnet", "Azure.Tenant.Automation.dll"]

