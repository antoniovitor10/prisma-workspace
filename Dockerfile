# Stage 1: Build the frontend React SPA
FROM node:20-alpine AS frontend-build
WORKDIR /app/web
COPY src/Detran.Kanban.Web/package*.json ./
RUN npm ci
COPY src/Detran.Kanban.Web/ ./
RUN npm run build

# Stage 2: Build the backend .NET API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src

# Copy solution and project files for restore
COPY src/Detran.Kanban.Domain/Detran.Kanban.Domain.csproj src/Detran.Kanban.Domain/
COPY src/Detran.Kanban.Application/Detran.Kanban.Application.csproj src/Detran.Kanban.Application/
COPY src/Detran.Kanban.Infrastructure/Detran.Kanban.Infrastructure.csproj src/Detran.Kanban.Infrastructure/
COPY src/Detran.Kanban.Api/Detran.Kanban.Api.csproj src/Detran.Kanban.Api/

RUN dotnet restore src/Detran.Kanban.Api/Detran.Kanban.Api.csproj

# Copy all source files
COPY src/Detran.Kanban.Domain/ src/Detran.Kanban.Domain/
COPY src/Detran.Kanban.Application/ src/Detran.Kanban.Application/
COPY src/Detran.Kanban.Infrastructure/ src/Detran.Kanban.Infrastructure/
COPY src/Detran.Kanban.Api/ src/Detran.Kanban.Api/

# Copy compiled frontend assets into wwwroot of the Web API
COPY --from=frontend-build /app/web/dist src/Detran.Kanban.Api/wwwroot/

# Publish the API
RUN dotnet publish src/Detran.Kanban.Api/Detran.Kanban.Api.csproj -c Release -o /app/publish

# Stage 3: Build the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=backend-build /app/publish .

# Environment variables
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "Detran.Kanban.Api.dll"]
