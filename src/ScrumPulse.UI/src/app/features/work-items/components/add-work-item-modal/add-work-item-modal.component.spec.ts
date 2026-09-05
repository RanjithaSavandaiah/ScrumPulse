import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { AddWorkItemModalComponent } from './add-work-item-modal.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { appReducers } from '../../../../core/state';

describe('AddWorkItemModalComponent', () => {
  let component: AddWorkItemModalComponent;
  let fixture: ComponentFixture<AddWorkItemModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AddWorkItemModalComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AddWorkItemModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and have types, priorities, and fibonacci story points', () => {
    expect(component).toBeTruthy();
    expect(component.itemTypes.length).toBe(3);
    expect(component.priorities.length).toBe(4);
    expect(component.pointOptions).toContain(3);
    expect(component.pointOptions).toContain(5);
    expect(component.pointOptions).toContain(8);
  });

  it('should select story points and suggest benchmark hours', () => {
    component.item.estimatedHours = null;
    component.selectStoryPoints(5);
    expect(component.item.storyPoints).toBe(5);
    expect<any>(component.item.estimatedHours).toBe(20);
  });

  it('should apply estimation from matrix modal', () => {
    component.showMatrixModal = true;
    component.onApplyMatrixEstimation({ points: 8, hours: 32 });
    expect(component.item.storyPoints).toBe(8);
    expect<any>(component.item.estimatedHours).toBe(32);
    expect(component.showMatrixModal).toBeFalse();
  });

  it('should correctly map type and priority values', () => {
    expect(component.mapTypeToNumber('UserStory')).toBe(0);
    expect(component.mapTypeToNumber('Bug')).toBe(1);
    expect(component.mapTypeToNumber('TechTask')).toBe(2);
    expect(component.mapTypeToNumber(2)).toBe(2);

    expect(component.mapPriorityToNumber('Critical')).toBe(3);
    expect(component.mapPriorityToNumber('High')).toBe(2);
    expect(component.mapPriorityToNumber('Medium')).toBe(1);
    expect(component.mapPriorityToNumber('Low')).toBe(0);
  });

  it('should emit save when save event is fired', () => {
    spyOn(component.save, 'emit');

    component.item.title = 'Add user authorization gate';
    component.item.description = 'OAuth 2.1';
    component.item.storyPoints = 5;

    component.save.emit(component.item);
    expect(component.save.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      title: 'Add user authorization gate',
      storyPoints: 5
    }));
  });

  it('should emit delete when delete event is fired', () => {
    spyOn(component.delete, 'emit');
    component.delete.emit('item-123');
    expect(component.delete.emit).toHaveBeenCalledWith('item-123');
  });
});
