# ScrumPulse Architecture

> Enterprise Scrum Management Platform — .NET 10 API + Angular 22 SPA + AI Coaching

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Browser (Angular 22 SPA)                       │
│  NgRx Store ← Effects ← Services ← HttpClient ← Interceptors           │
└───────────────────────────────────┬──────────────────────────────────────┘
                                    │ HTTP/REST
┌───────────────────────────────────▼──────────────────────────────────────┐
│                          ASP.NET 10 API Layer                           │
│  SecurityHeaders → Tenant → RateLimiting → ExceptionHandler → Routing  │
│                                                                         │
│  ┌──────────────┐  ┌──────────────────────────────────────────────────┐ │
│  │  Controllers  │→│  IMediator (CQRS) → Handlers → UoW → SaveChanges│ │
│  │  (thin)       │  │  ISaga → Steps → Compensate                     │ │
│  └──────────────┘  └──────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼──────────────────────────────────────┐
│                          Infrastructure Layer                           │
│  EfRepository<T> → AppDbContext → SQLite/PostgreSQL                     │
│  MetricsCalculatorService │ TeamPerformanceService │ DomainEventDispatch │
└──────────────────────────────────────────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼──────────────────────────────────────┐
│                          Domain Layer (Zero Dependencies)               │
│  Entities │ Value Objects │ Enums │ Domain Events │ Result Monad         │
│  WorkItemValidator │ BaseEntity (soft-delete, audit)                     │
└──────────────────────────────────────────────────────────────────────────┘
```

## Layer Responsibilities

### Domain (`ScrumPulse.Domain`)
- **Entities**: `WorkItem`, `Sprint`, `Team`, `TeamMember`, `Blocker`, `DailyStandup`, `TeamLeave`, `Monthly1on1Feedback`, `RetroCard`, `RetroActionItem`, `KudosCard`, `TechDebtItem`, `TechTalkLog`, `PullRequestReviewLog`
- **Base Entity**: Provides `Id`, `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted` (soft-delete), `DomainEvents` collection
- **Enums**: `WorkItemStatus` (Created→PickedUp→PrCreated→PrApproved→PrMerged→QaTesting→Completed), `WorkItemType`, `PriorityLevel`, `RoleType`, `BlockerCategory`
- **Validators**: `WorkItemValidator` — centralized validation for acceptance criteria, title requirements
- **Result Monad**: Railway-oriented error handling without exceptions

### Application (`ScrumPulse.Application`)
- **CQRS**: Commands (Create/Update/Delete mutations) and Queries (read-only) with typed handlers
- **Sagas**: `WorkItemCompletionSaga` with compensating transactions for multi-step workflows
- **DTOs**: Immutable records with `[Required]`, `[StringLength]`, `[Range]` validation attributes
- **Specifications**: Composable query filters with paging, ordering, and includes
- **Mapping**: `MappingExtensions` for Entity↔DTO projection

### Infrastructure (`ScrumPulse.Infrastructure`)
- **Persistence**: EF Core `AppDbContext` with global query filters (soft-delete), `DbInitializer` with schema migration
- **Repositories**: Generic `EfRepository<T>` with Specification pattern, `EfUnitOfWork` with domain event dispatch
- **Services**: `MetricsCalculatorService`, `TeamPerformanceService`, `MemoryIdempotencyStore`
- **Registration**: `CqrsHandlerScanner` for auto-discovery of handlers (OCP)

### API (`ScrumPulse.Api`)
- **Controllers**: Thin orchestrators — parse request → dispatch command → return result
- **Middleware Pipeline**: `SecurityHeaders` → `Tenant` → `RateLimiting` → `ExceptionHandler` → Routing
- **Filters**: `RequireScrumMasterAttribute` for role-based endpoint protection
- **Health**: `/health` endpoint backed by EF Core DbContext health check

### AI (`ScrumPulse.AI`)
- **MicrosoftAgentService**: Orchestrates AI coaching with RAG-like context building
- **Strategies**: `IInsightGenerator` implementations for different analysis types
- **Prompt**: Configurable prompt templates for copilot chat

## Design Patterns

| Pattern | Implementation | Location |
|---|---|---|
| CQRS | `ICommandHandler<TCmd, TRes>`, `IQueryHandler<TQry, TRes>` | Application/CQRS |
| Mediator | `AppMediator` dispatching to typed handlers | Infrastructure/Services |
| Repository | `EfRepository<T>` with Specification pattern | Infrastructure/Repositories |
| Unit of Work | `EfUnitOfWork` with domain event dispatch | Infrastructure/Repositories |
| Saga | `ISagaStep<TContext>` with compensating transactions | Application/Sagas |
| Strategy | `IInsightGenerator` for AI analysis strategies | AI/Strategies |
| Result Monad | `Result<T>` with Map/Bind/Tap for functional composition | Domain/Common |
| Specification | `ISpecification<T>` with criteria, includes, ordering, paging | Application/Specifications |
| Domain Events | `IDomainEvent` dispatched post-commit via `DomainEventDispatcher` | Domain/Events |

## Security Architecture

1. **OWASP Security Headers**: CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy
2. **Rate Limiting**: `System.Threading.RateLimiting` with 60 req/min per IP
3. **Idempotency**: `X-Idempotency-Key` header for POST endpoints with `MemoryIdempotencyStore`
4. **Tenant Isolation**: `TenantMiddleware` resolves team context from headers/query/cookies
5. **Role Authorization**: `[RequireScrumMaster]` filter for SM/CDL/AgileCoach-only endpoints
6. **Error Sanitization**: Production responses scrub SQL, EF Core, and stack trace details
7. **Server Fingerprint Suppression**: `X-Powered-By` and `Server` headers removed

## Data Flow

```
User Action → Angular Component → NgRx Action → Effect → HTTP Service
    → API Controller → IMediator.SendAsync(Command) → CommandHandler
    → IUnitOfWork.Repository<T>() → EfRepository → AppDbContext
    → SaveChangesAsync → DomainEventDispatcher → Response DTO
    → Controller → HTTP Response → Effect → Reducer → Selector → Component
```

## Deployment

- **Hosting**: Render.com (free tier) at `https://scrumpulse.onrender.com`
- **CI/CD**: GitHub Actions → Backend Tests → Frontend Tests → E2E → Deploy webhook
- **Database**: SQLite (dev/demo), PostgreSQL-ready via `SchemaDialect` abstraction
- **Frontend**: Angular SPA built and deployed to API's `wwwroot/` directory
