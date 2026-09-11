import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { appReducers } from '../../../../core/state';
import { QualityGatesModalComponent } from './quality-gates-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { WorkItem } from '../../../../core/models/scrum.models';

describe('QualityGatesModalComponent', () => {
  let component: QualityGatesModalComponent;
  let fixture: ComponentFixture<QualityGatesModalComponent>;

  const mockItem: WorkItem = {
    id: 'w-1',
    key: 'SP-10',
    title: 'Quality Gates verification',
    description: '',
    type: 'UserStory',
    status: 'InQa',
    storyPoints: 5,
    priority: 'High',
    createdAtUtc: new Date().toISOString(),
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: true,
    dodUnitTestsPassed: true,
    dodPeerReviewCompleted: true,
    dodMergedToMaster: true,
    dodStagingVerified: false,
    isEscapedDefect: false
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [QualityGatesModalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers),
        ScrumStateService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(QualityGatesModalComponent);
    component = fixture.componentInstance;
    component.item = mockItem;
    fixture.detectChanges();
  });

  it('should create and receive item input', () => {
    expect(component).toBeTruthy();
    expect(component.item.key).toBe('SP-10');
  });

  it('should emit save and close events', () => {
    spyOn(component.save, 'emit');
    spyOn(component.close, 'emit');

    component.onSave();
    expect(component.save.emit).toHaveBeenCalledWith(mockItem);

    component.close.emit();
    expect(component.close.emit).toHaveBeenCalled();
  });

  it('should count met criteria correctly', () => {
    expect(component.getDorMetCount()).toBeGreaterThan(0);
  });

  it('should toggle criterion state and keep legacy booleans in sync', () => {
    component.onToggleCriterion('dor-ac', false);
    expect(component.isCriterionChecked('dor-ac')).toBeFalse();
    expect(component.item.dorAcceptanceCriteriaDefined).toBeFalse();

    component.onToggleCriterion('dor-ac', true);
    expect(component.isCriterionChecked('dor-ac')).toBeTrue();
    expect(component.item.dorAcceptanceCriteriaDefined).toBeTrue();
  });

  it('should emit openConfigure event when openConfigure is called', () => {
    spyOn(component.openConfigure, 'emit');
    component.openConfigure.emit();
    expect(component.openConfigure.emit).toHaveBeenCalled();
  });

  it('should return exact legacy DOM ids for backward-compatible selectors', () => {
    expect(component.getCriterionDomId({ id: 'dor-ac', label: '', isRequired: true })).toBe('gateDorAcceptanceCriteria');
    expect(component.getCriterionDomId({ id: 'dor-dep', label: '', isRequired: true })).toBe('gateDorDependencies');
    expect(component.getCriterionDomId({ id: 'dor-wireframe', label: '', isRequired: false })).toBe('gateDorWireframe');
    expect(component.getCriterionDomId({ id: 'dod-tests', label: '', isRequired: true })).toBe('gateDodUnitTests');
    expect(component.getCriterionDomId({ id: 'dod-review', label: '', isRequired: true })).toBe('gateDodPeerReview');
    expect(component.getCriterionDomId({ id: 'dod-master', label: '', isRequired: true })).toBe('gateDodMergedMaster');
    expect(component.getCriterionDomId({ id: 'dod-staging', label: '', isRequired: true })).toBe('gateDodStagingVerified');
  });

  it('should return sanitized DOM id for custom criteria', () => {
    expect(component.getCriterionDomId({ id: 'dor-sec-audit', label: '', isRequired: true })).toBe('gate-dor-sec-audit');
  });

  it('should track custom criteria checks and update results dictionary on save', () => {
    component.onToggleCriterion('custom-gate-1', true);
    expect(component.isCriterionChecked('custom-gate-1')).toBeTrue();

    component.onSave();
    expect(component.item.qualityGateResults?.['custom-gate-1']).toBeTrue();

    component.onToggleCriterion('custom-gate-1', false);
    expect(component.isCriterionChecked('custom-gate-1')).toBeFalse();

    component.onSave();
    expect(component.item.qualityGateResults?.['custom-gate-1']).toBeFalse();
  });
});
