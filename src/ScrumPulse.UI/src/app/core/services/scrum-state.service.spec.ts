import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Store, provideStore } from '@ngrx/store';
import { ScrumStateService } from './scrum-state.service';
import { appReducers } from '../state';
import { Team, TeamMember } from '../models/scrum.models';

describe('ScrumStateService (Modular NgRx Facade)', () => {
  let service: ScrumStateService;
  let httpMock: HttpTestingController;
  let store: Store;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    });
    service = TestBed.inject(ScrumStateService);
    httpMock = TestBed.inject(HttpTestingController);
    store = TestBed.inject(Store);

    // Flush initial loadTeams() call triggered by constructor
    const req = httpMock.expectOne('/api/teams');
    req.flush([]);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created and expose reactive signals for state slices', () => {
    expect(service).toBeTruthy();
    expect(service.sprints()).toEqual([]);
    expect(service.members()).toEqual([]);
    expect(service.workItems()).toEqual([]);
    expect(service.prLogs()).toEqual([]);
    expect(service.currentRole()).toBe('Developer');
    expect(service.isDarkMode()).toBeTrue();
    expect(service.sayDoRatio()).toBeGreaterThanOrEqual(0);
  });

  it('should update role and persist to state when setCurrentRole is called', () => {
    service.setCurrentRole('ScrumMaster');
    expect(service.currentRole()).toBe('ScrumMaster');

    service.setCurrentRole('QaEngineer');
    expect(service.currentRole()).toBe('QaEngineer');
  });

  it('should toggle dark mode via store dispatch', () => {
    spyOn(store, 'dispatch');
    service.toggleDarkMode();
    expect(store.dispatch).toHaveBeenCalled();
  });

  it('should verify and unlock SM session via API endpoint', () => {
    expect(service.isSmAuthenticated()).toBeFalse();

    service.verifyAndUnlockSm('1234').subscribe(success => {
      expect(success).toBeTrue();
      expect(service.isSmAuthenticated()).toBeTrue();
      expect(service.currentRole()).toBe('ScrumMaster');
    });

    const req = httpMock.expectOne('/api/auth/verify-pin');
    expect(req.request.method).toBe('POST');
    req.flush({ success: true, message: 'Authenticated' });

    service.lockSmSession();
    expect(service.isSmAuthenticated()).toBeFalse();
    expect(service.currentRole()).toBe('Developer');
  });

  it('should create and join teams via HTTP API', () => {
    const mockTeam: Team = {
      id: 'team-phoenix',
      name: 'Phoenix Squad',
      slug: 'phoenix-squad',
      description: 'Core platform engineering',
      joinCode: 'PHX-101',
      isActive: true,
      createdAtUtc: '2026-09-01T00:00:00Z'
    };

    service.createTeam({ name: 'Phoenix Squad', description: 'Core platform engineering' }).subscribe(res => {
      expect(res.name).toBe('Phoenix Squad');
      expect(service.teams()).toContain(mockTeam);
      expect(service.currentTeam()).toEqual(mockTeam);
    });

    const createReq = httpMock.expectOne('/api/teams');
    expect(createReq.request.method).toBe('POST');
    createReq.flush(mockTeam);

    service.joinTeam({ joinCode: 'PHX-101' }).subscribe(res => {
      expect(res.joinCode).toBe('PHX-101');
    });

    const joinReq = httpMock.expectOne('/api/teams/join');
    expect(joinReq.request.method).toBe('POST');
    joinReq.flush(mockTeam);
  });

  it('should call compareSprints and team performance API endpoints', () => {
    service.compareSprints('sp-1', 'sp-2').subscribe();
    const compReq = httpMock.expectOne('/api/executive-reports/compare?sprintA=sp-1&sprintB=sp-2');
    expect(compReq.request.method).toBe('GET');
    compReq.flush({});

    service.getTeamPerformanceSummary(6).subscribe();
    const perfReq = httpMock.expectOne('/api/team-performance/summary?sprintCount=6');
    expect(perfReq.request.method).toBe('GET');
    perfReq.flush({});

    service.getTeamHighlights(6).subscribe();
    const highReq = httpMock.expectOne('/api/team-performance/highlights?sprintCount=6');
    expect(highReq.request.method).toBe('GET');
    highReq.flush([]);

    service.getGrowthTrend(8).subscribe();
    const growthReq = httpMock.expectOne('/api/team-performance/growth-trend?sprintCount=8');
    expect(growthReq.request.method).toBe('GET');
    growthReq.flush([]);

    service.getVelocityTrend(6).subscribe();
    const velReq = httpMock.expectOne('/api/executive-reports/velocity-trend?count=6');
    expect(velReq.request.method).toBe('GET');
    velReq.flush({});

    service.getSprintHealth('sp-1').subscribe();
    const healthReq = httpMock.expectOne('/api/executive-reports/sprint/sp-1/health');
    expect(healthReq.request.method).toBe('GET');
    healthReq.flush({});
  });

  it('should call AI copilot and suggestion API endpoints', () => {
    service.askCopilot('Suggest sprint goal').subscribe();
    const askReq = httpMock.expectOne('/api/aicoach/ask');
    expect(askReq.request.method).toBe('POST');
    askReq.flush({ answer: 'Deliver high value', suggestedFollowUps: [], timestampUtc: '' });

    service.generateAiSuggestions('Individual', 'm-1').subscribe();
    const sugReq = httpMock.expectOne('/api/aicoach/suggest');
    expect(sugReq.request.method).toBe('POST');
    sugReq.flush({});

    service.getIndividualAi('m-1').subscribe();
    httpMock.expectOne('/api/aicoach/suggest').flush({});

    service.getProjectAi('sp-1').subscribe();
    httpMock.expectOne('/api/aicoach/suggest').flush({});

    service.getCompanyAi().subscribe();
    httpMock.expectOne('/api/aicoach/suggest').flush({});
  });

  it('should assign member squad and dispatch loadTeamMembers', () => {
    spyOn(store, 'dispatch');
    const mockMember: TeamMember = {
      id: 'm-1',
      name: 'Bob',
      email: 'bob@test.com',
      role: 'Developer',
      avatar: '',
      location: 'BLR',
      timeZone: 'IST',
      activeWipLimit: 3,
      teamId: 'team-phoenix'
    };

    service.assignMemberSquad('m-1', 'team-phoenix').subscribe(res => {
      expect(res.teamId).toBe('team-phoenix');
      expect(store.dispatch).toHaveBeenCalled();
    });

    const squadReq = httpMock.expectOne('/api/teammembers/m-1/squad');
    expect(squadReq.request.method).toBe('PUT');
    squadReq.flush(mockMember);
  });

  it('should dispatch store actions for CRUD operations cleanly', () => {
    spyOn(store, 'dispatch');

    service.createWorkItem({ title: 'Task 1' });
    service.updateWorkItem('wi-1', { title: 'Task 1 Updated' });
    service.deleteWorkItem('wi-1');
    service.advanceStage('wi-1', 'Done');
    service.updateQualityGates('wi-1', {});

    service.createBlocker({ title: 'Blocker 1' });
    service.updateBlocker('blk-1', { title: 'Updated' });
    service.deleteBlocker('blk-1');
    service.resolveBlocker('blk-1', 'Fixed');

    service.submitStandup({});
    service.updateStandup('std-1', {});
    service.deleteStandup('std-1');
    service.clearAllStandups();

    service.submitLeave({});
    service.updateLeave('lv-1', {});
    service.deleteLeave('lv-1');

    service.submitFeedback({});
    service.submitMonthlyFeedback({});
    service.updateMonthlyFeedback('fb-1', {});
    service.deleteMonthlyFeedback('fb-1');

    service.createRetroCard({});
    service.updateRetroCard('rc-1', {});
    service.deleteRetroCard('rc-1');
    service.voteRetroCard('rc-1');
    service.createRetroAction({});
    service.updateRetroAction('ra-1', {});
    service.deleteRetroAction('ra-1');
    service.toggleRetroAction('ra-1');

    service.giveKudos({});
    service.sendKudos({});
    service.addKudosReaction('kd-1', 'like');

    service.createTechDebt({});
    service.updateTechDebt('td-1', {});
    service.deleteTechDebt('td-1');
    service.resolveTechDebt('td-1');

    service.logTechTalk({});
    service.createTechTalk({});
    service.updateTechTalk('tt-1', {});
    service.deleteTechTalk('tt-1');

    expect(store.dispatch).toHaveBeenCalled();
  });
});
