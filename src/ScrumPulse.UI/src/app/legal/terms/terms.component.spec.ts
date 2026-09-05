import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TermsComponent } from './terms.component';

describe('TermsComponent', () => {
  let component: TermsComponent;
  let fixture: ComponentFixture<TermsComponent>;

  beforeEach(async () => {
    (window as any).adsbygoogle = [];

    await TestBed.configureTestingModule({
      imports: [TermsComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(TermsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the terms component and display lastUpdated date', () => {
    expect(component).toBeTruthy();
    expect(component.lastUpdated).toContain('2026');
  });
});
