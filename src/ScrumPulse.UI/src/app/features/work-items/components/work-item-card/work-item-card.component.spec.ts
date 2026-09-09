import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { WorkItemCardComponent } from './work-item-card.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';
import { WorkItem } from '../../../../core/models/scrum.models';

describe('WorkItemCardComponent', () => {
  let component: WorkItemCardComponent;
  let fixture: ComponentFixture<WorkItemCardComponent>;

  const mockItem: WorkItem = {
    id: 'wi-1',
    key: 'SP-101',
    title: 'Implement Dark Mode',
    description: 'Provide high contrast theme',
    type: 'UserStory',
    status: 'InProgress',
    priority: 'High',
    storyPoints: 5,
    estimatedHours: 20,
    assigneeId: 'm-1',
    assigneeName: 'Alice',
    sprintId: 'sp-1',
    isEscapedDefect: false,
    createdAtUtc: '2026-09-01T00:00:00Z',
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: true,
    dodUnitTestsPassed: true,
    dodPeerReviewCompleted: true,
    dodMergedToMaster: false,
    dodStagingVerified: false
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemCardComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkItemCardComponent);
    component = fixture.componentInstance;
    component.item = { ...mockItem };
    fixture.detectChanges();
  });

  it('should create and render work item details', () => {
    expect(component).toBeTruthy();
    expect(component.isStatus(component.item, 'InProgress')).toBeTrue();
    expect(component.isStatus(component.item, 'Done', 'Backlog')).toBeFalse();
  });

  it('should resolve assignee name correctly', () => {
    expect(component.getAssigneeName(component.item)).toBe('Alice');

    const unassignedItem = { ...mockItem, assigneeId: '', assigneeName: '' };
    expect(component.getAssigneeName(unassignedItem)).toBe('Unassigned');
  });

  it('should return correct status, type, and priority colors and labels', () => {
    expect(component.getStatusLabel(0)).toBe('Backlog');
    expect(component.getStatusLabel(1)).toBe('In Progress');
    expect(component.getStatusLabel('Done')).toBe('Done');

    expect(component.getTypeColor(0)).toBe('var(--accent-secondary)');
    expect(component.getTypeColor(1)).toBe('var(--accent-danger)');

    expect(component.getPriorityColor(0)).toBe('var(--text-muted)');
    expect(component.getPriorityColor(3)).toBe('var(--accent-danger)');
  });

  it('should emit events on advanceStage, openGates, and editItem', () => {
    spyOn(component.advanceStage, 'emit');
    spyOn(component.openGates, 'emit');
    spyOn(component.editItem, 'emit');

    component.advanceStage.emit({ item: component.item, targetStatus: 'Done' });
    expect(component.advanceStage.emit).toHaveBeenCalledWith({ item: component.item, targetStatus: 'Done' });

    component.openGates.emit(component.item);
    expect(component.openGates.emit).toHaveBeenCalledWith(component.item);

    component.editItem.emit(component.item);
    expect(component.editItem.emit).toHaveBeenCalledWith(component.item);
  });

  it('should evaluate canEditItem correctly for Scrum Master and Developer roles', () => {
    // 1. Scrum Master can edit any item
    const canEditSpy = spyOn(component.state, 'canEditOrDelete').and.returnValue(true);
    expect(component.canEditItem(component.item)).toBeTrue();

    const bugItem: WorkItem = { ...mockItem, type: 'Bug' };
    expect(component.canEditItem(bugItem)).toBeTrue();

    // 2. Developer role
    canEditSpy.and.returnValue(false);
    spyOn(component.state, 'currentRole').and.returnValue('Developer');

    // Developer CAN edit UserStory
    const storyItem: WorkItem = { ...mockItem, type: 'UserStory' };
    expect(component.canEditItem(storyItem)).toBeTrue();

    // Developer CANNOT edit Bug or TaskPbi
    expect(component.canEditItem(bugItem)).toBeFalse();

    const taskItem: WorkItem = { ...mockItem, type: 'TaskPbi' };
    expect(component.canEditItem(taskItem)).toBeFalse();
  });

  it('should render date timestamps below status in each stage step', () => {
    fixture.componentRef.setInput('item', {
      ...mockItem,
      pickedUpAtUtc: '2026-09-01T10:30:00Z',
      prCreatedAtUtc: '2026-09-02T14:15:00Z',
      prApprovedAtUtc: '2026-09-03T16:00:00Z',
      prMergedAtUtc: '2026-09-03T18:45:00Z',
      qaStartedAtUtc: '2026-09-04T09:00:00Z',
      completedAtUtc: '2026-09-05T11:20:00Z'
    });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const pickedUpTimestamp = compiled.querySelector('[data-testid="stage-timestamp-picked-up"]');
    const prCreatedTimestamp = compiled.querySelector('[data-testid="stage-timestamp-pr-created"]');
    const prApprovedTimestamp = compiled.querySelector('[data-testid="stage-timestamp-pr-approved"]');
    const prMergedTimestamp = compiled.querySelector('[data-testid="stage-timestamp-pr-merged"]');
    const qaStartedTimestamp = compiled.querySelector('[data-testid="stage-timestamp-qa-started"]');
    const completedTimestamp = compiled.querySelector('[data-testid="stage-timestamp-completed"]');

    expect(pickedUpTimestamp).toBeTruthy();
    expect(pickedUpTimestamp?.textContent).toContain('Sep 1, 2026');
    expect(prCreatedTimestamp?.textContent).toContain('Sep 2, 2026');
    expect(prApprovedTimestamp?.textContent).toContain('Sep 3, 2026');
    expect(prMergedTimestamp?.textContent).toContain('Sep 3, 2026');
    expect(qaStartedTimestamp?.textContent).toContain('Sep 4, 2026');
    expect(completedTimestamp?.textContent).toContain('Sep 5, 2026');
  });

  it('should render dash for pending stage step timestamps', () => {
    fixture.componentRef.setInput('item', {
      ...mockItem,
      pickedUpAtUtc: undefined,
      prCreatedAtUtc: undefined,
      prApprovedAtUtc: undefined,
      prMergedAtUtc: undefined,
      qaStartedAtUtc: undefined,
      completedAtUtc: undefined
    });
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const pickedUpTimestamp = compiled.querySelector('[data-testid="stage-timestamp-picked-up"]');
    const completedTimestamp = compiled.querySelector('[data-testid="stage-timestamp-completed"]');
    expect(pickedUpTimestamp?.textContent?.trim()).toBe('—');
    expect(completedTimestamp?.textContent?.trim()).toBe('—');
  });
});
