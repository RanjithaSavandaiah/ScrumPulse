# ScrumPulse

An agile delivery intelligence and engineering velocity platform built for distributed scrum teams, technical leads, and delivery managers in service-based client engagements.

ScrumPulse connects granular engineering metrics (micro-stage cycle times, PR turnaround, blocker SLAs, standup logs) with delivery governance (sprint predictability, capacity forecasting factoring in leave, team growth trends, and client-ready reporting).

---

## Table of Contents

- [Core Capabilities](#core-capabilities)
- [Technology Stack](#technology-stack)
- [System Architecture](#system-architecture)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Backend Setup](#backend-setup)
  - [Frontend Setup](#frontend-setup)
- [Configuration & Environment Variables](#configuration--environment-variables)
- [Database & Persistence Notes](#database--persistence-notes)
- [Testing](#testing)
- [Production Deployment & Docker](#production-deployment--docker)
- [API Overview](#api-overview)

---

## Core Capabilities

### 1. Work Item & PR Lifecycle Tracking
- Tracks work items across 7 explicit stages: `Backlog` &rarr; `InProgress` &rarr; `PrCreated` &rarr; `PrApproved` &rarr; `Merged` &rarr; `InQa` &rarr; `Done`.
- Calculates stage latencies automatically:
  - **Pickup Latency**: Duration between sprint start or item assignment and active pickup.
  - **Dev Cycle Time**: Time spent actively coding until pull request creation.
  - **PR Review Latency**: Time waiting for peer code review and approval.
  - **PR Merge Latency**: Time between approval and branch merge into the primary branch.
  - **QA Testing Latency**: Duration of verification on staging or test environments.
  - **Total Cycle Time**: Cumulative lead time from pickup to production-ready `Done`.

### 2. Interactive Quality Gates (DoR & DoD)
- **Definition of Ready (DoR)**: Verifies that acceptance criteria are unambiguous, wireframes/specifications are attached, and cross-team dependencies are resolved before work begins.
- **Definition of Done (DoD)**: Enforces unit test pass thresholds, peer review sign-offs, branch merges, and smoke testing verification before an item can be closed.

### 3. Blocker Management & SLA Escalation
- Tracks blockers categorized into four distinct areas: `ClientClarification`, `TechLeadArchitecture`, `EnvironmentAccess`, and `ThirdPartyApi`.
- Features real-time waiting-time counters, SLA breach alerts (>8 hours), and historical resolution tracking to pinpoint operational bottlenecks.

### 4. Sprint Capacity & Leave Planner
- Multi-member vacation and leave calendar supporting full-day, first-half, and second-half bookings across multiple leave categories.
- Uses working-day calculations (excluding weekends) to deduct hours from available individual and squad capacity.
- Generates data-driven story point commitment recommendations for sprint planning based on net focus hours.

### 5. Team Performance & Client Showcase
- Built specifically for service-based delivery organizations to demonstrate delivery maturity, capability improvement, and consistent velocity to client stakeholders.
- Tracks multi-sprint growth trends, defect leakage reduction, PR turnaround acceleration, and high-impact technical contributions.
- Provides one-click client-facing summary reports highlighting team wins and delivery value.

### 6. Daily Standups & Meeting Timer
- Asynchronous 3-question standup submissions (Yesterday's work, Today's plan, Blockers) with team mood and energy indexing.
- Includes a 2-minute round-robin meeting timer to keep live standup discussions concise and focused.

### 7. 360° Monthly 1:1 Reviews & Happiness Barometer
- Structured 4-way performance input:
  - Scrum Master guidance
  - Career Development Lead (CDL) technical coaching
  - Client / Product Owner satisfaction
  - Developer self-reflection
- Tracks Scrum Master ratings (1-10) and team happiness trends over time.

### 8. Sprint Retrospectives & Appreciation Wall
- 4-column retrospective board (`Went Well`, `Didn't Go Well`, `Ideas & Experiments`, `Action Items`) with card upvoting and action item assignment.
- Peer appreciation wall with recognition badges (`ProblemSolver`, `TeamPlayer`, `GoalCrusher`, `QualityGuardian`, `InnovationStar`, `ClientShoutout`) and emoji reactions.

### 9. Tech Debt & Knowledge Hub
- Backlog dedicated to tracking technical debt, categorized by severity with target payoff sprint planning.
- Knowledge-sharing log for tracking internal tech talks, architectural spikes, and documentation links.

### 10. Executive Suite & Reporting
- Composite **Sprint Health Score** calculated across 6 dimensions: velocity predictability, defect escape rate, PR turnaround efficiency, standup participation, blocker turnaround, and scope stability.
- Multi-sprint velocity trend analysis and side-by-side sprint comparisons.
- Data export in CSV, JSON, and PDF formats.

### 11. AI Coaching Engine
- Powered by the **Microsoft Agent Framework** to deliver:
  - Individual developer coaching based on review trends and cycle times.
  - Sprint risk radar analyzing WIP limits, say-do ratios, and open blockers.
  - Interactive Copilot chat for agile delivery guidance and process queries.

---

## Technology Stack

| Layer | Technology | Details |
|---|---|---|
| **Backend** | .NET 10 (C# 14) | ASP.NET Core Web API, Clean Architecture |
| **ORM** | Entity Framework Core 10 | PostgreSQL provider (production), SQLite provider (development) |
| **Frontend** | Angular 22 | Standalone components, signals, NgRx store, responsive CSS |
| **AI Integration** | Microsoft Agent Framework | Prompt orchestration, automated coaching, copilot chat |
| **Testing** | xUnit, Moq, Playwright | Domain tests, architecture tests, controller tests, E2E |
| **Containerization** | Docker | Multi-stage build (Alpine Node 22 + .NET 10 SDK + ASP.NET runtime) |
| **Deployment** | Render.com | Blueprint configuration via `render.yaml` with automated health checks |

---

## System Architecture

The solution follows Clean Architecture principles, ensuring that business rules remain decoupled from frameworks, databases, and UI delivery mechanisms:

```
ScrumPulse.Domain
  └── Entities, Enums, Value Objects, Domain Events, BaseEntity

ScrumPulse.Application
  └── Request/Response DTOs, CQRS Handlers, Sagas, Interfaces, Mapping

ScrumPulse.Infrastructure
  └── EF Core AppDbContext, Repositories, Migrations, Seed Data, Services

ScrumPulse.AI
  └── Microsoft Agent Framework integration, Prompt strategies, Coaches

ScrumPulse.Api
  └── ASP.NET Core Controllers, Rate Limiting, Compression, Swagger, SPA Host

ScrumPulse.UI
  └── Angular 22 Standalone Application (served via API wwwroot in production)
```

### Architectural Patterns

- **CQRS (Command Query Responsibility Segregation)**: Applied to high-activity domains (`WorkItems`, `Blockers`) via an in-process mediator (`IMediator`). Query handlers optimize for read projection, while command handlers enforce domain invariants.
- **Saga Orchestration**: The `WorkItemCompletionSaga` executes a 4-step workflow when work items are moved to Done:
  1. Validate DoD quality gates
  2. Transition work item status
  3. Recalculate sprint velocity metrics
  4. Trigger AI coaching evaluation
- **Domain Events**: Entities record domain events (e.g., `WorkItemCompletedEvent`, `BlockerRaisedEvent`) which are automatically dispatched upon `SaveChangesAsync` execution.
- **Idempotency**: Mutative commands can supply an idempotency key tracked by `IIdempotencyStore` with automated TTL cleanup to prevent duplicate processing.
- **Optimistic Concurrency**: Prevents race conditions during simultaneous updates using `RowVersion` (`xmin` system column in PostgreSQL, byte array in SQLite).
- **Soft Delete**: Global EF Core query filters apply to `BaseEntity.IsDeleted`. Queries needing deleted records explicitly use `.IgnoreQueryFilters()`.
- **Multi-Tenancy**: Tenant context (`ITenantContext`) provides squad-level isolation through `TeamId`.

---

## Project Structure

```
c:\ScrumPulse\
├── ScrumPulse.slnx                 # .NET solution file (modern XML format)
├── Dockerfile                      # Multi-stage production container build
├── render.yaml                     # Render.com deployment blueprint
├── CODEBASE.md                     # Detailed AI and developer reference index
├── src/
│   ├── ScrumPulse.Domain/          # Core domain models, enums, domain events
│   │   ├── Common/                 # BaseEntity, ValueObject, IDomainEvent
│   │   ├── Entities/               # Team, Sprint, WorkItem, Blocker, Leave, etc.
│   │   └── Enums/                  # WorkItemStatus, RoleType, BlockerCategory, etc.
│   │
│   ├── ScrumPulse.Application/     # Use cases, interfaces, DTOs, CQRS
│   │   ├── Common/                 # Interfaces (IAppDbContext, IMediator, etc.)
│   │   ├── CQRS/                   # Commands, queries, and handlers
│   │   ├── DTOs/                   # Strongly typed request/response records
│   │   ├── Mapping/                # Extension methods for DTO conversions
│   │   └── Sagas/                  # Multi-step saga orchestrations
│   │
│   ├── ScrumPulse.Infrastructure/  # Persistence, external services, migrations
│   │   ├── Persistence/            # AppDbContext, DbInitializer, Configurations
│   │   └── Services/               # MetricsCalculatorService, TeamPerformanceService
│   │
│   ├── ScrumPulse.AI/              # Microsoft Agent Framework service implementation
│   │   └── Services/               # MicrosoftAgentService, prompt strategies
│   │
│   ├── ScrumPulse.Api/             # Web API host & configuration
│   │   ├── Controllers/            # 17 REST controllers
│   │   ├── Middleware/             # Exception handler, rate limiter, security headers
│   │   ├── Program.cs              # DI wiring, middleware pipeline, SPA fallback
│   │   └── appsettings.json        # Application configuration
│   │
│   └── ScrumPulse.UI/              # Angular 22 frontend
│       ├── proxy.conf.json         # Local dev proxy config (/api -> localhost:5000)
│       └── src/app/
│           ├── core/               # State (NgRx), services, shared icons, models
│           ├── features/           # Standup, Sprint Board, Blockers, Capacity, etc.
│           └── app.component.ts    # Main shell and feature navigation
│
└── tests/
    └── ScrumPulse.Tests/           # Unit, integration, architecture, and controller tests
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/) and npm
- Optional: [Docker](https://www.docker.com/) (for containerized runs)

### Backend Setup

1. Restore dependencies and build the solution:
   ```bash
   dotnet build ScrumPulse.slnx
   ```

2. Run the API project:
   ```bash
   dotnet run --project src/ScrumPulse.Api
   ```

3. The API will start on:
   - HTTP: `http://localhost:5000`
   - HTTPS: `https://localhost:5001`
   - Interactive Swagger docs: `http://localhost:5000/swagger`

> **Note on Initial Run:** By default, the app uses SQLite (`ScrumPulse.db`). On first boot, `DbInitializer` automatically creates the schema and seeds realistic multi-sprint sample data (teams, members, work items, blockers, standups, and leaves) so the platform is immediately functional for testing.

### Frontend Setup

1. Navigate to the UI project directory:
   ```bash
   cd src/ScrumPulse.UI
   ```

2. Install npm packages:
   ```bash
   npm install
   ```

3. Start the Angular development server:
   ```bash
   npm start
   ```

4. Open `http://localhost:4200` in your browser.
   - `proxy.conf.json` is configured in `angular.json` to automatically proxy `/api`, `/swagger`, and `/health` requests to `http://localhost:5000`. No manual CORS configuration is required for local development.

---

## Configuration & Environment Variables

Configuration is handled via `appsettings.json`, environment variables, or cloud provider secret stores.

| Key / Environment Variable | Default | Description |
|---|---|---|
| `DatabaseProvider` | `Sqlite` | Database provider to use: `Sqlite` or `PostgreSql`. |
| `ConnectionStrings__DefaultConnection` / `DATABASE_URL` | `Data Source=ScrumPulse.db` | ADO.NET connection string or standard PostgreSQL connection URI (`postgres://...`). |
| `Auth__ScrumMasterPin` / `SM_PIN` | `""` (disabled) | Optional PIN used to authenticate Scrum Master actions (leave approval, sprint management). |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment: `Development` or `Production`. |
| `PORT` | `8080` (container) | Port for the web server to bind to in production. |
| `SeedDemoData` | `false` | When true, forces seeding of demonstration records even if database already has rows. |

---

## Database & Persistence Notes

### SQLite (Local Development)
- Used by default for rapid setup without external database dependencies.
- Database file is generated as `ScrumPulse.db` in the API runtime directory.

### PostgreSQL (Production / Cloud)
- Set `DatabaseProvider=PostgreSql` and supply `DATABASE_URL` or `ConnectionStrings__DefaultConnection`.
- **URI Normalization**: The application includes built-in normalization in `DependencyInjection.cs` that parses standard cloud PostgreSQL URIs (`postgres://user:password@host:port/database`) into the key-value format expected by Npgsql.
- **Enum Handling**: To prevent casting errors across PostgreSQL versions, all domain enums are explicitly stored as text strings via `HasConversion<string>()` configurations.
- **Timestamp Behavior**: Legacy timestamp behavior (`Npgsql.EnableLegacyTimestampBehavior`) is enabled in `Program.cs` to ensure smooth UTC handling across heterogeneous database versions.

---

## Testing

The test suite covers domain logic, architectural rules, saga workflows, services, and API controllers.

```bash
# Run all backend tests
dotnet test --nologo

# Run backend tests with detailed output
dotnet test --verbosity normal

# Run Angular unit tests (Karma / Jasmine)
cd src/ScrumPulse.UI
npm test

# Run End-to-End tests (Playwright)
cd src/ScrumPulse.UI
npm run test:e2e
```

---

## Production Deployment & Docker

### Multi-Stage Dockerfile

The included `Dockerfile` builds a lightweight, production-ready container:
- **Stage 1 (`node:22-alpine`)**: Builds the Angular 22 application using `--configuration=production`.
- **Stage 2 (`mcr.microsoft.com/dotnet/sdk:10.0-preview`)**: Compiles the .NET 10 Web API and embeds the compiled Angular SPA assets directly into the API's `wwwroot/` folder.
- **Stage 3 (`mcr.microsoft.com/dotnet/aspnet:10.0-preview`)**: Minimal runtime environment including `libgssapi-krb5-2` for secure Npgsql PostgreSQL connections. Exposes port 8080.

To build and run locally with Docker:
```bash
# Build container image
docker build -t scrumpulse:latest .

# Run container with SQLite
docker run -p 8080:8080 -e DatabaseProvider=Sqlite scrumpulse:latest

# Open http://localhost:8080
```

### Deploying to Render.com

ScrumPulse includes a ready-to-use `render.yaml` blueprint:
1. Push this repository to GitHub or GitLab.
2. In [Render.com](https://render.com), select **New +** &rarr; **Blueprint**.
3. Select your repository.
4. Set your environment variables (`DatabaseProvider=PostgreSql` and `ConnectionStrings__DefaultConnection` pointing to your PostgreSQL instance).
5. The container will automatically build, run database migrations on startup, and serve both the API and Angular frontend with automated SSL and health probes at `/healthz`.

---

## API Overview

All API endpoints follow RESTful conventions under the `/api/` prefix. Interactive documentation is available at `/swagger`.

| Endpoint | Methods | Description |
|---|---|---|
| `/api/work-items` | `GET`, `POST`, `PUT`, `DELETE` | Work item management with CQRS and micro-stage advancement |
| `/api/work-items/{id}/advance` | `POST` | Advances work item to next stage (triggers `WorkItemCompletionSaga` on Done) |
| `/api/blockers` | `GET`, `POST`, `PUT`, `DELETE` | Blocker management with category assignment and SLA tracking |
| `/api/blockers/{id}/resolve` | `POST` | Marks blocker as resolved with resolution audit timestamp |
| `/api/sprints` | `GET`, `POST`, `PUT`, `DELETE` | Sprint lifecycle, activation, confidence scoring |
| `/api/leaves` | `GET`, `POST`, `PUT`, `DELETE` | Leave requests, approvals, and member capacity calculation |
| `/api/leaves/capacity/{sprintId}` | `GET` | Computes net sprint capacity factoring in approved leaves |
| `/api/standups` | `GET`, `POST`, `PUT`, `DELETE` | Asynchronous standup submissions and squad history |
| `/api/team-performance/summary` | `GET` | Multi-sprint growth trends, delivery highlights, and velocity |
| `/api/executive-reports/sprint/{id}/health` | `GET` | 6-dimension sprint health composite radar |
| `/api/executive-reports/export-csv` | `GET` | Exports sprint metrics as CSV |
| `/api/executive-reports/export-json` | `GET` | Exports full sprint dataset as JSON |
| `/api/monthly-feedback` | `GET`, `POST`, `PUT` | 360° monthly review records with AI synthesis |
| `/api/retrospectives` | `GET`, `POST` | Retrospective cards, upvoting, and action items |
| `/api/kudos` | `GET`, `POST` | Peer recognition cards and emoji reactions |
| `/api/pull-requests` | `GET`, `POST` | PR turnaround metrics and review comment analysis |
| `/api/tech-hub` | `GET`, `POST` | Tech debt inventory and engineering knowledge sharing logs |
| `/api/ai-coach/copilot-chat` | `POST` | Interactive conversational coaching via Microsoft Agent Framework |
| `/healthz` | `GET` | Health check probe (database connectivity status) |
