# PR Turnaround & Blocker SLA Radar

High engineering velocity requires identifying delays before they impact sprint commitments. This guide covers ScrumPulse's PR review metrics and real-time Blocker SLA Radar.

---

## Feature Module 3: Git PRs & Code Review Telemetry

Code review is often the largest hidden latency in software delivery. ScrumPulse provides comprehensive telemetry to optimize review velocity and quality.

### Key Metrics Tracked
- **Turnaround Time (TAT)**: The elapsed hours between PR creation and initial review / final merge.
- **Actionable vs. Total Comment Ratio**: Distinguishes between substantial architectural comments, nitpicks, and approving comments.
- **Review Size (LOC Changes)**: Highlights large, risky PRs (>400 lines) that cause cognitive fatigue and review delays.
- **Developer Review Scorecards**: Recognizes active reviewers and identifies squad members needing code review assistance.

### Individual Reviewer Scorecard
Each squad member receives a balanced review scorecard:
- PRs reviewed vs. PRs opened.
- Average response time to peer review requests.
- Helpfulness rating and actionable contribution frequency.

---

## Feature Module 4: Blocker SLA Radar

Blockers are quantified impediments with active waiting-time counters and automated SLA breach detection.

### Root-Cause Categorization
When logging a blocker, team members assign one of four structured root causes:
1. **`ClientClarification`**: Waiting for business decisions, external product approval, or API spec sign-offs.
2. **`TechLeadArchitecture`**: Waiting for architectural decisions, framework upgrades, or schema approvals.
3. **`EnvironmentAccess`**: Blocked on cloud permissions, VPN tokens, secrets, or broken test environments.
4. **`ThirdPartyApi`**: Blocked by third-party vendor downtime, rate limits, or integration breaking changes.

### Real-Time Waiting Counter & Radar
- **Live Elapsed Time**: Counters continuously tick up in hours and minutes from the moment a blocker is created.
- **SLA Breach Threshold (>8 Hours)**:
- When a blocker remains unresolved for more than 8 working hours, it breaches the squad SLA.
- The blocker card pulses in bold red on the radar dashboard.
- Escalation alerts are surfaced on the Executive Dashboard for Delivery Leads and Scrum Masters to intervene immediately.

### Blocker Resolution & Post-Mortem Tracking
Resolving a blocker captures the exact resolution duration and notes, feeding the multi-sprint delivery maturity telemetry to identify chronic squad impediments.
