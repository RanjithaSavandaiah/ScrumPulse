import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { CapacityComponent } from './capacity.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { TeamLeave } from '../../core/models/scrum.models';

describe('CapacityComponent', () => {
  let component: CapacityComponent;
  let fixture: ComponentFixture<CapacityComponent>;
  let stateService: ScrumStateService;

  const mockLeave: TeamLeave = {
    id: 'l1',
    teamMemberId: 'm1',
    teamMemberName: 'Alice',
    startDate: '2026-09-10T00:00:00Z',
    endDate: '2026-09-12T00:00:00Z',
    totalDays: 3,
    isApproved: true,
    reason: 'Family event',
    leaveType: 'Privilege Leave',
    leaveSlot: 'FullDay',
    location: 'Bangalore Offshore'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CapacityComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CapacityComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create the capacity component', () => {
    expect(component).toBeTruthy();
    expect(component.sprintDailyHours).toBeGreaterThan(0);
  });

  it('should manage period selection and custom date initializations', () => {
    component.onPeriodChange('ALL');
    expect(component.selectedPeriod()).toBe('ALL');

    component.onPeriodChange('CUSTOM');
    expect(component.selectedPeriod()).toBe('CUSTOM');
    expect(component.customStartDate()).not.toBe('');
    expect(component.customEndDate()).not.toBe('');

    component.clearCustomDates();
    expect(component.customStartDate()).toBe('');
    expect(component.customEndDate()).toBe('');
  });

  it('should manage create and edit modal flow', () => {
    expect(component.showLeaveModal()).toBeFalse();
    component.openCreateLeave();
    expect(component.showLeaveModal()).toBeTrue();
    expect(component.selectedEditLeave()).toBeNull();

    spyOn(stateService, 'canEditOrDelete').and.returnValue(true);
    component.openEditLeave(mockLeave);
    expect(component.selectedEditLeave()).toBe(mockLeave);
  });

  it('should dispatch submitLeave when saving a new leave', () => {
    spyOn(stateService, 'submitLeave');

    component.openCreateLeave();
    component.onSaveLeave({
      teamMemberId: 'm1',
      startDate: '2026-09-10',
      endDate: '2026-09-12',
      leaveSlot: 'FullDay',
      reason: 'Annual Leave',
      leaveType: 'Privilege Leave'
    });

    expect(stateService.submitLeave).toHaveBeenCalledWith(jasmine.objectContaining({
      teamMemberId: 'm1',
      reason: 'Annual Leave',
      leaveType: 'Privilege Leave'
    }));
    expect(component.showLeaveModal()).toBeFalse();
  });

  it('should dispatch updateLeave when editing an existing leave', () => {
    spyOn(stateService, 'updateLeave');
    spyOn(stateService, 'canEditOrDelete').and.returnValue(true);

    component.openEditLeave(mockLeave);
    component.onSaveLeave({
      teamMemberId: 'm1',
      startDate: '2026-09-10',
      endDate: '2026-09-11',
      leaveSlot: 'FirstHalf',
      reason: 'Doctor appointment',
      leaveType: 'Casual Leave'
    });

    expect(stateService.updateLeave).toHaveBeenCalledWith('l1', jasmine.objectContaining({
      teamMemberId: 'm1',
      reason: 'Doctor appointment',
      leaveSlot: 'FirstHalf'
    }));
  });

  it('should manage leave deletion flow', () => {
    spyOn(stateService, 'deleteLeave');
    spyOn(stateService, 'canEditOrDelete').and.returnValue(true);

    component.leaveToDelete.set(mockLeave);
    component.onCancelDeleteLeave();
    expect(component.leaveToDelete()).toBeNull();

    component.leaveToDelete.set(mockLeave);
    component.onConfirmDeleteLeave();
    expect(stateService.deleteLeave).toHaveBeenCalledWith('l1');
    expect(component.leaveToDelete()).toBeNull();
  });
});
