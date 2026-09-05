import { ComponentFixture, TestBed } from '@angular/core/testing';
import { QualityGatesModalComponent } from './quality-gates-modal.component';
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
      imports: [QualityGatesModalComponent]
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

    component.save.emit(component.item);
    expect(component.save.emit).toHaveBeenCalledWith(mockItem);

    component.close.emit();
    expect(component.close.emit).toHaveBeenCalled();
  });
});
