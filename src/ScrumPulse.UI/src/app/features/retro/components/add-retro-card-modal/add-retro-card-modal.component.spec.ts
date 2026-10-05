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

  it('should create and have 3 retro categories (excluding Action Item)', () => {
    expect(component).toBeTruthy();
    expect(component.categories.length).toBe(3);
    const categoryValues = component.categories.map(c => c.value);
    expect(categoryValues).toEqual([0, 1, 2]);
    const categoryLabels = component.categories.map(c => c.label);
    expect(categoryLabels).not.toContain('Action Item');
  });

  it('should safely remap legacy ActionItem category to Ideas when editing', () => {
    component.editingCard = {
      id: 'c-legacy',
      sprintId: 's1',
      category: 'ActionItem' as any,
      content: 'Legacy note',
      authorId: 'm1',
      authorName: 'Alex',
      upvotesCount: 0,
      isAnonymous: false
    };
    component.ngOnInit();
    expect(component.card.category).toBe(2);
  });

  it('should apply preset content', () => {
    component.card.content = component.presets[0];
    expect(component.card.content).toBe(component.presets[0]);
  });

  it('should emit save when submitted with valid content', () => {
    spyOn(component.save, 'emit');

    component.card.content = 'Improved CI workflow';
    component.card.category = 0;
    component.card.isAnonymous = false;

    component.onSubmit();
    expect(component.save.emit).toHaveBeenCalledWith(component.card);
    expect(component.validationError()).toBeNull();
  });

  it('should validate mandatory note content on submit', () => {
    spyOn(component.save, 'emit');

    component.card.content = '   ';
    component.onSubmit();

    expect(component.validationError()).toBe('Retrospective note content is mandatory');
    expect(component.save.emit).not.toHaveBeenCalled();
  });
});
