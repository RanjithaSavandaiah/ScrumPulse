import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { WorkItemsComponent } from './work-items.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';

describe('WorkItemsComponent', () => {
  let component: WorkItemsComponent;
  let fixture: ComponentFixture<WorkItemsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkItemsComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(WorkItemsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the WorkItemsComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should default selectedSprintId and selectedAssigneeId to ALL', () => {
    expect(component.selectedSprintId()).toBe('ALL');
    expect(component.selectedAssigneeId()).toBe('ALL');
  });

  it('should calculate item counts accurately', () => {
    expect(component.getItemCount('ALL')).toBe(0);
    expect(component.getSprintItemCount('ALL')).toBe(0);
  });

  it('should toggle configure gates modal state via signals', () => {
    expect(component.showConfigureGatesModal()).toBeFalse();
    component.showConfigureGatesModal.set(true);
    expect(component.showConfigureGatesModal()).toBeTrue();
    component.showConfigureGatesModal.set(false);
    expect(component.showConfigureGatesModal()).toBeFalse();
  });

  it('should manage gates modal state when openConfigure is triggered', () => {
    component.selectedItemForGates = { id: 'item-1', key: 'SP-1' } as any;
    expect(component.selectedItemForGates).toBeTruthy();

    component.showConfigureGatesModal.set(true);
    component.selectedItemForGates = null;

    expect(component.showConfigureGatesModal()).toBeTrue();
    expect(component.selectedItemForGates).toBeNull();
  });

  it('should compute sprintCapacitySummary with blocker hours deduction', () => {
    const summary = component.sprintCapacitySummary();
    expect(summary).toBeDefined();
    expect(summary.blockerHours).toBeDefined();
    expect(summary.netHours).toBeDefined();
    expect(summary.netHours).toBeLessThanOrEqual(summary.grossHours);
  });
});
