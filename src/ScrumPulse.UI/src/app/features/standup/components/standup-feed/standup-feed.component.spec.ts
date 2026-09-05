import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { StandupFeedComponent } from './standup-feed.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';
import { DailyStandup } from '../../../../core/models/scrum.models';

describe('StandupFeedComponent', () => {
  let component: StandupFeedComponent;
  let fixture: ComponentFixture<StandupFeedComponent>;
  let stateService: ScrumStateService;

  const mockStandup: DailyStandup = {
    id: 'std-1',
    teamMemberId: 'm-1',
    teamMemberName: 'Alice',
    teamMemberAvatar: '',
    standupDate: '2026-09-05',
    sprintId: 'sp-1',
    yesterdaySummary: 'Built auth interceptor',
    todayPlan: 'Add unit tests',
    blockersText: 'None',
    moodScore: 5
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StandupFeedComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    stateService = TestBed.inject(ScrumStateService);
    fixture = TestBed.createComponent(StandupFeedComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and calculate pagination safely', () => {
    expect(component).toBeTruthy();
    expect(component.totalPages()).toBeGreaterThanOrEqual(1);
    expect(component.currentPage()).toBe(1);

    component.setPage(1);
    expect(component.currentPage()).toBe(1);
  });

  it('should handle edit standup emit', () => {
    spyOn(component.editStandup, 'emit');
    component.onEditStandup(mockStandup);
    expect(component.editStandup.emit).toHaveBeenCalledWith(mockStandup);
  });

  it('should manage delete confirmation modal signals', () => {
    spyOn(stateService, 'deleteStandup');

    component.onDeleteStandup(mockStandup);
    expect(component.standupToDelete()).toEqual(mockStandup);

    component.onCancelDelete();
    expect(component.standupToDelete()).toBeNull();

    component.onDeleteStandup(mockStandup);
    component.onConfirmDelete();
    expect(stateService.deleteStandup).toHaveBeenCalledWith('std-1');
    expect(component.standupToDelete()).toBeNull();
  });
});
