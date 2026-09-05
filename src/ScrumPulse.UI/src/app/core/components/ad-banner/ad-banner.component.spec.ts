import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdBannerComponent } from './ad-banner.component';

describe('AdBannerComponent', () => {
  let component: AdBannerComponent;
  let fixture: ComponentFixture<AdBannerComponent>;

  beforeEach(async () => {
    (window as any).adsbygoogle = [];

    await TestBed.configureTestingModule({
      imports: [AdBannerComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AdBannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should have default publisher ID and responsive format', () => {
    expect(component.publisherId).toBe('ca-pub-1773214213114642');
    expect(component.adFormat).toBe('auto');
    expect(component.fullWidthResponsive).toBeTrue();
    expect(component.showLabel).toBeTrue();
  });

  it('should trigger adsbygoogle push in ngAfterViewInit', () => {
    const adsArr: any[] = [];
    (window as any).adsbygoogle = adsArr;

    component.ngAfterViewInit();
    expect(adsArr.length).toBe(1);
  });
});
