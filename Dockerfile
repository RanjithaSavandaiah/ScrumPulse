# ----------------------------------------------------
# Stage 1: Build Frontend (Angular Standalone)
# ----------------------------------------------------
FROM node:24-alpine AS frontend-build
WORKDIR /app/frontend
COPY src/ScrumPulse.UI/package*.json ./
RUN npm install --legacy-peer-deps
COPY src/ScrumPulse.UI/ ./
RUN npm run build

# ----------------------------------------------------
# Stage 2: Build Backend (.NET 10 Web API)
# ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS backend-build
WORKDIR /src

# Copy project files and restore
COPY src/ScrumPulse.Domain/ScrumPulse.Domain.csproj src/ScrumPulse.Domain/
COPY src/ScrumPulse.Application/ScrumPulse.Application.csproj src/ScrumPulse.Application/
COPY src/ScrumPulse.Infrastructure/ScrumPulse.Infrastructure.csproj src/ScrumPulse.Infrastructure/
COPY src/ScrumPulse.AI/ScrumPulse.AI.csproj src/ScrumPulse.AI/
COPY src/ScrumPulse.Api/ScrumPulse.Api.csproj src/ScrumPulse.Api/

RUN dotnet restore src/ScrumPulse.Api/ScrumPulse.Api.csproj

# Copy source files and publish
COPY src/ src/
RUN dotnet publish src/ScrumPulse.Api/ScrumPulse.Api.csproj -c Release -o /app/publish

# Copy compiled Angular assets into API's wwwroot directory
COPY --from=frontend-build /app/frontend/dist/scrum-pulse.ui/browser /app/publish/wwwroot/

# ----------------------------------------------------
# Stage 3: Runtime Environment
# ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

COPY --from=backend-build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "ScrumPulse.Api.dll"]
