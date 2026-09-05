import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TeamPerformanceComponent } from './team-performance.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { TeamPerformanceSummary } from '../../core/models/scrum.models';

describe('TeamPerformanceComponent', () => {
  let component: TeamPerformanceComponent;
  let fixture: ComponentFixture<TeamPerformanceComponent>;
  let stateService: ScrumStateService;

  const mockSummary: TeamPerformanceSummary = {
    teamName: 'Alpha Squad',
    performanceGrade: 'A',
    overallScore: 91,
    headline: 'Consistent high velocity and low blocker latency',
    sprintsAnalyzed: 3,
    evaluatedAtUtc: '2026-09-05T00:00:00Z',
    metrics: [
      {
        metricName: 'Say/Do Ratio',
        category: 'Delivery',
        currentValue: 95,
        previousValue: 90,
        deltaPercent: 5.5,
        trendDirection: 'Up',
        unit: '%',
        clientLabel: 'Predictability',
        icon: 'target'
      }
    ],
    sprintSnapshots: [
      {
        sprintId: 's1',
        sprintName: 'Sprint 1',
        startDate: '2026-08-01',
        endDate: '2026-08-14',
        deliveredPoints: 28,
        committedPoints: 30,
        sayDoPercent: 93.3,
        escapedDefects: 0,
        avgPrReviewHours: 3.5,
        blockersRaised: 1,
        blockersResolved: 1,
        teamMoodAvg: 4.5
      }
    ],
    highlights: [
      {
        category: 'Delivery',
        statement: 'Consistently hitting sprint target',
        sentiment: 'Positive',
        icon: 'award'
      }
    ],
    engagement: {
      avgMoodScore: 4.5,
      totalKudosGiven: 12,
      techTalksDelivered: 2,
      techDebtItemsResolved: 3,
      kudosPerSprint: 4,
      techTalksPerSprint: 0.7,
      engagementGrade: 'A'
    }
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamPerformanceComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers),
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TeamPerformanceComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
  });

  it('should create and load performance summary successfully', () => {
    spyOn(stateService, 'getTeamPerformanceSummary').and.returnValue(of(mockSummary));

    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.summary()).toEqual(mockSummary);
    expect(component.loading()).toBeFalse();
    expect(component.hasDataToAnalyze()).toBeTrue();
  });

  it('should handle API error gracefully', () => {
    spyOn(stateService, 'getTeamPerformanceSummary').and.returnValue(throwError(() => new Error('Server unavailable')));

    fixture.detectChanges();

    expect(component.error()).toContain('Server unavailable');
    expect(component.loading()).toBeFalse();
    expect(component.hasDataToAnalyze()).toBeFalse();
  });

  it('should resolve grade classes properly', () => {
    expect(component.getGradeClass('A+')).toBe('grade-aplus');
    expect(component.getGradeClass('A')).toBe('grade-a');
    expect(component.getGradeClass('B+')).toBe('grade-bplus');
    expect(component.getGradeClass('B')).toBe('grade-b');
    expect(component.getGradeClass('N/A')).toBe('grade-na');
    expect(component.getGradeClass('C')).toBe('grade-c');
  });

  it('should resolve trend icons and classes', () => {
    expect(component.getTrendIcon('Up')).toBe('trending-up');
    expect(component.getTrendIcon('Down')).toBe('trending-down');
    expect(component.getTrendIcon('Flat')).toBe('minus');

    expect(component.getTrendClass('Up')).toBe('trend-up');
    expect(component.getTrendClass('Down')).toBe('trend-down');
    expect(component.getTrendClass('Flat')).toBe('trend-stable');
  });

  it('should resolve metric category and sentiment classes', () => {
    expect(component.getMetricCategoryClass('Delivery')).toBe('cat-delivery');
    expect(component.getMetricCategoryClass('Quality')).toBe('cat-quality');
    expect(component.getHighlightSentimentClass('Positive')).toBe('sentiment-positive');
    expect(component.getHighlightSentimentClass('Neutral')).toBe('sentiment-neutral');
  });

  it('should calculate max delivered accurately', () => {
    component.summary.set(mockSummary);
    expect(component.getMaxDelivered()).toBe(30);

    component.summary.set(null);
    expect(component.getMaxDelivered()).toBe(1);
  });
});
