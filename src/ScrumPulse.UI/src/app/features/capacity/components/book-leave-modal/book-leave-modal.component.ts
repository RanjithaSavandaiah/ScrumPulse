import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { TeamLeave, TeamMember } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';

import { CORE_PIPES } from '../../../../core/pipes';
import { DEFAULT_DAILY_WORKING_HOURS } from '../../../../core/constants/scrum.constants';

@Component({
  selector: 'app-book-leave-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './book-leave-modal.component.html',
  styleUrl: './book-leave-modal.component.css'
})
export class BookLeaveModalComponent implements OnInit {
  state = inject(ScrumStateService);

  @Input() members: TeamMember[] = [];
  @Input() editLeave: TeamLeave | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{
    teamMemberId: string;
    startDate: string;
    endDate: string;
    leaveSlot: string;
    reason: string;
    leaveType: string;
  }>();
  @Output() delete = new EventEmitter<string>();

  leave: {
    teamMemberId: string;
    startDate: string;
    endDate: string;
    leaveSlot: 'FullDay' | 'FirstHalf' | 'SecondHalf';
    reason: string;
    leaveType: string;
  } = {
    teamMemberId: '',
    startDate: new Date().toISOString().split('T')[0],
    endDate: new Date().toISOString().split('T')[0],
    leaveSlot: 'FullDay',
    reason: '',
    leaveType: 'Privilege Leave'
  };

  get isEditMode(): boolean {
    return !!this.editLeave;
  }

  get isInvalidDateRange(): boolean {
    if (!this.leave.startDate || !this.leave.endDate) return false;
    return this.leave.endDate < this.leave.startDate;
  }

  get selectedDurationDays(): number {
    if (this.leave.leaveSlot !== 'FullDay') return 0.5;
    if (!this.leave.startDate || !this.leave.endDate) return 1;
    const start = new Date(this.leave.startDate);
    const end = new Date(this.leave.endDate);
    if (end < start) return 0;
    return Math.max(1, Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1);
  }

  Math = Math;
  get sprintDailyHours(): number {
    return this.state.activeSprint()?.dailyWorkingHours || DEFAULT_DAILY_WORKING_HOURS;
  }

  get canSubmit(): boolean {
    return !!this.leave.teamMemberId &&
           !!this.leave.startDate &&
           !!this.leave.endDate &&
           !this.isInvalidDateRange;
  }

  leaveTypes: { value: string; label: string; icon: IconName; desc: string }[] = [
    { value: 'Privilege Leave', label: 'Privilege Leave', icon: 'palmtree', desc: 'Planned personal time off & vacation' },
    { value: 'Sick Leave', label: 'Sick Leave', icon: 'shield-alert', desc: 'Health, medical recovery & rest' },
    { value: 'Comp Off', label: 'Comp Off', icon: 'gift', desc: 'Compensatory off for sprint/weekend delivery' },
    { value: 'Offshore Public Holiday', label: 'Offshore Public Holiday', icon: 'calendar', desc: 'Regional festival / national public holiday' }
  ];

  slotOptions: { value: 'FullDay' | 'FirstHalf' | 'SecondHalf'; label: string; desc: string; duration: string }[] = [
    { value: 'FullDay', label: 'Full Day', desc: 'Standard full day off', duration: '1.0 Day' },
    { value: 'FirstHalf', label: 'First Half (Morning)', desc: 'Morning off &bull; Available 2nd half', duration: '0.5 Day' },
    { value: 'SecondHalf', label: 'Second Half (Afternoon)', desc: 'Available 1st half &bull; Afternoon off', duration: '0.5 Day' }
  ];

  reasonPresets = [
    'Annual Family Vacation',
    'Personal Errands / Doctor Appointment',
    'Festival Public Holiday (Diwali / New Year)',
    'Sprint Crunch / Weekend Support Comp Off',
    'Medical Checkup / Sick Leave'
  ];

  get teamMembers(): TeamMember[] {
    return this.members.length > 0 ? this.members : this.state.members();
  }

  getRoleLabel(role: string): string {
    switch (role) {
      case 'ScrumMaster': return 'Scrum Master';
      case 'Developer': return 'Developer';
      case 'QaEngineer': return 'QA Engineer';
      case 'Cdl': return 'CDL';
      default: return role;
    }
  }

  ngOnInit(): void {
    if (this.editLeave) {
      let rawType = this.editLeave.leaveType || 'Privilege Leave';
      rawType = rawType.replace(/\s*\([^)]*\)/g, '').trim();
      if (rawType === 'Planned PTO' || rawType === 'PTO' || rawType === 'Vacation / PTO') rawType = 'Privilege Leave';
      if (rawType === 'National Holiday' || rawType === 'Offshore Holiday') rawType = 'Offshore Public Holiday';
      if (rawType === 'Training / Workshop' || rawType === 'Tech Workshop') rawType = 'Privilege Leave';
      if (rawType === 'Medical Recovery') rawType = 'Sick Leave';

      const sDate = this.editLeave.startDate ? new Date(this.editLeave.startDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0];
      const eDate = this.editLeave.endDate ? new Date(this.editLeave.endDate).toISOString().split('T')[0] : sDate;

      this.leave = {
        teamMemberId: this.editLeave.teamMemberId,
        startDate: sDate,
        endDate: eDate < sDate ? sDate : eDate,
        leaveSlot: (this.editLeave.leaveSlot as any) || 'FullDay',
        reason: this.editLeave.reason || '',
        leaveType: rawType
      };
    } else if (!this.leave.teamMemberId && this.teamMembers.length > 0) {
      this.leave.teamMemberId = this.teamMembers[0].id;
    }
  }

  onSelectSlot(slot: 'FullDay' | 'FirstHalf' | 'SecondHalf'): void {
    this.leave.leaveSlot = slot;
    if (slot !== 'FullDay') {
      this.leave.endDate = this.leave.startDate;
    }
  }

  onStartDateChange(): void {
    if (!this.leave.endDate || this.leave.endDate < this.leave.startDate || this.leave.leaveSlot !== 'FullDay') {
      this.leave.endDate = this.leave.startDate;
    }
  }

  onEndDateChange(): void {
    if (this.leave.endDate && this.leave.endDate < this.leave.startDate) {
      this.leave.endDate = this.leave.startDate;
    }
  }

  applyHalfDay(slot: 'FirstHalf' | 'SecondHalf'): void {
    this.onSelectSlot(slot);
    this.leave.endDate = this.leave.startDate;
  }

  applyDuration(days: number): void {
    this.leave.leaveSlot = 'FullDay';
    const start = new Date(this.leave.startDate || new Date());
    const end = new Date(start);
    end.setDate(start.getDate() + (days - 1));
    this.leave.endDate = end.toISOString().split('T')[0];
  }

  setReason(preset: string): void {
    this.leave.reason = preset;
  }

  onConfirmSubmit(): void {
    if (!this.canSubmit) return;

    if (this.leave.endDate < this.leave.startDate || this.leave.leaveSlot !== 'FullDay') {
      this.leave.endDate = this.leave.startDate;
    }

    const payload = {
      ...this.leave,
      reason: this.leave.reason?.trim() || 'Planned Leave'
    };

    this.save.emit(payload);
  }

  onDelete(): void {
    if (this.editLeave) {
      this.delete.emit(this.editLeave.id);
    }
  }
}
