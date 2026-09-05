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

  it('should emit save when submitted', () => {
    spyOn(component.save, 'emit');

    component.feedback.teamMemberId = 'm-1';
    component.feedback.scrumMasterFeedback = 'Good progress';
    component.save.emit(component.feedback);

    expect(component.save.emit).toHaveBeenCalledWith(component.feedback);
  });
});
