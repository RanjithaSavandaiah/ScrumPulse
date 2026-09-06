# API Reference & Endpoints

The ScrumPulse RESTful Web API exposes strongly typed JSON endpoints under `/api`. All endpoints require the `X-Team-Id` header to scope operations to the appropriate squad tenant unless globally scoped (e.g., authentication).

---

## Global Headers & Authentication

| Header | Type | Required | Description |
| :--- | :--- | :---: | :--- |
| `Authorization` | String | Yes (for protected endpoints) | Bearer JWT token (`Bearer <token>`). |
| `X-Team-Id` | GUID | Yes | Scopes request data to the active squad tenant. |
| `Content-Type` | String | Yes | `application/json` |

---

## 1. Authentication & Security Gateway (`/api/auth`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Authenticates user credentials and returns JWT bearer token. |
| `POST` | `/api/auth/verify-pin` | Verifies Scrum Master PIN (`SM_PIN`) to unlock privileged role elevation. |

---

## 2. Squads & Roster (`/api/teams`, `/api/teammembers`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/teams` | Lists all squads accessible to the authenticated user. |
| `POST` | `/api/teams` | Creates a new squad (requires SM role). |
| `GET` | `/api/teams/{id}` | Retrieves details for a specific squad. |
| `GET` | `/api/teammembers` | Returns active members, roles, and avatar identifiers for the current squad. |
| `POST` | `/api/teammembers` | Adds a new member to the squad roster. |
| `PUT` | `/api/teammembers/{id}` | Updates a team member's role or active WIP limit. |
| `DELETE` | `/api/teammembers/{id}` | Removes a member from the squad (requires SM PIN). |

---

## 3. Sprints & Work Items (`/api/sprints`, `/api/workitems`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/sprints/active` | Retrieves the currently active sprint for the squad. |
| `POST` | `/api/sprints` | Creates and plans a new sprint cycle. |
| `PUT` | `/api/sprints/{id}` | Updates sprint goal, start/end dates, or status (`Active` / `Completed`). |
| `GET` | `/api/workitems` | Lists all work items within the current sprint. |
| `POST` | `/api/workitems` | Creates a new user story, task, or bug ticket. |
| `PUT` | `/api/workitems/{id}` | Updates work item details, story points, or assignee. |
| `PATCH` | `/api/workitems/{id}/stage` | Transitions a work item across the 7 stages (`Backlog` -> `Done`). |
| `DELETE` | `/api/workitems/{id}` | Deletes a work item (requires SM role). |

---

## 4. Daily Standups & Timer (`/api/standups`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/standups/today` | Retrieves all submitted standup logs for the squad today. |
| `POST` | `/api/standups` | Submits a daily standup (Yesterday, Today, Blockers, Mood rating 1-5). |
| `GET` | `/api/standups/history` | Returns historical standup logs with date-range filtering. |

---

## 5. Pull Requests & Code Review (`/api/pullrequests`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/pullrequests` | Returns active and merged pull requests with review metrics. |
| `POST` | `/api/pullrequests` | Logs a new PR with turnaround metadata and comment ratios. |
| `GET` | `/api/pullrequests/scorecards` | Returns individual developer review scorecard metrics. |

---

## 6. Blockers & SLA Radar (`/api/blockers`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/blockers/active` | Retrieves unresolved blockers with live waiting times and SLA breach status. |
| `POST` | `/api/blockers` | Raises a new blocker with root-cause category and impact severity. |
| `PATCH` | `/api/blockers/{id}/resolve` | Resolves a blocker, logging resolution notes and total downtime hours. |

---

## 7. Leave & Capacity Management (`/api/leaves`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/leaves` | Retrieves booked leaves for the squad within a date window. |
| `POST` | `/api/leaves` | Books a full-day or half-day absence. |
| `GET` | `/api/leaves/capacity` | Calculates net squad focus hours and recommended story point commitments. |

---

## 8. Culture, Retros & Kudos (`/api/retrospectives`, `/api/kudos`, `/api/monthlyfeedback`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/retrospectives/{sprintId}` | Retrieves 4-column retro board cards and upvotes. |
| `POST` | `/api/retrospectives/cards` | Adds a card (`WentWell`, `DidntGoWell`, `Ideas`, `ActionItems`). |
| `POST` | `/api/retrospectives/cards/{id}/vote` | Toggles an upvote on a retro card. |
| `GET` | `/api/kudos` | Retrieves appreciation cards with live emoji reaction counts. |
| `POST` | `/api/kudos` | Posts a new peer appreciation card with a badge. |
| `POST` | `/api/kudos/{id}/react` | Adds an emoji reaction to a kudos card. |
| `GET` | `/api/monthlyfeedback` | Retrieves 360-degree monthly review coaching entries and happiness dials. |
| `POST` | `/api/monthlyfeedback` | Submits monthly feedback across SM, CDL, Client, and self-review. |

---

## 9. Performance, Tech Hub & AI (`/api/teamperformance`, `/api/techhub`, `/api/aicoach`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/teamperformance` | Returns multi-sprint Say-Do ratio, defect leakage, and letter grade badge. |
| `GET` | `/api/techhub/debt` | Retrieves technical debt backlog items with payoff target sprints. |
| `POST` | `/api/techhub/debt` | Logs a new technical debt item. |
| `GET` | `/api/techhub/talks` | Lists internal engineering tech talks and knowledge-share sessions. |
| `POST` | `/api/aicoach/chat` | Interacts with the Microsoft Agent Framework Copilot Agile Chat. |
| `GET` | `/api/aicoach/risk-radar` | Synthesizes an automated sprint risk analysis based on real-time metrics. |

---

## 10. Executive Reports & Exports (`/api/executivereports`)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/executivereports/health-score` | Computes the composite 6-dimension Sprint Health Score. |
| `GET` | `/api/executivereports/export/pdf` | Generates a downloadable executive PDF report. |
| `GET` | `/api/executivereports/export/csv` | Streams raw squad telemetry in CSV format. |
| `GET` | `/api/executivereports/export/json` | Returns machine-readable sprint performance telemetry in JSON format. |
