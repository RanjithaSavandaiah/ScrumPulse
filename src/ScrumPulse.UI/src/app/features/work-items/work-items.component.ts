import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { WorkItemCardComponent } from './components/work-item-card/work-item-card.component';
import { AddWorkItemModalComponent } from './components/add-work-item-modal/add-work-item-modal.component';
import { QualityGatesModalComponent } from './components/quality-gates-modal/quality-gates-modal.component';
import { SprintBurndownChartComponent } from './components/sprint-burndown-chart/sprint-burndown-chart.component';
import { EditSprintModalComponent } from './components/edit-sprint-modal/edit-sprint-modal.component';
import { EstimationMatrixModalComponent } from './components/estimation-matrix-modal/estimation-matrix-modal.component';
import { Sprint, WorkItem } from '../../core/models/scrum.models';
import { calculateWorkingDays } from '../../core/utils/date-utils';
import { isDeliveryRole } from '../../core/utils/format-utils';
import { CORE_PIPES } from '../../core/pipes';
import { DEFAULT_DAILY_WORKING_HOURS } from '../../core/constants/scrum.constants';

@Component({
  selector: 'app-work-items',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    IconComponent,
    WorkItemCardComponent,
    AddWorkItemModalComponent,
    QualityGatesModalComponent,
    SprintBurndownChartComponent,
    EditSprintModalComponent,
    EstimationMatrixModalComponent,
    ...CORE_PIPES
  ],
  templateUrl: './work-items.component.html',
  styleUrl: './work-items.component.css'
})
export class WorkItemsComponent {
  state = inject(ScrumStateService);

  showNewItemModal = signal(false);
  showEditSprintModal = signal(false);
  showEstimationMatrixModal = signal(false);
  selectedSprintForEdit = signal<Sprint | null>(null);
  showBurndownChart = signal(true);

  selectedItemForEdit = signal<WorkItem | null>(null);
  selectedItemForGates: WorkItem | null = null;
  selectedAssigneeId = signal<string>('ALL');
  selectedSprintId = signal<string>('ALL');

  currentEffectiveSprint = computed<Sprint | null>(() => {
    const id = this.selectedSprintId();
    if (id && id !== 'ALL') {
      return this.state.sprints().find(s => s.id === id) || this.state.activeSprint() || null;
    }
    return this.state.activeSprint() || this.state.sprints()[0] || null;
  });

  sprintLeaves = computed(() => {
    const sp = this.currentEffectiveSprint();
    if (!sp) return this.state.leaves();
    const start = new Date(sp.startDate || Date.now());
    const end = new Date(sp.endDate || (Date.now() + 14 * 24 * 60 * 60 * 1000));
    return this.state.leaves().filter(l => {
      const ls = new Date(l.startDate);
      const le = new Date(l.endDate);
      return ls <= end && le >= start;
    });
  });

  sprintCapacitySummary = computed(() => {
    const sp = this.currentEffectiveSprint();
    const allMembers = this.state.squadMembers().filter(m => (m.isActive ?? true));
    const devMembers = allMembers.filter(m => (m.role || '').toLowerCase() === 'developer');
    const deliveryMembers = allMembers.filter(m => isDeliveryRole(m.role));
    const targetDevs = devMembers.length > 0 ? devMembers : deliveryMembers;
    const mCount = targetDevs.length;
    const leaves = this.sprintLeaves().filter(l => l.isApproved);

    let totalLeaveDays = 0;
    leaves.forEach(l => {
      totalLeaveDays += l.totalDays || (l.leaveSlot && l.leaveSlot !== 'FullDay' ? 0.5 : 1.0);
    });

    const start = sp ? new Date(sp.startDate || Date.now()) : new Date();
    const end = sp ? new Date(sp.endDate || (Date.now() + 14 * 24 * 60 * 60 * 1000)) : new Date(Date.now() + 14 * 24 * 60 * 60 * 1000);
    const workingDays = calculateWorkingDays(start, end);
    const hoursPerDay = sp?.dailyWorkingHours && sp.dailyWorkingHours > 0 ? sp.dailyWorkingHours : DEFAULT_DAILY_WORKING_HOURS;
    const grossHours = Math.round(workingDays * mCount * hoursPerDay * 10) / 10;
    const leaveHours = Math.round(totalLeaveDays * hoursPerDay * 100) / 100;
    const netHours = Math.max(0, Math.round((grossHours - leaveHours) * 100) / 100);

    const sprintItems = sp ? this.state.workItems().filter(w => w.sprintId === sp.id) : this.state.workItems();
    const committed = sp?.committedStoryPoints || sprintItems.reduce((acc, w) => acc + (w.storyPoints || 0), 0) || 0;
    const delivered = sprintItems.filter(w => String(w.status).toLowerCase().includes('done')).reduce((acc, w) => acc + (w.storyPoints || 0), 0);

    return {
      workingDays,
      mCount,
      grossHours,
      totalLeaveDays,
      leaveHours,
      netHours,
      committed,
      delivered,
      velocityRatio: committed > 0 ? Math.min(100, Math.round((delivered / committed) * 100)) : 0
    };
  });

  contributingMembers = computed(() => {
    return this.state.squadMembers().filter(m => isDeliveryRole(m.role));
  });

  filteredWorkItems = computed(() => {
    const assigneeFilter = this.selectedAssigneeId();
    const sprintFilter = this.selectedSprintId();
    let items = this.state.workItems();

    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
      items = items.filter(item =>
        (item.teamId && item.teamId.toLowerCase().trim() === current.id.toLowerCase().trim()) ||
        (item.assigneeId && squadMemberIds.has(item.assigneeId.toLowerCase().trim()))
      );
    }

    // Sprint Filter
    if (sprintFilter !== 'ALL') {
      items = items.filter(item => item.sprintId === sprintFilter);
    }

    // Assignee Filter
    if (assigneeFilter === 'ALL') {
      return items;
    }
    if (assigneeFilter === 'UNASSIGNED') {
      return items.filter(item => !item.assigneeId && !item.assigneeName);
    }
    return items.filter(item => item.assigneeId === assigneeFilter || item.assigneeName?.toLowerCase().includes(assigneeFilter.toLowerCase()));
  });

  getItemCount(memberId: string): number {
    const sprintFilter = this.selectedSprintId();
    let items = this.state.workItems();

    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
      items = items.filter(item =>
        (item.teamId && item.teamId.toLowerCase().trim() === current.id.toLowerCase().trim()) ||
        (item.assigneeId && squadMemberIds.has(item.assigneeId.toLowerCase().trim()))
      );
    }

    if (sprintFilter !== 'ALL') {
      items = items.filter(item => item.sprintId === sprintFilter);
    }

    if (memberId === 'ALL') return items.length;
    if (memberId === 'UNASSIGNED') return items.filter(item => !item.assigneeId && !item.assigneeName).length;
    return items.filter(item => item.assigneeId === memberId).length;
  }

  getSprintItemCount(sprintId: string): number {
    if (sprintId === 'ALL') return this.state.workItems().length;
    return this.state.workItems().filter(item => item.sprintId === sprintId).length;
  }

  onAdvanceStage(event: { item: WorkItem; targetStatus: string }) {
    this.state.advanceStage(event.item.id, event.targetStatus);
  }

  onOpenCreateModal() {
    this.selectedItemForEdit.set(null);
    this.showNewItemModal.set(true);
  }

  onEditItem(item: WorkItem) {
    this.selectedItemForEdit.set(item);
    this.showNewItemModal.set(true);
  }

  onCloseItemModal() {
    this.selectedItemForEdit.set(null);
    this.showNewItemModal.set(false);
  }

  onSaveItem(newItem: { title: string; description: string; acceptanceCriteria?: string; type: number; priority: number; storyPoints: number; estimatedHours?: number | null; assigneeId?: string; sprintId?: string }) {
    const combinedDesc = newItem.acceptanceCriteria?.trim()
      ? `${newItem.description}\n\n**Acceptance Criteria (DoR):**\n${newItem.acceptanceCriteria}`
      : newItem.description;

    const targetSprint = newItem.sprintId || (this.selectedSprintId() !== 'ALL' ? this.selectedSprintId() : this.state.activeSprint()?.id);
    const parsedHours = (newItem.estimatedHours !== undefined && newItem.estimatedHours !== null && newItem.estimatedHours !== ('' as any))
      ? Number(newItem.estimatedHours)
      : null;

    const editItem = this.selectedItemForEdit();
    if (editItem) {
      this.state.updateWorkItem(editItem.id, {
        title: newItem.title.trim(),
        description: combinedDesc,
        type: newItem.type,
        priority: newItem.priority,
        storyPoints: newItem.storyPoints ?? 0,
        estimatedHours: parsedHours,
        sprintId: targetSprint || null,
        assigneeId: newItem.assigneeId || null
      });
    } else {
      this.state.createWorkItem({
        title: newItem.title.trim(),
        description: combinedDesc,
        type: newItem.type,
        priority: newItem.priority,
        storyPoints: newItem.storyPoints ?? 0,
        estimatedHours: parsedHours,
        sprintId: targetSprint || null,
        assigneeId: newItem.assigneeId || null
      });
    }

    this.selectedItemForEdit.set(null);
    this.showNewItemModal.set(false);
  }

  onDeleteItem(id: string) {
    this.state.deleteWorkItem(id);
    this.selectedItemForEdit.set(null);
    this.showNewItemModal.set(false);
  }

  openCreateSprintModal(): void {
    this.selectedSprintForEdit.set(null);
    this.showEditSprintModal.set(true);
  }

  openEditSprintGoal(sprint?: Sprint | null): void {
    const target = sprint || this.currentEffectiveSprint();
    this.selectedSprintForEdit.set(target);
    this.showEditSprintModal.set(true);
  }

  closeSprintModal(): void {
    this.showEditSprintModal.set(false);
    this.selectedSprintForEdit.set(null);
  }

  onSaveSprint(sprintData: Partial<Sprint>): void {
    if (sprintData.id) {
      this.state.updateSprint(sprintData.id, sprintData);
    } else {
      this.state.createSprint(sprintData);
    }
    this.closeSprintModal();
  }

  onDeleteSprint(id: string): void {
    this.state.deleteSprint(id);
    if (this.selectedSprintId() === id) {
      this.selectedSprintId.set('ALL');
    }
    this.closeSprintModal();
  }

  onSaveQualityGates(updatedItem: WorkItem) {
    this.state.updateQualityGates(updatedItem.id, {
      dorAcceptanceCriteria: updatedItem.dorAcceptanceCriteriaDefined,
      dorDependencies: updatedItem.dorDependenciesIdentified,
      dorWireframe: updatedItem.dorWireframeAvailable,
      dodUnitTests: updatedItem.dodUnitTestsPassed,
      dodPeerReview: updatedItem.dodPeerReviewCompleted,
      dodMergedToMaster: updatedItem.dodMergedToMaster,
      dodStagingVerified: updatedItem.dodStagingVerified
    });
    this.selectedItemForGates = null;
  }
}
