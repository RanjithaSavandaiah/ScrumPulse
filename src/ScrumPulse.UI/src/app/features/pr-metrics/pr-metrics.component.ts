import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { DeveloperPrMetrics, PullRequestLog } from '../../core/models/scrum.models';

import { CORE_PIPES } from '../../core/pipes';
import { cleanName, getInitials } from '../../core/utils/format-utils';

@Component({
  selector: 'app-pr-metrics',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './pr-metrics.component.html',
  styleUrl: './pr-metrics.component.css'
})
export class PrMetricsComponent {
  state = inject(ScrumStateService);

  selectedSprintId = signal<string>('ALL');
  selectedDeveloperId = signal<string>('ALL');
  showLogPrModal = signal<boolean>(false);

  newPr = {
    workItemId: '',
    authorId: '',
    reviewerId: '',
    sprintId: '',
    prNumber: '',
    prTitle: '',
    prUrl: '',
    totalCommentsCount: 0,
    actionableCommentsCount: 0,
    reviewSummary: '',
    reviewStatus: 'Approved'
  };

  // Contributing squad developers / engineers (excludes ScrumMaster and CDL)
  developerMembers = computed(() => {
    return this.state.members().filter(m => {
      const role = (m.role || '').toLowerCase();
      return role !== 'scrummaster' && role !== 'cdl' && role !== 'sm';
    });
  });

  // Filtered PR list
  filteredPrLogs = computed(() => {
    let list = this.state.prLogs();
    const sprintFilter = this.selectedSprintId();
    const devFilter = this.selectedDeveloperId();

    if (sprintFilter !== 'ALL') {
      list = list.filter(p => p.sprintId === sprintFilter);
    }
    if (devFilter !== 'ALL') {
      list = list.filter(p => p.authorId === devFilter);
    }
    return list;
  });

  // Aggregated Summary Stats
  totalPrs = computed(() => this.filteredPrLogs().length);
  totalComments = computed(() => this.filteredPrLogs().reduce((acc, p) => acc + p.totalCommentsCount, 0));
  totalActionableComments = computed(() => this.filteredPrLogs().reduce((acc, p) => acc + p.actionableCommentsCount, 0));
  overallActionabilityRate = computed(() => {
    const total = this.totalComments();
    if (total === 0) return 0;
    return Math.round((this.totalActionableComments() / total) * 100);
  });

  // Developer Scorecards (Only contributing engineers)
  developerMetrics = computed<DeveloperPrMetrics[]>(() => {
    const contributingDevs = this.developerMembers();
    const prs = this.selectedSprintId() === 'ALL'
      ? this.state.prLogs()
      : this.state.prLogs().filter(p => p.sprintId === this.selectedSprintId());

    return contributingDevs.map(dev => {
      const devPrs = prs.filter(p => p.authorId === dev.id);
      const devTotalPrs = devPrs.length;
      const devTotalComments = devPrs.reduce((acc, p) => acc + p.totalCommentsCount, 0);
      const devActionable = devPrs.reduce((acc, p) => acc + p.actionableCommentsCount, 0);
      const rate = devTotalComments > 0 ? Math.round((devActionable / devTotalComments) * 100) : 0;
      const avg = devTotalPrs > 0 ? Math.round((devTotalComments / devTotalPrs) * 10) / 10 : 0;
      const cleanDevName = cleanName(dev.name);

      return {
        developerId: dev.id,
        developerName: cleanDevName,
        developerRole: dev.role,
        developerAvatar: dev.avatar || getInitials(cleanDevName),
        totalPrsCreated: devTotalPrs,
        totalCommentsReceived: devTotalComments,
        actionableCommentsReceived: devActionable,
        actionabilityRatePercentage: rate,
        avgCommentsPerPr: avg,
        prs: devPrs
      };
    });
  });

  openLogPrModal(): void {
    const active = this.state.activeSprint();
    const devs = this.developerMembers();
    const defaultDev = devs[0] || this.state.members()[0];

    this.newPr = {
      workItemId: '',
      authorId: defaultDev ? defaultDev.id : '',
      reviewerId: '',
      sprintId: active ? active.id : '',
      prNumber: '',
      prTitle: '',
      prUrl: '',
      totalCommentsCount: 0,
      actionableCommentsCount: 0,
      reviewSummary: '',
      reviewStatus: 'Approved'
    };

    this.showLogPrModal.set(true);
  }

  onSavePrLog(): void {
    if (!this.newPr.prTitle.trim() || !this.newPr.authorId) return;

    this.state.createPullRequestLog({
      workItemId: this.newPr.workItemId || null,
      authorId: this.newPr.authorId,
      reviewerId: null,
      sprintId: this.newPr.sprintId || null,
      prNumber: this.newPr.prNumber.trim() || '#PR',
      prTitle: this.newPr.prTitle.trim(),
      prUrl: this.newPr.prUrl.trim(),
      totalCommentsCount: this.newPr.totalCommentsCount || 0,
      actionableCommentsCount: Math.min(this.newPr.actionableCommentsCount || 0, this.newPr.totalCommentsCount || 0),
      reviewSummary: this.newPr.reviewSummary.trim(),
      reviewStatus: this.newPr.reviewStatus
    });

    this.showLogPrModal.set(false);
  }

  onDeletePr(id: string): void {
    this.state.deletePullRequestLog(id);
  }

  getReviewStatusColor(status: string): string {
    switch (status?.toLowerCase()) {
      case 'merged': return 'var(--accent-success)';
      case 'approved': return 'var(--accent-primary)';
      case 'changesrequested': return 'var(--accent-warning)';
      default: return 'var(--text-secondary)';
    }
  }
}
