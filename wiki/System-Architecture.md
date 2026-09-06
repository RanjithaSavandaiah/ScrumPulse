# System Architecture

ScrumPulse is engineered as an enterprise-grade, clean-architecture application. It couples a modular .NET 10 ASP.NET Core Web API backend with an Angular 18 reactive single-page frontend, persistent storage via Entity Framework Core 10, and an intelligent coaching copilot driven by Microsoft Agent Framework.

---

## Architectural Layers

The backend follows Uncle Bob's Clean Architecture pattern with clear inversion of control:

```
                  ┌───────────────────────────────┐
                  │        ScrumPulse.Api         │
                  │ (Controllers, Middleware, Host)│
                  └───────────────┬───────────────┘
                                  │
                  ┌───────────────▼───────────────┐
                  │    ScrumPulse.Application     │
                  │ (Use Cases, CQRS, DTOs, Sagas)│
                  └───────┬───────────────┬───────┘
                          │               │
         ┌────────────────▼─────┐   ┌─────▼────────────────┐
         │  ScrumPulse.Domain   │   │   ScrumPulse.AI      │
         │ (Entities, Enums,    │   │ (Microsoft Agent     │
         │  Domain Events)      │   │  Framework, Copilot) │
         └────────────────▲─────┘   └──────────────────────┘
                          │
         ┌────────────────┴───────────────┐
         │   ScrumPulse.Infrastructure   │
         │ (EF Core, AppDbContext, Seed)  │
         └────────────────────────────────┘
```

### 1. `ScrumPulse.Domain` (Core)
- Holds pure domain models, business logic invariants, and enums without any external framework dependencies.
- Key Domain Entities:
  - `Team` / `TeamMember` / `UserRole`
  - `Sprint` / `WorkItem` / `WorkItemStage`
  - `DailyStandup` / `MoodRating`
  - `PullRequestMetric` / `PrComment`
  - `Blocker` / `BlockerRootCause`
  - `LeaveEntry` / `CapacityCalculation`
  - `MonthlyReview` / `ReviewDimension`
  - `RetroCard` / `RetroColumn` / `RetroVote`
  - `AppreciationBadge` / `KudosCard`
  - `TechDebtItem` / `TechTalk`

### 2. `ScrumPulse.Application` (Use Cases)
- Coordinates business transactions, data validation, and application workflows.
- Contains DTO models, mapping logic, service interfaces (`IStandupService`, `IWorkItemService`, `ICapacityCalculator`, `IExecutiveReporter`), and domain event handlers.
- Enforces multi-tenant squad partitioning through the current execution context (`TeamId`).

### 3. `ScrumPulse.Infrastructure` (Data & Persistence)
- Implements repository interfaces and encapsulates `AppDbContext` built on Entity Framework Core 10.
- Supports dual database providers:
  - **PostgreSQL**: Production-grade relational database with connection pooling and schema migrations.
  - **SQLite**: Local developer convenience database requiring zero external infrastructure.
  - **InMemory**: Ultra-fast isolated state provider for xUnit unit/integration tests.
- Configures Global Query Filters:
  ```csharp
  modelBuilder.Entity<WorkItem>().HasQueryFilter(w => w.TeamId == _currentTenant.TeamId);
  ```

### 4. `ScrumPulse.AI` (Agentic Intelligence)
- Integrates with Microsoft Agent Framework and Azure OpenAI / Semantic Kernel.
- Provides context-aware agile coaching prompts, sprint risk prediction, automated retro synthesis, and the interactive ScrumPulse Copilot Agile Chat.

### 5. `ScrumPulse.Api` (Presentation & Gateway)
- ASP.NET Core Web API with strongly-typed endpoint routing.
- Configures security headers middleware, global exception handlers, sliding-window rate limiters, and CORS policies.
- Serves pre-built Angular static assets in production (`wwwroot`).

---

## Frontend Architecture (Angular 18)

The frontend is built using modern Angular 18 best practices:
- **Standalone Components**: Eliminates legacy NgModules for faster lazy loading and tree-shaking.
- **Signals**: Fine-grained reactive state tracking for high-frequency UI components (e.g., 2-minute speaker timer countdown).
- **NgRx Store & Effects**: Predictable unidirectional data flow for enterprise squad state, cached telemetry, and offline tolerance.
- **Vanilla Responsive CSS**: Eliminates heavy utility CSS frameworks, providing handcrafted glassmorphic design and dark-mode aesthetics.

---

## Multi-Tenant Data Isolation

ScrumPulse enforces logical multi-tenancy at the squad level:
1. Every squad receives a unique `TeamId` GUID.
2. The user's active squad is tracked in local state and forwarded via standard request headers (`X-Team-Id`).
3. Entity Framework Core applies global query filters automatically to prevent cross-squad data contamination.
4. Switching squads re-hydrates the NgRx store with the target squad's partition.
