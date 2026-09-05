import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { StandupComponent } from './standup.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { DailyStandup } from '../../core/models/scrum.models';

describe('StandupComponent', () => {
  let component: StandupComponent;
  let fixture: ComponentFixture<StandupComponent>;
  let stateService: ScrumStateService;

  const mockStandup: DailyStandup = {
    id: 'st-1',
    sprintId: 's1',
    teamMemberId: 'm1',
    teamMemberName: 'Alice',
    teamMemberAvatar: 'AL',
    yesterdaySummary: 'Finished authentication flow',
    todayPlan: 'Working on unit tests',
    blockersText: 'None',
    moodScore: 5,
    standupDate: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StandupComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(StandupComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should create and handle timer toggles using signals', () => {
    expect(component).toBeTruthy();
    expect(component.timerSeconds()).toBe(120);
    expect(component.timerRunning()).toBeFalse();

    component.toggleTimer();
    expect(component.timerRunning()).toBeTrue();

    component.toggleTimer();
    expect(component.timerRunning()).toBeFalse();

    component.resetTimer();
    expect(component.timerSeconds()).toBe(120);
  });

  it('should manage modal open for create and edit', () => {
    expect(component.showStandupModal()).toBeFalse();
    component.openCreateStandup();
    expect(component.showStandupModal()).toBeTrue();
    expect(component.selectedEditStandup()).toBeNull();

    component.openEditStandup(mockStandup);
    expect(component.showStandupModal()).toBeTrue();
    expect(component.selectedEditStandup()).toBe(mockStandup);
  });

  it('should dispatch submitStandup when creating a new standup entry', () => {
    spyOn(stateService, 'submitStandup');

    component.openCreateStandup();
    component.onSaveStandup({
      teamMemberId: 'm1',
      yesterdaySummary: 'PR reviewed',
      todayPlan: 'Merge and deploy',
      blockersText: '',
      moodScore: 4
    });

    expect(stateService.submitStandup).toHaveBeenCalledWith(jasmine.objectContaining({
      teamMemberId: 'm1',
      yesterdaySummary: 'PR reviewed'
    }));
    expect(component.showStandupModal()).toBeFalse();
  });

  it('should dispatch updateStandup when editing an existing standup', () => {
    spyOn(stateService, 'updateStandup');

    component.openEditStandup(mockStandup);
    component.onSaveStandup({
      teamMemberId: 'm1',
      yesterdaySummary: 'Updated PR reviewed',
      todayPlan: 'Merge and deploy',
      blockersText: '',
      moodScore: 4
    });

    expect(stateService.updateStandup).toHaveBeenCalledWith('st-1', jasmine.objectContaining({
      yesterdaySummary: 'Updated PR reviewed',
      sprintId: 's1'
    }));
  });

  it('should handle speaker selection and speaker advancement', () => {
    component.selectSpeaker(2);
    expect(component.currentSpeakerIndex()).toBe(2);
    expect(component.timerSeconds()).toBe(120);
  });

  it('should handle standup deletion', () => {
    spyOn(stateService, 'deleteStandup');

    component.onDeleteStandup('st-1');
    expect(stateService.deleteStandup).toHaveBeenCalledWith('st-1');
    expect(component.showStandupModal()).toBeFalse();
  });
});
