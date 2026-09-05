import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { ExecutiveComponent } from './executive.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { ReportExportService } from '../../core/services/report-export.service';
import { appReducers } from '../../core/state';
import { SprintVelocityTrend, SprintHealth } from '../../core/models/scrum.models';

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
});
