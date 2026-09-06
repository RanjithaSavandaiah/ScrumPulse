import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { DailyStandup } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { CORE_PIPES } from '../../../../core/pipes';
import { cleanName, isDeliveryRole } from '../../../../core/utils/format-utils';

import { ConfirmModalComponent } from '../../../../core/components/confirm-modal/confirm-modal.component';

export const DEFAULT_PAGE_SIZE = 5;
export const INITIAL_PAGE_NUMBER = 1;

@Component({
  selector: 'app-standup-feed',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ConfirmModalComponent, ...CORE_PIPES],
  templateUrl: './standup-feed.component.html',
  styleUrl: './standup-feed.component.css'
})
export class StandupFeedComponent {
  state = inject(ScrumStateService);

  @Input() standups: DailyStandup[] = [];
  @Output() logStandup = new EventEmitter<void>();
  @Output() editStandup = new EventEmitter<DailyStandup>();

  // Filter signals
  selectedSprintId = signal<string>('ALL');
  selectedMemberId = signal<string>('ALL');

  // Pagination signals
  currentPage = signal<number>(INITIAL_PAGE_NUMBER);
  pageSize = signal<number>(DEFAULT_PAGE_SIZE);

  // Custom Delete Confirmation Modal Signal
  standupToDelete = signal<DailyStandup | null>(null);

  // Reactive filtered list directly bound to state signals
  filteredStandups = computed(() => {
    let list = this.state.standups();
    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(member => member.id.toLowerCase().trim()));
      list = list.filter(standup => standup.teamMemberId && squadMemberIds.has(standup.teamMemberId.toLowerCase().trim()));
    }

    if (this.selectedSprintId() !== 'ALL') {
      list = list.filter(standup => standup.sprintId === this.selectedSprintId());
    }

    if (this.selectedMemberId() !== 'ALL') {
      list = list.filter(standup => standup.teamMemberId === this.selectedMemberId());
    }

    return list;
  });

  // Total pages
  totalPages = computed(() => {
    const total = this.filteredStandups().length;
    return Math.max(1, Math.ceil(total / this.pageSize()));
  });

  // Paginated slice
  paginatedStandups = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.filteredStandups().slice(start, start + this.pageSize());
  });

  // Contributing members for filter
  contributingMembers = computed(() => {
    return this.state.squadMembers().filter(member => isDeliveryRole(member.role));
  });

  setPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.currentPage.set(page);
    }
  }

  onEditStandup(standup: DailyStandup): void {
    this.editStandup.emit(standup);
  }

  onDeleteStandup(standup: DailyStandup): void {
    this.standupToDelete.set(standup);
  }

  onConfirmDelete(): void {
    const target = this.standupToDelete();
    if (target) {
      this.state.deleteStandup(target.id);
      if (this.paginatedStandups().length === 1 && this.currentPage() > 1) {
        this.currentPage.update(previousPage => previousPage - 1);
      }
      this.standupToDelete.set(null);
    }
  }

  onCancelDelete(): void {
    this.standupToDelete.set(null);
  }
}
