import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AddRetroCardModalComponent } from './add-retro-card-modal.component';

describe('AddRetroCardModalComponent', () => {
  let component: AddRetroCardModalComponent;
  let fixture: ComponentFixture<AddRetroCardModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AddRetroCardModalComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AddRetroCardModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create and have 4 retro categories', () => {
    expect(component).toBeTruthy();
    expect(component.categories.length).toBe(4);
  });

  it('should apply preset content', () => {
    component.card.content = component.presets[0];
    expect(component.card.content).toBe(component.presets[0]);
  });

  it('should emit save when submitted with content', () => {
    spyOn(component.save, 'emit');

    component.card.content = 'Improved CI workflow';
    component.card.category = 0;
    component.card.isAnonymous = false;

    component.save.emit(component.card);
    expect(component.save.emit).toHaveBeenCalledWith(component.card);
  });
});
