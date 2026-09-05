import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { AiSuggestionResponse, Sprint, TeamMember } from '../../../../core/models/scrum.models';

import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-ai-insights-card',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './ai-insights-card.component.html',
  styleUrl: './ai-insights-card.component.css'
})
export class AiInsightsCardComponent {
  @Input() aiTier: string = 'individual';
  @Input() aiData: AiSuggestionResponse | null = null;
  @Input() members: TeamMember[] = [];
  @Input() sprints: Sprint[] = [];
  @Input() selectedMemberId: string = '';
  @Input() selectedSprintId: string = '';

  @Output() tierChange = new EventEmitter<string>();
  @Output() memberChange = new EventEmitter<string>();
  @Output() sprintChange = new EventEmitter<string>();

  onMemberSelect(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.memberChange.emit(val);
  }

  onSprintSelect(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.sprintChange.emit(val);
  }
}
