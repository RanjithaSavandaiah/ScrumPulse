import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { EstimationMatrixModalComponent } from './estimation-matrix-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('EstimationMatrixModalComponent', () => {
  let component: EstimationMatrixModalComponent;
  let fixture: ComponentFixture<EstimationMatrixModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EstimationMatrixModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EstimationMatrixModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and have guide items for Fibonacci scale', () => {
    expect(component).toBeTruthy();
    expect(component.matrixItems.length).toBe(7);
  });

  it('should adopt configured daily working hours in formula', () => {
    // Default 8.5
    expect(component.benchmarkHoursFormatted).toBe('8.5');

    // Configured 8.0
    component.dailyWorkingHours = 8.0;
    fixture.detectChanges();
    expect(component.benchmarkHoursFormatted).toBe('8.0');
    expect(fixture.nativeElement.querySelector('.formula-box').textContent).toContain('8.0 hrs/pt');

    // Configured 8.5
    component.dailyWorkingHours = 8.5;
    fixture.detectChanges();
    expect(component.benchmarkHoursFormatted).toBe('8.5');
    expect(fixture.nativeElement.querySelector('.formula-box').textContent).toContain('8.5 hrs/pt');

    // Configured 9.0
    component.dailyWorkingHours = 9.0;
    fixture.detectChanges();
    expect(component.benchmarkHoursFormatted).toBe('9.0');
    expect(fixture.nativeElement.querySelector('.formula-box').textContent).toContain('9.0 hrs/pt');
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
