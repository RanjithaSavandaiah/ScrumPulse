# Security Policy

## Supported Versions

| Version | Supported | Notes |
| :--- | :---: | :--- |
| `1.0.x` (`main`) | Yes | Active development branch; receives patches. |
| `< 1.0` | No | Unsupported. |

## Reporting a Vulnerability

Do not open public GitHub issues or PRs for security vulnerabilities.

### Method 1: GitHub Security Advisory (Preferred)
Open a draft advisory under [Security > Advisories > Report a vulnerability](https://github.com/RanjithaSavandaiah/ScrumPulse/security/advisories/new). This keeps the report confidential to maintainers.

### Method 2: Email
Send details to `lsranjitha@gmail.com` with the subject line:
```
[Security Bug] ScrumPulse: <short description>
```

### Report Details
Include the following in your submission:
1. Vulnerability description and potential impact (e.g. auth bypass, SQLi, XSS, tenant data leak).
2. Affected component/file (e.g. `src/ScrumPulse.Api/Controllers/WorkItemsController.cs`, `src/ScrumPulse.UI`).
3. Minimal proof-of-concept (PoC) or exact curl/HTTP request and reproduction steps.
4. Suggested fix, if you have one.

## Response Timelines
- **Acknowledgment**: Within 48 hours.
- **Triage & severity validation**: Within 3 business days.
- **Patch release**: Critical/High issues are prioritized for an out-of-band release; Medium/Low are patched in the next scheduled build.

## In-Code Security Controls
For reference, the repository implements the following baseline controls:
- **Data Access**: 100% Entity Framework Core parameterized LINQ queries; no raw string interpolation in SQL.
- **Multi-Tenancy**: Global EF Core query filters on `TeamId` to isolate squad data.
- **Headers**: `SecurityHeadersMiddleware` sets `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, and `Strict-Transport-Security`.
- **Rate Limiting**: ASP.NET Core rate limiting middleware (sliding window: 60 req/min for general API routes; fixed window: 5 attempts/min on auth routes).
- **Role Elevation**: Privileged Scrum Master actions require a secondary PIN verified against a PBKDF2/SHA-256 hash.
- **Dependencies**: Automated audit via `dotnet list package --vulnerable --include-transitive` in CI.
