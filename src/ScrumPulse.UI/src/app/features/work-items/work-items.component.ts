import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { NotificationService } from '../../core/services/notification.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { WorkItemCardComponent } from './components/work-item-card/work-item-card.component';
import { AddWorkItemModalComponent } from './components/add-work-item-modal/add-work-item-modal.component';
import { QualityGatesModalComponent } from './components/quality-gates-modal/quality-gates-modal.component';
import { SprintBurndownChartComponent } from './components/sprint-burndown-chart/sprint-burndown-chart.component';
import { EditSprintModalComponent } from './components/edit-sprint-modal/edit-sprint-modal.component';
import { EstimationMatrixModalComponent } from './components/estimation-matrix-modal/estimation-matrix-modal.component';
import { ConfigureGatesModalComponent } from './components/configure-gates-modal/configure-gates-modal.component';
import { Sprint, WorkItem } from '../../core/models/scrum.models';
import { calculateWorkingDays } from '../../core/utils/date-utils';
import { isDeliveryRole } from '../../core/utils/format-utils';
import { CORE_PIPES } from '../../core/pipes';
import { DEFAULT_DAILY_WORKING_HOURS } from '../../core/constants/scrum.constants';

const TWO_WEEKS_IN_DAYS = 14;
const HOURS_PER_DAY = 24;
const MINUTES_PER_HOUR = 60;
const SECONDS_PER_MINUTE = 60;
const MILLISECONDS_PER_SECOND = 1000;
const TWO_WEEKS_IN_MS = TWO_WEEKS_IN_DAYS * HOURS_PER_DAY * MINUTES_PER_HOUR * SECONDS_PER_MINUTE * MILLISECONDS_PER_SECOND;
const HALF_DAY_LEAVE_PORTION = 0.5;
const FULL_DAY_LEAVE_PORTION = 1.0;
const ONE_DECIMAL_PLACE_ROUNDING_FACTOR = 10;
const TWO_DECIMAL_PLACES_ROUNDING_FACTOR = 100;
const MAX_VELOCITY_RATIO_PERCENTAGE = 100;

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
    ConfigureGatesModalComponent,
    ...CORE_PIPES
  ],
  templateUrl: './work-items.component.html',
  styleUrl: './work-items.component.css'
})
export class WorkItemsComponent {
  state = inject(ScrumStateService);
  notification = inject(NotificationService);

  showNewItemModal = signal(false);
  showEditSprintModal = signal(false);
  showEstimationMatrixModal = signal<boolean>(false);
  showConfigureGatesModal = signal<boolean>(false);
  selectedSprintForEdit = signal<Sprint | null>(null);
  showBurndownChart = signal(true);

  selectedItemForEdit = signal<WorkItem | null>(null);
  selectedItemForGates: WorkItem | null = null;
  selectedAssigneeId = signal<string>('ALL');
  selectedSprintId = signal<string>('ALL');

  currentEffectiveSprint = computed<Sprint | null>(() => {
    const id = this.selectedSprintId();
    if (id && id !== 'ALL') {
      return this.state.sprints().find(sprint => sprint.id === id) || this.state.activeSprint() || null;
    }
    return this.state.activeSprint() || this.state.sprints()[0] || null;
  });

  sprintLeaves = computed(() => {
    const sp = this.currentEffectiveSprint();
    if (!sp) return this.state.leaves();
    const start = new Date(sp.startDate || Date.now());
    const end = new Date(sp.endDate || (Date.now() + TWO_WEEKS_IN_MS));
    return this.state.leaves().filter(leave => {
      const leaveStart = new Date(leave.startDate);
      const leaveEnd = new Date(leave.endDate);
      return leaveStart <= end && leaveEnd >= start;
    });
  });

  sprintCapacitySummary = computed(() => {
    const sp = this.currentEffectiveSprint();
    const allMembers = this.state.squadMembers().filter(member => (member.isActive ?? true));
    const devMembers = allMembers.filter(member => (member.role || '').toLowerCase() === 'developer');
    const deliveryMembers = allMembers.filter(member => isDeliveryRole(member.role));
    const targetDevs = devMembers.length > 0 ? devMembers : deliveryMembers;
    const memberCount = targetDevs.length;
    const leaves = this.sprintLeaves().filter(leave => leave.isApproved);

    let totalLeaveDays = 0;
    leaves.forEach(leave => {
      totalLeaveDays += leave.totalDays || (leave.leaveSlot && leave.leaveSlot !== 'FullDay' ? HALF_DAY_LEAVE_PORTION : FULL_DAY_LEAVE_PORTION);
    });

    const start = sp ? new Date(sp.startDate || Date.now()) : new Date();
    const end = sp ? new Date(sp.endDate || (Date.now() + TWO_WEEKS_IN_MS)) : new Date(Date.now() + TWO_WEEKS_IN_MS);
    const workingDays = calculateWorkingDays(start, end);
    const hoursPerDay = sp?.dailyWorkingHours && sp.dailyWorkingHours > 0 ? sp.dailyWorkingHours : DEFAULT_DAILY_WORKING_HOURS;
    const grossHours = Math.round(workingDays * memberCount * hoursPerDay * ONE_DECIMAL_PLACE_ROUNDING_FACTOR) / ONE_DECIMAL_PLACE_ROUNDING_FACTOR;
    const leaveHours = Math.round(totalLeaveDays * hoursPerDay * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR;

    const sprintBlockers = sp ? this.state.blockers().filter(b => b.sprintId === sp.id) : this.state.blockers();
    const blockerHours = Math.round(sprintBlockers.reduce((sum, b) => sum + (b.blockedHours || 0), 0) * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR;
    const netHours = Math.max(0, Math.round((grossHours - leaveHours - blockerHours) * TWO_DECIMAL_PLACES_ROUNDING_FACTOR) / TWO_DECIMAL_PLACES_ROUNDING_FACTOR);

    const sprintItems = sp ? this.state.workItems().filter(item => item.sprintId === sp.id) : this.state.workItems();
    const committed = sp?.committedStoryPoints || sprintItems.reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0) || 0;
    const delivered = sprintItems.filter(item => String(item.status).toLowerCase().includes('done')).reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);

    return {
      workingDays,
      mCount: memberCount,
      grossHours,
      totalLeaveDays,
      leaveHours,
      blockerHours,
      netHours,
      committed,
      delivered,
      velocityRatio: committed > 0 ? Math.min(MAX_VELOCITY_RATIO_PERCENTAGE, Math.round((delivered / committed) * TWO_DECIMAL_PLACES_ROUNDING_FACTOR)) : 0
    };
  });

  contributingMembers = computed(() => {
    return this.state.squadMembers().filter(member => isDeliveryRole(member.role));
  });

  filteredWorkItems = computed(() => {
    const assigneeFilter = this.selectedAssigneeId();
    const sprintFilter = this.selectedSprintId();
    let items = this.state.workItems();

    const current = this.state.currentTeam();
    if (current) {
      const squadMemberIds = new Set(this.state.squadMembers().map(member => member.id.toLowerCase().trim()));
      items = items.filter(item =>
        (item.teamId && item.teamId.toLowerCase().trim() === current.id.toLowerCase().trim()) ||
        (item.assigneeId && squadMemberIds.has(item.assigneeId.toLowerCase().trim())) ||
        (!item.assigneeId)
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
      const squadMemberIds = new Set(this.state.squadMembers().map(member => member.id.toLowerCase().trim()));
      items = items.filter(item =>
        (item.teamId && item.teamId.toLowerCase().trim() === current.id.toLowerCase().trim()) ||
        (item.assigneeId && squadMemberIds.has(item.assigneeId.toLowerCase().trim())) ||
        (!item.assigneeId)
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
    const isSm = this.state.canEditOrDelete();
    const isDev = this.state.currentRole() === 'Developer';
    const isStory = item.type === 'UserStory' || (item.type as any) === 0;
    if (!isSm && !(isDev && isStory)) {
      return;
    }
    this.selectedItemForEdit.set(item);
    this.showNewItemModal.set(true);
  }

  onCloseItemModal() {
    this.selectedItemForEdit.set(null);
    this.showNewItemModal.set(false);
  }

  onSaveItem(newItem: { title: string; description: string; acceptanceCriteria?: string; type: number; priority: number; storyPoints: number; estimatedHours?: number | null; assigneeId?: string; sprintId?: string }) {
    // Validate mandatory acceptance criteria for User Stories
    if (newItem.type === 0 && !newItem.acceptanceCriteria?.trim()) {
      this.notification.showError('Validation Error', 'Acceptance criteria is mandatory to add user story', 'alert-triangle');
      return;
    }

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
      this.notification.showSuccess('Work Item Updated', `"${newItem.title.trim()}" updated successfully.`);
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
      this.notification.showSuccess('Work Item Added', `"${newItem.title.trim()}" added successfully.`);
    }

    this.selectedItemForEdit.set(null);
    this.showNewItemModal.set(false);
  }

  onDeleteItem(id: string) {
    this.state.deleteWorkItem(id);
    this.notification.showSuccess('Work Item Deleted', 'The work item was removed from the sprint pipeline.', 'trash-2');
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
      this.notification.showSuccess('Sprint Updated', `Sprint "${sprintData.name || 'Sprint'}" updated successfully.`, 'calendar');
    } else {
      this.state.createSprint(sprintData);
      this.notification.showSuccess('Sprint Created', `Sprint "${sprintData.name || 'Sprint'}" added successfully.`, 'calendar');
    }
    this.closeSprintModal();
  }

  onDeleteSprint(id: string): void {
    this.state.deleteSprint(id);
    this.notification.showSuccess('Sprint Deleted', 'Sprint board was deleted.', 'trash-2');
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
      dodStagingVerified: updatedItem.dodStagingVerified,
      customCriteriaChecks: updatedItem.qualityGateResults
    });
    this.notification.showSuccess('Quality Gates Saved', `DoR / DoD criteria updated for "${updatedItem.title}".`, 'shield-check');
    this.selectedItemForGates = null;
  }
}
