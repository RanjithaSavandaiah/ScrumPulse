import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { ExecutiveComponent } from './executive.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { ReportExportService } from '../../core/services/report-export.service';
import { appReducers } from '../../core/state';
import { SprintVelocityTrend, SprintHealth, ExecutiveReport } from '../../core/models/scrum.models';

describe('ExecutiveComponent', () => {
  let component: ExecutiveComponent;
  let fixture: ComponentFixture<ExecutiveComponent>;
  let stateService: ScrumStateService;

  const mockVelocity: SprintVelocityTrend = {
    sprints: [
      { sprintId: 's1', sprintName: 'Sprint 31', startDate: '2026-08-01', endDate: '2026-08-14', committedPoints: 40, deliveredPoints: 38, sayDoPercentage: 95, rollingAverageVelocity: 38 },
      { sprintId: 's2', sprintName: 'Sprint 32', startDate: '2026-08-15', endDate: '2026-08-28', committedPoints: 42, deliveredPoints: 40, sayDoPercentage: 95.2, rollingAverageVelocity: 39 }
    ],
    averageVelocity: 39,
    predictabilityScore: 95
  };

  const mockHealth: SprintHealth = {
    sprintId: 's1',
    sprintName: 'Sprint 33',
    overallScore: 92,
    healthGrade: 'A',
    statusSummary: 'Sprint on track for timely delivery',
    factors: [],
    evaluatedAtUtc: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExecutiveComponent],
      providers: [
        ScrumStateService,
        ReportExportService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ExecutiveComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);

    spyOn(stateService, 'getVelocityTrend').and.returnValue(of(mockVelocity));
    spyOn(stateService, 'getSprintHealth').and.returnValue(of(mockHealth));
    spyOn(stateService, 'getCompanyAi').and.returnValue(of(null as any));

    fixture.detectChanges();
  });

  it('should create successfully and load metrics', () => {
    expect(component).toBeTruthy();
    expect(component.selectedTimeScope()).toBe('SPRINT');
    expect(component.velocityTrend()).toEqual(mockVelocity);
  });

  it('should clean names properly', () => {
    expect(component.cleanName('Alice Cooper (CDL)')).toBe('Alice Cooper');
    expect(component.cleanName('Bob')).toBe('Bob');
    expect(component.cleanName('')).toBe('');
  });

  it('should format role labels properly', () => {
    expect(component.getRoleLabel('ScrumMaster')).toBe('Scrum Master');
    expect(component.getRoleLabel('Developer')).toBe('Developer');
    expect(component.getRoleLabel('QaEngineer')).toBe('QA Engineer');
    expect(component.getRoleLabel('Cdl')).toBe('CDL');
    expect(component.getRoleLabel('ProductOwner')).toBe('Product Owner');
  });

  it('should apply preset ranges cleanly', () => {
    component.applyCustomPreset(7);
    expect(component.startDate()).toBeDefined();
    expect(component.endDate()).toBeDefined();

    component.applyThisMonthPreset();
    expect(component.startDate()).toBeDefined();
    expect(component.endDate()).toBeDefined();
  });

  it('should render blocker drag panel and impact statement when executive report has blocker hours', () => {
    const reportSignal = signal<ExecutiveReport | null>({
      sprintId: 's1',
      sprintName: 'Sprint 33',
      sprintGoal: 'Deliver features',
      sayDoRatioPercentage: 90,
      committedPoints: 30,
      deliveredPoints: 27,
      inFlightPoints: 3,
      avgPickupLatencyHours: 2,
      avgDevTimeHours: 10,
      avgPrReviewHours: 4,
      avgPrMergeHours: 1,
      avgQaTestingHours: 5,
      avgTotalCycleTimeHours: 22,
      activeBlockersCount: 1,
      avgBlockerResolutionHours: 3,
      escapedDefectsCount: 0,
      inSprintBugsCount: 1,
      totalBlockedHours: 16,
      lostStoryPointsCapacity: 2,
      blockerCapacityImpactSummary: 'Because of blockers for 16 hours, our capacity went down by 16h',
      executiveSummaryMarkdown: 'Executive summary'
    });

    Object.defineProperty(stateService, 'executiveReport', { value: reportSignal, writable: true });
    fixture.detectChanges();

    const panel = fixture.nativeElement.querySelector('.blocker-drag-panel');
    expect(panel).toBeTruthy();
    expect(panel.textContent).toContain('16h Blocked');
    expect(panel.textContent).toContain('Because of blockers for 16 hours, our capacity went down by 16h');

    // Test zero blocker hours state
    reportSignal.set({
      ...reportSignal()!,
      totalBlockedHours: 0,
      lostStoryPointsCapacity: 0
    });
    fixture.detectChanges();

    expect(panel.textContent).toContain('No blocker hours recorded this sprint');
  });
});

