# Stage 1: Build the frontend React SPA
FROM node:20-alpine AS frontend-build
WORKDIR /app/web
COPY src/Prisma.Workspace.Web/package*.json ./
RUN npm ci
COPY src/Prisma.Workspace.Web/ ./
RUN npm run build

# Stage 2: Build the backend .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

# Copy solution and project files for restore
COPY src/Prisma.Workspace.Domain/Prisma.Workspace.Domain.csproj src/Prisma.Workspace.Domain/
COPY src/Prisma.Workspace.Application/Prisma.Workspace.Application.csproj src/Prisma.Workspace.Application/
COPY src/Prisma.Workspace.Infrastructure/Prisma.Workspace.Infrastructure.csproj src/Prisma.Workspace.Infrastructure/
COPY src/Prisma.Workspace.Api/Prisma.Workspace.Api.csproj src/Prisma.Workspace.Api/

RUN dotnet restore src/Prisma.Workspace.Api/Prisma.Workspace.Api.csproj

# Copy all source files
COPY src/Prisma.Workspace.Domain/ src/Prisma.Workspace.Domain/
COPY src/Prisma.Workspace.Application/ src/Prisma.Workspace.Application/
COPY src/Prisma.Workspace.Infrastructure/ src/Prisma.Workspace.Infrastructure/
COPY src/Prisma.Workspace.Api/ src/Prisma.Workspace.Api/

# Copy compiled frontend assets into wwwroot of the Web API
COPY --from=frontend-build /app/web/dist src/Prisma.Workspace.Api/wwwroot/

# Publish the API
RUN dotnet publish src/Prisma.Workspace.Api/Prisma.Workspace.Api.csproj -c Release -o /app/publish

# Stage 3: Build the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=backend-build /app/publish .

# Environment variables
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --retries=12 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Prisma.Workspace.Api.dll"]
