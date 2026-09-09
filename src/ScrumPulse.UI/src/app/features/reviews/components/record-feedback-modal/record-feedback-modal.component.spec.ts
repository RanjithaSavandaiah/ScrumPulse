import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { RecordFeedbackModalComponent } from './record-feedback-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('RecordFeedbackModalComponent', () => {
  let component: RecordFeedbackModalComponent;
  let fixture: ComponentFixture<RecordFeedbackModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecordFeedbackModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RecordFeedbackModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and format role labels', () => {
    expect(component).toBeTruthy();
    expect(component.getRoleLabel('Developer')).toBe('Developer');
    expect(component.getRoleLabel('ScrumMaster')).toBe('Scrum Master');
    expect(component.getRoleLabel('Cdl')).toBe('CDL');
  });

  it('should emit save when submitted with valid fields', () => {
    spyOn(component.save, 'emit');

    component.feedback.teamMemberId = 'm-1';
    component.feedback.monthYear = '2026-09';
    component.feedback.scrumMasterFeedback = 'Good progress';
    component.onSubmit();

    expect(component.save.emit).toHaveBeenCalledWith(component.feedback);
    expect(component.validationError()).toBeNull();
  });

  it('should validate mandatory fields on submit', () => {
    spyOn(component.save, 'emit');

    component.feedback.teamMemberId = '';
    component.onSubmit();
    expect(component.validationError()).toBe('Please select a team member');
    expect(component.save.emit).not.toHaveBeenCalled();

    component.feedback.teamMemberId = 'm-1';
    component.feedback.monthYear = '';
    component.onSubmit();
    expect(component.validationError()).toBe('Month/Year is mandatory');
    expect(component.save.emit).not.toHaveBeenCalled();

    component.feedback.monthYear = '2026-09';
    component.feedback.scrumMasterFeedback = '   ';
    component.onSubmit();
    expect(component.validationError()).toBe('Scrum Master feedback is mandatory');
    expect(component.save.emit).not.toHaveBeenCalled();
  });
});
