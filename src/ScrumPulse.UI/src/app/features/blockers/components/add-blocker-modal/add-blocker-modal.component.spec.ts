import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AddBlockerModalComponent } from './add-blocker-modal.component';

describe('AddBlockerModalComponent', () => {
  let component: AddBlockerModalComponent;
  let fixture: ComponentFixture<AddBlockerModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AddBlockerModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AddBlockerModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and initialize default form', () => {
    expect(component).toBeTruthy();
    expect(component.blocker.slaHoursLimit).toBe(4);
    expect(component.categories.length).toBe(4);
  });

  it('should apply preset title cleanly', () => {
    component.blocker.title = component.presets[0];
    expect(component.blocker.title).toBe(component.presets[0]);
  });

  it('should emit save when form is submitted with title and blockedHours', () => {
    spyOn(component.save, 'emit');

    component.blocker.title = 'DB down';
    component.blocker.description = 'Timeout';
    component.blocker.category = 2;
    component.blocker.slaHoursLimit = 8;
    component.blocker.blockedHours = 12.5;

    component.save.emit(component.blocker);
    expect(component.save.emit).toHaveBeenCalledWith(jasmine.objectContaining({
      title: 'DB down',
      blockedHours: 12.5
    }));
  });

  it('should validate mandatory title and description on onSubmit', () => {
    spyOn(component.save, 'emit');

    component.blocker.title = '';
    component.blocker.description = 'Timeout';
    component.onSubmit();
    expect(component.validationError()).toBe('Blocker title is mandatory');
    expect(component.save.emit).not.toHaveBeenCalled();

    component.blocker.title = 'Database unreachable';
    component.blocker.description = '  ';
    component.onSubmit();
    expect(component.validationError()).toBe('Blocker context is mandatory');
    expect(component.save.emit).not.toHaveBeenCalled();

    component.blocker.description = 'Urgent connection needed';
    component.onSubmit();
    expect(component.validationError()).toBeNull();
    expect(component.save.emit).toHaveBeenCalledWith(component.blocker);
  });

  it('should emit close event', () => {
    spyOn(component.close, 'emit');
    component.close.emit();
    expect(component.close.emit).toHaveBeenCalled();
  });
});
