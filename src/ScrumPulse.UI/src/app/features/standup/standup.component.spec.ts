import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { StandupComponent } from './standup.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('StandupComponent', () => {
  let component: StandupComponent;
  let fixture: ComponentFixture<StandupComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StandupComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(StandupComponent);
    component = fixture.componentInstance;
  });

  it('should create and handle timer toggles', () => {
    expect(component).toBeTruthy();
    expect(component.timerSeconds).toBe(120);
    expect(component.timerRunning).toBeFalse();

    component.toggleTimer();
    expect(component.timerRunning).toBeTrue();

    component.toggleTimer();
    expect(component.timerRunning).toBeFalse();

    component.resetTimer();
    expect(component.timerSeconds).toBe(120);
  });
});
