import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { ReviewsComponent } from './reviews.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';

describe('ReviewsComponent', () => {
  let component: ReviewsComponent;
  let fixture: ComponentFixture<ReviewsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewsComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(ReviewsComponent);
    component = fixture.componentInstance;
  });

  it('should create and toggle feedback modal', () => {
    expect(component).toBeTruthy();
    expect(component.showFeedbackModal()).toBeFalse();

    component.showFeedbackModal.set(true);
    expect(component.showFeedbackModal()).toBeTrue();
  });
});
