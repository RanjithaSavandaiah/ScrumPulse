import { Component, effect, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { AiInsightsCardComponent } from './components/ai-insights-card/ai-insights-card.component';
import { CopilotChatComponent } from './components/copilot-chat/copilot-chat.component';
import { AiSuggestionResponse, CopilotChatResponse } from '../../core/models/scrum.models';

@Component({
  selector: 'app-ai-coach',
  standalone: true,
  imports: [CommonModule, AiInsightsCardComponent, CopilotChatComponent],
  templateUrl: './ai-coach.component.html',
  styleUrl: './ai-coach.component.css'
})
export class AiCoachComponent implements OnInit {
  state = inject(ScrumStateService);
  private readonly cdr = inject(ChangeDetectorRef);

  aiTier = 'individual';
  aiData: AiSuggestionResponse | null = null;
  selectedMemberId: string = '';
  selectedSprintId: string = '';

  chatMessages: { isUser: boolean; text: string }[] = [
    { isUser: false, text: 'Hello! I am your Microsoft Agent Framework Agile Coach. Ask me about sprint risks, developer cycle times, or 1:1 coaching plans.' }
  ];

  constructor() {
    effect(() => {
      const members = this.state.squadMembers();
      if (members.length > 0 && !this.selectedMemberId && this.aiTier === 'individual') {
        this.selectedMemberId = members[0].id;
        this.state.getIndividualAi(members[0].id).subscribe({
          next: (aiSuggestion: AiSuggestionResponse) => {
            this.aiData = aiSuggestion;
            this.cdr.markForCheck();
          },
          error: err => console.error('[AiCoachComponent] Failed to load individual AI suggestions:', err)
        });
      }
    });
  }

  ngOnInit() {
    this.loadAiSuggestion('individual');
  }

  loadAiSuggestion(tier: string) {
    this.aiTier = tier;
    if (tier === 'individual') {
      const targetId = this.selectedMemberId || this.state.squadMembers()[0]?.id;
      if (targetId) {
        this.selectedMemberId = targetId;
        this.state.getIndividualAi(targetId).subscribe({
          next: (aiSuggestion: AiSuggestionResponse) => {
            this.aiData = aiSuggestion;
            this.cdr.markForCheck();
          },
          error: err => console.error('[AiCoachComponent] Failed to load individual AI suggestions:', err)
        });
      }
    } else if (tier === 'project') {
      const targetSprintId = this.selectedSprintId || this.state.activeSprint()?.id || this.state.sprints()[0]?.id;
      if (targetSprintId) {
        this.selectedSprintId = targetSprintId;
        this.state.getProjectAi(targetSprintId).subscribe({
          next: (aiSuggestion: AiSuggestionResponse) => {
            this.aiData = aiSuggestion;
            this.cdr.markForCheck();
          },
          error: err => console.error('[AiCoachComponent] Failed to load project AI suggestions:', err)
        });
      }
    } else {
      this.state.getCompanyAi().subscribe({
        next: (aiSuggestion: AiSuggestionResponse) => {
          this.aiData = aiSuggestion;
          this.cdr.markForCheck();
        },
        error: err => console.error('[AiCoachComponent] Failed to load company AI suggestions:', err)
      });
    }
  }

  onMemberChange(memberId: string): void {
    this.selectedMemberId = memberId;
    this.state.getIndividualAi(memberId).subscribe({
      next: (aiSuggestion: AiSuggestionResponse) => {
        this.aiData = aiSuggestion;
        this.cdr.markForCheck();
      },
      error: err => console.error('[AiCoachComponent] Failed to load individual AI suggestions for member:', err)
    });
  }

  onSprintChange(sprintId: string): void {
    this.selectedSprintId = sprintId;
    this.state.getProjectAi(sprintId).subscribe({
      next: (aiSuggestion: AiSuggestionResponse) => {
        this.aiData = aiSuggestion;
        this.cdr.markForCheck();
      },
      error: err => console.error('[AiCoachComponent] Failed to load project AI suggestions for sprint:', err)
    });
  }

  sendChat(promptMessage: string) {
    this.chatMessages.push({ isUser: true, text: promptMessage });
    this.cdr.markForCheck();

    this.state.askCopilot(promptMessage, this.state.currentRole()).subscribe({
      next: (chatResponse: CopilotChatResponse) => {
        this.chatMessages.push({ isUser: false, text: chatResponse.answer });
        this.cdr.markForCheck();
      },
      error: err => {
        console.error('[AiCoachComponent] Copilot request failed:', err);
        this.chatMessages.push({
          isUser: false,
          text: 'Sorry, I encountered an issue processing your query. Please review the logs.'
        });
        this.cdr.markForCheck();
      }
    });
  }
}
