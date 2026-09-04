import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { TeamLeave, TeamMember } from '../../core/models/scrum.models';
import { BookLeaveModalComponent } from './components/book-leave-modal/book-leave-modal.component';

import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { generateDynamicMonths } from '../../core/utils/date-utils';
import { CORE_PIPES } from '../../core/pipes';

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
    return this.state.activeSprint()?.dailyWorkingHours || 8.5;
  }

  // Filters per Month & per Individual
  selectedMonth = signal<string>('ALL');
  selectedMemberId = signal<string>('ALL');

  monthsList = generateDynamicMonths(6, 6, true);

  private isDateInMonth(dateStr: string | Date | undefined | null, targetYearMonth: string): boolean {
    if (!dateStr || targetYearMonth === 'ALL') return true;
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return false;
    const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    return ym === targetYearMonth;
  }

  filteredLeaves = computed(() => {
    let list = this.state.leaves();

    if (this.selectedMemberId() !== 'ALL') {
      list = list.filter(l => l.teamMemberId === this.selectedMemberId());
    }

    if (this.selectedMonth() !== 'ALL') {
      list = list.filter(l => this.isDateInMonth(l.startDate, this.selectedMonth()) || this.isDateInMonth(l.endDate, this.selectedMonth()));
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
