# Team Growth, Tech Hub & Microsoft AI Coach

This guide details ScrumPulse's long-term performance analytics, technical debt governance, and agentic AI coaching capabilities.

---

## Feature Module 10: Team Growth & Performance Telemetry

ScrumPulse tracks team delivery maturity over multiple sprints to provide actionable, trend-based governance rather than isolated snapshot metrics.

### Key Maturity Metrics
- **Say-Do Predictability Ratio**: Percentage of story points completed vs. committed at sprint planning. Healthy teams consistently target 85%-100%.
- **Defect Leakage Rate**: Number of bugs identified in `InQa` or production per 100 story points delivered.
- **Velocity Stability Index**: Standard deviation of sprint velocity across the last 6 sprints.
- **Automated Letter Grade Badges**:
- **`A+`**: >95% Say-Do, <2% Defect Leakage, zero blocker SLA breaches.
- **`A`**: 85%-94% Say-Do, <5% Defect Leakage.
- **`B+` / `B`**: 75%-84% Say-Do with minor delivery deviations.
- **`C` / `D`**: Chronic over-commitment or high defect rates requiring retrospection.

---

## Feature Module 11: Tech Hub (Debt Inventory & Tech Talks)

Engineering velocity grinds to a halt when technical debt is ignored. ScrumPulse treats technical debt as a first-class citizen alongside product features.

### Technical Debt Payoff Inventory
- **Severity Levels**: `Critical`, `High`, `Medium`, `Low`.
- **Payoff Target Sprint**: Assigns debt remediation tickets to specific upcoming sprints to ensure debt isn't forgotten in the backlog.
- **Interest Rate Metric**: Categorizes the friction cost of unaddressed debt (e.g., build slowdowns, manual deployment steps, brittle test flakiness).

### Engineering Tech Talks Log
- Catalogs internal knowledge-sharing sessions, lunch-and-learns, and architecture discussions.
- Stores talk title, speaker, date, slide links, and key takeaways for future team onboarding.

---

## Feature Module 12: Microsoft AI Coach

Powered by the **Microsoft Agent Framework** and Azure OpenAI, ScrumPulse provides an intelligent, context-aware coaching copilot.

### Core Capabilities
1. **Developer 1:1 Coaching Prompts**: Provides personalized reflection questions for developers based on recent PR review latency and standup blockers.
2. **Sprint Risk Radar Synthesis**: Analyzes current work item micro-pipeline stages, remaining days, and blocker history to synthesize executive sprint risk summaries.
3. **Interactive Copilot Agile Chat**:
- Natural language conversational assistant embedded within the UI.
- Answers questions like: *"Which squad members are overloaded this sprint?"*, *"What was our average PR review turnaround over the last 3 sprints?"*, and *"Draft a retrospective agenda focusing on our third-party API blockers."*
- Protected by token-bucket rate limiting and contextual data privacy guards.
