# ScrumPulse Wiki

Welcome to the official documentation and engineering wiki for **ScrumPulse** — an enterprise-grade agile delivery intelligence and engineering velocity platform built for distributed scrum teams, technical leads, and delivery managers in client engagements.

ScrumPulse bridges the gap between granular engineering telemetry (micro-stage cycle times, PR turnaround, blocker SLAs, standup logs) and high-level delivery governance (sprint predictability, capacity forecasting, team happiness, and executive reporting).

---

## Quick Navigation

| Section | Description | Target Page |
| :--- | :--- | :--- |
| 🏛️ **System Architecture** | Clean Architecture, .NET 10, Angular 18, EF Core, AI Coach | [[System Architecture|System-Architecture]] |
| ⏱️ **Standups & Work Items** | 2-min speaker timer, async standups, 7-stage micro-pipeline | [[Daily Standup & Work Items|Daily-Standup-and-Work-Items]] |
| 🔍 **PR Turnaround & Blockers** | PR review telemetry, developer scorecards, Blocker SLA radar | [[PR Turnaround & Blocker SLA|PR-Turnaround-and-Blocker-SLA]] |
| 📅 **Capacity & Roster** | Leave calendar, net focus hours, roster & WIP limits | [[Leave, Capacity & Team Roster|Leave-Capacity-and-Team-Roster]] |
| 🤝 **Reviews, Retros & Kudos** | 360° monthly reviews, 4-column retro board, appreciation badges | [[Reviews, Retros & Appreciation|Reviews-Retros-and-Appreciation]] |
| 🚀 **Growth, Tech Hub & AI** | Sprint letter grades, tech debt inventory, Microsoft AI Coach | [[Growth, Tech Hub & AI Coach|Growth-TechHub-and-AICoach]] |
| 📊 **Executive Suite & RBAC** | Composite health score, multi-format export, SM PIN gateway | [[Executive Suite & RBAC|Executive-Suite-and-RBAC]] |
| 🛡️ **Security Architecture** | RBAC, OWASP security headers, rate limiting, vulnerability SLA | [[Security Architecture|Security-Architecture]] |
| 💻 **Developer Setup** | Local environment, prerequisites, running backend & frontend | [[Developer Setup Guide|Developer-Setup-Guide]] |
| 🚢 **CI/CD & Deployment** | GitHub Actions, Docker multi-stage build, Render deployment | [[CI/CD & Deployment|CI-CD-and-Deployment]] |
| 📡 **API Reference** | RESTful endpoints, request/response models, error codes | [[API Reference|API-Reference]] |

---

## Platform Highlights

### 1. 13 Core Delivery & Culture Modules
ScrumPulse is organized into 13 feature modules designed to cover the entire agile lifecycle:
1. **Daily Standup & Timer**: Asynchronous submissions with mood indexing (1-5) and a 2-minute round-robin speaker clock.
2. **Work Items & Lifecycle**: 7-stage micro-pipeline (`Backlog` &rarr; `InProgress` &rarr; `PrCreated` &rarr; `PrApproved` &rarr; `Merged` &rarr; `InQa` &rarr; `Done`) with DoR/DoD quality gates.
3. **Git PRs & Code Review**: Pull request review logs, actionable comment ratios, and turnaround time scorecards.
4. **Blocker SLA Radar**: Real-time waiting-time counters, root-cause categorization, and >8h SLA breach alerts.
5. **Leave & Capacity**: Half/full-day leave bookings recalculating net squad focus hours and recommended story points.
6. **Team Roster**: Squad directory with role assignments, avatar selection, and active WIP limit governance.
7. **Monthly 1:1 Reviews**: 360° feedback capturing SM, CDL, Client, and self-reflection alongside happiness dials.
8. **Retrospective Board**: 4-column retro board (`Went Well`, `Didn't Go Well`, `Ideas`, `Action Items`) with anonymous cards and peer upvoting.
9. **Appreciation Wall**: Peer recognition cards with custom badges and live emoji reactions.
10. **Team Growth & Performance**: Multi-sprint delivery maturity telemetry, say-do predictability ratios, defect leakage reduction tracking, and letter grades (`A+` to `D`).
11. **Tech Hub**: Technical debt inventory with payoff sprint targets and tech talk logs.
12. **Microsoft AI Coach**: Powered by Microsoft Agent Framework for individual developer coaching and sprint risk radar synthesis.
13. **Executive Suite & Export**: Composite 6-dimension Sprint Health Score, custom duration filters, and CSV/JSON/PDF export.

### 2. Multi-Tenant Squad Isolation
Every entity in ScrumPulse belongs to a specific `TeamId`. Global query filters in Entity Framework Core enforce strict data isolation between squads while enabling seamless squad switching for multi-team leads.

### 3. Role-Based Access Control (RBAC) & PIN Security
Privileged operations (sprint configuration, work item deletion, roster management, squad creation) require authentication as a **Scrum Master**, guarded by an encrypted PIN gateway (`SM_PIN`).

---

## Technology Stack Summary

```
Frontend:  Angular 18 | NgRx Store & Effects | Signals | Standalone Components | Vanilla CSS
Backend:   .NET 10 (C# 14) | ASP.NET Core Web API | Clean Architecture
Database:  PostgreSQL (Production) | SQLite (Dev) | EF Core 10
AI Engine: Microsoft Agent Framework | Azure OpenAI / Semantic Kernel
DevOps:    GitHub Actions CI/CD | Docker Multi-Stage | Render.com PaaS
```

---

## Getting Started

- To set up ScrumPulse on your local machine, head over to the [[Developer Setup Guide|Developer-Setup-Guide]].
- To learn more about our architectural patterns, visit [[System Architecture|System-Architecture]].
- To understand how to deploy and configure production environments, check [[CI/CD & Deployment|CI-CD-and-Deployment]].
