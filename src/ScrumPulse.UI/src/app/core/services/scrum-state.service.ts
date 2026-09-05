import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Store } from '@ngrx/store';
import { Observable, catchError, map, of, tap } from 'rxjs';
import {
  AppState,
  SprintActions,
  selectAllSprints,
  selectActiveSprint,
  WorkItemActions,
  selectAllWorkItems,
  PullRequestActions,
  selectPullRequestLogs,
  selectDeveloperPrMetrics,
  TeamMemberActions,
  selectAllMembers,
  selectCurrentRole,
  selectIsDarkMode,
  BlockerActions,
  selectAllBlockers,
  selectActiveBlockersCount,
  StandupActions,
  selectDailyStandups,
  LeaveActions,
  selectTeamLeaves,
  selectSprintCapacity,
  ReviewActions,
  selectMonthlyFeedbacks,
  RetroActions,
  selectRetroCards,
  selectRetroActions,
  KudosActions,
  selectKudos,
  TechHubActions,
  selectTechDebtItems,
  selectTechTalkLogs
} from '../state';
import {
  AiSuggestionResponse,
  Blocker,
  CopilotChatResponse,
  DailyStandup,
  DeveloperPrMetrics,
  ExecutiveReport,
  KudosCard,
  MonthlyFeedback,
  PullRequestLog,
  RetroActionItem,
  RetroCard,
  RoleType,
  Sprint,
  SprintCapacity,
  SprintComparison,
  SprintHealth,
  SprintVelocityTrend,
  Team,
  CreateTeamRequest,
  JoinTeamRequest,
  TeamLeave,
  TeamMember,
  TechDebtItem,
  TechTalkLog,
  WorkItem,
  TeamPerformanceSummary,
  TeamHighlight,
  SprintGrowthSnapshot
} from '../models/scrum.models';

@Injectable({ providedIn: 'root' })
export class ScrumStateService {
  private readonly store = inject(Store<AppState>);
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api';

  // Modular Signals exposed via feature selectors
  readonly sprints = this.store.selectSignal(selectAllSprints);
  readonly activeSprint = this.store.selectSignal(selectActiveSprint);
  readonly teams = signal<Team[]>([]);
  readonly currentTeam = signal<Team | null>(null);
  readonly members = this.store.selectSignal(selectAllMembers);
  readonly squadMembers = computed(() => {
    const team = this.currentTeam();
    if (!team) return this.members();
    const targetId = team.id.toLowerCase().trim();
    return this.members().filter(m => m.teamId && m.teamId.toLowerCase().trim() === targetId);
  });
  readonly workItems = this.store.selectSignal(selectAllWorkItems);
  readonly blockers = this.store.selectSignal(selectAllBlockers);
  readonly standups = this.store.selectSignal(selectDailyStandups);
  readonly leaves = this.store.selectSignal(selectTeamLeaves);
  readonly capacity = this.store.selectSignal(selectSprintCapacity);
  readonly monthlyFeedbacks = this.store.selectSignal(selectMonthlyFeedbacks);
  readonly retroCards = this.store.selectSignal(selectRetroCards);
  readonly retroActions = this.store.selectSignal(selectRetroActions);
  readonly kudos = this.store.selectSignal(selectKudos);
  readonly techDebt = this.store.selectSignal(selectTechDebtItems);
  readonly techTalks = this.store.selectSignal(selectTechTalkLogs);
  readonly prLogs = this.store.selectSignal(selectPullRequestLogs);
  readonly developerPrMetrics = this.store.selectSignal(selectDeveloperPrMetrics);
  readonly currentRole = this.store.selectSignal(selectCurrentRole);
  readonly isDarkMode = this.store.selectSignal(selectIsDarkMode);

  readonly isSmAuthenticated = signal<boolean>(false);
  readonly isScrumMaster = computed(() => this.currentRole() === 'ScrumMaster' && this.isSmAuthenticated());
  readonly canEditOrDelete = computed(() => this.currentRole() === 'ScrumMaster' && this.isSmAuthenticated());

  readonly activeBlockersCount = this.store.selectSignal(selectActiveBlockersCount);
  readonly breachedBlockersCount = computed(() => this.blockers().filter(b => b.isSlaBreached).length);
  readonly currentMember = computed(() => {
    const list = this.members();
    return list.find(m => m.role === this.currentRole()) || list[0] || null;
  });

  readonly executiveReport = computed<ExecutiveReport | null>(() => {
    const active = this.activeSprint();
    const items = this.workItems();
    const delivered = items.filter(i => i.status === 'Done').reduce((acc, i) => acc + i.storyPoints, 0);
    const committed = active?.committedStoryPoints ?? 0;
    const inFlight = items.filter(i => i.status !== 'Done' && i.status !== 'Backlog').reduce((acc, i) => acc + i.storyPoints, 0);
    const activeBlockers = this.activeBlockersCount();

    const calcAvg = (vals: (number | undefined)[]) => {
      const valid = vals.filter((v): v is number => typeof v === 'number' && !isNaN(v) && v > 0);
      return valid.length > 0 ? Math.round((valid.reduce((a, b) => a + b, 0) / valid.length) * 10) / 10 : 0;
    };

    const avgPickup = calcAvg(items.map(i => i.pickupLatencyHours));
    const avgDev = calcAvg(items.map(i => i.devCycleTimeHours));
    const avgPrReview = calcAvg(items.map(i => i.prReviewLatencyHours));
    const avgPrMerge = calcAvg(items.map(i => i.prMergeLatencyHours));
    const avgQa = calcAvg(items.map(i => i.qaTestingLatencyHours));
    const avgTotalCycle = calcAvg(items.map(i => i.totalCycleTimeHours));

    const blockers = this.blockers();
    const resolvedBlockers = blockers.filter(b => b.isResolved);
    const avgBlockerResolution = calcAvg(resolvedBlockers.map(b => b.hoursWaiting));

    return {
      sprintId: active?.id || 'all',
      sprintName: active?.name || 'Sprint Board',
      sprintGoal: active?.goal || 'Sprint Objectives & Velocity Deliverables',
      sayDoRatioPercentage: committed > 0 ? Math.min(100, Math.round((delivered / committed) * 100)) : 0,
      committedPoints: committed,
      deliveredPoints: delivered,
      inFlightPoints: inFlight,
      avgPickupLatencyHours: avgPickup,
      avgDevTimeHours: avgDev,
      avgPrReviewHours: avgPrReview,
      avgPrMergeHours: avgPrMerge,
      avgQaTestingHours: avgQa,
      avgTotalCycleTimeHours: avgTotalCycle,
      activeBlockersCount: activeBlockers,
      avgBlockerResolutionHours: avgBlockerResolution,
      escapedDefectsCount: items.filter(i => i.isEscapedDefect).length,
      inSprintBugsCount: items.filter(i => i.type === 'Bug').length,
      executiveSummaryMarkdown: `### Executive Sprint Governance Summary\n- **Delivered Velocity:** ${delivered} Story Points completed.\n- **Active In-Flight:** ${inFlight} Story Points.\n- **Blocker Resolution:** ${activeBlockers} active blockers currently under SLA monitoring.`
    };
  });

  readonly sayDoRatio = computed(() => this.executiveReport()?.sayDoRatioPercentage ?? 0);

  constructor() {
    this.loadTeams();
    this.refreshAllData();
  }

  refreshAllData(): void {
    this.store.dispatch(SprintActions.loadSprints());
    this.store.dispatch(TeamMemberActions.loadTeamMembers());
    this.store.dispatch(WorkItemActions.loadWorkItems({}));
    this.store.dispatch(BlockerActions.loadBlockers());
    this.store.dispatch(StandupActions.loadStandups());
    this.store.dispatch(LeaveActions.loadLeaves());
    this.store.dispatch(ReviewActions.loadFeedbacks());
    this.store.dispatch(RetroActions.loadRetros({}));
    this.store.dispatch(KudosActions.loadKudos());
    this.store.dispatch(TechHubActions.loadTechHub());
    this.store.dispatch(PullRequestActions.loadPullRequests({}));
    this.store.dispatch(PullRequestActions.loadDeveloperPrMetrics({}));
  }

  loadSprintData(sprintId: string): void {
    this.store.dispatch(LeaveActions.loadCapacity({ sprintId }));
    this.store.dispatch(WorkItemActions.loadWorkItems({ sprintId }));
    this.store.dispatch(RetroActions.loadRetros({ sprintId }));
    this.store.dispatch(PullRequestActions.loadPullRequests({ sprintId }));
    this.store.dispatch(PullRequestActions.loadDeveloperPrMetrics({ sprintId }));
  }

  // Sprints
  createSprint(sprint: Partial<Sprint>): void {
    this.store.dispatch(SprintActions.createSprint({ sprint }));
  }

  updateSprint(id: string, sprint: Partial<Sprint>): void {
    this.store.dispatch(SprintActions.updateSprint({ id, sprint }));
  }

  activateSprint(sprintId: string): void {
    this.store.dispatch(SprintActions.activateSprint({ sprintId }));
  }

  deleteSprint(sprintId: string): void {
    this.store.dispatch(SprintActions.deleteSprint({ sprintId }));
  }

  // Work Items
  createWorkItem(item: any): void {
    this.store.dispatch(WorkItemActions.createWorkItem({ item }));
  }

  updateWorkItem(id: string, item: any): void {
    this.store.dispatch(WorkItemActions.updateWorkItem({ id, item }));
  }

  deleteWorkItem(id: string): void {
    this.store.dispatch(WorkItemActions.deleteWorkItem({ id }));
  }

  advanceStage(id: string, targetStatus: string): void {
    this.store.dispatch(WorkItemActions.advanceWorkItemStage({ id, targetStatus }));
  }

  updateQualityGates(id: string, gates: any): void {
    this.store.dispatch(WorkItemActions.updateQualityGates({ id, gates }));
  }

  // Team Members
  createTeamMember(member: Partial<TeamMember>): void {
    if (!member.teamId && this.currentTeam()?.id) {
      member.teamId = this.currentTeam()!.id;
    }
    this.store.dispatch(TeamMemberActions.createTeamMember({ member }));
  }

  assignMemberSquad(memberId: string, teamId: string | null): Observable<TeamMember> {
    return this.http.put<TeamMember>(`${this.apiUrl}/teammembers/${memberId}/squad`, { teamId }).pipe(
      tap(() => this.store.dispatch(TeamMemberActions.loadTeamMembers()))
    );
  }

  deleteTeamMember(id: string): void {
    this.store.dispatch(TeamMemberActions.deleteTeamMember({ id }));
  }

  // Pull Requests & Review Analytics
  createPullRequestLog(request: any): void {
    this.store.dispatch(PullRequestActions.createPullRequestLog({ request }));
  }

  deletePullRequestLog(id: string): void {
    this.store.dispatch(PullRequestActions.deletePullRequestLog({ id }));
  }

  loadDeveloperPrMetrics(sprintId?: string): void {
    this.store.dispatch(PullRequestActions.loadDeveloperPrMetrics({ sprintId }));
  }

  // Blockers
  createBlocker(blocker: any): void {
    this.store.dispatch(BlockerActions.createBlocker({ blocker }));
  }

  updateBlocker(id: string, blocker: any): void {
    this.store.dispatch(BlockerActions.updateBlocker({ id, blocker }));
  }

  deleteBlocker(id: string): void {
    this.store.dispatch(BlockerActions.deleteBlocker({ id }));
  }

  resolveBlocker(id: string, notes?: string): void {
    this.store.dispatch(BlockerActions.resolveBlocker({ id, notes }));
  }

  // Standup
  submitStandup(standup: any): void {
    this.store.dispatch(StandupActions.submitStandup({ standup }));
  }

  updateStandup(id: string, standup: any): void {
    this.store.dispatch(StandupActions.updateStandup({ id, standup }));
  }

  deleteStandup(id: string): void {
    this.store.dispatch(StandupActions.deleteStandup({ id }));
  }

  clearAllStandups(): void {
    this.store.dispatch(StandupActions.clearAllStandups());
  }

  // Leaves
  submitLeave(leave: any): void {
    this.store.dispatch(LeaveActions.submitLeave({ leave }));
  }

  updateLeave(id: string, leave: any): void {
    this.store.dispatch(LeaveActions.updateLeave({ id, leave }));
  }

  deleteLeave(id: string): void {
    this.store.dispatch(LeaveActions.deleteLeave({ id }));
  }

  // 1on1 Reviews
  submitFeedback(feedback: any): void {
    this.store.dispatch(ReviewActions.submitFeedback({ feedback }));
  }

  submitMonthlyFeedback(feedback: any): void {
    this.submitFeedback(feedback);
  }

  updateMonthlyFeedback(id: string, feedback: any): void {
    this.store.dispatch(ReviewActions.updateFeedback({ id, feedback }));
  }

  deleteMonthlyFeedback(id: string): void {
    this.store.dispatch(ReviewActions.deleteFeedback({ id }));
  }

  // Retrospectives
  createRetroCard(card: any): void {
    this.store.dispatch(RetroActions.createRetroCard({ card }));
  }

  updateRetroCard(id: string, card: any): void {
    this.store.dispatch(RetroActions.updateRetroCard({ id, card }));
  }

  deleteRetroCard(id: string): void {
    this.store.dispatch(RetroActions.deleteRetroCard({ id }));
  }

  voteRetroCard(id: string): void {
    this.store.dispatch(RetroActions.voteRetroCard({ id }));
  }

  createRetroAction(action: any): void {
    this.store.dispatch(RetroActions.createRetroAction({ action }));
  }

  updateRetroAction(id: string, action: any): void {
    this.store.dispatch(RetroActions.updateRetroAction({ id, action }));
  }

  deleteRetroAction(id: string): void {
    this.store.dispatch(RetroActions.deleteRetroAction({ id }));
  }

  toggleRetroAction(id: string): void {
    this.store.dispatch(RetroActions.toggleRetroAction({ id }));
  }

  // Kudos
  giveKudos(kudos: any): void {
    this.store.dispatch(KudosActions.giveKudos({ kudos }));
  }

  sendKudos(kudos: any): void {
    this.giveKudos(kudos);
  }

  addKudosReaction(id: string, reactionKey: string): void {
    this.store.dispatch(KudosActions.addKudosReaction({ id, reactionKey }));
  }

  // Tech Hub
  createTechDebt(item: any): void {
    this.store.dispatch(TechHubActions.createTechDebt({ item }));
  }

  updateTechDebt(id: string, item: any): void {
    this.store.dispatch(TechHubActions.updateTechDebt({ id, item }));
  }

  deleteTechDebt(id: string): void {
    this.store.dispatch(TechHubActions.deleteTechDebt({ id }));
  }

  resolveTechDebt(id: string, status: string = 'Resolved'): void {
    this.store.dispatch(TechHubActions.resolveTechDebt({ id, status }));
  }

  logTechTalk(log: any): void {
    this.store.dispatch(TechHubActions.logTechTalk({ log }));
  }

  createTechTalk(log: any): void {
    this.logTechTalk(log);
  }

  updateTechTalk(id: string, log: any): void {
    this.store.dispatch(TechHubActions.updateTechTalk({ id, log }));
  }

  deleteTechTalk(id: string): void {
    this.store.dispatch(TechHubActions.deleteTechTalk({ id }));
  }

  // UI Preferences
  setCurrentRole(role: RoleType): void {
    try {
      localStorage.setItem('scrumpulse_current_role', role);
    } catch (err) {
      console.warn('[ScrumStateService] Could not persist current role to localStorage:', err);
    }
    this.store.dispatch(TeamMemberActions.setCurrentRole({ role }));
  }

  verifyAndUnlockSm(pin: string): Observable<boolean> {
    return this.http.post<{ success: boolean; message: string }>(`${this.apiUrl}/auth/verify-pin`, { pin }).pipe(
      map(res => {
        if (res && res.success) {
          this.isSmAuthenticated.set(true);
          this.setCurrentRole('ScrumMaster');
          return true;
        }
        return false;
      }),
      catchError(err => {
        console.error('[ScrumStateService] Failed to verify SM PIN:', err);
        return of(false);
      })
    );
  }

  lockSmSession(): void {
    this.isSmAuthenticated.set(false);
    this.setCurrentRole('Developer');
  }

  toggleDarkMode(): void {
    this.store.dispatch(TeamMemberActions.toggleDarkMode());
  }

  // AI & Reports queries
  getIndividualAi(teamMemberId: string): Observable<AiSuggestionResponse> {
    return this.generateAiSuggestions('Individual', teamMemberId);
  }

  getProjectAi(sprintId?: string): Observable<AiSuggestionResponse> {
    return this.generateAiSuggestions('Project', undefined, sprintId);
  }

  getCompanyAi(): Observable<AiSuggestionResponse> {
    return this.generateAiSuggestions('Company');
  }

  generateAiSuggestions(level: 'Individual' | 'Project' | 'Company', teamMemberId?: string, sprintId?: string): Observable<AiSuggestionResponse> {
    return this.http.post<AiSuggestionResponse>(`${this.apiUrl}/aicoach/suggest`, { level, teamMemberId, sprintId });
  }

  askCopilot(prompt: string, context?: any): Observable<CopilotChatResponse> {
    return this.http.post<CopilotChatResponse>(`${this.apiUrl}/aicoach/ask`, { prompt, context });
  }

  getExecutiveReport(sprintId: string): Observable<ExecutiveReport> {
    return this.http.get<ExecutiveReport>(`${this.apiUrl}/metrics/sprint/${sprintId}/report`);
  }

  getVelocityTrend(count: number = 6): Observable<SprintVelocityTrend> {
    return this.http.get<SprintVelocityTrend>(`${this.apiUrl}/executive-reports/velocity-trend?count=${count}`);
  }

  getSprintHealth(sprintId: string): Observable<SprintHealth> {
    return this.http.get<SprintHealth>(`${this.apiUrl}/executive-reports/sprint/${sprintId}/health`);
  }

  // Multi-Team Tenant Management
  loadTeams(): void {
    this.http.get<Team[]>(`${this.apiUrl}/teams`).pipe(
      catchError(err => {
        console.error('[ScrumStateService] Failed to load teams from API:', err);
        return of([]);
      })
    ).subscribe({
      next: list => {
        this.teams.set(list);
        let savedId: string | null = null;
        try {
          savedId = localStorage.getItem('scrumpulse_current_team_id');
        } catch (storageErr) {
          console.warn('[ScrumStateService] Could not read saved team ID from localStorage:', storageErr);
        }
        const found = list.find(t => t.id === savedId) || list[0] || null;
        if (found && (!this.currentTeam() || this.currentTeam()?.id !== found.id)) {
          this.selectTeam(found, false);
        }
      },
      error: err => {
        console.error('[ScrumStateService] Error subscribing to teams stream:', err);
      }
    });
  }

  selectTeam(team: Team | null, triggerReload: boolean = true): void {
    this.currentTeam.set(team);
    try {
      if (team) {
        localStorage.setItem('scrumpulse_current_team_id', team.id);
      } else {
        localStorage.removeItem('scrumpulse_current_team_id');
      }
    } catch (err) {
      console.warn('[ScrumStateService] Could not update team in localStorage:', err);
    }
    if (triggerReload) {
      this.refreshAllData();
    }
  }

  createTeam(request: CreateTeamRequest): Observable<Team> {
    return this.http.post<Team>(`${this.apiUrl}/teams`, request).pipe(
      map(team => {
        this.teams.update(list => [...list, team]);
        this.selectTeam(team);
        return team;
      })
    );
  }

  joinTeam(request: JoinTeamRequest): Observable<Team> {
    return this.http.post<Team>(`${this.apiUrl}/teams/join`, request).pipe(
      map(team => {
        if (!this.teams().some(t => t.id === team.id)) {
          this.teams.update(list => [...list, team]);
        }
        this.selectTeam(team);
        return team;
      })
    );
  }

  compareSprints(sprintA: string, sprintB: string): Observable<SprintComparison> {
    return this.http.get<SprintComparison>(`${this.apiUrl}/executive-reports/compare?sprintA=${sprintA}&sprintB=${sprintB}`);
  }

  // ── Team Performance & Growth ──────────────────────────────────────
  getTeamPerformanceSummary(sprintCount: number = 6): Observable<TeamPerformanceSummary> {
    return this.http.get<TeamPerformanceSummary>(`${this.apiUrl}/team-performance/summary?sprintCount=${sprintCount}`);
  }

  getTeamHighlights(sprintCount: number = 6): Observable<TeamHighlight[]> {
    return this.http.get<TeamHighlight[]>(`${this.apiUrl}/team-performance/highlights?sprintCount=${sprintCount}`);
  }

  getGrowthTrend(sprintCount: number = 8): Observable<SprintGrowthSnapshot[]> {
    return this.http.get<SprintGrowthSnapshot[]>(`${this.apiUrl}/team-performance/growth-trend?sprintCount=${sprintCount}`);
  }

  exportSprintCsv(sprintId: string): void {
    window.open(`${this.apiUrl}/executive-reports/sprint/${sprintId}/export-csv`, '_blank');
  }

  exportDataJson(): void {
    const data = {
      sprints: this.sprints(),
      members: this.members(),
      workItems: this.workItems(),
      prLogs: this.prLogs(),
      developerPrMetrics: this.developerPrMetrics(),
      blockers: this.blockers(),
      exportedAt: new Date().toISOString()
    };
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `ScrumPulse_Export_${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
