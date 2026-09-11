import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { NotificationService } from '../../core/services/notification.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { BlockerCardComponent } from './components/blocker-card/blocker-card.component';
import { AddBlockerModalComponent } from './components/add-blocker-modal/add-blocker-modal.component';
import { ResolveBlockerModalComponent } from './components/resolve-blocker-modal/resolve-blocker-modal.component';
import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { Blocker } from '../../core/models/scrum.models';

@Component({
  selector: 'app-blockers',
  standalone: true,
  imports: [
    CommonModule,
    IconComponent,
    BlockerCardComponent,
    AddBlockerModalComponent,
    ResolveBlockerModalComponent,
    ConfirmModalComponent
  ],
  templateUrl: './blockers.component.html',
  styleUrl: './blockers.component.css'
})
export class BlockersComponent {
  state = inject(ScrumStateService);
  notification = inject(NotificationService);
  showNewBlockerModal = signal(false);
  selectedBlockerForEdit = signal<Blocker | null>(null);
  selectedBlockerForResolution = signal<Blocker | null>(null);
  blockerToDelete = signal<Blocker | null>(null);

  onOpenCreateModal(): void {
    this.selectedBlockerForEdit.set(null);
    this.showNewBlockerModal.set(true);
  }

  onOpenEditModal(blocker: Blocker): void {
    this.selectedBlockerForEdit.set(blocker);
    this.showNewBlockerModal.set(true);
  }

  onCloseBlockerModal(): void {
    this.selectedBlockerForEdit.set(null);
    this.showNewBlockerModal.set(false);
  }

  onSaveBlocker(blockerData: { title: string; description: string; category: number; slaHoursLimit: number; blockedHours?: number }): void {
    const editItem = this.selectedBlockerForEdit();
    if (editItem) {
      this.state.updateBlocker(editItem.id, {
        ...blockerData,
        blockedHours: blockerData.blockedHours || 0,
        sprintId: editItem.sprintId || this.state.activeSprint()?.id,
        raisedById: editItem.raisedById || this.state.squadMembers()[0]?.id
      });
      this.notification.showSuccess('Blocker Updated', `Blocker "${blockerData.title}" updated successfully.`, 'shield-alert');
    } else {
      this.state.createBlocker({
        ...blockerData,
        blockedHours: blockerData.blockedHours || 0,
        sprintId: this.state.activeSprint()?.id,
        raisedById: this.state.squadMembers()[0]?.id
      });
      this.notification.showSuccess('Blocker Logged', `Blocker "${blockerData.title}" added successfully.`, 'shield-alert');
    }

    this.onCloseBlockerModal();
  }

  onOpenResolveModal(blocker: Blocker): void {
    this.selectedBlockerForResolution.set(blocker);
  }

  onConfirmResolve(event: { id: string; notes: string; blockedHours?: number }): void {
    const target = this.selectedBlockerForResolution();
    const title = target?.title || 'Item';
    this.state.resolveBlocker(event.id, event.notes, event.blockedHours);
    this.notification.showSuccess('Blocker Resolved', `Blocker "${title}" resolved successfully.`, 'check-circle');
    this.selectedBlockerForResolution.set(null);
  }

  onPromptDeleteBlocker(blocker: Blocker): void {
    this.blockerToDelete.set(blocker);
  }

  onConfirmDeleteBlocker(): void {
    const target = this.blockerToDelete();
    if (target?.id) {
      this.state.deleteBlocker(target.id);
      this.notification.showSuccess('Blocker Deleted', `"${target.title}" removed from radar.`, 'trash-2');
      this.blockerToDelete.set(null);
    }
  }

  onDeleteFromModal(id: string): void {
    this.state.deleteBlocker(id);
    this.notification.showSuccess('Blocker Deleted', 'Blocker removed from radar.', 'trash-2');
    this.onCloseBlockerModal();
  }
}
