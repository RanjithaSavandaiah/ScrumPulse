import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NavigationEnd, Router, provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { VoiceCaptureBarComponent } from './voice-capture-bar.component';
import { VoiceDictationService } from '../../services/voice-dictation.service';

describe('VoiceCaptureBarComponent', () => {
  let component: VoiceCaptureBarComponent;
  let fixture: ComponentFixture<VoiceCaptureBarComponent>;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VoiceCaptureBarComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    router = TestBed.inject(Router);
    fixture = TestBed.createComponent(VoiceCaptureBarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should be hidden on marketing, theory, and landing pages (root /)', () => {
    expect(component.isVisible()).toBeFalse();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.voice-bar')).toBeNull();
  });

  it('should become visible when navigating to /app', () => {
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/app', '/app'));
    fixture.detectChanges();

    expect(component.isVisible()).toBeTrue();
  });

  it('should be hidden on theory and blog routes (/blog, /about)', () => {
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/blog', '/blog'));
    fixture.detectChanges();
    expect(component.isVisible()).toBeFalse();

    (router.events as Subject<unknown>).next(new NavigationEnd(2, '/about', '/about'));
    fixture.detectChanges();
    expect(component.isVisible()).toBeFalse();
  });

  it('should be visible on demo route (/demo)', () => {
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/demo', '/demo'));
    fixture.detectChanges();

    expect(component.isVisible()).toBeTrue();
  });

  it('should be visible on deep application routes with query parameters (/app?teamId=123#overview)', () => {
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/app?teamId=123#overview', '/app?teamId=123#overview'));
    fixture.detectChanges();

    expect(component.isVisible()).toBeTrue();
  });

  it('should remain hidden on theory routes even if query parameters or hashes are present (/blog?search=metrics#section2)', () => {
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/blog?search=metrics#section2', '/blog?search=metrics#section2'));
    fixture.detectChanges();

    expect(component.isVisible()).toBeFalse();
  });

  it('should remain visible regardless of route if an active voice capture session is running', () => {
    const dictationService = TestBed.inject(VoiceDictationService);
    spyOn(dictationService, 'isCapturing').and.returnValue(true);

    // Even on a theory route like /privacy-policy
    (router.events as Subject<unknown>).next(new NavigationEnd(1, '/privacy-policy', '/privacy-policy'));
    fixture.detectChanges();

    expect(component.isVisible()).toBeTrue();
  });
});
