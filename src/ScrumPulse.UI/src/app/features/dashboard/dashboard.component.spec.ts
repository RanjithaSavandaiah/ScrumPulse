import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';

describe('DashboardComponent', () => {
  let component: DashboardComponent;
  let fixture: ComponentFixture<DashboardComponent>;

  beforeEach(async () => {
    (window as any).adsbygoogle = [];

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers),
        {
          provide: ActivatedRoute,
          useValue: {
            fragment: of('performance')
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the dashboard component', () => {
    expect(component).toBeTruthy();
  });

  it('should synchronize activeTab from route fragment', () => {
    expect(component.activeTab()).toBe('performance');
  });

  it('should switch tabs when signal is updated', () => {
    component.activeTab.set('standup');
    expect(component.activeTab()).toBe('standup');

    component.activeTab.set('retros');
    expect(component.activeTab()).toBe('retros');
  });
});
