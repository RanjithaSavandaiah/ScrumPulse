import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { TeamRosterComponent } from './team-roster.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers } from '../../core/state';

describe('TeamRosterComponent', () => {
  let component: TeamRosterComponent;
  let fixture: ComponentFixture<TeamRosterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TeamRosterComponent],
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TeamRosterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the TeamRosterComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should toggle new member modal', () => {
    expect(component.showNewMemberModal()).toBeFalse();
    component.showNewMemberModal.set(true);
    expect(component.showNewMemberModal()).toBeTrue();
  });

  it('should format role labels properly', () => {
    expect(component.getRoleLabel('ScrumMaster')).toBe('Scrum Master');
    expect(component.getRoleLabel('Developer')).toBe('Developer');
    expect(component.getRoleLabel('QaEngineer')).toBe('QA Engineer');
  });
});
