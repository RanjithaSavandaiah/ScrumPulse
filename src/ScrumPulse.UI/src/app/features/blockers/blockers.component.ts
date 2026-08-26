import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
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

  onSaveBlocker(blockerData: { title: string; description: string; category: number; slaHoursLimit: number }): void {
    const editItem = this.selectedBlockerForEdit();
    if (editItem) {
      this.state.updateBlocker(editItem.id, {
        ...blockerData,
        sprintId: editItem.sprintId || this.state.activeSprint()?.id,
        raisedById: editItem.raisedById || this.state.members()[0]?.id
      });
    } else {
      this.state.createBlocker({
        ...blockerData,
        sprintId: this.state.activeSprint()?.id,
        raisedById: this.state.members()[3]?.id || this.state.members()[0]?.id
      });
    }

    this.onCloseBlockerModal();
  }

  onOpenResolveModal(blocker: Blocker): void {
    this.selectedBlockerForResolution.set(blocker);
  }

  onConfirmResolve(event: { id: string; notes: string }): void {
    this.state.resolveBlocker(event.id, event.notes);
    this.selectedBlockerForResolution.set(null);
  }

  onPromptDeleteBlocker(blocker: Blocker): void {
    this.blockerToDelete.set(blocker);
  }

  onConfirmDeleteBlocker(): void {
    const target = this.blockerToDelete();
    if (target?.id) {
      this.state.deleteBlocker(target.id);
      this.blockerToDelete.set(null);
    }
  }

  onDeleteFromModal(id: string): void {
    this.state.deleteBlocker(id);
    this.onCloseBlockerModal();
  }
}
