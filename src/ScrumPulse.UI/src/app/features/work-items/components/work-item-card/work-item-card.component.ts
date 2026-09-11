import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { WorkItem } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';

@Component({
  selector: 'app-work-item-card',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './work-item-card.component.html',
  styleUrl: './work-item-card.component.css'
})
export class WorkItemCardComponent {
  state = inject(ScrumStateService);

  @Input({ required: true }) item!: WorkItem;
  @Output() advanceStage = new EventEmitter<{ item: WorkItem; targetStatus: string }>();
  @Output() openGates = new EventEmitter<WorkItem>();
  @Output() editItem = new EventEmitter<WorkItem>();

  canEditItem(item: WorkItem): boolean {
    if (this.state.canEditOrDelete()) return true;
    const isDev = this.state.currentRole() === 'Developer';
    const isUserStory = item.type === 'UserStory' || (item.type as any) === 0;
    return isDev && isUserStory;
  }

  getAssigneeName(item: WorkItem): string {
    if (!item.assigneeId) return item.assigneeName || 'Unassigned';
    const member = this.state.members().find(member => member.id === item.assigneeId);
    return member?.name || item.assigneeName || 'Unassigned';
  }

  isStatus(item: WorkItem, ...statuses: (string | number)[]): boolean {
    const currentStatus = String(item.status);
    return statuses.some(status => currentStatus === String(status));
  }

  getStatusLabel(status: any): string {
    const labels = ['Backlog', 'In Progress', 'PR Created', 'PR Approved', 'Merged to Master', 'In QA Testing', 'Done'];
    if (typeof status === 'number') return labels[status] || 'Backlog';
    return status || 'Backlog';
  }

  getTypeColor(workItemType: any): string {
    const colors = ['var(--accent-secondary)', 'var(--accent-danger)', 'var(--accent-purple)', 'var(--accent-warning)'];
    if (typeof workItemType === 'number') return colors[workItemType] || 'var(--text-secondary)';
    return 'var(--accent-secondary)';
  }

  getPriorityColor(priorityLevel: any): string {
    const colors = ['var(--text-muted)', 'var(--accent-secondary)', 'var(--accent-warning)', 'var(--accent-danger)'];
    if (typeof priorityLevel === 'number') return colors[priorityLevel] || 'var(--text-muted)';
    return 'var(--accent-warning)';
  }

  getStepLatencyText(item: WorkItem, stepNumber: number): string {
    const isDone = this.isStatus(item, 'Done', 6) || !!item.completedAtUtc;

    switch (stepNumber) {
      case 1: { // Picked Up
        if (item.pickedUpAtUtc) {
          const hours = item.pickupLatencyHours !== undefined && item.pickupLatencyHours !== null
            ? item.pickupLatencyHours
            : 0;
          return `${hours}h latency`;
        }
        return 'Pending';
      }
      case 2: { // PR Created
        if (item.prCreatedAtUtc) {
          const hours = item.devCycleTimeHours !== undefined && item.devCycleTimeHours !== null
            ? item.devCycleTimeHours
            : 0;
          return `${hours}h dev`;
        }
        if (this.isStatus(item, 'InProgress', 1)) {
          return 'Active';
        }
        return 'Pending';
      }
      case 3: { // PR Approved
        if (item.prApprovedAtUtc) {
          const hours = item.prReviewLatencyHours !== undefined && item.prReviewLatencyHours !== null
            ? item.prReviewLatencyHours
            : 0;
          return `${hours}h review`;
        }
        if (this.isStatus(item, 'PrCreated', 2)) {
          return 'Active';
        }
        return 'Pending';
      }
      case 4: { // Merged to Master
        if (item.prMergedAtUtc) {
          const hours = item.prMergeLatencyHours !== undefined && item.prMergeLatencyHours !== null
            ? item.prMergeLatencyHours
            : 0;
          return `${hours}h merge`;
        }
        if (this.isStatus(item, 'PrApproved', 3)) {
          return 'Active';
        }
        return 'Pending';
      }
      case 5: { // In QA Testing
        if (isDone) {
          const hours = item.qaTestingLatencyHours !== undefined && item.qaTestingLatencyHours !== null
            ? item.qaTestingLatencyHours
            : 0;
          return `${hours}h testing`;
        }
        if (item.qaStartedAtUtc || this.isStatus(item, 'InQa', 5, 'Merged', 4)) {
          return 'Active';
        }
        return 'Pending';
      }
      case 6: { // Marked Done
        if (isDone) {
          const hours = item.totalCycleTimeHours !== undefined && item.totalCycleTimeHours !== null
            ? item.totalCycleTimeHours
            : 0;
          return `${hours}h total`;
        }
        return 'Pending';
      }
      default:
        return 'Pending';
    }
  }
}

