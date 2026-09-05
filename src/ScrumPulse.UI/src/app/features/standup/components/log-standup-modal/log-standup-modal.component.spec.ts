import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { LogStandupModalComponent } from './log-standup-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('LogStandupModalComponent', () => {
  let component: LogStandupModalComponent;
  let fixture: ComponentFixture<LogStandupModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LogStandupModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LogStandupModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and default mood score to 5', () => {
    expect(component).toBeTruthy();
    expect(component.standup.moodScore).toBe(5);
  });

  it('should apply presets for yesterday and today', () => {
    component.standup.yesterdaySummary = component.yesterdayPresets[0];
    expect(component.standup.yesterdaySummary).toBe(component.yesterdayPresets[0]);

    component.standup.todayPlan = component.todayPresets[0];
    expect(component.standup.todayPlan).toBe(component.todayPresets[0]);
  });

  it('should emit save when submitted', () => {
    spyOn(component.save, 'emit');

    component.standup.teamMemberId = 'm-1';
    component.standup.yesterdaySummary = 'Testing';
    component.standup.todayPlan = 'Deployment';

    component.save.emit(component.standup);
    expect(component.save.emit).toHaveBeenCalledWith(component.standup);
  });
});
