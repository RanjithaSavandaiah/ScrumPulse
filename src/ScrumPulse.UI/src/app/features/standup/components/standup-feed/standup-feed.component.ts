import { Component, EventEmitter, Input, Output, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { DailyStandup } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { CORE_PIPES } from '../../../../core/pipes';
import { cleanName } from '../../../../core/utils/format-utils';

import { ConfirmModalComponent } from '../../../../core/components/confirm-modal/confirm-modal.component';

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
  currentPage = signal<number>(1);
  pageSize = signal<number>(5);

  // Custom Delete Confirmation Modal Signal
  standupToDelete = signal<DailyStandup | null>(null);

  // Reactive filtered list directly bound to state signals
  filteredStandups = computed(() => {
    let list = this.state.standups();

    if (this.selectedSprintId() !== 'ALL') {
      list = list.filter(s => s.sprintId === this.selectedSprintId());
    }

    if (this.selectedMemberId() !== 'ALL') {
      list = list.filter(s => s.teamMemberId === this.selectedMemberId());
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
    return this.state.members().filter(m => {
      const role = (m.role || '').toLowerCase();
      return role !== 'scrummaster' && role !== 'cdl' && role !== 'sm';
    });
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
        this.currentPage.update(p => p - 1);
      }
      this.standupToDelete.set(null);
    }
  }

  onCancelDelete(): void {
    this.standupToDelete.set(null);
  }
}
