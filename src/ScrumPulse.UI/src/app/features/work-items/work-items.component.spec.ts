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
});
