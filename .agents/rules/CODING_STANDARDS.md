# ScrumPulse Coding Standards & Agent Rules

These rules MUST be followed for ALL code generation, modification, and review across the ScrumPulse codebase. This file serves as the persistent coding standard for AI agents operating on this repository.

---

## Architecture Rules

### Clean Architecture Layers
1. **Domain** — Entities, value objects, enums, domain events, `Result` monad. ZERO external dependencies.
2. **Application** — CQRS commands/queries, DTOs, specifications, sagas, mapping, service interfaces. References only Domain.
3. **Infrastructure** — EF Core persistence, repository implementations, external service integrations, DI registration. References Application + Domain.
4. **API** — Thin controllers, middleware, filters. References Application + Infrastructure + AI.
5. **AI** — Agent service, strategies, prompt building. References Application.

### SOLID Principles
- **SRP**: Controllers must NOT contain business logic. All validation, transformation, and persistence logic goes in Application/Domain services or CQRS handlers.
- **OCP**: New CQRS handlers are auto-discovered via `CqrsHandlerScanner`. Do NOT manually register handlers in `DependencyInjection.cs`.
- **LSP**: All repository implementations must satisfy `IAsyncRepository<T>` contract including soft-delete awareness.
- **ISP**: Prefer specific interfaces (`IMetricsCalculatorService`, `ITeamPerformanceService`) over fat god-interfaces.
- **DIP**: Controllers depend on `IMediator`, `IAppDbContext`, not concrete implementations.

### Validation Pattern
- Use `WorkItemValidator` (Domain layer) for work item validation — do NOT duplicate validation in controllers.
- Use `[ApiController]` on `BaseApiController` for automatic model validation of `[Required]`, `[StringLength]`, `[Range]` attributes.
- Use `Result` monad for domain validation results — do NOT throw exceptions for expected validation failures.

---

## Security Rules (Critical — Public Repo & Public Hosting)

### Authorization
- Use `[RequireScrumMaster]` attribute filter for SM-only endpoints. Do NOT inline role-checking in controllers.
- Role is read from `X-User-Role` header. The filter denies `Developer`, `QaEngineer`, `ProductOwner`, `ClientStakeholder` roles.

### Input Sanitization
- All string inputs stored in the database MUST be `.Trim()`ed.
- CSV export MUST escape double quotes in user-generated content.
- NEVER expose raw exception messages in production — `SanitizeErrorDetail()` scrubs SQL, EF Core, and stack trace patterns.

### Headers
- `SecurityHeadersMiddleware` sets OWASP headers (CSP, HSTS, X-Frame-Options, etc.).
- `X-Powered-By` and `Server` headers are stripped to prevent tech stack fingerprinting.
- CORS is restricted to configured origins only.

### Data Protection
- SQLite database files (`*.db`, `*.db-shm`, `*.db-wal`) are in `.gitignore` — NEVER commit database files.
- `appsettings.Development.json` is gitignored — NEVER commit secrets or connection strings.

---

## .NET 10 Coding Standards

### Required Patterns
- Use **primary constructors** for DI injection in controllers, middleware, and services.
- Use **collection expressions** `[..]` instead of `new List<T>()` or `Array.Empty<T>()`.
- Use **file-scoped namespaces** (`namespace X;` not `namespace X { }`).
- Use **nullable reference types** — the project enforces `<Nullable>enable</Nullable>`.
- Use **`CancellationToken ct = default`** on all async controller actions and service methods.
- Use **`AsNoTracking()`** for read-only EF Core queries.

### Build Requirements
- Build MUST pass with `/warnaserror` — zero warnings, zero errors.
- All public APIs must have XML documentation comments (`///`).
- Use `sealed` on classes not designed for inheritance.

### Naming Conventions
- Entities: PascalCase, singular (`WorkItem`, `Sprint`, `TeamMember`).
- DTOs: `{Entity}Dto` suffix.
- Commands: `{Verb}{Entity}Command` (e.g., `CreateWorkItemCommand`).
- Queries: `Get{Entity}Query` (e.g., `GetWorkItemsQuery`).
- Handlers: `{CommandName}Handler` suffix.
- Controllers: `{Entity}Controller` (plural) — `WorkItemsController`, `SprintsController`.

---

## Angular 22 Coding Standards

### Required Patterns
- **Standalone components** — all components must be standalone (no NgModules).
- **Lazy routes** — use `loadComponent: () => import(...)` in `app.routes.ts`, not eager imports.
- **NgRx Store** for state management — actions, reducers, effects, selectors.
- **Reactive forms** for all form handling.
- Use **`@defer`** blocks for heavy UI sections (reports, charts).
- Use **`trackBy`** on all `@for` loops to prevent unnecessary DOM re-renders.

### HTTP Patterns
- All HTTP calls go through NgRx effects → service → `HttpClient`.
- Use `tenantInterceptor` to attach `X-Team-Id` header.
- Use `errorInterceptor` for global error handling.

### Testing
- Component tests: Use Angular `TestBed` with `provideHttpClientTesting()`.
- Service tests: Mock HTTP with `HttpTestingController`.
- State tests: Test reducers and selectors as pure functions.
- E2E tests: Playwright with page object model.

---

## Testing Standards

### Backend (xUnit)
- Test file location: `tests/ScrumPulse.Tests/{Layer}/{ClassName}Tests.cs`.
- Use in-memory SQLite for data layer tests: `UseInMemoryDatabase(Guid.NewGuid().ToString())`.
- Test naming: `{Method}_When{Condition}_Returns{Expected}` or `{Method}_{Scenario}_{Expected}`.
- MUST test: all CQRS handlers, middleware, filters, domain validators, saga steps.

### Frontend (Karma/Jasmine)
- Spec file co-located with component: `*.spec.ts`.
- CI command: `npm run test:ci` (ChromeHeadlessCI, no watch).

### E2E (Playwright)
- Location: `src/ScrumPulse.UI/e2e/*.spec.ts`.
- Helpers in `e2e/helpers.ts`.
- CI command: `npx playwright test`.
- MUST include console sentinel tests — zero `console.error` in production.

---

## CI/CD Pipeline Requirements
- **Backend**: `dotnet build -c Release /warnaserror` → `dotnet test`
- **Frontend**: `npm ci --legacy-peer-deps` → `npm run build` → `npm run test:ci`
- **E2E**: Full stack deployment → `npx playwright test`
- Deploy: Render webhook on main branch push (after all gates pass)
