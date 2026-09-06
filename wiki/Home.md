# ScrumPulse Documentation

ScrumPulse is an engineering telemetry and sprint tracking tool designed for scrum teams, tech leads, and delivery managers. It connects day-to-day developer activity (commit logs, PR reviews, daily standups, blocker resolution times) to sprint-level metrics (cycle time, predictability, capacity, health scores).

---

## Documentation Index

| Topic | Summary | Link |
| :--- | :--- | :--- |
| **System Architecture** | Solution structure, Clean Architecture layers, multi-tenant isolation, state management. | [[System Architecture|System-Architecture]] |
| **Security Architecture** | RBAC rules, Scrum Master PIN gateway, OWASP headers, rate limiting, vulnerability reporting. | [[Security Architecture|Security-Architecture]] |
| **Daily Standup & Work Items** | 2-minute speaker timer, async submissions, 7-stage micro-pipeline, DoR/DoD quality gates. | [[Daily Standup & Work Items|Daily-Standup-and-Work-Items]] |
| **PR Turnaround & Blocker SLA** | PR turnaround telemetry, review scorecards, blocker categories, >8h SLA breach alerts. | [[PR Turnaround & Blocker SLA|PR-Turnaround-and-Blocker-SLA]] |
| **Leave, Capacity & Team Roster** | Working-day capacity formulas, holiday calendar math, squad roster, active WIP limits. | [[Leave, Capacity & Team Roster|Leave-Capacity-and-Team-Roster]] |
| **Reviews, Retros & Appreciation** | 360-degree monthly reviews, 4-column retro board with upvoting, peer recognition badges. | [[Reviews, Retros & Appreciation|Reviews-Retros-and-Appreciation]] |
| **Growth, Tech Hub & AI Coach** | Say-Do predictability, sprint letter grades, technical debt tracking, Microsoft AI Coach. | [[Growth, Tech Hub & AI Coach|Growth-TechHub-and-AICoach]] |
| **Executive Suite & RBAC Hub** | 6-dimension sprint health score formula, PDF/CSV/JSON export, squad tenant management. | [[Executive Suite & RBAC|Executive-Suite-and-RBAC]] |
| **Developer Setup Guide** | Local environment setup, .NET 10, Node 22, database seeding, running test suites. | [[Developer Setup Guide|Developer-Setup-Guide]] |
| **CI/CD & Deployment** | GitHub Actions workflow, multi-stage Dockerfile, Render deployment blueprint. | [[CI/CD & Deployment|CI-CD-and-Deployment]] |
| **API Reference** | Route catalog, HTTP verbs, payload schemas, query parameters, error responses. | [[API Reference|API-Reference]] |

---

## Tech Stack Overview

| Layer | Technologies | Notes |
| :--- | :--- | :--- |
| **Backend** | .NET 10, C# 14, ASP.NET Core Web API | Clean Architecture with CQRS handlers. |
| **ORM / Storage** | Entity Framework Core 10, PostgreSQL, SQLite | PostgreSQL in production; SQLite for zero-dependency local dev. |
| **Frontend** | Angular 22, TypeScript, NgRx, Signals | Standalone components, reactive store, responsive CSS (no Tailwind). |
| **AI Integration** | Microsoft Agent Framework, Azure OpenAI | Prompt orchestration, automated risk synthesis, copilot chat. |
| **Testing** | xUnit, Moq, Karma, Jasmine, Playwright | 93 backend unit/integration tests, 259 UI tests, 30 Playwright E2E tests. |
| **Containers / Hosting** | Docker, Render.com, GitHub Actions | Multi-stage Alpine build; auto-deploy on push to `main`. |

---

## Quick Start Links

- Setting up locally: see the [[Developer Setup Guide|Developer-Setup-Guide]].
- REST endpoints and payloads: see the [[API Reference|API-Reference]].
- Vulnerability reports: see the [[Security Policy|Security-Architecture]] or [`.github/SECURITY.md`](https://github.com/RanjithaSavandaiah/ScrumPulse/blob/main/.github/SECURITY.md).
