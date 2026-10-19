import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LandingComponent } from './landing.component';
import { DemoModeService } from '../../demo/demo-mode.service';

describe('LandingComponent', () => {
  let component: LandingComponent;
  let fixture: ComponentFixture<LandingComponent>;
  let demoMode: DemoModeService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LandingComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    demoMode = TestBed.inject(DemoModeService);
    fixture = TestBed.createComponent(LandingComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    demoMode.disable();
  });

  it('should create landing page with hero and feature highlights', () => {
    expect(component).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('See where your sprint is actually losing time');
    expect(compiled.querySelectorAll('.feature-card').length).toBeGreaterThanOrEqual(6);
  });

  it('should disable demo mode when hero Launch Dashboard & Tabs is clicked', () => {
    demoMode.enable();
    expect(demoMode.isActive()).toBeTrue();

    const compiled = fixture.nativeElement as HTMLElement;
    const heroLaunchBtn = compiled.querySelector('.hero-actions .cta-primary') as HTMLAnchorElement;
    expect(heroLaunchBtn).not.toBeNull();

    heroLaunchBtn.click();
    fixture.detectChanges();

    expect(demoMode.isActive()).toBeFalse();
  });

  it('should disable demo mode when bottom CTA Launch Dashboard & Tabs is clicked', () => {
    demoMode.enable();
    expect(demoMode.isActive()).toBeTrue();

    const compiled = fixture.nativeElement as HTMLElement;
    const allLaunchBtns = compiled.querySelectorAll('.cta-primary');
    expect(allLaunchBtns.length).toBeGreaterThanOrEqual(2);

    const bottomLaunchBtn = allLaunchBtns[1] as HTMLAnchorElement;
    bottomLaunchBtn.click();
    fixture.detectChanges();

    expect(demoMode.isActive()).toBeFalse();
  });
});
