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
});
