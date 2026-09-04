import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { Sprint, TeamMember, WorkItem } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { EstimationMatrixModalComponent } from '../estimation-matrix-modal/estimation-matrix-modal.component';

import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-add-work-item-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, EstimationMatrixModalComponent, ...CORE_PIPES],
  templateUrl: './add-work-item-modal.component.html',
  styleUrl: './add-work-item-modal.component.css'
})
export class AddWorkItemModalComponent implements OnInit {
  state = inject(ScrumStateService);

  @Input() editItem: WorkItem | null = null;
  @Input() members: TeamMember[] = [];
  @Input() sprints: Sprint[] = [];
  @Input() defaultSprintId: string = '';
  @Output() close = new EventEmitter<void>();
  @Output() delete = new EventEmitter<string>();
  @Output() save = new EventEmitter<{
    id?: string;
    title: string;
    description: string;
    acceptanceCriteria: string;
    type: number;
    priority: number;
    storyPoints: number;
    estimatedHours?: number | null;
    assigneeId: string;
    sprintId?: string;
  }>();

  item: {
    title: string;
    description: string;
    acceptanceCriteria: string;
    type: number;
    priority: number;
    storyPoints: number;
    estimatedHours: number | null;
    assigneeId: string;
    sprintId: string;
  } = {
    title: '',
    description: '',
    acceptanceCriteria: '',
    type: 0,
    priority: 1,
    storyPoints: 3,
    estimatedHours: 12,
    assigneeId: '',
    sprintId: ''
  };

  showMatrixModal: boolean = false;

  readonly fibonacciHints: Record<number, { range: string; desc: string }> = {
    0: { range: '< 1h', desc: 'Trivial copy or config change' },
    1: { range: '1–4h', desc: 'Minor fix or quick component tweak' },
    2: { range: '4–8h', desc: 'Standard single-day task' },
    3: { range: '8–16h', desc: 'Full user story with UI + API + Tests' },
    5: { range: '16–24h', desc: 'Multi-component feature / integration' },
    8: { range: '24–40h', desc: 'Major refactor / high complexity' },
    13: { range: '>40h', desc: 'Caution: Must decompose into smaller stories' }
  };

  ngOnInit(): void {
    if (this.editItem) {
      let desc = this.editItem.description || '';
      let ac = '';
      const dorMarker = '**Acceptance Criteria (DoR):**\n';
      const dorIndex = desc.indexOf(dorMarker);
      if (dorIndex !== -1) {
        ac = desc.substring(dorIndex + dorMarker.length).trim();
        desc = desc.substring(0, dorIndex).trim();
      }

      this.item = {
        title: this.editItem.title || '',
        description: desc,
        acceptanceCriteria: ac,
        type: this.mapTypeToNumber(this.editItem.type),
        priority: this.mapPriorityToNumber(this.editItem.priority),
        storyPoints: this.editItem.storyPoints ?? 3,
        estimatedHours: this.editItem.estimatedHours ?? null,
        assigneeId: this.editItem.assigneeId || '',
        sprintId: this.editItem.sprintId || ''
      };
    } else if (this.defaultSprintId) {
      this.item.sprintId = this.defaultSprintId;
    } else if (this.state.activeSprint()) {
      this.item.sprintId = this.state.activeSprint()!.id;
    }
  }

  selectStoryPoints(pts: number): void {
    this.item.storyPoints = pts;
    // Suggest default hours if empty
    if (this.item.estimatedHours === null || this.item.estimatedHours === 0) {
      const defaultHourMap: Record<number, number> = {
        0: 0.5,
        1: 2,
        2: 6,
        3: 12,
        5: 20,
        8: 32,
        13: 40
      };
      this.item.estimatedHours = defaultHourMap[pts] ?? (pts * 4);
    }
  }

  onApplyMatrixEstimation(event: { points: number; hours: number }): void {
    this.item.storyPoints = event.points;
    this.item.estimatedHours = event.hours;
    this.showMatrixModal = false;
  }

  mapTypeToNumber(type: any): number {
    if (typeof type === 'number') return type;
    switch (type) {
      case 'UserStory': return 0;
      case 'Bug': return 1;
      case 'TechTask': return 2;
      default: return 0;
    }
  }

  mapPriorityToNumber(priority: any): number {
    if (typeof priority === 'number') return priority;
    switch (priority) {
      case 'Critical': return 3;
      case 'High': return 2;
      case 'Medium': return 1;
      case 'Low': return 0;
      default: return 1;
    }
  }

  get availableSprints(): Sprint[] {
    return this.sprints.length > 0 ? this.sprints : this.state.sprints();
  }

  get teamMembers(): TeamMember[] {
    const list = this.members.length > 0 ? this.members : this.state.squadMembers();
    return list.filter(m => {
      const role = (m.role || '').toLowerCase();
      return role !== 'scrummaster' && role !== 'cdl' && role !== 'sm';
    });
  }

  itemTypes: { value: number; label: string; icon: IconName; desc: string }[] = [
    { value: 0, label: 'User Story', icon: 'book-open', desc: 'Deliver business value' },
    { value: 1, label: 'Bug Fix', icon: 'shield-alert', desc: 'Resolve defect in flow' },
    { value: 2, label: 'Tech Task', icon: 'wrench', desc: 'Refactor / Infra spike' }
  ];

  priorities = [
    { value: 3, label: 'Critical', color: 'var(--accent-danger)' },
    { value: 2, label: 'High', color: 'var(--accent-warning)' },
    { value: 1, label: 'Medium', color: 'var(--accent-secondary)' },
    { value: 0, label: 'Low', color: 'var(--text-secondary)' }
  ];

  pointOptions = [0, 1, 2, 3, 5, 8, 13];
  hourOptions = [0, 2, 4, 8, 16, 24, 40];
}
