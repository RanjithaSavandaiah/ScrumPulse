import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EstimationMatrixModalComponent } from './estimation-matrix-modal.component';

describe('EstimationMatrixModalComponent', () => {
  let component: EstimationMatrixModalComponent;
  let fixture: ComponentFixture<EstimationMatrixModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EstimationMatrixModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(EstimationMatrixModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and have guide items for Fibonacci scale', () => {
    expect(component).toBeTruthy();
    expect(component.matrixItems.length).toBe(7);
  });

  it('should calculate points from hours correctly', () => {
    component.inputHours = 0.5;
    expect(component.calculatedPointFromHours.points).toBe(0);

    component.inputHours = 3;
    expect(component.calculatedPointFromHours.points).toBe(1);

    component.inputHours = 6;
    expect(component.calculatedPointFromHours.points).toBe(2);

    component.inputHours = 12;
    expect(component.calculatedPointFromHours.points).toBe(3);

    component.inputHours = 20;
    expect(component.calculatedPointFromHours.points).toBe(5);

    component.inputHours = 32;
    expect(component.calculatedPointFromHours.points).toBe(8);

    component.inputHours = 60;
    expect(component.calculatedPointFromHours.points).toBe(13);
  });

  it('should calculate benchmark hours from story points', () => {
    component.selectedPoint = 0;
    expect(component.calculatedHoursFromPoint.average).toBe(0.5);

    component.selectedPoint = 3;
    expect(component.calculatedHoursFromPoint.average).toBe(12);

    component.selectedPoint = 5;
    expect(component.calculatedHoursFromPoint.average).toBe(20);

    component.selectedPoint = 13;
    expect(component.calculatedHoursFromPoint.average).toBe(48);
  });

  it('should emit selectEstimation and close on applyToItem in hoursToPoints mode', () => {
    spyOn(component.selectEstimation, 'emit');
    spyOn(component.close, 'emit');

    component.calculatorMode = 'hoursToPoints';
    component.inputHours = 16;

    component.applyToItem();
    expect(component.selectEstimation.emit).toHaveBeenCalledWith({ points: 3, hours: 16 });
    expect(component.close.emit).toHaveBeenCalled();
  });

  it('should emit selectEstimation and close on applyToItem in pointsToHours mode', () => {
    spyOn(component.selectEstimation, 'emit');
    spyOn(component.close, 'emit');

    component.calculatorMode = 'pointsToHours';
    component.selectedPoint = 5;

    component.applyToItem();
    expect(component.selectEstimation.emit).toHaveBeenCalledWith({ points: 5, hours: 20 });
    expect(component.close.emit).toHaveBeenCalled();
  });
});
