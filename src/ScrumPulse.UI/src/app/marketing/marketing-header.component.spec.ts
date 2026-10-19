import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MarketingHeaderComponent } from './marketing-header.component';
import { DemoModeService } from '../demo/demo-mode.service';

describe('MarketingHeaderComponent', () => {
  let component: MarketingHeaderComponent;
  let fixture: ComponentFixture<MarketingHeaderComponent>;
  let demoMode: DemoModeService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarketingHeaderComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    demoMode = TestBed.inject(DemoModeService);
    fixture = TestBed.createComponent(MarketingHeaderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    demoMode.disable();
  });

  it('should create header with navigation links', () => {
    expect(component).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('ScrumPulse');
    expect(compiled.querySelector('.btn-launch')?.textContent).toContain('Launch Dashboard');
  });

  it('should disable demo mode when Launch Dashboard is clicked', () => {
    demoMode.enable();
    expect(demoMode.isActive()).toBeTrue();

    const compiled = fixture.nativeElement as HTMLElement;
    const launchBtn = compiled.querySelector('.btn-launch') as HTMLAnchorElement;
    expect(launchBtn).not.toBeNull();

    launchBtn.click();
    fixture.detectChanges();

    expect(demoMode.isActive()).toBeFalse();
  });
});
