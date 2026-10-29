# ==========================================
# Stage 1: Build & Publish (SDK Image)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (optimizes Docker caching)
COPY ["tran_nguyen_vi_khang_api.csproj", "./"]
RUN dotnet restore "tran_nguyen_vi_khang_api.csproj"

# Copy remaining source code and publish Release build
COPY . .
RUN dotnet publish "tran_nguyen_vi_khang_api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Stage 2: Production Runtime
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install curl for container health check
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run with non-root security context
USER app

# ASP.NET Core .NET 8 default container listening port
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "tran_nguyen_vi_khang_api.dll"]

