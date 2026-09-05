import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { EditSprintModalComponent } from './edit-sprint-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('EditSprintModalComponent', () => {
  let component: EditSprintModalComponent;
  let fixture: ComponentFixture<EditSprintModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditSprintModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EditSprintModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and calculate working days', () => {
    expect(component).toBeTruthy();
    expect(component.calculatedWorkingDays).toBeGreaterThan(0);
    expect(component.dailyWorkingHours).toBe(8.5);
  });

  it('should emit save when submitted with valid name', () => {
    spyOn(component.save, 'emit');

    component.name = 'Sprint 34';
    component.goal = 'Complete OAuth and Native Federation';
    component.committedStoryPoints = 40;

    component.onSubmit();
    expect(component.save.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      name: 'Sprint 34',
      committedStoryPoints: 40
    }));
  });

  it('should not emit save when name is blank', () => {
    spyOn(component.save, 'emit');

    component.name = '   ';
    component.onSubmit();

    expect(component.save.emit).not.toHaveBeenCalled();
  });
});
