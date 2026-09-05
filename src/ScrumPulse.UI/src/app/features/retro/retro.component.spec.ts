import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Store, provideStore } from '@ngrx/store';
import { RetroComponent } from './retro.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers, RetroActions } from '../../core/state';
import { RetroActionItem, RetroCard } from '../../core/models/scrum.models';

describe('RetroComponent', () => {
  let component: RetroComponent;
  let fixture: ComponentFixture<RetroComponent>;
  let stateService: ScrumStateService;
  let store: Store;

  const mockCards: RetroCard[] = [
    { id: 'c1', sprintId: 's1', category: 'WentWell', content: 'Great team momentum', authorId: 'm1', authorName: 'Alice', isAnonymous: false, upvotesCount: 3 },
    { id: 'c2', sprintId: 's1', category: 'DidntGoWell', content: 'CI pipeline was flaky', authorId: 'm2', authorName: 'Bob', isAnonymous: false, upvotesCount: 1 },
    { id: 'c3', sprintId: 's1', category: 'Ideas', content: 'Adopt mob programming on spikes', authorId: 'm3', authorName: 'Carol', isAnonymous: false, upvotesCount: 5 }
  ];

  const mockActions: RetroActionItem[] = [
    { id: 'a1', sprintId: 's1', title: 'Stabilize flaky E2E runners', assigneeId: 'm1', assigneeName: 'Alice', dueDate: '2026-09-12', isCompleted: false },
    { id: 'a2', sprintId: 's1', title: 'Schedule spike on mob programming', assigneeId: 'm2', assigneeName: 'Bob', dueDate: '2026-09-15', isCompleted: true }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RetroComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RetroComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    store = TestBed.inject(Store);

    store.dispatch(RetroActions.loadRetrosSuccess({ cards: mockCards, actions: mockActions }));
    fixture.detectChanges();
  });

  it('should create the RetroComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should filter retro cards by category index', () => {
    const wentWellCards = component.getCardsByCategory(0);
    expect(wentWellCards.length).toBe(1);
    expect(wentWellCards[0].content).toBe('Great team momentum');

    const didntGoWellCards = component.getCardsByCategory(1);
    expect(didntGoWellCards.length).toBe(1);
    expect(didntGoWellCards[0].content).toBe('CI pipeline was flaky');

    const ideasCards = component.getCardsByCategory(2);
    expect(ideasCards.length).toBe(1);
    expect(ideasCards[0].content).toBe('Adopt mob programming on spikes');
  });

  it('should manage add card modal state', () => {
    expect(component.showRetroModal()).toBeFalse();
    component.onOpenAddCard();
    expect(component.showRetroModal()).toBeTrue();
    expect(component.editingCard()).toBeNull();
  });

  it('should manage edit card modal state', () => {
    component.onEditCard(mockCards[0]);
    expect(component.showRetroModal()).toBeTrue();
    expect(component.editingCard()).toBe(mockCards[0]);
  });

  it('should manage delete card flow', () => {
    spyOn(stateService, 'deleteRetroCard');

    component.onDeleteCard(mockCards[0]);
    expect(component.cardToDelete()).toBe(mockCards[0]);

    component.onCancelDeleteCard();
    expect(component.cardToDelete()).toBeNull();

    component.onDeleteCard(mockCards[0]);
    component.onConfirmDeleteCard();
    expect(stateService.deleteRetroCard).toHaveBeenCalledWith('c1');
    expect(component.cardToDelete()).toBeNull();
  });

  it('should dispatch createRetroCard when saving new card', () => {
    spyOn(stateService, 'createRetroCard');

    component.editingCard.set(null);
    component.showRetroModal.set(true);

    component.onSaveRetroCard({
      category: 0,
      authorId: 'm1',
      content: 'Refactored state machine',
      isAnonymous: false
    });

    expect(stateService.createRetroCard).toHaveBeenCalledWith(jasmine.objectContaining({
      content: 'Refactored state machine',
      category: 0,
      isAnonymous: false
    }));
    expect(component.showRetroModal()).toBeFalse();
  });

  it('should dispatch updateRetroCard when saving existing card', () => {
    spyOn(stateService, 'updateRetroCard');

    component.onEditCard(mockCards[0]);
    component.onSaveRetroCard({
      category: 0,
      authorId: 'm1',
      content: 'Updated content',
      isAnonymous: false
    });

    expect(stateService.updateRetroCard).toHaveBeenCalledWith('c1', jasmine.objectContaining({
      content: 'Updated content',
      sprintId: 's1'
    }));
    expect(component.editingCard()).toBeNull();
    expect(component.showRetroModal()).toBeFalse();
  });

  it('should manage action items modal lifecycle and actions', () => {
    spyOn(stateService, 'createRetroAction');
    spyOn(stateService, 'updateRetroAction');
    spyOn(stateService, 'deleteRetroAction');

    // Add action modal
    component.onOpenAddAction();
    expect(component.showActionModal()).toBeTrue();
    expect(component.editingAction()).toBeNull();
    expect(component.actionForm.title).toBe('');

    // Save empty action should not trigger create
    component.actionForm.title = '   ';
    component.onSaveAction();
    expect(stateService.createRetroAction).not.toHaveBeenCalled();

    // Valid create
    component.actionForm.title = 'Review flaky test report';
    component.onSaveAction();
    expect(stateService.createRetroAction).toHaveBeenCalledWith(jasmine.objectContaining({
      title: 'Review flaky test report'
    }));
    expect(component.showActionModal()).toBeFalse();

    // Edit action
    component.onEditAction(mockActions[0]);
    expect(component.showActionModal()).toBeTrue();
    expect(component.editingAction()).toBe(mockActions[0]);
    expect(component.actionForm.title).toBe('Stabilize flaky E2E runners');

    component.actionForm.title = 'Stabilize all test runners';
    component.onSaveAction();
    expect(stateService.updateRetroAction).toHaveBeenCalledWith('a1', jasmine.objectContaining({
      title: 'Stabilize all test runners'
    }));

    // Delete action
    component.onDeleteAction(mockActions[0]);
    expect(component.actionToDelete()).toBe(mockActions[0]);
    component.onCancelDeleteAction();
    expect(component.actionToDelete()).toBeNull();

    component.onDeleteAction(mockActions[0]);
    component.onConfirmDeleteAction();
    expect(stateService.deleteRetroAction).toHaveBeenCalledWith('a1');
    expect(component.actionToDelete()).toBeNull();
  });
});
