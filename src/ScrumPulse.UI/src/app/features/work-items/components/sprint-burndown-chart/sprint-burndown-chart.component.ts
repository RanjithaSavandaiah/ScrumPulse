import { Component, Input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { Sprint, WorkItem, TeamLeave, TeamMember } from '../../../../core/models/scrum.models';
import { calculateWorkingDays } from '../../../../core/utils/date-utils';
import { cleanName, isDeliveryRole } from '../../../../core/utils/format-utils';
import { DEFAULT_DAILY_WORKING_HOURS, HOURS_PER_STORY_POINT_BENCHMARK } from '../../../../core/constants/scrum.constants';

export interface BurndownDayPoint {
  dayIndex: number;
  dayLabel: string;
  dateStr: string;
  isToday: boolean;
  isPast: boolean;
  idealRemaining: number;
  actualRemaining: number | null;
  deliveredOnDay: number;
}

@Component({
  selector: 'app-sprint-burndown-chart',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './sprint-burndown-chart.component.html',
  styleUrl: './sprint-burndown-chart.component.css'
})
export class SprintBurndownChartComponent {
  @Input({ required: true }) sprint!: Sprint;
  @Input() workItems: WorkItem[] = [];
  @Input() leaves: TeamLeave[] = [];
  @Input() members: TeamMember[] = [];

  protected readonly Math = Math;

  // 1. Capacity Auto-Calculation from Leaves
  capacityAnalysis = computed(() => {
    const allMembers = (this.members && this.members.length > 0 ? this.members : []).filter(m => (m.isActive ?? true));
    const devMembers = allMembers.filter(m => (m.role || '').toLowerCase() === 'developer');
    const deliveryMembers = allMembers.filter(m => isDeliveryRole(m.role));
    const targetDevs = devMembers.length > 0 ? devMembers : deliveryMembers;
    const memberCount = targetDevs.length;

    if (!this.sprint) {
      return {
        workingDays: 0,
        memberCount,
        grossHours: 0,
        totalLeaveDays: 0,
        leaveHoursDeducted: 0,
        netAvailableHours: 0,
        committedPoints: 0,
        deliveredPoints: 0,
        remainingPoints: 0,
        utilizationRate: 0,
        leaveBreakdown: []
      };
    }

    const start = new Date(this.sprint.startDate || Date.now());
    const end = new Date(this.sprint.endDate || (Date.now() + 14 * 24 * 60 * 60 * 1000));
    const workingDays = calculateWorkingDays(start, end);
    const hoursPerDay: number = this.sprint?.dailyWorkingHours && this.sprint.dailyWorkingHours > 0
      ? this.sprint.dailyWorkingHours
      : DEFAULT_DAILY_WORKING_HOURS;

    // Filter leaves that intersect this sprint window
    const relevantLeaves = this.leaves.filter(l => {
      if (!l.isApproved) return false;
      const lStart = new Date(l.startDate);
      const lEnd = new Date(l.endDate);
      return lStart <= end && lEnd >= start;
    });

    const leaveBreakdown: { memberName: string; leaveDays: number; leaveHours: number; leaveType: string; slot: string }[] = [];
    let totalLeaveDays = 0;

    for (const member of targetDevs) {
      const memberLeaves = relevantLeaves.filter(l => l.teamMemberId === member.id);
      let mDays = 0;
      for (const ml of memberLeaves) {
        const d = ml.totalDays || (ml.leaveSlot && ml.leaveSlot !== 'FullDay' ? 0.5 : 1.0);
        mDays += d;
        const leaveHours = Math.round(d * hoursPerDay * 100) / 100;
        leaveBreakdown.push({
          memberName: cleanName(ml.teamMemberName || member.name),
          leaveDays: d,
          leaveHours,
          leaveType: ml.leaveType || 'Planned Leave',
          slot: ml.leaveSlot === 'FirstHalf' ? '1st Half' : (ml.leaveSlot === 'SecondHalf' ? '2nd Half' : 'Full Day')
        });
      }
      totalLeaveDays += mDays;
    }

    // Configurable productive hours per day (default 8.5h)
    const grossHours = Math.round(workingDays * memberCount * hoursPerDay * 10) / 10;
    const leaveHoursDeducted = Math.round(totalLeaveDays * hoursPerDay * 100) / 100;
    const netAvailableHours = Math.max(0, Math.round((grossHours - leaveHoursDeducted) * 100) / 100);

    // Sprint Items Story Points
    const sprintItems = this.workItems.filter(w => w.sprintId === this.sprint.id || (!w.sprintId && this.sprint.isActive));
    const totalScope = sprintItems.reduce((acc, w) => acc + (w.storyPoints || 0), 0);
    const committedPoints = this.sprint.committedStoryPoints || totalScope;
    const deliveredPoints = sprintItems
      .filter(w => String(w.status).toLowerCase().includes('done'))
      .reduce((acc, w) => acc + (w.storyPoints || 0), 0);
    const remainingPoints = Math.max(0, committedPoints - deliveredPoints);

    const requiredHours = committedPoints * HOURS_PER_STORY_POINT_BENCHMARK;
    const utilizationRate = netAvailableHours > 0 ? Math.round((requiredHours / netAvailableHours) * 100) : 0;

    return {
      workingDays,
      memberCount,
      grossHours,
      totalLeaveDays,
      leaveHoursDeducted,
      netAvailableHours,
      committedPoints,
      deliveredPoints,
      remainingPoints,
      utilizationRate,
      leaveBreakdown
    };
  });

  // 2. Day-by-Day Burndown Trend Data
  burndownData = computed(() => {
    const analysis = this.capacityAnalysis();
    const totalCommitted = analysis.committedPoints;
    const workingDays = analysis.workingDays;
    const start = new Date(this.sprint.startDate || Date.now());
    const end = new Date(this.sprint.endDate || (Date.now() + 14 * 24 * 60 * 60 * 1000));
    const sprintItems = this.workItems.filter(w => w.sprintId === this.sprint.id || (!w.sprintId && this.sprint.isActive));

    const today = new Date();
    today.setHours(23, 59, 59, 999);

    const days: BurndownDayPoint[] = [];

    // Day 0: Start of Sprint
    days.push({
      dayIndex: 0,
      dayLabel: 'Day 0',
      dateStr: start.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      isToday: false,
      isPast: true,
      idealRemaining: totalCommitted,
      actualRemaining: totalCommitted,
      deliveredOnDay: 0
    });

    // Compute exact calendar business days
    const businessDates: Date[] = [];
    const cur = new Date(start);
    cur.setHours(0, 0, 0, 0);
    const endDay = new Date(end);
    endDay.setHours(23, 59, 59, 999);
    while (cur <= endDay && businessDates.length < workingDays) {
      if (cur.getDay() !== 0 && cur.getDay() !== 6) { // Skip Sat (6) and Sun (0)
        businessDates.push(new Date(cur));
      }
      cur.setDate(cur.getDate() + 1);
    }

    let cumulativeDelivered = 0;

    for (let i = 1; i <= workingDays; i++) {
      // Calculate date for business day i
      const d = businessDates[i - 1] ? new Date(businessDates[i - 1]) : new Date(start);
      d.setHours(23, 59, 59, 999);

      const isPast = d <= today;
      const isToday = d.toDateString() === new Date().toDateString();

      // Ideal linear decay
      const idealRemaining = Math.max(0, Math.round(totalCommitted - (i * (totalCommitted / workingDays))));

      // Actual delivered up to date d
      let actualRemaining: number | null = null;
      let deliveredOnDay = 0;

      if (isPast) {
        const doneUpToDate = sprintItems.filter(w => {
          if (!String(w.status).toLowerCase().includes('done')) return false;
          if (!w.completedAtUtc) return true; // completed in this sprint
          return new Date(w.completedAtUtc) <= d;
        });

        const deliveredTotal = doneUpToDate.reduce((acc, w) => acc + (w.storyPoints || 0), 0);
        deliveredOnDay = Math.max(0, deliveredTotal - cumulativeDelivered);
        cumulativeDelivered = deliveredTotal;
        actualRemaining = Math.max(0, totalCommitted - deliveredTotal);
      }

      days.push({
        dayIndex: i,
        dayLabel: `Day ${i}`,
        dateStr: d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
        isToday,
        isPast,
        idealRemaining,
        actualRemaining,
        deliveredOnDay
      });
    }

    // Compute SVG coordinates (viewBox 0 0 600 240)
    const svgWidth = 540;
    const svgHeight = 180;
    const paddingX = 40;
    const paddingY = 20;

    const maxVal = Math.max(1, totalCommitted);
    const count = days.length;

    const getX = (index: number) => paddingX + (index / (count - 1)) * (svgWidth - paddingX * 2);
    const getY = (val: number) => paddingY + (1 - val / maxVal) * (svgHeight - paddingY * 2);

    // Ideal line path
    let idealPath = '';
    days.forEach((pt, idx) => {
      const x = getX(idx);
      const y = getY(pt.idealRemaining);
      idealPath += idx === 0 ? `M ${x} ${y}` : ` L ${x} ${y}`;
    });

    // Actual line path
    const pastDays = days.filter(d => d.actualRemaining !== null);
    let actualPath = '';
    let areaPath = '';

    pastDays.forEach((pt, idx) => {
      const x = getX(idx);
      const y = getY(pt.actualRemaining!);
      if (idx === 0) {
        actualPath = `M ${x} ${y}`;
        areaPath = `M ${x} ${getY(0)} L ${x} ${y}`;
      } else {
        actualPath += ` L ${x} ${y}`;
        areaPath += ` L ${x} ${y}`;
      }
    });

    if (pastDays.length > 0) {
      const lastX = getX(pastDays.length - 1);
      areaPath += ` L ${lastX} ${getY(0)} Z`;
    }

    // Status: Ahead, OnTrack, Behind, Completed
    const lastActual = pastDays.length > 0 ? pastDays[pastDays.length - 1] : days[0];
    let paceStatus: 'Ahead' | 'OnTrack' | 'Behind' | 'Completed' = 'OnTrack';

    if (lastActual.actualRemaining === 0) {
      paceStatus = 'Completed';
    } else if (lastActual.actualRemaining !== null) {
      const diff = lastActual.actualRemaining - lastActual.idealRemaining;
      if (diff <= -3) paceStatus = 'Ahead';
      else if (diff >= 3) paceStatus = 'Behind';
      else paceStatus = 'OnTrack';
    }

    return {
      days,
      pastDays,
      idealPath,
      actualPath,
      areaPath,
      paceStatus,
      getX,
      getY
    };
  });
}
