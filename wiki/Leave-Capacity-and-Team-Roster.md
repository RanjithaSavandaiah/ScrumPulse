# Leave, Capacity & Team Roster Governance

Accurate sprint planning requires precise knowledge of squad availability. This guide covers ScrumPulse's capacity forecasting engine and team roster management.

---

## Feature Module 5: Leave & Capacity Forecasting

Sprint commitment failures frequently stem from static velocity assumptions that ignore leaves, public holidays, and ceremony overhead. ScrumPulse implements dynamic, working-day capacity math.

### Multi-Member Leave Calendar
- Squad members book planned absences directly within the calendar.
- Supports **Full-Day** and **Half-Day** bookings.
- Automatically accounts for squad-specific working days (Monday–Friday) and official regional holidays.

### Net Focus Hours Formula
ScrumPulse calculates available capacity at both the individual and squad levels:

$$\text{Net Focus Hours} = (\text{Working Days} - \text{Leave Days}) \times \text{Daily Productive Hours} \times \text{Ceremony Friction Factor}$$

- **Daily Productive Hours**: Standardized to 6 hours/day (factoring in context switching and operational tasks).
- **Ceremony Friction Factor**: Automatically subtracts time reserved for Sprint Planning, Daily Standup, Review, and Retrospectives.

### Recommended Story Point Commitments
Based on historical story point burn rates per focus hour, ScrumPulse suggests the optimal story point commitment for the upcoming sprint:
- Prevents over-commitment during holiday-heavy sprints.
- Flags under-capacity warnings if total committed points exceed net available focus hours.

---

## Feature Module 6: Team Roster & WIP Limit Governance

The Team Roster module manages squad members, role assignments, avatars, and Work-In-Progress (WIP) boundaries.

### Squad Roles & Governance
Every squad member is assigned one of seven roles:
- `ScrumMaster`
- `Developer`
- `QaEngineer`
- `Cdl` (Client Delivery Lead)
- `ProductOwner`
- `ClientStakeholder`
- `AgileCoach`

### Active WIP Limits
To avoid task thrashing and half-finished work:
- The Scrum Master sets maximum allowed active items per developer (default: **2 items**).
- When a developer attempts to move a third work item into `InProgress`, the UI displays a soft warning gate recommending pairing or resolving existing work first.
- Roster cards display live status chips indicating how many items each developer currently has in flight.
