import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RetroComponent } from './retro.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { RetroCard } from '../../core/models/scrum.models';

describe('RetroComponent', () => {
  let component: RetroComponent;
  let fixture: ComponentFixture<RetroComponent>;
  let stateService: ScrumStateService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RetroComponent, HttpClientTestingModule],
      providers: [ScrumStateService]
    }).compileComponents();

    fixture = TestBed.createComponent(RetroComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
  });

  it('should filter retro cards by category index', () => {
    const mockCards: RetroCard[] = [
      { id: '1', sprintId: 's1', category: 'WentWell', content: 'Good release', authorId: 'a1', authorName: 'Alice', isAnonymous: false, upvotesCount: 2, upvoterMemberIds: [] },
      { id: '2', sprintId: 's1', category: 'DidntGoWell', content: 'Slow PRs', authorId: 'a2', authorName: 'Bob', isAnonymous: false, upvotesCount: 1, upvoterMemberIds: [] }
    ];

    stateService.retroCards.set(mockCards);

    const wentWellCards = component.getCardsByCategory(0);
    expect(wentWellCards.length).toBe(1);
    expect(wentWellCards[0].content).toBe('Good release');
  });
});
