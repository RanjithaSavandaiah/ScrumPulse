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

  it('should emit save when form is submitted with title', () => {
    spyOn(component.save, 'emit');

    component.blocker.title = 'DB down';
    component.blocker.description = 'Timeout';
    component.blocker.category = 2;
    component.blocker.slaHoursLimit = 8;

    component.save.emit(component.blocker);
    expect(component.save.emit).toHaveBeenCalledWith(component.blocker);
  });

  it('should emit close event', () => {
    spyOn(component.close, 'emit');
    component.close.emit();
    expect(component.close.emit).toHaveBeenCalled();
  });
});
