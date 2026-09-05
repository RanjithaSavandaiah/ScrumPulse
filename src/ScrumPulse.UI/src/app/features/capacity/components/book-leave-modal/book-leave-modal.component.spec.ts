import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { BookLeaveModalComponent } from './book-leave-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('BookLeaveModalComponent', () => {
  let component: BookLeaveModalComponent;
  let fixture: ComponentFixture<BookLeaveModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BookLeaveModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(BookLeaveModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and calculate duration days for full day and half days', () => {
    expect(component).toBeTruthy();
    expect(component.isEditMode).toBeFalse();

    component.onSelectSlot('FirstHalf');
    expect(component.selectedDurationDays).toBe(0.5);

    component.leave.leaveSlot = 'FullDay';
    component.leave.startDate = '2026-09-01';
    component.leave.endDate = '2026-09-03';
    expect(component.selectedDurationDays).toBeGreaterThanOrEqual(1);
  });

  it('should detect invalid date ranges', () => {
    component.leave.startDate = '2026-09-10';
    component.leave.endDate = '2026-09-05';
    expect(component.isInvalidDateRange).toBeTrue();

    component.leave.endDate = '2026-09-12';
    expect(component.isInvalidDateRange).toBeFalse();
  });

  it('should format role labels correctly', () => {
    expect(component.getRoleLabel('ScrumMaster')).toBe('Scrum Master');
    expect(component.getRoleLabel('Developer')).toBe('Developer');
    expect(component.getRoleLabel('QaEngineer')).toBe('QA Engineer');
    expect(component.getRoleLabel('Cdl')).toBe('CDL');
    expect(component.getRoleLabel('ProductOwner')).toBe('Product Owner');
  });

  it('should emit save event on valid submission via onConfirmSubmit', () => {
    spyOn(component.save, 'emit');

    component.leave.teamMemberId = 'm-1';
    component.leave.startDate = '2026-09-10';
    component.leave.endDate = '2026-09-11';
    component.leave.reason = 'Conference';

    expect(component.canSubmit).toBeTrue();
    component.onConfirmSubmit();
    expect(component.save.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      teamMemberId: 'm-1',
      reason: 'Conference'
    }));
  });

  it('should emit delete event when editLeave exists', () => {
    spyOn(component.delete, 'emit');

    component.editLeave = {
      id: 'leave-99',
      teamMemberId: 'm-1',
      teamMemberName: 'John',
      startDate: '2026-09-10',
      endDate: '2026-09-11',
      leaveSlot: 'FullDay',
      reason: 'Sick',
      leaveType: 'Sick Leave',
      location: 'Bangalore',
      isApproved: true,
      totalDays: 2
    };

    component.onDelete();
    expect(component.delete.emit).toHaveBeenCalledWith('leave-99');
  });
});
