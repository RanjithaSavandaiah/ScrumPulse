import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { DeveloperPrMetrics, PullRequestLog } from '../../core/models/scrum.models';

import { CORE_PIPES } from '../../core/pipes';
import { cleanName, getInitials, isDeliveryRole } from '../../core/utils/format-utils';

const PERCENTAGE_FACTOR = 100;
const DECIMAL_ROUNDING_FACTOR = 10;

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

  // Contributing squad developers / engineers (Developers and QA Engineers)
  developerMembers = computed(() => {
    return this.state.squadMembers().filter(member => isDeliveryRole(member.role));
  });

  // Filtered PR list
  filteredPrLogs = computed(() => {
    let list = this.state.prLogs();
    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(member => member.id.toLowerCase().trim()));
      list = list.filter(pr => pr.authorId && squadMemberIds.has(pr.authorId.toLowerCase().trim()));
    }

    const sprintFilter = this.selectedSprintId();
    const devFilter = this.selectedDeveloperId();

    if (sprintFilter !== 'ALL') {
      list = list.filter(pr => pr.sprintId === sprintFilter);
    }
    if (devFilter !== 'ALL') {
      list = list.filter(pr => pr.authorId === devFilter);
    }
    return list;
  });

  // Aggregated Summary Stats
  totalPrs = computed(() => this.filteredPrLogs().length);
  totalComments = computed(() => this.filteredPrLogs().reduce((totalCommentsCount, pr) => totalCommentsCount + pr.totalCommentsCount, 0));
  totalActionableComments = computed(() => this.filteredPrLogs().reduce((totalActionableCount, pr) => totalActionableCount + pr.actionableCommentsCount, 0));
  overallActionabilityRate = computed(() => {
    const total = this.totalComments();
    if (total === 0) return 0;
    return Math.round((this.totalActionableComments() / total) * PERCENTAGE_FACTOR);
  });

  // Developer Scorecards (Only contributing engineers)
  developerMetrics = computed<DeveloperPrMetrics[]>(() => {
    const contributingDevs = this.developerMembers();
    const prs = this.selectedSprintId() === 'ALL'
      ? this.state.prLogs()
      : this.state.prLogs().filter(pr => pr.sprintId === this.selectedSprintId());

    return contributingDevs.map(dev => {
      const devPrs = prs.filter(pr => pr.authorId === dev.id);
      const devTotalPrs = devPrs.length;
      const devTotalComments = devPrs.reduce((totalCommentsCount, pr) => totalCommentsCount + pr.totalCommentsCount, 0);
      const devActionable = devPrs.reduce((totalActionableCount, pr) => totalActionableCount + pr.actionableCommentsCount, 0);
      const rate = devTotalComments > 0 ? Math.round((devActionable / devTotalComments) * PERCENTAGE_FACTOR) : 0;
      const avg = devTotalPrs > 0 ? Math.round((devTotalComments / devTotalPrs) * DECIMAL_ROUNDING_FACTOR) / DECIMAL_ROUNDING_FACTOR : 0;
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
    const defaultDev = devs[0] || this.state.squadMembers()[0];

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
