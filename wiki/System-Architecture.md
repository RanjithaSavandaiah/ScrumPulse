# System Architecture

ScrumPulse is structured as a Clean Architecture solution with a .NET 10 ASP.NET Core backend and an Angular 22 single-page frontend.

---

## Project Structure & Dependencies

The backend projects strictly enforce inward dependency flow:

```
src/
├── ScrumPulse.Domain/ # Core entities, enums, value objects (no external deps)
├── ScrumPulse.Application/ # CQRS use cases, DTOs, interfaces, validation rules
├── ScrumPulse.Infrastructure/ # EF Core AppDbContext, migrations, repos, seed data
├── ScrumPulse.AI/ # Microsoft Agent Framework service & prompt pipelines
├── ScrumPulse.Api/ # Web API host, middleware, routing, wwwroot SPA host
└── ScrumPulse.UI/ # Angular 22 SPA (standalone components, NgRx store)
```

Dependency relationships:
- `ScrumPulse.Domain`: Has zero project dependencies. Contains entities (`Team`, `WorkItem`, `Sprint`, `Blocker`, etc.) and domain constants.
- `ScrumPulse.Application`: References `Domain`. Contains application service contracts, request/response models, and business logic.
- `ScrumPulse.Infrastructure`: References `Application` and `Domain`. Implements database persistence via EF Core 10, migrations, and seed logic.
- `ScrumPulse.AI`: References `Application` and `Domain`. Implements LLM orchestration via Microsoft Agent Framework.
- `ScrumPulse.Api`: References `Infrastructure`, `Application`, and `AI`. Configures middleware, DI container, HTTP endpoints, rate limiters, and serves static frontend assets.

---

## Data Layer & Multi-Tenancy

### Storage Providers
- **Development**: SQLite (`Data Source=ScrumPulse.db`). Requires no external daemon; `DbInitializer.cs` runs migrations and seeds demo data on boot if empty.
- **Production**: PostgreSQL. Configured via `ConnectionStrings:DefaultConnection` or `DATABASE_URL`.
- **Testing**: EF Core InMemory provider for isolated xUnit runs.

### Tenant Isolation via Global Query Filters
All team-scoped entities inherit a common `TeamId` property. Multi-tenancy is enforced at the DbContext level using EF Core Global Query Filters:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
 base.OnModelCreating(modelBuilder);

 // Global query filter ensures queries are scoped to the active squad
 modelBuilder.Entity<WorkItem>()
 .HasQueryFilter(w => w.TeamId == _currentTenant.TeamId);

 modelBuilder.Entity<DailyStandup>()
 .HasQueryFilter(s => s.TeamId == _currentTenant.TeamId);

 modelBuilder.Entity<Blocker>()
 .HasQueryFilter(b => b.TeamId == _currentTenant.TeamId);
}
```

The active squad is passed in incoming HTTP requests via the `X-Team-Id` header and resolved in DI by `TenantContextMiddleware`.

---

## Frontend Architecture (Angular 22)

The frontend is located at `src/ScrumPulse.UI`:
- **Standalone Components**: No `NgModule` boilerplate. Each feature component explicitly imports its required Angular directives.
- **Angular Signals**: Used for fast, reactive UI components (e.g., the 2-minute speaker countdown timer, live blocker timers) to prevent unnecessary change-detection cycles.
- **NgRx Store & Effects**: Used for squad-wide data that requires caching across tabs: work items, capacity logs, active sprint metadata, and current squad context.
- **Styling**: Handcrafted responsive CSS without Tailwind. Uses CSS custom properties (`--bg-primary`, `--accent-color`, etc.) for consistent dark/light styling.
- **Production Delivery**: In Docker/production, Angular builds to `src/ScrumPulse.Api/wwwroot`, where ASP.NET Core serves `index.html` with SPA fallback routing.
