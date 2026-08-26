import { ComponentFixture, TestBed } from '@angular/core/testing';
import { IconComponent } from './icon.component';

describe('IconComponent', () => {
  let component: IconComponent;
  let fixture: ComponentFixture<IconComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IconComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(IconComponent);
    component = fixture.componentInstance;
  });

  it('should create and render default SVG icon', () => {
    component.name = 'rocket';
    component.size = 20;
    component.color = '#10B981';
    fixture.detectChanges();

    expect(component).toBeTruthy();
    const svgEl = fixture.nativeElement.querySelector('svg');
    expect(svgEl).toBeTruthy();
    expect(svgEl.getAttribute('width')).toBe('20');
    expect(svgEl.getAttribute('height')).toBe('20');
  });

  it('should fall back to sparkles icon when unknown icon name is provided', () => {
    component.name = 'non-existent-icon' as any;
    fixture.detectChanges();

    const svgEl = fixture.nativeElement.querySelector('svg');
    expect(svgEl).toBeTruthy();
  });
});
