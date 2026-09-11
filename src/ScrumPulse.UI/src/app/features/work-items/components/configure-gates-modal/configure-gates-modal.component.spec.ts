import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { appReducers } from '../../../../core/state';
import { ConfigureGatesModalComponent } from './configure-gates-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { Team } from '../../../../core/models/scrum.models';

describe('ConfigureGatesModalComponent', () => {
  let component: ConfigureGatesModalComponent;
  let fixture: ComponentFixture<ConfigureGatesModalComponent>;

  const mockTeam: Team = {
    id: 'team-alpha',
    name: 'Alpha Squad',
    slug: 'alpha-squad',
    description: 'Engineering squad',
    joinCode: 'ALPHA1',
    isActive: true,
    createdAtUtc: '2026-01-01T00:00:00Z',
    dorCriteria: [
      { id: 'dor-1', label: 'AC verified', isRequired: true }
    ],
    dodCriteria: [
      { id: 'dod-1', label: 'Unit tests passed', isRequired: true }
    ]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfigureGatesModalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers),
        ScrumStateService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ConfigureGatesModalComponent);
    component = fixture.componentInstance;
    component.team = mockTeam;
    fixture.detectChanges();
  });

  it('should create and initialize with team criteria', () => {
    expect(component).toBeTruthy();
    expect(component.dorList().length).toBe(1);
    expect(component.dodList().length).toBe(1);
    expect(component.dorList()[0].label).toBe('AC verified');
  });

  it('should add a new criterion to active tab', () => {
    component.activeGateTab.set('dor');
    component.newCriterionLabel.set('API documentation verified');
    component.addCriterion();

    expect(component.dorList().length).toBe(2);
    expect(component.dorList()[1].label).toBe('API documentation verified');
    expect(component.newCriterionLabel()).toBe('');
  });

  it('should prevent removing the last criterion', () => {
    component.removeCriterion('dor', 0);
    expect(component.dorList().length).toBe(1);
    expect(component.errorMessage()).toContain('At least one');
  });

  it('should toggle required flag on criterion', () => {
    const item = component.dorList()[0];
    expect(item.isRequired).toBeTrue();
    component.toggleRequired(item);
    expect(item.isRequired).toBeFalse();
  });

  it('should reject adding criterion with empty title', () => {
    component.newCriterionLabel.set('   ');
    component.addCriterion();
    expect(component.errorMessage()).toContain('Please provide a criteria description');
    expect(component.dorList().length).toBe(1);
  });

  it('should reorder criteria with moveUp and moveDown', () => {
    component.newCriterionLabel.set('Second criterion');
    component.addCriterion();
    expect(component.dorList().length).toBe(2);
    expect(component.dorList()[0].label).toBe('AC verified');
    expect(component.dorList()[1].label).toBe('Second criterion');

    // Move second item up
    component.moveUp('dor', 1);
    expect(component.dorList()[0].label).toBe('Second criterion');
    expect(component.dorList()[1].label).toBe('AC verified');

    // Move first item down
    component.moveDown('dor', 0);
    expect(component.dorList()[0].label).toBe('AC verified');
    expect(component.dorList()[1].label).toBe('Second criterion');
  });

  it('should add criteria to DoD when DoD tab is active', () => {
    component.activeGateTab.set('dod');
    component.newCriterionLabel.set('Load testing passed');
    component.newCriterionDesc.set('Verified in staging');
    component.newCriterionRequired.set(true);
    component.addCriterion();

    expect(component.dodList().length).toBe(2);
    expect(component.dodList()[1].label).toBe('Load testing passed');
    expect(component.dodList()[1].description).toBe('Verified in staging');
    expect(component.dodList()[1].isRequired).toBeTrue();
  });

  it('should reset criteria to standard agile defaults for active tab', () => {
    component.activeGateTab.set('dor');
    component.resetToDefaults();
    expect(component.dorList().length).toBeGreaterThan(1);

    component.activeGateTab.set('dod');
    component.resetToDefaults();
    expect(component.dodList().length).toBeGreaterThan(1);
    expect(component.errorMessage()).toBeNull();
  });

  it('should call state.configureTeamQualityGates and emit events on successful save', () => {
    const stateService = TestBed.inject(ScrumStateService);
    spyOn(stateService, 'configureTeamQualityGates').and.returnValue({
      subscribe: (observer: any) => {
        observer.next(mockTeam);
        return { unsubscribe() {} };
      }
    } as any);
    spyOn(component.saved, 'emit');
    spyOn(component.close, 'emit');

    component.onSave();

    expect(stateService.configureTeamQualityGates).toHaveBeenCalledWith(
      mockTeam.id,
      jasmine.any(Array),
      jasmine.any(Array)
    );
    expect(component.saved.emit).toHaveBeenCalled();
    expect(component.close.emit).toHaveBeenCalled();
  });

  it('should display error message when configureTeamQualityGates fails', () => {
    const stateService = TestBed.inject(ScrumStateService);
    spyOn(stateService, 'configureTeamQualityGates').and.returnValue({
      subscribe: (observer: any) => {
        observer.error({ error: { error: 'Failed to update quality gates' } });
        return { unsubscribe() {} };
      }
    } as any);

    component.onSave();
    expect(component.errorMessage()).toBe('Failed to update quality gates');
    expect(component.isSubmitting()).toBeFalse();
  });
});
