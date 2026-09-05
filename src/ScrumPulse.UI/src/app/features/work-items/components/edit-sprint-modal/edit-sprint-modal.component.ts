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

@Component({
  selector: 'app-edit-sprint-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, EstimationMatrixModalComponent, ConfirmModalComponent],
  templateUrl: './edit-sprint-modal.component.html',
  styleUrl: './edit-sprint-modal.component.css'
})
export class EditSprintModalComponent implements OnInit {
  private state = inject(ScrumStateService);

  @Input() sprint: Sprint | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<Partial<Sprint>>();
  @Output() delete = new EventEmitter<string>();

  showDeleteConfirm = signal<boolean>(false);

  name: string = '';
  goal: string = '';
  startDate: string = new Date().toISOString().split('T')[0];
  endDate: string = new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString().split('T')[0];
  
  // Daily Working Hours Configuration (SM Configurable)
  dailyWorkingHours: number = DEFAULT_DAILY_WORKING_HOURS;

  // Dual Target Commitment System
  targetMode: 'storyPoints' | 'hours' = 'storyPoints';
  committedStoryPoints: number = 0;
  committedHours: number = 0;
  hoursPerPointRatio: number = HOURS_PER_POINT_RATIO; // Standard team conversion ratio
  
  // Helpers & Guide Modals
  showMatrixModal: boolean = false;
  capacityCalculationSummary: string | null = null;
  isActive: boolean = true;

  rosterDeveloperCount = computed(() => {
    const all = this.state.squadMembers().filter(m => (m.isActive ?? true));
    const devs = all.filter(m => (m.role || '').toLowerCase() === 'developer');
    if (devs.length > 0) return devs.length;
    return all.filter(m => isDeliveryRole(m.role)).length;
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
        const end = new Date(start.getTime() + 14 * 24 * 60 * 60 * 1000);
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
      this.committedStoryPoints = this.sprint.committedStoryPoints || 0;
      this.committedHours = Math.round(this.committedStoryPoints * this.hoursPerPointRatio);
      this.isActive = this.sprint.isActive ?? true;
    } else {
      this.dailyWorkingHours = DEFAULT_DAILY_WORKING_HOURS;
      this.committedHours = Math.round(this.committedStoryPoints * this.hoursPerPointRatio);
    }
  }

  setDailyHours(hours: number): void {
    this.dailyWorkingHours = hours;
    if (this.capacityCalculationSummary) {
      this.autoCalculateFromCapacity();
    }
  }

  onDailyHoursChange(): void {
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
    const end = new Date(this.endDate || (Date.now() + 14 * 24 * 60 * 60 * 1000));
    const workingDays = this.calculatedWorkingDays;
    const hoursPerDay = this.dailyWorkingHours > 0 ? this.dailyWorkingHours : DEFAULT_DAILY_WORKING_HOURS;

    // Read developer count dynamically from Team Roster
    const allMembers = this.state.squadMembers().filter(m => (m.isActive ?? true));
    const devMembers = allMembers.filter(m => (m.role || '').toLowerCase() === 'developer');
    const deliveryMembers = allMembers.filter(m => isDeliveryRole(m.role));

    const activeDevs = devMembers.length > 0 ? devMembers : deliveryMembers;
    const memberCount = activeDevs.length;

    if (memberCount === 0) {
      this.capacityCalculationSummary = `<strong>Notice:</strong> No active developers found in the Team Roster. Please add team members under the <strong>Team Roster</strong> tab to calculate sprint capacity.`;
      return;
    }

    // Leaves within window for active roster developers
    const leaves = this.state.leaves();
    const relevantLeaves = leaves.filter(l => {
      if (!l.isApproved) return false;
      const lStart = new Date(l.startDate);
      const lEnd = new Date(l.endDate);
      return lStart <= end && lEnd >= start;
    });

    let totalLeaveDays = 0;
    for (const member of activeDevs) {
      const memberLeaves = relevantLeaves.filter(l => l.teamMemberId === member.id);
      for (const ml of memberLeaves) {
        const d = ml.totalDays || (ml.leaveSlot && ml.leaveSlot !== 'FullDay' ? 0.5 : 1.0);
        totalLeaveDays += d;
      }
    }

    const grossHours = Math.round(workingDays * memberCount * hoursPerDay * 10) / 10;
    const leaveHoursDeducted = Math.round(totalLeaveDays * hoursPerDay * 100) / 100;
    const netAvailableHours = Math.max(0, Math.round((grossHours - leaveHoursDeducted) * 100) / 100);
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
    if (!this.canSubmit) return;

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
