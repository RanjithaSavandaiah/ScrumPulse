# Executive Suite, Reporting & Multi-Squad RBAC

This guide covers ScrumPulse's executive delivery governance, multi-format export capabilities, and multi-squad tenant administration.

---

## Feature Module 13: Executive Suite & Health Score

Delivery Managers, Client Partners, and CTOs require synthesized visibility without getting lost in granular daily tickets. The Executive Suite provides strategic governance.

### Composite 6-Dimension Sprint Health Score
ScrumPulse synthesizes six vital signs into an aggregate sprint health index (0–100):

| Dimension | Weight | Target Threshold |
| :--- | :---: | :--- |
| **1. Sprint Predictability (Say-Do)** | 25% | $\ge 85\%$ of committed points delivered |
| **2. Blocker SLA Adherence** | 20% | $< 8\text{ hours}$ average resolution time |
| **3. PR Turnaround Velocity** | 15% | $< 24\text{ hours}$ average review turnaround |
| **4. Defect Leakage Rate** | 15% | $< 3\%$ defect ratio during QA |
| **5. Squad Morale / Happiness** | 15% | $\ge 4.0 / 5.0$ daily standup mood rating |
| **6. Capacity Utilization** | 10% | $85\% - 95\%$ net focus hour commitment |

### Duration Filters
Telemetry can be filtered across multiple time windows:
- **Last 7 Days**: Immediate sprint health and daily standup trends.
- **Last 14 Days**: Typical 2-week sprint cycle view.
- **Last 30 Days**: Monthly delivery stability and 1:1 review correlation.
- **Last 90 Days**: Quarterly delivery velocity and tech debt trajectory.

### Multi-Format Data Export
The Executive Suite provides one-click export for client reporting:
- **PDF**: Professional, executive-ready sprint summary report with visual charts and scorecards.
- **CSV**: Raw telemetry data for custom spreadsheet analysis and financial billing reconciliation.
- **JSON**: Machine-readable payload for external enterprise BI pipelines.

---

## Multi-Squad Navigation & RBAC Hub

Enterprise engineering organizations rarely operate with a single team. ScrumPulse natively supports multi-squad topologies:

### Multi-Squad Switcher
- Header dropdown allows instant switching between squads (e.g., *Frontend Squad*, *Core API Squad*, *Mobile Squad*).
- Switching updates the client-side `TeamId` header and re-hydrates the NgRx store.

### Squad Creation & Join Codes
- **Creation**: Authenticated Scrum Masters can spawn new squads with custom naming, sprint cadence (1–4 weeks), and initial roster members.
- **Join Codes**: Squads can generate secure, expiring join codes allowing new developers to onboard into the squad with appropriate default permissions.

### Scrum Master PIN Boundary
To prevent unauthorized squad modifications or accidental deletions:
- Switching to the `ScrumMaster` role opens the PIN verification gateway.
- Entering the validated `SM_PIN` unlocks administrative tools for that session.
