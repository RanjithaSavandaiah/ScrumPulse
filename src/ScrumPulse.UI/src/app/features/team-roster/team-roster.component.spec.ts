import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { TeamRosterComponent } from './team-roster.component';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { appReducers, TeamMemberActions, WorkItemActions } from '../../core/state';
import { TeamMember, WorkItem } from '../../core/models/scrum.models';

describe('TeamRosterComponent', () => {
  let component: TeamRosterComponent;
  let fixture: ComponentFixture<TeamRosterComponent>;
  let stateService: ScrumStateService;
  let store: Store;

  const mockMembers: TeamMember[] = [
    { id: 'm1', name: 'Dev Alice', email: 'alice@scrum.io', role: 'Developer', location: 'Offshore', timeZone: 'IST', avatar: 'DA', activeWipLimit: 3, teamId: 't1' },
    { id: 'm2', name: 'QA Bob', email: 'bob@scrum.io', role: 'QaEngineer', location: 'Offshore', timeZone: 'IST', avatar: 'QB', activeWipLimit: 3, teamId: 't1' },
    { id: 'm3', name: 'SM Carol', email: 'carol@scrum.io', role: 'ScrumMaster', location: 'Onsite', timeZone: 'EST', avatar: 'SC', activeWipLimit: 3, teamId: 't1' },
    { id: 'm4', name: 'CDL Dave', email: 'dave@scrum.io', role: 'Cdl', location: 'Onsite', timeZone: 'EST', avatar: 'CD', activeWipLimit: 3, teamId: 't2' }
  ];

  const mockWorkItems: WorkItem[] = [
    { id: 'w1', key: 'SP-1', title: 'Implement tests', description: '', type: 'TaskPbi', status: 'InProgress', storyPoints: 3, priority: 'High', assigneeId: 'm1', assigneeName: 'Dev Alice', sprintId: 's1' } as WorkItem,
    { id: 'w2', key: 'SP-2', title: 'QA test suite', description: '', type: 'UserStory', status: 'InQa', storyPoints: 5, priority: 'Medium', assigneeId: 'm2', assigneeName: 'QA Bob', sprintId: 's1' } as WorkItem
  ];

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
    stateService = TestBed.inject(ScrumStateService);
    store = TestBed.inject(Store);

    store.dispatch(TeamMemberActions.loadTeamMembersSuccess({ members: mockMembers }));
    store.dispatch(WorkItemActions.loadWorkItemsSuccess({ items: mockWorkItems }));
    fixture.detectChanges();
  });

  it('should create the TeamRosterComponent', () => {
    expect(component).toBeTruthy();
  });

  it('should compute role counts accurately', () => {
    expect(component.developerCount()).toBe(1);
    expect(component.qaCount()).toBe(1);
    // ScrumMaster and Cdl are leadership roles
    expect(component.leadershipCount()).toBe(2);
  });

  it('should toggle add member modal via signal', () => {
    expect(component.showAddModal()).toBeFalse();
    component.openAddModal();
    expect(component.showAddModal()).toBeTrue();
    expect(component.newMember.role).toBe('Developer');
    expect(component.newMember.name).toBe('');
  });

  it('should calculate assigned work item count for a member', () => {
    expect(component.getAssignedCount('m1')).toBe(1);
    expect(component.getAssignedCount('m2')).toBe(1);
    expect(component.getAssignedCount('m3')).toBe(0);
  });

  it('should provide appropriate color tokens for roles', () => {
    expect(component.getRoleBadgeColor('ScrumMaster')).toBe('var(--accent-warning)');
    expect(component.getRoleBadgeColor('Developer')).toBe('var(--accent-primary)');
    expect(component.getRoleBadgeColor('QaEngineer')).toBe('var(--accent-success)');
    expect(component.getRoleBadgeColor('Cdl')).toBe('var(--accent-purple)');
    expect(component.getRoleBadgeColor('ProductOwner')).toBe('var(--accent-secondary)');
    expect(component.getRoleBadgeColor('AgileCoach')).toBe('#ec4899');
    expect(component.getRoleBadgeColor('UnknownRole')).toBe('var(--text-secondary)');
  });

  it('should handle team and squad association helpers', () => {
    expect(component.getSquadName(null)).toBe('Unassigned Pool');
    expect(component.getSquadName('')).toBe('Unassigned Pool');

    expect(component.isMemberInSquad(mockMembers[0], 't1')).toBeTrue();
    expect(component.isMemberInSquad(mockMembers[0], 't2')).toBeFalse();
    expect(component.isMemberInSquad(mockMembers[0], '')).toBeFalse();
  });

  it('should dispatch createTeamMember on valid save and reset form', () => {
    spyOn(stateService, 'createTeamMember');

    component.openAddModal();
    component.newMember.name = 'Eve Engineer';
    component.newMember.role = 'Developer';
    component.newMember.teamId = 't1';

    component.onSaveMember();

    expect(stateService.createTeamMember).toHaveBeenCalledWith(jasmine.objectContaining({
      name: 'Eve Engineer',
      role: 'Developer',
      avatar: 'EE'
    }));
    expect(component.showAddModal()).toBeFalse();
  });

  it('should not dispatch createTeamMember if name is blank', () => {
    spyOn(stateService, 'createTeamMember');

    component.openAddModal();
    component.newMember.name = '   ';
    component.onSaveMember();

    expect(stateService.createTeamMember).not.toHaveBeenCalled();
  });

  it('should handle member deletion workflow', () => {
    spyOn(stateService, 'deleteTeamMember');

    component.onDeleteMember(mockMembers[0]);
    expect(component.memberToDelete()).toBe(mockMembers[0]);

    component.onCancelDeleteMember();
    expect(component.memberToDelete()).toBeNull();

    component.onDeleteMember(mockMembers[0]);
    component.onConfirmDeleteMember();
    expect(stateService.deleteTeamMember).toHaveBeenCalledWith('m1');
    expect(component.memberToDelete()).toBeNull();
  });

  it('should handle squad linking when permissions allow', () => {
    spyOn(stateService, 'canEditOrDelete').and.returnValue(true);
    spyOn(stateService, 'assignMemberSquad').and.returnValue(of(mockMembers[0]));

    // Without current team
    component.onLinkMember('m1');
    expect(stateService.assignMemberSquad).not.toHaveBeenCalled();

    // With current team
    stateService.currentTeam.set({ id: 't1', name: 'Alpha Squad' } as any);
    component.onLinkMember('m1');
    expect(stateService.assignMemberSquad).toHaveBeenCalledWith('m1', 't1');

    // Assign explicit squad ID
    component.onAssignSquadId('m1', 't2');
    expect(stateService.assignMemberSquad).toHaveBeenCalledWith('m1', 't2');

    // Assign null squad ID
    component.onAssignSquadId('m1', null);
    expect(stateService.assignMemberSquad).toHaveBeenCalledWith('m1', null);
  });
});
