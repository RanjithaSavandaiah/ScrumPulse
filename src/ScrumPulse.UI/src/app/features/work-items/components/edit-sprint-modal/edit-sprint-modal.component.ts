import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { Sprint } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { EstimationMatrixModalComponent } from '../estimation-matrix-modal/estimation-matrix-modal.component';

import { computed, signal } from '@angular/core';
import { ConfirmModalComponent } from '../../../../core/components/confirm-modal/confirm-modal.component';
import { calculateWorkingDays } from '../../../../core/utils/date-utils';
import { isDeliveryRole } from '../../../../core/utils/format-utils';
import { DEFAULT_DAILY_WORKING_HOURS, HOURS_PER_POINT_RATIO, DEFAULT_FOCUS_FACTOR } from '../../../../core/constants/scrum.constants';
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

@Component({
  selector: 'app-edit-sprint-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, EstimationMatrixModalComponent, ConfirmModalComponent, ...CORE_PIPES],
  templateUrl: './edit-sprint-modal.component.html',
  styleUrl: './edit-sprint-modal.component.css'
})
export class EditSprintModalComponent implements OnInit {
  state = inject(ScrumStateService);
  isSubmitting = signal(false);
  validationError = signal<string | null>(null);

  @Input() sprint: Sprint | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<Partial<Sprint>>();
  @Output() delete = new EventEmitter<string>();

  name: string = '';
  goal: string = '';
  startDate: string = new Date().toISOString().split('T')[0];
  endDate: string = new Date(Date.now() + TWO_WEEKS_IN_MS).toISOString().split('T')[0];
  dailyWorkingHours: number = DEFAULT_DAILY_WORKING_HOURS;
  committedStoryPoints: number = 0;
  committedHours: number = 0;
  hoursPerPointRatio: number = DEFAULT_DAILY_WORKING_HOURS;
  targetMode: 'storyPoints' | 'hours' = 'storyPoints';
  isActive: boolean = true;
  showMatrixModal: boolean = false;
  capacityCalculationSummary: string | null = null;

  // Confirmation modal state
  showDeleteConfirm = signal<boolean>(false);

  rosterDeveloperCount = computed(() => {
    const all = this.state.squadMembers().filter(member => (member.isActive ?? true));
    const devs = all.filter(member => (member.role || '').toLowerCase() === 'developer');
    if (devs.length > 0) return devs.length;
    return all.filter(member => isDeliveryRole(member.role)).length;
  });

  get calculatedWorkingDays(): number {
    return calculateWorkingDays(this.startDate, this.endDate);
  }

  get isInvalidDateRange(): boolean {
    if (!this.startDate || !this.endDate) return false;
    return new Date(this.endDate) < new Date(this.startDate);
  }

  get canSubmit(): boolean {
    return !!this.name.trim() &&
           !!this.startDate &&
           !!this.endDate &&
           !this.isInvalidDateRange;
  }

  onStartDateChange(): void {
    if (this.startDate && this.endDate) {
      if (new Date(this.endDate) < new Date(this.startDate)) {
        // Auto-advance endDate by 14 days from startDate
        const start = new Date(this.startDate);
        const end = new Date(start.getTime() + TWO_WEEKS_IN_MS);
        this.endDate = end.toISOString().split('T')[0];
      }
    }
  }

  ngOnInit(): void {
    if (this.sprint) {
      this.name = this.sprint.name || '';
      this.goal = this.sprint.goal || '';
      this.startDate = this.sprint.startDate ? new Date(this.sprint.startDate).toISOString().split('T')[0] : this.startDate;
      this.endDate = this.sprint.endDate ? new Date(this.sprint.endDate).toISOString().split('T')[0] : this.endDate;
      this.dailyWorkingHours = this.sprint.dailyWorkingHours || DEFAULT_DAILY_WORKING_HOURS;
      this.hoursPerPointRatio = this.dailyWorkingHours;
      this.committedStoryPoints = this.sprint.committedStoryPoints || 0;
      this.committedHours = Math.round(this.committedStoryPoints * this.hoursPerPointRatio);
      this.isActive = this.sprint.isActive ?? true;
    } else {
      this.dailyWorkingHours = DEFAULT_DAILY_WORKING_HOURS;
      this.hoursPerPointRatio = this.dailyWorkingHours;
      this.committedHours = Math.round(this.committedStoryPoints * this.hoursPerPointRatio);
    }
  }

  setDailyHours(hours: number): void {
    this.dailyWorkingHours = hours;
    this.hoursPerPointRatio = hours;
    if (this.targetMode === 'storyPoints') {
      this.onPointsChange();
    } else {
      this.onHoursChange();
    }
    if (this.capacityCalculationSummary) {
      this.autoCalculateFromCapacity();
    }
  }

  onDailyHoursChange(): void {
    if (this.dailyWorkingHours > 0) {
      this.hoursPerPointRatio = this.dailyWorkingHours;
      if (this.targetMode === 'storyPoints') {
        this.onPointsChange();
      } else {
        this.onHoursChange();
      }
    }
    if (this.capacityCalculationSummary) {
      this.autoCalculateFromCapacity();
    }
  }

  onPointsChange(): void {
    const pts = Math.max(1, this.committedStoryPoints || 1);
    this.committedHours = Math.round(pts * this.hoursPerPointRatio);
  }

  onHoursChange(): void {
    const hrs = Math.max(1, this.committedHours || 1);
    this.committedStoryPoints = Math.max(1, Math.round(hrs / this.hoursPerPointRatio));
  }

  onRatioChange(): void {
    if (this.targetMode === 'storyPoints') {
      this.onPointsChange();
    } else {
      this.onHoursChange();
    }
  }

  setTargetMode(mode: 'storyPoints' | 'hours'): void {
    this.targetMode = mode;
    if (mode === 'storyPoints') {
      this.onPointsChange();
    } else {
      this.onHoursChange();
    }
  }

  autoCalculateFromCapacity(): void {
    const start = new Date(this.startDate || Date.now());
    const end = new Date(this.endDate || (Date.now() + TWO_WEEKS_IN_MS));
    const workingDays = this.calculatedWorkingDays;
    const hoursPerDay = this.dailyWorkingHours > 0 ? this.dailyWorkingHours : DEFAULT_DAILY_WORKING_HOURS;

    // Read developer count dynamically from Team Roster
    const allMembers = this.state.squadMembers().filter(member => (member.isActive ?? true));
    const devMembers = allMembers.filter(member => (member.role || '').toLowerCase() === 'developer');
    const deliveryMembers = allMembers.filter(member => isDeliveryRole(member.role));

    const activeDevs = devMembers.length > 0 ? devMembers : deliveryMembers;
    const memberCount = activeDevs.length;

    if (memberCount === 0) {
      this.capacityCalculationSummary = `<strong>Notice:</strong> No active developers found in the Team Roster. Please add team members under the <strong>Team Roster</strong> tab to calculate sprint capacity.`;
      return;
    }

    // Leaves within window for active roster developers
    const leaves = this.state.leaves();
    const relevantLeaves = leaves.filter(leave => {
      if (!leave.isApproved) return false;
      const lStart = new Date(leave.startDate);
      const lEnd = new Date(leave.endDate);
      return lStart <= end && lEnd >= start;
    });

    let totalLeaveDays = 0;
    for (const member of activeDevs) {
      const memberLeaves = relevantLeaves.filter(leave => leave.teamMemberId === member.id);
      for (const memberLeave of memberLeaves) {
        const leavePortion = memberLeave.totalDays || (memberLeave.leaveSlot && memberLeave.leaveSlot !== 'FullDay' ? HALF_DAY_LEAVE_PORTION : FULL_DAY_LEAVE_PORTION);
        totalLeaveDays += leavePortion;
      }
    }

    const grossHours = Math.round(workingDays * memberCount * hoursPerDay * ONE_DECIMAL_PLACE_ROUNDING_FACTOR) / ONE_DECIMAL_PLACE_ROUNDING_FACTOR;
    const leaveHoursDeducted = Math.round(totalLeaveDays * hoursPerDay * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR;
    const netAvailableHours = Math.max(0, Math.round((grossHours - leaveHoursDeducted) * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR);
    const productiveFocusHours = Math.round(netAvailableHours * DEFAULT_FOCUS_FACTOR); // focus factor
    const suggestedPoints = Math.max(1, Math.round(productiveFocusHours / this.hoursPerPointRatio));

    this.committedHours = productiveFocusHours;
    this.committedStoryPoints = suggestedPoints;

    this.capacityCalculationSummary = `Auto-calculated for <strong>${memberCount} developer${memberCount === 1 ? '' : 's'}</strong> from Team Roster across ${workingDays} working days (@ ${hoursPerDay}h/day): ${grossHours}h gross - ${leaveHoursDeducted}h leave (${totalLeaveDays}d) = ${netAvailableHours}h net &times; 70% focus = ${productiveFocusHours}h (${suggestedPoints} SP).`;
  }

  onSelectMatrixEstimation(event: { points: number; hours: number }): void {
    this.committedStoryPoints = event.points;
    this.committedHours = Math.round(event.points * this.hoursPerPointRatio);
    this.showMatrixModal = false;
  }

  onSubmit(): void {
    if (this.isSubmitting()) return;

    if (!this.name.trim()) {
      this.validationError.set('Sprint name is mandatory');
      return;
    }
    if (!this.startDate) {
      this.validationError.set('Start date is mandatory');
      return;
    }
    if (!this.endDate) {
      this.validationError.set('End date is mandatory');
      return;
    }
    if (new Date(this.endDate) < new Date(this.startDate)) {
      this.validationError.set('End date cannot be earlier than start date');
      return;
    }

    this.validationError.set(null);
    this.isSubmitting.set(true);

    // Ensure story points is non-zero if hours entered
    const finalPoints = this.committedStoryPoints > 0 
      ? this.committedStoryPoints 
      : (this.committedHours > 0 ? Math.max(1, Math.round(this.committedHours / this.hoursPerPointRatio)) : 0);

    const payload: Partial<Sprint> = {
      name: this.name.trim(),
      goal: this.goal.trim(),
      startDate: new Date(this.startDate).toISOString(),
      endDate: new Date(this.endDate).toISOString(),
      committedStoryPoints: finalPoints,
      isActive: this.isActive,
      dailyWorkingHours: this.dailyWorkingHours > 0 ? this.dailyWorkingHours : DEFAULT_DAILY_WORKING_HOURS
    };

    if (this.sprint?.id) {
      payload.id = this.sprint.id;
    }

    this.save.emit(payload);
  }

  onDelete(): void {
    if (this.sprint?.id) {
      this.showDeleteConfirm.set(true);
    }
  }

  onConfirmDeleteSprint(): void {
    if (this.sprint?.id) {
      this.delete.emit(this.sprint.id);
      this.showDeleteConfirm.set(false);
    }
  }

  onCancelDeleteSprint(): void {
    this.showDeleteConfirm.set(false);
  }
}
