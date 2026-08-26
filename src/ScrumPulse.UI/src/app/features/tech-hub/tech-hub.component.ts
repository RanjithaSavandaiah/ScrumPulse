import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { TechDebtItem, TechTalkLog } from '../../core/models/scrum.models';
import { TechDebtModalComponent } from './components/tech-debt-modal/tech-debt-modal.component';
import { TechTalkModalComponent } from './components/tech-talk-modal/tech-talk-modal.component';

import { ConfirmModalComponent } from '../../core/components/confirm-modal/confirm-modal.component';
import { cleanName } from '../../core/utils/format-utils';

@Component({
  selector: 'app-tech-hub',
  standalone: true,
  imports: [CommonModule, IconComponent, TechDebtModalComponent, TechTalkModalComponent, ConfirmModalComponent],
  templateUrl: './tech-hub.component.html',
  styleUrl: './tech-hub.component.css'
})
export class TechHubComponent {
  state = inject(ScrumStateService);

  // Modals state
  isTechDebtModalOpen = signal<boolean>(false);
  selectedTechDebt = signal<TechDebtItem | null>(null);
  debtToDelete = signal<TechDebtItem | null>(null);

  isTechTalkModalOpen = signal<boolean>(false);
  selectedTechTalk = signal<TechTalkLog | null>(null);
  talkToDelete = signal<TechTalkLog | null>(null);

  // Filters
  techDebtFilter = signal<string>('ALL');

  filteredTechDebt = computed(() => {
    const list = this.state.techDebt();
    const filter = this.techDebtFilter();
    if (filter === 'ALL') return list;
    return list.filter(i => (i.status || 'Identified').toLowerCase() === filter.toLowerCase());
  });

  getPresenterName(talk: TechTalkLog): string {
    if (talk.presenterName) return cleanName(talk.presenterName);
    const member = this.state.members().find(m => m.id === talk.presenterId);
    return member ? cleanName(member.name) : 'Team Member';
  }

  getSprintName(sprintId?: string): string {
    if (!sprintId) return 'Unassigned Backlog';
    const sprint = this.state.sprints().find(s => s.id === sprintId);
    return sprint ? sprint.name : 'Unassigned Backlog';
  }

  getAssigneeName(item: TechDebtItem): string {
    if (item.assigneeName) return cleanName(item.assigneeName);
    if (item.assigneeId) {
      const member = this.state.members().find(m => m.id === item.assigneeId);
      if (member) return cleanName(member.name);
    }
    return 'Unassigned';
  }

  // Tech Debt Actions
  openCreateTechDebt(): void {
    this.selectedTechDebt.set(null);
    this.isTechDebtModalOpen.set(true);
  }

  openEditTechDebt(item: TechDebtItem): void {
    this.selectedTechDebt.set(item);
    this.isTechDebtModalOpen.set(true);
  }

  closeTechDebtModal(): void {
    this.isTechDebtModalOpen.set(false);
    this.selectedTechDebt.set(null);
  }

  saveTechDebt(payload: any): void {
    if (payload.id) {
      this.state.updateTechDebt(payload.id, payload);
    } else {
      this.state.createTechDebt(payload);
    }
    this.closeTechDebtModal();
  }

  deleteTechDebt(id: string): void {
    const item = this.state.techDebt().find(t => t.id === id);
    if (item) {
      this.closeTechDebtModal();
      this.debtToDelete.set(item);
    } else {
      this.state.deleteTechDebt(id);
      this.closeTechDebtModal();
    }
  }

  quickDeleteTechDebt(item: TechDebtItem, event: MouseEvent): void {
    event.stopPropagation();
    this.debtToDelete.set(item);
  }

  onConfirmDeleteTechDebt(): void {
    const target = this.debtToDelete();
    if (target) {
      this.state.deleteTechDebt(target.id);
      this.debtToDelete.set(null);
    }
  }

  onCancelDeleteTechDebt(): void {
    this.debtToDelete.set(null);
  }

  toggleResolveTechDebt(item: TechDebtItem, event: MouseEvent): void {
    event.stopPropagation();
    const newStatus = item.status === 'Resolved' ? 'Identified' : 'Resolved';
    this.state.resolveTechDebt(item.id, newStatus);
  }

  // Tech Talk Actions
  openCreateTechTalk(): void {
    this.selectedTechTalk.set(null);
    this.isTechTalkModalOpen.set(true);
  }

  openEditTechTalk(talk: TechTalkLog): void {
    this.selectedTechTalk.set(talk);
    this.isTechTalkModalOpen.set(true);
  }

  closeTechTalkModal(): void {
    this.isTechTalkModalOpen.set(false);
    this.selectedTechTalk.set(null);
  }

  saveTechTalk(payload: any): void {
    if (payload.id) {
      this.state.updateTechTalk(payload.id, payload);
    } else {
      this.state.createTechTalk(payload);
    }
    this.closeTechTalkModal();
  }

  deleteTechTalk(id: string): void {
    const talk = this.state.techTalks().find(t => t.id === id);
    if (talk) {
      this.closeTechTalkModal();
      this.talkToDelete.set(talk);
    } else {
      this.state.deleteTechTalk(id);
      this.closeTechTalkModal();
    }
  }

  quickDeleteTechTalk(talk: TechTalkLog, event: MouseEvent): void {
    event.stopPropagation();
    this.talkToDelete.set(talk);
  }

  onConfirmDeleteTechTalk(): void {
    const target = this.talkToDelete();
    if (target) {
      this.state.deleteTechTalk(target.id);
      this.talkToDelete.set(null);
    }
  }

  onCancelDeleteTechTalk(): void {
    this.talkToDelete.set(null);
  }
}
