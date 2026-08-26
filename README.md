# ⚡ ScrumPulse - Distributed Agile & Engineering Velocity Intelligence Platform

> **Engineered for High-Performing Distributed Teams, Offshore/Onshore Collaboration & Executive Flow Visibility.**
> Built with **.NET 10 Web API (Clean Architecture)** + **Angular Standalone (Glassmorphism & Signals)** + **Microsoft Agent AI Framework**.

---

## 🌟 Key Architecture & Capabilities

### 1. ⏱️ Micro-Stage Work Item & PR Lifecycle Tracking
- Tracks granular developer lifecycle: **Backlog &rarr; Picked Up &rarr; Dev Work &rarr; PR Created &rarr; Code Review &rarr; Merged to Master &rarr; QA Testing &rarr; Marked Done**.
- Automatic calculation of:
  - **Pickup Latency**: Time between sprint start / assignment and active pickup.
  - **Dev Cycle Time**: Active code authoring until Pull Request opening.
  - **PR Review Latency**: Time waiting for peer reviewer approval.
  - **PR Merge Latency**: Time from approval to branch merge into `main`.
  - **QA Testing Latency**: Stage verification duration on staging environments.
  - **Total Lead Cycle Time**: Overall ticket throughput.

### 2. 🛡️ Interactive Quality Gates (DoR & DoD)
- **Definition of Ready (DoR)**: Acceptance criteria defined, cross-team dependencies resolved, wireframes/specifications attached.
- **Definition of Done (DoD)**: Unit tests passing (85%+), peer review sign-off, merged to main branch, staging smoke test passed.

### 3. 🤖 Microsoft Agent Framework AI Intelligence (3-Tier Engine)
- **Individual Developer Coaching**: Synthesizes 360 feedback, PR throughput, and happiness metrics to provide personalized growth paths and burnout mitigation.
- **Sprint & Project Risk Radar**: Analyzes WIP limits, say-do ratios, PR bottlenecks, and escaped defect metrics to predict sprint completion probabilities.
- **Strategic Company & Distributed Insights**: Recommends async collaboration strategies, offshore timezone handoff rituals, and architectural investment areas.
- **Interactive Agile Copilot Chat**: Real-time conversational AI coach supporting custom agile queries.

### 4. 🚨 Blocker & Question Resolution SLA Radar
- Logs offshore/onshore blockers across 4 categories: *Client Clarification*, *Tech Lead Architecture*, *Environment Access*, *Third-Party API*.
- Live countdown timers, waiting hour gauges, and visual **SLA Breach Warnings** for tickets waiting > 8 hours.

### 5. 👥 Daily Standup Feed & 2-Minute Co-Located Timer
- Asynchronous 3-question daily logging (Yesterday, Today, Blockers) with team energy index.
- Live meeting round-robin speaking queue with a **2-Minute Speaking Timer**.

### 6. 🏖️ Multi-Calendar Leave & Dynamic Capacity Planner
- Multi-member vacation / PTO tracking.
- Automatically calculates available productive focus hours per sprint and outputs **Recommended Story Point Commitments**.

### 7. 📝 360° Monthly 1:1 Reviews & Happiness Barometer
- Comprehensive 4-way performance input:
  - 👩‍💼 Scrum Master Guidance
  - 👨‍💼 Career Development Lead (CDL) Technical Coaching
  - 👩‍💻 Client / Product Owner Satisfaction
  - 🧑‍💻 Developer Self-Reflection
- SM Rating (1-10) and Team Happiness Barometer (1-10) with AI-powered trend analysis.

### 8. 🔄 Interactive Sprint Retrospective Board
- 4-column retro system: *😊 Went Well*, *😟 Didn't Go Well*, *💡 Ideas & Experiments*, *🎯 Action Items*.
- Live card upvoting and action item ownership tracking.

### 9. 🌟 Team Appreciation & Kudos Wall
- Peer recognition badges: 🚀 *Problem Solver*, 🤝 *Team Player*, 🎯 *Goal Crusher*, 🛡️ *Quality Guardian*, 💡 *Innovation Star*, 🏆 *Client Shoutout*.
- Interactive emoji reactions (👏, 🚀, ❤️) and celebrate cards.

### 10. 📚 Tech Debt Backlog & Offshore Knowledge Hub
- Severity-tagged technical debt payoff backlog.
- Weekly engineering knowledge sharing log.

### 11. 📊 Executive Suite & 1-Click Client Summary
- **Say-Do Predictability Gauge** (Committed vs Delivered Story Points).
- Escaped QA Defect tracking (Target: 0 Defect leakage).
- **1-Click Executive Summary Generator** for client emails with 1-Click Copy and JSON backup export.

---

## 🚀 How to Run Locally

### Prerequisites
- [.NET 10 SDK Preview](https://dotnet.microsoft.com/download/dotnet/10.0) or .NET 8/9
- [Node.js 20+](https://nodejs.org/)

### 1. Run Backend (.NET 10 Web API)
```bash
cd src/ScrumPulse.Api
dotnet run
```
The API will start at `http://localhost:5000` and automatically seed the SQLite database `ScrumPulse.db` with realistic multi-sprint data.
Swagger documentation is available at `http://localhost:5000/swagger`.

### 2. Run Frontend (Angular Standalone)
```bash
cd src/ScrumPulse.UI
npm install
npm start
```
Navigate to `http://localhost:4200`.

---

## ☁️ Deploy 100% Free on Render.com

ScrumPulse includes a multi-stage `Dockerfile` and `render.yaml` configuration:

1. Push this repository to GitHub or GitLab.
2. Sign in to [Render.com](https://render.com) (100% Free account).
3. Click **New +** &rarr; **Blueprint** & select your repository (or **New +** &rarr; **Web Service** with Environment = **Docker**).
4. Render will automatically build the unified container and host both the .NET 10 API and Angular static SPA on the free tier with automated HTTPS.

---

## 🧱 Solution Architecture

```
C:ScrumPulse
├── src
│   ├── ScrumPulse.Domain          # Pure Domain Entities, Enums, Result Pattern
│   ├── ScrumPulse.Application     # Interfaces, DTOs, Business Contracts
│   ├── ScrumPulse.Infrastructure  # EF Core (SQLite / PostgreSQL), Microsoft Agent AI, DbInitializer
│   ├── ScrumPulse.Api             # ASP.NET Core 10 REST Controllers, Swagger, SPA Hosting
│   └── ScrumPulse.UI              # Angular 18/21 Standalone App (Signals, Glassmorphic CSS)
├── Dockerfile                     # Multi-stage production container
├── render.yaml                    # Render.com 1-click free deployment configuration
└── ScrumPulse.sln                 # Visual Studio Solution file
```
