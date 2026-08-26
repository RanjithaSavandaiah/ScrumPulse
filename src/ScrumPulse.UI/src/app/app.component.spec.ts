import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { AppComponent } from './app.component';
import { ScrumStateService } from './core/services/scrum-state.service';

describe('AppComponent', () => {
  let component: AppComponent;
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    component = fixture.componentInstance;
  });

  it('should create the app and initialize default activeTab', () => {
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('lifecycle');
  });

  it('should allow switching tabs', () => {
    component.activeTab.set('blockers');
    expect(component.activeTab()).toBe('blockers');

    component.activeTab.set('ai-coach');
    expect(component.activeTab()).toBe('ai-coach');
  });
});
