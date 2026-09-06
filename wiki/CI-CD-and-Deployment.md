# CI/CD Pipeline & Production Deployment

This guide documents ScrumPulse's automated CI/CD pipeline, containerization strategy, and production cloud hosting.

---

## 1. CI/CD Architecture (GitHub Actions)

ScrumPulse runs automated quality gates on every push to `main` and all pull requests via `.github/workflows/ci.yml`.

```
                    ┌─────────────────────────┐
                    │      Push / PR Event    │
                    └────────────┬────────────┘
                                 │
                 ┌───────────────┴───────────────┐
                 │                               │
       ┌─────────▼─────────┐           ┌─────────▼─────────┐
       │   Backend Gate    │           │   Frontend Gate   │
       │  (Ubuntu Latest)  │           │  (Ubuntu Latest)  │
       └─────────┬─────────┘           └─────────┬─────────┘
                 │                               │
        1. Setup .NET 10                1. Setup Node 22
        2. dotnet restore               2. npm ci
        3. dotnet build /warnaserror    3. ng test (Headless)
        4. dotnet test (xUnit)          4. Console Sentinel Check
        5. Vulnerability Audit
                 │                               │
                 └───────────────┬───────────────┘
                                 │
                    ┌────────────▼────────────┐
                    │   Quality Gate Passed   │
                    │   & Container Trigger   │
                    └─────────────────────────┘
```

### Key Automated Quality Gates
1. **Strict Warning-as-Error**: The backend build fails on any compiler warning (`/warnaserror`).
2. **Zero NuGet Vulnerabilities**: `dotnet list package --vulnerable --include-transitive` verifies all dependencies are CVE-free.
3. **Console Sentinel**: Frontend tests enforce zero console warnings or uncaught exceptions during execution.

---

## 2. Production Containerization (Docker)

ScrumPulse uses a multi-stage `Dockerfile` producing a lean, production-hardened container:

### Stage Breakdown
- **Stage 1: Frontend Build (`node:22-alpine`)**
  - Installs npm dependencies.
  - Compiles optimized Angular 18 production bundle with Ahead-of-Time (AOT) compilation.
- **Stage 2: Backend Build & Publish (`mcr.microsoft.com/dotnet/sdk:10.0`)**
  - Compiles .NET solution in `Release` configuration.
  - Publishes trimmed binaries.
  - Copies Angular production artifacts into `src/ScrumPulse.Api/wwwroot`.
- **Stage 3: Runtime Container (`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`)**
  - Minimal Alpine Linux base image.
  - Non-root user execution (`USER app`).
  - Read-only filesystem privileges where applicable.

### Local Docker Build & Run
```bash
# Build production image
docker build -t scrumpulse:latest .

# Run container exposing port 8080
docker run -d -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=scrumpulse;Username=postgres;Password=secret" \
  scrumpulse:latest
```

---

## 3. Cloud Deployment (Render.com)

ScrumPulse is configured for automated Infrastructure-as-Code (IaC) deployment via `render.yaml`:

- **Web Service**: Runs the Dockerized web API serving both backend endpoints and Angular SPA static files.
- **Managed Database**: PostgreSQL database with daily automated backups, connection pooling, and SSL enforcement.
- **Automatic Deploys**: Render automatically pulls and builds the updated Docker container upon every commit to `main`.
