import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { TeamLeave, TeamMember } from '../../core/models/scrum.models';
import { BookLeaveModalComponent } from './components/book-leave-modal/book-leave-modal.component';

import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { generateCalendarYearMonths, isLeaveInPeriod, SelectOption } from '../../core/utils/date-utils';
import { CORE_PIPES } from '../../core/pipes';
import { DEFAULT_DAILY_WORKING_HOURS } from '../../core/constants/scrum.constants';

@Component({
  selector: 'app-capacity',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, BookLeaveModalComponent, ConfirmModalComponent, ...CORE_PIPES],
  templateUrl: './capacity.component.html',
  styleUrl: './capacity.component.css'
})
export class CapacityComponent {
  state = inject(ScrumStateService);

  showLeaveModal = signal(false);
  selectedEditLeave = signal<TeamLeave | null>(null);
  leaveToDelete = signal<TeamLeave | null>(null);

  Math = Math;
  get sprintDailyHours(): number {
    return this.state.activeSprint()?.dailyWorkingHours || DEFAULT_DAILY_WORKING_HOURS;
  }

  // Dynamic Calendar Years & Months (Jan to Dec calendar year standard)
  currentYear = new Date().getFullYear();
  selectedPeriod = signal<string>('YEAR_' + this.currentYear);
  customStartDate = signal<string>('');
  customEndDate = signal<string>('');
  selectedMemberId = signal<string>('ALL');

  currentYearMonths: SelectOption<string>[] = generateCalendarYearMonths(this.currentYear);
  nextYearMonths: SelectOption<string>[] = generateCalendarYearMonths(this.currentYear + 1);
  previousYearMonths: SelectOption<string>[] = generateCalendarYearMonths(this.currentYear - 1);

  onPeriodChange(val: string): void {
    this.selectedPeriod.set(val);
    if (val === 'CUSTOM') {
      if (!this.customStartDate() && !this.customEndDate()) {
        const now = new Date();
        const y = now.getFullYear();
        const m = String(now.getMonth() + 1).padStart(2, '0');
        const lastDay = new Date(y, now.getMonth() + 1, 0).getDate();
        this.customStartDate.set(`${y}-${m}-01`);
        this.customEndDate.set(`${y}-${m}-${String(lastDay).padStart(2, '0')}`);
      }
    }
  }

  clearCustomDates(): void {
    this.customStartDate.set('');
    this.customEndDate.set('');
  }

  filteredLeaves = computed(() => {
    let list = this.state.leaves();
    const current = this.state.currentTeam();
    if (current && this.state.squadMembers().length > 0) {
      const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
      const squadMemberNames = new Set(this.state.squadMembers().map(m => m.name.toLowerCase().trim()));
      list = list.filter(l =>
        (l.teamMemberId && squadMemberIds.has(l.teamMemberId.toLowerCase().trim())) ||
        (l.teamMemberName && squadMemberNames.has(l.teamMemberName.toLowerCase().trim()))
      );
    }

    if (this.selectedMemberId() !== 'ALL') {
      list = list.filter(l => l.teamMemberId === this.selectedMemberId());
    }

    if (this.selectedPeriod() !== 'ALL') {
      list = list.filter(l =>
        isLeaveInPeriod(l, this.selectedPeriod(), this.customStartDate(), this.customEndDate())
      );
    }

    return list;
  });

  openCreateLeave(): void {
    this.selectedEditLeave.set(null);
    this.showLeaveModal.set(true);
  }

  openEditLeave(leave: TeamLeave): void {
    if (!this.state.canEditOrDelete()) return;
    this.selectedEditLeave.set(leave);
    this.showLeaveModal.set(true);
  }

  onSaveLeave(leaveData: { teamMemberId: string; startDate: string; endDate: string; leaveSlot?: string; reason: string; leaveType: string }): void {
    const editItem = this.selectedEditLeave();
    const cleanStartDate = leaveData.startDate;
    const cleanEndDate = leaveData.endDate < leaveData.startDate ? leaveData.startDate : leaveData.endDate;

    const payload = {
      teamMemberId: leaveData.teamMemberId,
      startDate: new Date(cleanStartDate).toISOString(),
      endDate: new Date(cleanEndDate).toISOString(),
      reason: leaveData.reason?.trim() || 'Planned Leave',
      leaveType: leaveData.leaveSlot && leaveData.leaveSlot !== 'FullDay'
        ? `${leaveData.leaveType} (${leaveData.leaveSlot === 'FirstHalf' ? '1st Half' : '2nd Half'})`
        : (leaveData.leaveType || 'Privilege Leave'),
      leaveSlot: leaveData.leaveSlot || 'FullDay',
      location: 'Bangalore Offshore'
    };

    if (editItem) {
      this.state.updateLeave(editItem.id, payload as any);
    } else {
      this.state.submitLeave(payload as any);
    }

    this.showLeaveModal.set(false);
    this.selectedEditLeave.set(null);
  }

  onDeleteLeave(id: string): void {
    if (!this.state.canEditOrDelete()) return;
    const leave = this.state.leaves().find(l => l.id === id);
    if (leave) {
      this.showLeaveModal.set(false);
      this.selectedEditLeave.set(null);
      this.leaveToDelete.set(leave);
    } else {
      this.state.deleteLeave(id);
      this.showLeaveModal.set(false);
      this.selectedEditLeave.set(null);
    }
  }

  onConfirmDeleteLeave(): void {
    const target = this.leaveToDelete();
    if (target) {
      this.state.deleteLeave(target.id);
      this.leaveToDelete.set(null);
    }
  }

  onCancelDeleteLeave(): void {
    this.leaveToDelete.set(null);
  }
}
