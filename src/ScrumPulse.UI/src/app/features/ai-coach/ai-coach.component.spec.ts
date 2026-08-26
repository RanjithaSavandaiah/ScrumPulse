import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { AiCoachComponent } from './ai-coach.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('AiCoachComponent', () => {
  let component: AiCoachComponent;
  let fixture: ComponentFixture<AiCoachComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiCoachComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(AiCoachComponent);
    component = fixture.componentInstance;
  });

  it('should create and have default tier', () => {
    expect(component).toBeTruthy();
    expect(component.aiTier).toBe('individual');
    expect(component.chatMessages.length).toBeGreaterThan(0);
  });
});
