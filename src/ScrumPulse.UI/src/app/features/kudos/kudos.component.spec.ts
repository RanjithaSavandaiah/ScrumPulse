import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { KudosComponent } from './kudos.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';

describe('KudosComponent', () => {
  let component: KudosComponent;
  let fixture: ComponentFixture<KudosComponent>;
  let stateService: ScrumStateService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KudosComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(KudosComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create the KudosComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should map badge types to correct Lucide icon names', () => {
    expect(component.getBadgeIcon(0)).toBe('rocket');
    expect(component.getBadgeIcon(1)).toBe('users');
    expect(component.getBadgeIcon(2)).toBe('target');
    expect(component.getBadgeIcon(3)).toBe('shield');
    expect(component.getBadgeIcon(4)).toBe('sparkles');
    expect(component.getBadgeIcon(5)).toBe('trophy');
    expect(component.getBadgeIcon(99)).toBe('award');
    expect(component.getBadgeIcon(null)).toBe('award');
  });

  it('should safely read reaction emoji counts', () => {
    expect(component.getReactionCount(null, 'clap')).toBe(0);
    expect(component.getReactionCount({}, 'clap')).toBe(0);

    const cardWithReactions = {
      reactionEmojis: { clap: 4, heart: 2 }
    };
    expect(component.getReactionCount(cardWithReactions, 'clap')).toBe(4);
    expect(component.getReactionCount(cardWithReactions, 'fire')).toBe(0);
  });

  it('should toggle kudos modal on trigger', () => {
    expect(component.showKudosModal()).toBeFalse();
    component.showKudosModal.set(true);
    expect(component.showKudosModal()).toBeTrue();
  });

  it('should dispatch sendKudos when onSaveKudos is called', () => {
    const sendSpy = spyOn(stateService, 'sendKudos');
    component.showKudosModal.set(true);

    component.onSaveKudos({
      senderId: 'm1',
      receiverId: 'm2',
      badge: 1,
      message: 'Great collaboration on code reviews!'
    });

    expect(sendSpy).toHaveBeenCalledWith({
      senderId: 'm1',
      receiverId: 'm2',
      badge: 1,
      message: 'Great collaboration on code reviews!'
    });
    expect(component.showKudosModal()).toBeFalse();
  });
});
