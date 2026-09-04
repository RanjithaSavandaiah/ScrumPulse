import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { RecordFeedbackModalComponent } from './components/record-feedback-modal/record-feedback-modal.component';
import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { CleanNamePipe } from '../../core/pipes/clean-name.pipe';
import { MonthlyFeedback } from '../../core/models/scrum.models';

@Component({
  selector: 'app-reviews',
  standalone: true,
  imports: [CommonModule, IconComponent, RecordFeedbackModalComponent, ConfirmModalComponent, CleanNamePipe],
  templateUrl: './reviews.component.html',
  styleUrl: './reviews.component.css'
})
export class ReviewsComponent {
  state = inject(ScrumStateService);
  showFeedbackModal = signal(false);
  selectedFeedbackForEdit = signal<MonthlyFeedback | null>(null);
  feedbackToDelete = signal<MonthlyFeedback | null>(null);

  squadFeedbacks = computed(() => {
    const list = this.state.monthlyFeedbacks();
    const current = this.state.currentTeam();
    if (!current) return list;
    const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
    return list.filter(f => f.teamMemberId && squadMemberIds.has(f.teamMemberId.toLowerCase().trim()));
  });

  onOpenCreateModal(): void {
    this.selectedFeedbackForEdit.set(null);
    this.showFeedbackModal.set(true);
  }

  onOpenEditModal(feedback: MonthlyFeedback): void {
    this.selectedFeedbackForEdit.set(feedback);
    this.showFeedbackModal.set(true);
  }

  onCloseFeedbackModal(): void {
    this.selectedFeedbackForEdit.set(null);
    this.showFeedbackModal.set(false);
  }

  onSaveFeedback(feedbackData: any): void {
    const editItem = this.selectedFeedbackForEdit();
    if (editItem) {
      this.state.updateMonthlyFeedback(editItem.id, feedbackData);
    } else {
      this.state.submitMonthlyFeedback(feedbackData);
    }
    this.onCloseFeedbackModal();
  }

  onPromptDeleteFeedback(feedback: MonthlyFeedback): void {
    this.feedbackToDelete.set(feedback);
  }

  onConfirmDeleteFeedback(): void {
    const target = this.feedbackToDelete();
    if (target?.id) {
      this.state.deleteMonthlyFeedback(target.id);
      this.feedbackToDelete.set(null);
    }
  }

  onDeleteFromModal(id: string): void {
    this.state.deleteMonthlyFeedback(id);
    this.onCloseFeedbackModal();
  }
}
