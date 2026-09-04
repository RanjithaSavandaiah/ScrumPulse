import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { AddRetroCardModalComponent } from './components/add-retro-card-modal/add-retro-card-modal.component';
import { RetroActionItem, RetroCard, Sprint } from '../../core/models/scrum.models';

@Component({
  selector: 'app-retro',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, AddRetroCardModalComponent, ConfirmModalComponent],
  templateUrl: './retro.component.html',
  styleUrl: './retro.component.css'
})
export class RetroComponent implements OnInit {
  state = inject(ScrumStateService);

  showRetroModal = signal(false);
  editingCard = signal<RetroCard | null>(null);
  cardToDelete = signal<RetroCard | null>(null);

  showActionModal = signal(false);
  editingAction = signal<RetroActionItem | null>(null);
  actionToDelete = signal<RetroActionItem | null>(null);

  selectedSprintId = signal<string>('ALL');

  ngOnInit(): void {
    const active = this.state.activeSprint();
    if (active) {
      this.selectedSprintId.set(active.id);
    }
  }

  actionForm = {
    title: '',
    assigneeId: '',
    dueDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
    isCompleted: false
  };

  selectedSprintObj = computed(() => {
    const id = this.selectedSprintId();
    if (id === 'ALL') return null;
    return this.state.sprints().find(s => s.id === id) || null;
  });

  filteredCards = computed(() => {
    const sprintId = this.selectedSprintId();
    let cards = this.state.retroCards();
    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
      cards = cards.filter(card => !card.authorId || squadMemberIds.has(card.authorId.toLowerCase().trim()));
    }
    if (sprintId === 'ALL') return cards;
    return cards.filter(card => card.sprintId === sprintId);
  });

  filteredActions = computed(() => {
    const sprintId = this.selectedSprintId();
    let actions = this.state.retroActions();
    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
      actions = actions.filter(action => !action.assigneeId || squadMemberIds.has(action.assigneeId.toLowerCase().trim()));
    }
    if (sprintId === 'ALL') return actions;
    return actions.filter(action => action.sprintId === sprintId);
  });

  getCardsByCategory(categoryIndex: number) {
    const categoryNames = ['WentWell', 'DidntGoWell', 'Ideas', 'ActionItem'];
    return this.filteredCards().filter((retroCard: any) => {
      return retroCard.category === categoryIndex || retroCard.category === categoryNames[categoryIndex];
    });
  }

  onOpenAddCard(): void {
    this.editingCard.set(null);
    this.showRetroModal.set(true);
  }

  onEditCard(card: RetroCard): void {
    this.editingCard.set(card);
    this.showRetroModal.set(true);
  }

  onDeleteCard(card: RetroCard): void {
    this.cardToDelete.set(card);
  }

  onConfirmDeleteCard(): void {
    const card = this.cardToDelete();
    if (card) {
      this.state.deleteRetroCard(card.id);
      this.cardToDelete.set(null);
    }
  }

  onCancelDeleteCard(): void {
    this.cardToDelete.set(null);
  }

  onSaveRetroCard(cardData: { category: number; authorId: string; content: string; isAnonymous: boolean }) {
    const edit = this.editingCard();
    if (edit) {
      this.state.updateRetroCard(edit.id, {
        ...cardData,
        sprintId: edit.sprintId
      });
      this.editingCard.set(null);
    } else {
      const targetSprintId = this.selectedSprintId() !== 'ALL'
        ? this.selectedSprintId()
        : this.state.activeSprint()?.id;

      this.state.createRetroCard({
        ...cardData,
        sprintId: targetSprintId,
        authorId: cardData.authorId || this.state.squadMembers()[0]?.id
      });
    }
    this.showRetroModal.set(false);
  }

  onOpenAddAction(): void {
    this.editingAction.set(null);
    this.actionForm = {
      title: '',
      assigneeId: this.state.squadMembers()[0]?.id || '',
      dueDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
      isCompleted: false
    };
    this.showActionModal.set(true);
  }

  onEditAction(action: RetroActionItem): void {
    this.editingAction.set(action);
    this.actionForm = {
      title: action.title,
      assigneeId: action.assigneeId || '',
      dueDate: action.dueDate ? new Date(action.dueDate).toISOString().split('T')[0] : '',
      isCompleted: action.isCompleted
    };
    this.showActionModal.set(true);
  }

  onDeleteAction(action: RetroActionItem): void {
    this.actionToDelete.set(action);
  }

  onConfirmDeleteAction(): void {
    const action = this.actionToDelete();
    if (action) {
      this.state.deleteRetroAction(action.id);
      this.actionToDelete.set(null);
    }
  }

  onCancelDeleteAction(): void {
    this.actionToDelete.set(null);
  }

  onSaveAction(): void {
    if (!this.actionForm.title.trim()) return;

    const edit = this.editingAction();
    const targetSprintId = this.selectedSprintId() !== 'ALL'
      ? this.selectedSprintId()
      : this.state.activeSprint()?.id;

    if (edit) {
      this.state.updateRetroAction(edit.id, {
        sprintId: edit.sprintId || targetSprintId,
        title: this.actionForm.title.trim(),
        assigneeId: this.actionForm.assigneeId || null,
        dueDate: this.actionForm.dueDate || null,
        isCompleted: this.actionForm.isCompleted
      });
      this.editingAction.set(null);
    } else {
      this.state.createRetroAction({
        sprintId: targetSprintId,
        title: this.actionForm.title.trim(),
        assigneeId: this.actionForm.assigneeId || null,
        dueDate: this.actionForm.dueDate || null
      });
    }

    this.showActionModal.set(false);
  }
}
