# Security Architecture & Governance

ScrumPulse is designed with a defense-in-depth security model protecting agile metrics, sprint governance data, team rosters, and AI interactions across all architectural layers.

---

## 1. Role-Based Access Control (RBAC)

ScrumPulse enforces clear privilege boundaries across seven roles:

| Role | Permissions & Governance |
| :--- | :--- |
| **Developer** (Default) | Submit daily standup logs, log PR metrics, create work items, post kudos, add retro cards, and vote. |
| **Scrum Master** (Privileged) | Modify sprint dates/goals, delete work items, approve leaves, manage roster membership, and create new squads. Protected by PIN. |
| **QA Engineer** | Advance work items into `InQa` and `Done`, log defect leakage, track automated test suite health. |
| **CDL / Delivery Lead** | Access multi-squad analytics, executive dashboards, 360 review coaching, capacity forecasting. |
| **Product Owner** | Manage backlog priority, define sprint goals, verify Definition of Ready (DoR) gates. |
| **Client Stakeholder** | View-only access to executive suite, sprint health score, team velocity trends, and exported reports. |
| **Agile Coach** | Access team growth telemetry, AI coaching insights, maturity metrics, and retrospective action items. |

### Scrum Master PIN Gateway
Privileged administrative actions (such as deleting items or editing sprint parameters) require entering a secure Scrum Master PIN (`SM_PIN`).
- The PIN is verified against a PBKDF2/SHA-256 hashed secret.
- Failed PIN attempts are intercepted, logged, and rate-limited to 5 attempts per minute to stop brute-force attacks.

---

## 2. OWASP Security Headers

The ASP.NET Core pipeline implements `SecurityHeadersMiddleware`, injecting the following protective headers on every response:

```http
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; frame-ancestors 'none';
X-Frame-Options: DENY
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
Strict-Transport-Security: max-age=31536000; includeSubDomains
Permissions-Policy: camera=(), microphone=(), geolocation=()
Referrer-Policy: strict-origin-when-cross-origin
```

- **Clickjacking Defense**: `X-Frame-Options: DENY` and `frame-ancestors 'none'` prevent embedding inside malicious iframes.
- **MIME Sniffing**: `X-Content-Type-Options: nosniff` stops browser MIME-type confusion attacks.
- **Strict HTTPS**: `Strict-Transport-Security` enforces end-to-end encrypted transport.

---

## 3. Rate Limiting Architecture

To guard against Denial of Service (DoS) and API abuse, ScrumPulse uses partitioned ASP.NET Core rate limiters:

1. **Global Sliding Window**: 60 requests per minute per client IP across general API routes.
2. **Authentication / PIN Fixed Window**: 5 attempts per minute per client IP to mitigate password and PIN dictionary attacks.
3. **AI Coach Token Bucket**: Bounded token bucket limiter for generative AI coaching endpoints to prevent API quota exhaustion and financial spikes.

---

## 4. Injection & Sanitization Safeguards

- **SQL Injection**: 100% of database interactions use Entity Framework Core parameterized LINQ queries. Raw unescaped SQL strings are strictly prohibited in the codebase.
- **Cross-Site Scripting (XSS)**: Angular’s contextual DOM sanitization automatically neutralizes malicious script payloads in templates. Markdown and rich-text rendering is passed through `DOMPurify`.
- **Dependency Auditing**: The CI/CD pipeline enforces `dotnet list package --vulnerable --include-transitive` and `npm audit` on every pull request and push to `main`. Zero known vulnerabilities are permitted.

---

## 5. Security Policy & Vulnerability Reporting

ScrumPulse maintains an official security policy located at [`.github/SECURITY.md`](https://github.com/RanjithaSavandaiah/ScrumPulse/blob/main/.github/SECURITY.md).

Researchers can report vulnerabilities via:
- **GitHub Private Vulnerability Reporting** via the repository Security tab.
- **Email**: `lsranjitha@gmail.com` with subject `[SECURITY] ScrumPulse Vulnerability Report`.
- **Response SLAs**: Initial response within 24–48 hours; triage within 3 business days; emergency patches within 7–14 days.
