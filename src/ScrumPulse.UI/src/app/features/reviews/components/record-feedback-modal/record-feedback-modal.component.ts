import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { MonthlyFeedback, TeamMember } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';

@Component({
  selector: 'app-record-feedback-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './record-feedback-modal.component.html',
  styleUrl: './record-feedback-modal.component.css'
})
export class RecordFeedbackModalComponent implements OnInit {
  state = inject(ScrumStateService);

  @Input() members: TeamMember[] = [];
  @Input() editFeedback: MonthlyFeedback | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<any>();
  @Output() delete = new EventEmitter<string>();

  feedback = {
    teamMemberId: '',
    monthYear: new Date().toISOString().slice(0, 7),
    scrumMasterFeedback: '',
    cdlFeedback: '',
    clientFeedback: '',
    selfReflection: '',
    smRating: 5,
    happinessIndex: 5,
    actionItems: 'Maintain focus on DoD quality checks and architectural mentoring',
    nextMonthGoals: 'Lead architectural spike for distributed cloud streaming'
  };

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
    if (this.editFeedback) {
      this.feedback = {
        teamMemberId: this.editFeedback.teamMemberId || '',
        monthYear: this.editFeedback.monthYear || new Date().toISOString().slice(0, 7),
        scrumMasterFeedback: this.editFeedback.scrumMasterFeedback || '',
        cdlFeedback: this.editFeedback.cdlFeedback || '',
        clientFeedback: this.editFeedback.clientFeedback || '',
        selfReflection: this.editFeedback.selfReflection || '',
        smRating: this.editFeedback.smRating || 5,
        happinessIndex: this.editFeedback.happinessIndex || 5,
        actionItems: this.editFeedback.actionItems || 'Maintain focus on DoD quality checks and architectural mentoring',
        nextMonthGoals: this.editFeedback.nextMonthGoals || 'Lead architectural spike for distributed cloud streaming'
      };
    } else if (!this.feedback.teamMemberId && this.teamMembers.length > 0) {
      this.feedback.teamMemberId = this.teamMembers[0].id;
    }
  }

  onDelete(): void {
    if (this.editFeedback?.id) {
      this.delete.emit(this.editFeedback.id);
    }
  }

  happinessDials: { score: number; icon: IconName; label: string }[] = [
    { score: 5, icon: 'sparkles', label: 'Exceptional (5/5)' },
    { score: 4, icon: 'zap', label: 'Thriving (4/5)' },
    { score: 3, icon: 'smile', label: 'Satisfied (3/5)' },
    { score: 2, icon: 'clock', label: 'Neutral (2/5)' },
    { score: 1, icon: 'shield-alert', label: 'At Risk (1/5)' }
  ];

  smPresets = [
    'Outstanding sprint cadence with zero escaped bugs',
    'Swift PR review turnaround and active pair programming',
    'Demonstrated strong DoD rigor and clear standup updates'
  ];

  cdlPresets = [
    'Ready for Senior Developer promotion track',
    'Great leadership during tech talks and knowledge sharing',
    'Strong cross-squad alignment and architectural vision'
  ];

  clientPresets = [
    'High confidence in offshore sprint delivery and demos',
    'Appreciates proactive communication during overlap hours',
    'Clear requirement walkthroughs and fast resolution of questions'
  ];

  selfPresets = [
    'Achieved 100% of my committed story points',
    'Improved test coverage and resolved complex edge cases',
    'Collaborated closely with onshore team on API contracts'
  ];

  setSmFeedback(preset: string): void { this.feedback.scrumMasterFeedback = preset; }
  setCdlFeedback(preset: string): void { this.feedback.cdlFeedback = preset; }
  setClientFeedback(preset: string): void { this.feedback.clientFeedback = preset; }
  setSelfReflection(preset: string): void { this.feedback.selfReflection = preset; }
}
