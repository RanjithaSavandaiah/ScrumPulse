import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavigationEnd, Router, provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { DemoBannerComponent } from './demo-banner.component';
import { DemoModeService } from './demo-mode.service';

describe('DemoBannerComponent', () => {
  let component: DemoBannerComponent;
  let fixture: ComponentFixture<DemoBannerComponent>;
  let demoMode: DemoModeService;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DemoBannerComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    demoMode = TestBed.inject(DemoModeService);
    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(DemoBannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    demoMode.disable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not display banner when demo mode is inactive', () => {
    demoMode.disable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/app', '/app'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.demo-banner')).toBeNull();
  });

  it('should not display banner on public marketing routes (/) even when demo mode is active', () => {
    demoMode.enable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/', '/'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.demo-banner')).toBeNull();
  });

  it('should not display banner on theory/blog routes (/blog) even when demo mode is active', () => {
    demoMode.enable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/blog', '/blog'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.demo-banner')).toBeNull();
  });

  it('should display banner on /app when demo mode is active', () => {
    demoMode.enable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/app', '/app'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const banner = compiled.querySelector('.demo-banner');
    expect(banner).not.toBeNull();
    expect(banner?.textContent).toContain('Sample data for a fictional squad');
  });

  it('should disable demo mode and navigate home when exitDemo is clicked', () => {
    demoMode.enable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/app', '/app'));
    fixture.detectChanges();

    spyOn(router, 'navigate');
    const compiled = fixture.nativeElement as HTMLElement;
    const exitBtn = compiled.querySelector('.demo-exit') as HTMLButtonElement;
    expect(exitBtn).not.toBeNull();

    exitBtn.click();
    fixture.detectChanges();

    expect(demoMode.isActive()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should display banner on nested app routes with query params when demo mode is active (/app/board-connection?tab=azure)', () => {
    demoMode.enable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/app/board-connection?tab=azure', '/app/board-connection?tab=azure'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.demo-banner')).not.toBeNull();
  });

  it('should not display banner on legal/theory routes with query params even when demo mode is active (/terms?ref=footer#privacy)', () => {
    demoMode.enable();
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/terms?ref=footer#privacy', '/terms?ref=footer#privacy'));
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.demo-banner')).toBeNull();
  });
});
