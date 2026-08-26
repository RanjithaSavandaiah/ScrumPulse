import { ActionReducerMap } from '@ngrx/store';
import { sprintsReducer, SprintsState } from './sprints/sprints.reducer';
import { workItemsReducer, WorkItemsState } from './work-items/work-items.reducer';
import { pullRequestsReducer, PullRequestsState } from './pull-requests/pull-requests.reducer';
import { teamMembersReducer, TeamMembersState } from './team-members/team-members.reducer';
import { blockersReducer, BlockersState } from './blockers/blockers.reducer';
import { standupsReducer, StandupsState } from './standups/standups.reducer';
import { leavesReducer, LeavesState } from './leaves/leaves.reducer';
import { reviewsReducer, ReviewsState } from './reviews/reviews.reducer';
import { retrosReducer, RetrosState } from './retros/retros.reducer';
import { kudosReducer, KudosState } from './kudos/kudos.reducer';
import { techHubReducer, TechHubState } from './tech-hub/tech-hub.reducer';

import { SprintsEffects } from './sprints/sprints.effects';
import { WorkItemsEffects } from './work-items/work-items.effects';
import { PullRequestsEffects } from './pull-requests/pull-requests.effects';
import { TeamMembersEffects } from './team-members/team-members.effects';
import { BlockersEffects } from './blockers/blockers.effects';
import { StandupsEffects } from './standups/standups.effects';
import { LeavesEffects } from './leaves/leaves.effects';
import { ReviewsEffects } from './reviews/reviews.effects';
import { RetrosEffects } from './retros/retros.effects';
import { KudosEffects } from './kudos/kudos.effects';
import { TechHubEffects } from './tech-hub/tech-hub.effects';

export interface AppState {
  sprints: SprintsState;
  workItems: WorkItemsState;
  pullRequests: PullRequestsState;
  teamMembers: TeamMembersState;
  blockers: BlockersState;
  standups: StandupsState;
  leaves: LeavesState;
  reviews: ReviewsState;
  retros: RetrosState;
  kudos: KudosState;
  techHub: TechHubState;
}

export const appReducers: ActionReducerMap<AppState> = {
  sprints: sprintsReducer,
  workItems: workItemsReducer,
  pullRequests: pullRequestsReducer,
  teamMembers: teamMembersReducer,
  blockers: blockersReducer,
  standups: standupsReducer,
  leaves: leavesReducer,
  reviews: reviewsReducer,
  retros: retrosReducer,
  kudos: kudosReducer,
  techHub: techHubReducer
};

export const appEffects = [
  SprintsEffects,
  WorkItemsEffects,
  PullRequestsEffects,
  TeamMembersEffects,
  BlockersEffects,
  StandupsEffects,
  LeavesEffects,
  ReviewsEffects,
  RetrosEffects,
  KudosEffects,
  TechHubEffects
];

// Re-export all feature actions and selectors
export * from './sprints/sprints.actions';
export * from './sprints/sprints.selectors';
export * from './work-items/work-items.actions';
export * from './work-items/work-items.selectors';
export * from './pull-requests/pull-requests.actions';
export * from './pull-requests/pull-requests.selectors';
export * from './team-members/team-members.actions';
export * from './team-members/team-members.selectors';
export * from './blockers/blockers.actions';
export * from './blockers/blockers.selectors';
export * from './standups/standups.actions';
export * from './standups/standups.selectors';
export * from './leaves/leaves.actions';
export * from './leaves/leaves.selectors';
export * from './reviews/reviews.actions';
export * from './reviews/reviews.selectors';
export * from './retros/retros.actions';
export * from './retros/retros.selectors';
export * from './kudos/kudos.actions';
export * from './kudos/kudos.selectors';
export * from './tech-hub/tech-hub.actions';
export * from './tech-hub/tech-hub.selectors';
