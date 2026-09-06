# Daily Standup & Work Item Lifecycle

This guide covers the core day-to-day engineering workflows within ScrumPulse: asynchronous and live daily standups, speaker timing, and the 7-stage work item micro-pipeline.

---

## Feature Module 1: Daily Standup & Timer

The Daily Standup module eliminates low-value status meetings and keeps distributed teams synchronized asynchronously or during live syncs.

### Asynchronous Standup Submissions
Every squad member can submit their daily log answering three focused questions:
1. **Yesterday**: What did you accomplish yesterday?
2. **Today**: What will you commit to completing today?
3. **Blockers**: Are there any impediments or dependencies stopping you?

Additionally, submissions capture a **Mood Index** from 1 to 5 (1 to 5). Aggregated mood scores generate the squad daily morale index visible in the Executive Suite.

### 2-Minute Round-Robin Speaker Timer
For teams conducting live syncs, ScrumPulse includes a real-time speaker clock:
- **Duration**: Preset to 120 seconds (2 minutes) per speaker.
- **Controls**: Interactive Start, Pause, Reset, and Next Speaker buttons.
- **Visual Telemetry**: Smooth circular progress ring with color transitions (Green -> Amber at 30s remaining -> Red pulse upon expiration).
- **Squad Roster Queue**: Automatically cycles through all present squad members.

---

## Feature Module 2: Work Items & 7-Stage Micro-Pipeline

ScrumPulse replaces generic 3-column boards with a granular 7-stage micro-pipeline that surfaces invisible development bottlenecks:

```
[Backlog] -> [InProgress] -> [PrCreated] -> [PrApproved] -> [Merged] -> [InQa] -> [Done]
```

### Stage Definitions & Responsibilities

| Stage | Trigger / Meaning | Quality Gate Enforced |
| :--- | :--- | :--- |
| **1. Backlog** | Story or bug prioritized by Product Owner. | **Definition of Ready (DoR)**: Acceptance criteria, story points, and design specs defined. |
| **2. InProgress** | Developer starts local implementation. | Active WIP Limit check (warns if squad WIP limit is exceeded). |
| **3. PrCreated** | Pull request opened in Git repository. | Automated CI pipeline triggered; PR link attached to work item. |
| **4. PrApproved** | Code review approved by peer / tech lead. | Minimum 1 peer sign-off; all actionable review comments resolved. |
| **5. Merged** | PR merged into development / staging branch. | Clean merge verification; automated unit/integration tests pass. |
| **6. InQa** | Deployed to test environment for QA verification. | QA acceptance criteria verified; no regression defects. |
| **7. Done** | Verified in production or release branch. | **Definition of Done (DoD)**: Documentation updated, release notes logged. |

### Micro-Stage Cycle Time Telemetry
For every transition, ScrumPulse automatically logs timestamps and calculates elapsed time:
- **Coding Latency**: Time between `InProgress` and `PrCreated`.
- **Review Latency**: Time waiting in `PrCreated` before approval and merge.
- **QA Verification Latency**: Time between `Merged` and `Done`.

This telemetry pinpoints where work gets stuck - whether in code review queues or testing environments.
