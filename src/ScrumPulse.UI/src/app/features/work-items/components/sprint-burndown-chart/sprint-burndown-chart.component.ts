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

import { CORE_PIPES } from '../../../../core/pipes';

const TWO_WEEKS_IN_DAYS = 14;
const HOURS_PER_DAY = 24;
const MINUTES_PER_HOUR = 60;
const SECONDS_PER_MINUTE = 60;
const MILLISECONDS_PER_SECOND = 1000;
const TWO_WEEKS_IN_MS = TWO_WEEKS_IN_DAYS * HOURS_PER_DAY * MINUTES_PER_HOUR * SECONDS_PER_MINUTE * MILLISECONDS_PER_SECOND;
const HALF_DAY_LEAVE_PORTION = 0.5;
const FULL_DAY_LEAVE_PORTION = 1.0;
const ONE_DECIMAL_PLACE_ROUNDING_FACTOR = 10;
const TWO_DECIMAL_PLACES_ROUNDING_FACTOR = 100;
const PACE_VARIANCE_TOLERANCE_POINTS = 3;

@Component({
  selector: 'app-sprint-burndown-chart',
  standalone: true,
  imports: [CommonModule, IconComponent, ...CORE_PIPES],
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
    const allMembers = (this.members && this.members.length > 0 ? this.members : []).filter(member => (member.isActive ?? true));
    const devMembers = allMembers.filter(member => (member.role || '').toLowerCase() === 'developer');
    const deliveryMembers = allMembers.filter(member => isDeliveryRole(member.role));
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
    const end = new Date(this.sprint.endDate || (Date.now() + TWO_WEEKS_IN_MS));
    const workingDays = calculateWorkingDays(start, end);
    const hoursPerDay: number = this.sprint?.dailyWorkingHours && this.sprint.dailyWorkingHours > 0
      ? this.sprint.dailyWorkingHours
      : DEFAULT_DAILY_WORKING_HOURS;

    // Filter leaves that intersect this sprint window
    const relevantLeaves = this.leaves.filter(leave => {
      if (!leave.isApproved) return false;
      const lStart = new Date(leave.startDate);
      const lEnd = new Date(leave.endDate);
      return lStart <= end && lEnd >= start;
    });

    const leaveBreakdown: { memberName: string; leaveDays: number; leaveHours: number; leaveType: string; slot: string }[] = [];
    let totalLeaveDays = 0;

    for (const member of targetDevs) {
      const memberLeaves = relevantLeaves.filter(leave => leave.teamMemberId === member.id);
      let memberDays = 0;
      for (const memberLeave of memberLeaves) {
        const leavePortion = memberLeave.totalDays || (memberLeave.leaveSlot && memberLeave.leaveSlot !== 'FullDay' ? HALF_DAY_LEAVE_PORTION : FULL_DAY_LEAVE_PORTION);
        memberDays += leavePortion;
        const leaveHours = Math.round(leavePortion * hoursPerDay * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR;
        leaveBreakdown.push({
          memberName: cleanName(memberLeave.teamMemberName || member.name),
          leaveDays: leavePortion,
          leaveHours,
          leaveType: memberLeave.leaveType || 'Planned Leave',
          slot: memberLeave.leaveSlot === 'FirstHalf' ? '1st Half' : (memberLeave.leaveSlot === 'SecondHalf' ? '2nd Half' : 'Full Day')
        });
      }
      totalLeaveDays += memberDays;
    }

    // Configurable productive hours per day (default 8.5h)
    const grossHours = Math.round(workingDays * memberCount * hoursPerDay * ONE_DECIMAL_PLACE_ROUNDING_FACTOR) / ONE_DECIMAL_PLACE_ROUNDING_FACTOR;
    const leaveHoursDeducted = Math.round(totalLeaveDays * hoursPerDay * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR;
    const netAvailableHours = Math.max(0, Math.round((grossHours - leaveHoursDeducted) * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR);

    // Sprint Items Story Points
    const sprintItems = this.workItems.filter(item => item.sprintId === this.sprint.id || (!item.sprintId && this.sprint.isActive));
    const totalScope = sprintItems.reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
    const committedPoints = this.sprint.committedStoryPoints || totalScope;
    const deliveredPoints = sprintItems
      .filter(item => String(item.status).toLowerCase().includes('done'))
      .reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
    const remainingPoints = Math.max(0, committedPoints - deliveredPoints);

    const requiredHours = committedPoints * HOURS_PER_STORY_POINT_BENCHMARK;
    const utilizationRate = netAvailableHours > 0 ? Math.round((requiredHours / netAvailableHours) * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) : 0;

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
    const end = new Date(this.sprint.endDate || (Date.now() + TWO_WEEKS_IN_MS));
    const sprintItems = this.workItems.filter(item => item.sprintId === this.sprint.id || (!item.sprintId && this.sprint.isActive));

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

    for (let dayIndex = 1; dayIndex <= workingDays; dayIndex++) {
      // Calculate date for business day dayIndex
      const businessDate = businessDates[dayIndex - 1] ? new Date(businessDates[dayIndex - 1]) : new Date(start);
      businessDate.setHours(23, 59, 59, 999);

      const isPast = businessDate <= today;
      const isToday = businessDate.toDateString() === new Date().toDateString();

      // Ideal linear decay
      const idealRemaining = Math.max(0, Math.round(totalCommitted - (dayIndex * (totalCommitted / workingDays))));

      // Actual delivered up to date businessDate
      let actualRemaining: number | null = null;
      let deliveredOnDay = 0;

      if (isPast) {
        const doneUpToDate = sprintItems.filter(item => {
          if (!String(item.status).toLowerCase().includes('done')) return false;
          if (!item.completedAtUtc) return true; // completed in this sprint
          return new Date(item.completedAtUtc) <= businessDate;
        });

        const deliveredTotal = doneUpToDate.reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
        deliveredOnDay = Math.max(0, deliveredTotal - cumulativeDelivered);
        cumulativeDelivered = deliveredTotal;
        actualRemaining = Math.max(0, totalCommitted - deliveredTotal);
      }

      days.push({
        dayIndex,
        dayLabel: `Day ${dayIndex}`,
        dateStr: businessDate.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
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
    days.forEach((point, pointIndex) => {
      const x = getX(pointIndex);
      const y = getY(point.idealRemaining);
      idealPath += pointIndex === 0 ? `M ${x} ${y}` : ` L ${x} ${y}`;
    });

    // Actual line path
    const pastDays = days.filter(day => day.actualRemaining !== null);
    let actualPath = '';
    let areaPath = '';

    pastDays.forEach((point, pointIndex) => {
      const x = getX(pointIndex);
      const y = getY(point.actualRemaining!);
      if (pointIndex === 0) {
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
      if (diff <= -PACE_VARIANCE_TOLERANCE_POINTS) paceStatus = 'Ahead';
      else if (diff >= PACE_VARIANCE_TOLERANCE_POINTS) paceStatus = 'Behind';
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
