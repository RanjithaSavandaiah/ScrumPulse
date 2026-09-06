# Developer Setup & Local Onboarding

This step-by-step guide walks you through configuring your local development environment to build, run, and test ScrumPulse.

---

## 1. Prerequisites

Ensure the following runtimes and tools are installed on your machine:

| Tool | Minimum Version | Verification Command |
| :--- | :--- | :--- |
| **.NET SDK** | .NET 10.0+ | `dotnet --version` |
| **Node.js** | 22.0.0+ | `node -v` |
| **npm** | 10.0.0+ | `npm -v` |
| **Git** | 2.40+ | `git --version` |
| **Docker** *(Optional)* | 24.0+ | `docker --version` |

---

## 2. Repository Cloning

```bash
git clone https://github.com/RanjithaSavandaiah/ScrumPulse.git
cd ScrumPulse
```

---

## 3. Backend Setup (.NET 10)

1. **Restore dependencies**:
 ```bash
 dotnet restore ScrumPulse.slnx
 ```

2. **Build the solution with warning-as-error verification**:
 ```bash
 dotnet build ScrumPulse.slnx -c Debug
 ```

3. **Run the API server**:
 ```bash
 dotnet run --project src/ScrumPulse.Api
 ```
 The backend API will start on `http://localhost:5000` (or `https://localhost:5001`), seeding sample squad data automatically via SQLite if no PostgreSQL instance is configured.

---

## 4. Frontend Setup (Angular 22)

1. **Navigate to the UI directory**:
 ```bash
 cd src/ScrumPulse.UI
 ```

2. **Install frontend dependencies**:
 ```bash
 npm ci --legacy-peer-deps
 ```

3. **Start the Angular development server**:
 ```bash
 npm start
 ```
 The single-page application will be accessible at `http://localhost:4200`. Requests to `/api/*` are automatically proxied to `http://localhost:5000`.

---

## 5. Configuration & Environment Variables

Key configuration parameters can be configured via `appsettings.json` or environment variables:

| Variable | Description | Default (Local Dev) |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | Hosting environment (`Development` / `Production`) | `Development` |
| `ConnectionStrings__DefaultConnection` | Database connection string | SQLite: `Data Source=scrumpulse.db` |
| `JWT_SECRET_KEY` | HMAC-SHA256 signing key (min 256-bit) | Dev fallback key |
| `SCRUM_MASTER_PIN` | Authorized Scrum Master PIN | `1234` |
| `AI__OpenAiApiKey` | Azure OpenAI / OpenAI API key | *(Optional for AI Coach)* |
| `AI__ModelId` | LLM deployment identifier | `gpt-4o` |

---

## 6. Running the 3-Tier Test Matrix

ScrumPulse enforces an automated testing matrix across backend, frontend, and end-to-end flows:

### Tier 1: Backend Tests (.NET 10 / xUnit)
Runs 93 unit, integration, architecture, and controller tests:
```bash
dotnet test ScrumPulse.slnx -c Release --verbosity normal
```

### Tier 2: Frontend Unit Tests (Karma / Jasmine)
Runs Angular component, service, and NgRx store unit tests with console sentinel verification:
```bash
cd src/ScrumPulse.UI
npm test -- --no-watch --browsers=ChromeHeadless
```

### Tier 3: End-to-End Test Suite (Playwright)
Executes 30 end-to-end user journeys covering all 13 feature tabs:
```bash
cd src/ScrumPulse.UI
npx playwright test
```
To run Playwright tests in interactive UI mode:
```bash
npx playwright test --ui
```
