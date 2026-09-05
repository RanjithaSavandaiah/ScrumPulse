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
  let stateService: ScrumStateService;

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
    stateService = TestBed.inject(ScrumStateService);
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

  it('should open and initialize log PR modal correctly', () => {
    component.openLogPrModal();
    expect(component.showLogPrModal()).toBeTrue();
    expect(component.newPr.prNumber).toBe('');
    expect(component.newPr.reviewStatus).toBe('Approved');
  });

  it('should dispatch createPullRequestLog on valid save', () => {
    spyOn(stateService, 'createPullRequestLog');

    component.openLogPrModal();
    component.newPr.authorId = 'dev-1';
    component.newPr.prTitle = 'Feature: Add OAuth';
    component.newPr.prNumber = '#101';
    component.newPr.totalCommentsCount = 5;
    component.newPr.actionableCommentsCount = 2;

    component.onSavePrLog();

    expect(stateService.createPullRequestLog).toHaveBeenCalledWith(jasmine.objectContaining({
      authorId: 'dev-1',
      prTitle: 'Feature: Add OAuth',
      prNumber: '#101',
      totalCommentsCount: 5,
      actionableCommentsCount: 2
    }));
    expect(component.showLogPrModal()).toBeFalse();
  });

  it('should not dispatch createPullRequestLog if title or author is empty', () => {
    spyOn(stateService, 'createPullRequestLog');

    component.openLogPrModal();
    component.newPr.prTitle = '';
    component.newPr.authorId = '';

    component.onSavePrLog();
    expect(stateService.createPullRequestLog).not.toHaveBeenCalled();
  });
});
