import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { PrMetricsComponent } from './pr-metrics.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';

describe('PrMetricsComponent', () => {
  let component: PrMetricsComponent;
  let fixture: ComponentFixture<PrMetricsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PrMetricsComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PrMetricsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the PR Metrics component', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize with ALL sprints filter and closed modal', () => {
    expect(component.selectedSprintId()).toBe('ALL');
    expect(component.showLogPrModal()).toBeFalse();
  });

  it('should compute initial KPI aggregates cleanly', () => {
    expect(component.totalPrs()).toBe(0);
    expect(component.totalComments()).toBe(0);
    expect(component.totalActionableComments()).toBe(0);
    expect(component.overallActionabilityRate()).toBe(0);
  });

  it('should toggle log PR modal', () => {
    component.openLogPrModal();
    expect(component.showLogPrModal()).toBeTrue();
    expect(component.newPr.prNumber).toContain('PR-');
  });
});
