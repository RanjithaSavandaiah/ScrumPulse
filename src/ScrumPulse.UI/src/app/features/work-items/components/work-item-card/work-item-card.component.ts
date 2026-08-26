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

  getAssigneeName(item: WorkItem): string {
    if (!item.assigneeId) return item.assigneeName || 'Unassigned';
    const member = this.state.members().find(m => m.id === item.assigneeId);
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
}
