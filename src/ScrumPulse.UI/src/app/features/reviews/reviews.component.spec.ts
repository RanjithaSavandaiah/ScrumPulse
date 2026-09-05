import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { ReviewsComponent } from './reviews.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { MonthlyFeedback } from '../../core/models/scrum.models';

describe('ReviewsComponent', () => {
  let component: ReviewsComponent;
  let fixture: ComponentFixture<ReviewsComponent>;
  let stateService: ScrumStateService;

  const mockFeedback: MonthlyFeedback = {
    id: 'f1',
    teamMemberId: 'm1',
    teamMemberName: 'Alice Developer',
    monthYear: 'September 2026',
    scrumMasterFeedback: 'Great sprint participation and reliable estimates',
    cdlFeedback: 'Solid architectural contributions',
    clientFeedback: 'Responsive during demos',
    selfReflection: 'Want to focus more on test automation',
    smRating: 5,
    happinessIndex: 9,
    actionItems: 'Conduct workshop on Cypress',
    nextMonthGoals: 'Zero regressions',
    createdAtUtc: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewsComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ReviewsComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create the ReviewsComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should manage create and edit modal lifecycle', () => {
    expect(component.showFeedbackModal()).toBeFalse();
    expect(component.selectedFeedbackForEdit()).toBeNull();

    component.onOpenCreateModal();
    expect(component.showFeedbackModal()).toBeTrue();
    expect(component.selectedFeedbackForEdit()).toBeNull();

    component.onOpenEditModal(mockFeedback);
    expect(component.showFeedbackModal()).toBeTrue();
    expect(component.selectedFeedbackForEdit()).toBe(mockFeedback);

    component.onCloseFeedbackModal();
    expect(component.showFeedbackModal()).toBeFalse();
    expect(component.selectedFeedbackForEdit()).toBeNull();
  });

  it('should dispatch submitMonthlyFeedback when creating feedback', () => {
    spyOn(stateService, 'submitMonthlyFeedback');

    component.onOpenCreateModal();
    component.onSaveFeedback(mockFeedback);

    expect(stateService.submitMonthlyFeedback).toHaveBeenCalledWith(mockFeedback);
    expect(component.showFeedbackModal()).toBeFalse();
  });

  it('should dispatch updateMonthlyFeedback when editing feedback', () => {
    spyOn(stateService, 'updateMonthlyFeedback');

    component.onOpenEditModal(mockFeedback);
    const updated = { ...mockFeedback, smRating: 4 };
    component.onSaveFeedback(updated);

    expect(stateService.updateMonthlyFeedback).toHaveBeenCalledWith('f1', updated);
    expect(component.showFeedbackModal()).toBeFalse();
  });

  it('should handle deletion lifecycle', () => {
    spyOn(stateService, 'deleteMonthlyFeedback');

    component.onPromptDeleteFeedback(mockFeedback);
    expect(component.feedbackToDelete()).toBe(mockFeedback);

    component.onConfirmDeleteFeedback();
    expect(stateService.deleteMonthlyFeedback).toHaveBeenCalledWith('f1');
    expect(component.feedbackToDelete()).toBeNull();

    component.onDeleteFromModal('f1');
    expect(stateService.deleteMonthlyFeedback).toHaveBeenCalledWith('f1');
  });
});
