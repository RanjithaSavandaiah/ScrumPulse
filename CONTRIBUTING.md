# Contributing to ScrumPulse

Thank you for contributing to ScrumPulse! This guide ensures consistency across the codebase.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Playwright](https://playwright.dev/) (`npx playwright install --with-deps chromium`)

## Quick Start

```bash
# Backend
dotnet restore ScrumPulse.slnx
dotnet build ScrumPulse.slnx -c Release /warnaserror
dotnet test ScrumPulse.slnx -c Release

# Frontend
cd src/ScrumPulse.UI
npm ci --legacy-peer-deps
npm run build
npm run test:ci

# E2E
npx playwright test
```

## Branch Naming

| Type | Pattern | Example |
|---|---|---|
| Feature | `feature/{ticket}-{short-description}` | `feature/SP-42-blocker-hours` |
| Bugfix | `fix/{ticket}-{short-description}` | `fix/SP-99-latency-calc` |
| Refactor | `refactor/{area}` | `refactor/cqrs-handlers` |
| Docs | `docs/{topic}` | `docs/architecture` |

## Pull Request Guidelines

1. **Title**: `[SP-{ticket}] {imperative verb} {what changed}` — e.g., `[SP-42] Add blocker hours impact to velocity`
2. **Description**: Include What, Why, and How sections
3. **Tests**: Every PR MUST include backend, frontend, and E2E tests
4. **Build Gate**: PR must pass `dotnet build -c Release /warnaserror` with zero warnings
5. **Review**: At least 1 approval required before merge

## Code Style

See [ARCHITECTURE.md](ARCHITECTURE.md) for layer responsibilities and `.agents/rules/CODING_STANDARDS.md` for detailed coding standards.

### Key Rules

- **Controllers are thin** — all business logic in CQRS handlers or domain services
- **CQRS handlers are auto-discovered** — no manual DI registration needed
- **Use `Result` monad** for domain validation — don't throw exceptions for expected failures
- **All async methods** accept `CancellationToken ct = default`
- **Read queries** use `AsNoTracking()`
- **Strings from users** are `.Trim()`ed before storage

## Testing Requirements

| Layer | Framework | Command | Location |
|---|---|---|---|
| Backend Unit | xUnit | `dotnet test` | `tests/ScrumPulse.Tests/` |
| Frontend Unit | Karma/Jasmine | `npm run test:ci` | `*.spec.ts` co-located |
| E2E | Playwright | `npx playwright test` | `src/ScrumPulse.UI/e2e/` |

### Test Naming Convention
```
{Method}_{WhenCondition}_{ExpectedResult}
// Example: Create_WhenUserIsDeveloper_ReturnsForbidden
```

## Security Checklist (for reviewers)

- [ ] No secrets or connection strings in committed code
- [ ] User input is trimmed and validated
- [ ] New endpoints have appropriate `[RequireScrumMaster]` or public access justification
- [ ] No raw exception messages exposed in production responses
- [ ] CSV/JSON exports escape user-generated content
