import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { DailyStandup, TeamMember } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';

import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-log-standup-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './log-standup-modal.component.html',
  styleUrl: './log-standup-modal.component.css'
})
export class LogStandupModalComponent implements OnInit {
  state = inject(ScrumStateService);

  @Input() members: TeamMember[] = [];
  @Input() editStandup: DailyStandup | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ teamMemberId: string; yesterdaySummary: string; todayPlan: string; blockersText: string; moodScore: number }>();
  @Output() delete = new EventEmitter<string>();

  standup = {
    teamMemberId: '',
    yesterdaySummary: '',
    todayPlan: '',
    blockersText: 'None',
    moodScore: 5
  };

  get isEditMode(): boolean {
    return !!this.editStandup;
  }

  get teamMembers(): TeamMember[] {
    const list = this.members.length > 0 ? this.members : this.state.squadMembers();
    return list.filter(m => {
      const role = (m.role || '').toLowerCase();
      return role !== 'scrummaster' && role !== 'cdl' && role !== 'sm';
    });
  }

  ngOnInit(): void {
    if (this.editStandup) {
      this.standup = {
        teamMemberId: this.editStandup.teamMemberId,
        yesterdaySummary: this.editStandup.yesterdaySummary || '',
        todayPlan: this.editStandup.todayPlan || '',
        blockersText: this.editStandup.blockersText || 'None',
        moodScore: this.editStandup.moodScore || 5
      };
    } else if (!this.standup.teamMemberId && this.teamMembers.length > 0) {
      this.standup.teamMemberId = this.teamMembers[0].id;
    }
  }

  yesterdayPresets = [
    'Merged feature branch & unit tests',
    'Completed PR review with onshore squad',
    'Fixed sprint backlog defect',
    'Refactored API endpoint schema'
  ];

  todayPresets = [
    'Building OAuth authentication flow',
    'Conducting Staging QA verification',
    'Pairing on architecture review',
    'Refining backlog user stories'
  ];

  blockerPresets = [
    'None (Smooth Flow)',
    'Waiting on DB credentials from DevOps',
    'Pending API contract signoff from client',
    'Staging environment pipeline restart needed'
  ];

  energyLevels: { score: number; icon: IconName; label: string; desc: string }[] = [
    { score: 5, icon: 'sparkles', label: 'Peak Flow', desc: 'Crushing sprint goals' },
    { score: 4, icon: 'zap', label: 'High Energy', desc: 'Steady focus & momentum' },
    { score: 3, icon: 'smile', label: 'Steady Cadence', desc: 'Normal delivery pace' },
    { score: 2, icon: 'clock', label: 'Fatigued', desc: 'Heavy context switching' },
    { score: 1, icon: 'shield-alert', label: 'Stuck / Blocked', desc: 'Urgent unblocking needed' }
  ];

  appendYesterday(text: string): void {
    this.standup.yesterdaySummary = this.standup.yesterdaySummary ? `${this.standup.yesterdaySummary}; ${text}` : text;
  }

  appendToday(text: string): void {
    this.standup.todayPlan = this.standup.todayPlan ? `${this.standup.todayPlan}; ${text}` : text;
  }

  setBlocker(text: string): void {
    this.standup.blockersText = text;
  }

  onDelete(): void {
    if (this.editStandup) {
      this.delete.emit(this.editStandup.id);
    }
  }
}
