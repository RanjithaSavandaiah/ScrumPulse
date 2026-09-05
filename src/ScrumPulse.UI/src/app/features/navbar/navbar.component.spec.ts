import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { of, throwError } from 'rxjs';
import { NavbarComponent } from './navbar.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';
import { Team } from '../../core/models/scrum.models';

describe('NavbarComponent', () => {
  let component: NavbarComponent;
  let fixture: ComponentFixture<NavbarComponent>;
  let stateService: ScrumStateService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(NavbarComponent);
    component = fixture.componentInstance;
    stateService = TestBed.inject(ScrumStateService);
    fixture.detectChanges();
  });

  it('should create the navbar component', () => {
    expect(component).toBeTruthy();
  });

  it('should toggle theme attribute on html element', () => {
    const initialTheme = component.isDark;
    component.toggleTheme();
    expect(component.isDark).toBe(!initialTheme);
    expect(document.documentElement.getAttribute('data-theme')).toBe(component.isDark ? 'dark' : 'light');
  });

  it('should prompt PIN modal when switching to ScrumMaster role without authentication', () => {
    spyOn(stateService, 'isSmAuthenticated').and.returnValue(false);

    const mockSelect = { value: 'ScrumMaster' } as HTMLSelectElement;
    const mockEvent = { target: mockSelect } as unknown as Event;

    component.onRoleChange(mockEvent);
    expect(component.showPinModal()).toBeTrue();
  });

  it('should allow role switch to Developer directly', () => {
    spyOn(stateService, 'setCurrentRole');

    const mockSelect = { value: 'Developer' } as HTMLSelectElement;
    const mockEvent = { target: mockSelect } as unknown as Event;

    component.onRoleChange(mockEvent);
    expect(stateService.setCurrentRole).toHaveBeenCalledWith('Developer');
    expect(component.showPinModal()).toBeFalse();
  });

  it('should manage squad modal state and tabs', () => {
    component.openSquadModal('join');
    expect(component.showSquadModal()).toBeTrue();
    expect(component.squadModalTab()).toBe('join');

    component.closeSquadModal();
    expect(component.showSquadModal()).toBeFalse();
  });

  it('should handle squad creation validation and dispatch', () => {
    spyOn(stateService, 'canEditOrDelete').and.returnValue(true);
    const mockTeam: Team = { id: 't-1', name: 'Spartans', slug: 'spartans', description: '', joinCode: 'SPAR-1', isActive: true, createdAtUtc: '' };
    spyOn(stateService, 'createTeam').and.returnValue(of(mockTeam));

    component.openSquadModal('create');
    component.newSquadName.set('Spartans');
    component.handleCreateSquad();

    expect(stateService.createTeam).toHaveBeenCalledWith(jasmine.objectContaining({ name: 'Spartans' }));
    expect(component.showSquadModal()).toBeFalse();
  });

  it('should handle join squad with join code', () => {
    const mockTeam: Team = { id: 't-1', name: 'Spartans', slug: 'spartans', description: '', joinCode: 'SPAR-1', isActive: true, createdAtUtc: '' };
    spyOn(stateService, 'joinTeam').and.returnValue(of(mockTeam));

    component.openSquadModal('join');
    component.joinCodeInput.set('SPAR-1');
    component.handleJoinSquad();

    expect(stateService.joinTeam).toHaveBeenCalledWith({ joinCode: 'SPAR-1' });
    expect(component.showSquadModal()).toBeFalse();
  });

  it('should lock SM session', () => {
    spyOn(stateService, 'lockSmSession');
    component.lockSm();
    expect(stateService.lockSmSession).toHaveBeenCalled();
  });
});
