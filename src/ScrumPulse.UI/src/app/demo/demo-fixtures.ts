import {
  Blocker,
  DailyStandup,
  Sprint,
  TeamMember,
  WorkItem
} from '../core/models/scrum.models';

/**
 * Fictional squad data backing the public demo.
 *
 * Every person, sprint and ticket here is invented. No real squad's data is exposed by the
 * demo, which is the whole reason it exists: a reviewer or prospective user can see a
 * populated product without anyone's standups or one-to-one feedback being made public.
 *
 * The numbers are deliberately imperfect. A demo where every sprint lands exactly on its
 * commitment and no blocker breaches SLA demonstrates nothing, because the product exists to
 * surface exactly those problems. This squad is doing reasonably well and has a review
 * bottleneck and one long-running client dependency, which is what the metrics should reveal.
 */

/** Fixed clock so the demo reads the same on every visit and the relative times make sense. */
const DEMO_NOW = new Date();

function hoursAgo(hours: number): string {
  return new Date(DEMO_NOW.getTime() - hours * 60 * 60 * 1000).toISOString();
}

function daysAgo(days: number): string {
  return hoursAgo(days * 24);
}

function daysAhead(days: number): string {
  return new Date(DEMO_NOW.getTime() + days * 24 * 60 * 60 * 1000).toISOString();
}

export const DEMO_TEAM_ID = '00000000-0000-4000-8000-00000000d3m0';

export const DEMO_MEMBERS: TeamMember[] = [
  {
    id: 'demo-member-1',
    name: 'Anita Raghavan',
    email: 'anita@example.invalid',
    role: 'ScrumMaster',
    location: 'Bengaluru',
    timeZone: 'Asia/Kolkata',
    avatar: 'AR',
    activeWipLimit: 3,
    teamId: DEMO_TEAM_ID,
    isActive: true
  },
  {
    id: 'demo-member-2',
    name: 'Tom Whitfield',
    email: 'tom@example.invalid',
    role: 'Developer',
    location: 'Manchester',
    timeZone: 'Europe/London',
    avatar: 'TW',
    activeWipLimit: 3,
    teamId: DEMO_TEAM_ID,
    isActive: true
  },
  {
    id: 'demo-member-3',
    name: 'Priya Desai',
    email: 'priya@example.invalid',
    role: 'Developer',
    location: 'Bengaluru',
    timeZone: 'Asia/Kolkata',
    avatar: 'PD',
    activeWipLimit: 3,
    teamId: DEMO_TEAM_ID,
    isActive: true
  },
  {
    id: 'demo-member-4',
    name: 'Марко Ilić',
    email: 'marko@example.invalid',
    role: 'QaEngineer',
    location: 'Belgrade',
    timeZone: 'Europe/Belgrade',
    avatar: 'MI',
    activeWipLimit: 4,
    teamId: DEMO_TEAM_ID,
    isActive: true
  }
];

export const DEMO_SPRINTS: Sprint[] = [
  {
    id: 'demo-sprint-current',
    name: 'Sprint 24 - Checkout hardening',
    goal: 'Cut payment retry failures and clear the refunds backlog.',
    startDate: daysAgo(6),
    endDate: daysAhead(8),
    isActive: true,
    committedStoryPoints: 34,
    deliveredStoryPoints: 19,
    confidenceScore: 7,
    confidenceNotes: 'Refund edge cases are larger than estimated.',
    dailyWorkingHours: 8.5
  },
  {
    id: 'demo-sprint-previous',
    name: 'Sprint 23 - Payment retries',
    goal: 'Introduce idempotent retries on the payment gateway.',
    startDate: daysAgo(20),
    endDate: daysAgo(7),
    isActive: false,
    committedStoryPoints: 31,
    deliveredStoryPoints: 28,
    confidenceScore: 8,
    dailyWorkingHours: 8.5
  }
];

/**
 * Work items spread across the pipeline, with latency figures that tell a story: development
 * is quick, review is where items sit. That is the pattern the cycle-time breakdown exists to
 * expose, so the demo should show it rather than a uniformly healthy board.
 */
export const DEMO_WORK_ITEMS: WorkItem[] = [
  {
    id: 'demo-item-1',
    key: 'SP-241',
    title: 'Retry failed card authorisations with idempotency key',
    description: 'Gateway occasionally double-charges on retry. Needs an idempotency key per attempt.',
    type: 'UserStory',
    status: 'Done',
    priority: 'High',
    storyPoints: 8,
    assigneeId: 'demo-member-2',
    assigneeName: 'Tom Whitfield',
    sprintId: 'demo-sprint-current',
    teamId: DEMO_TEAM_ID,
    prNumber: '418',
    prUrl: 'https://example.invalid/pulls/418',
    prReviewerId: 'demo-member-3',
    prReviewerName: 'Priya Desai',
    createdAtUtc: daysAgo(6),
    pickedUpAtUtc: daysAgo(6),
    prCreatedAtUtc: daysAgo(4),
    prApprovedAtUtc: daysAgo(3),
    prMergedAtUtc: daysAgo(3),
    qaStartedAtUtc: daysAgo(3),
    completedAtUtc: daysAgo(2),
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: true,
    dodUnitTestsPassed: true,
    dodPeerReviewCompleted: true,
    dodMergedToMaster: true,
    dodStagingVerified: true,
    isEscapedDefect: false,
    devCycleTimeHours: 14.5,
    prReviewLatencyHours: 9.5,
    totalCycleTimeHours: 38.0
  },
  {
    id: 'demo-item-2',
    key: 'SP-244',
    title: 'Refund partial amounts against a settled order',
    description: 'Finance needs partial refunds without reversing the whole settlement.',
    type: 'UserStory',
    status: 'PrCreated',
    priority: 'High',
    storyPoints: 13,
    assigneeId: 'demo-member-3',
    assigneeName: 'Priya Desai',
    sprintId: 'demo-sprint-current',
    teamId: DEMO_TEAM_ID,
    prNumber: '423',
    prUrl: 'https://example.invalid/pulls/423',
    createdAtUtc: daysAgo(5),
    pickedUpAtUtc: daysAgo(4),
    prCreatedAtUtc: hoursAgo(26),
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: false,
    dodUnitTestsPassed: true,
    dodPeerReviewCompleted: false,
    dodMergedToMaster: false,
    dodStagingVerified: false,
    isEscapedDefect: false,
    devCycleTimeHours: 22.0,
    prReviewLatencyHours: 26.0
  },
  {
    id: 'demo-item-3',
    key: 'SP-247',
    title: 'Duplicate confirmation email on retried payment',
    description: 'Customers receive two confirmations when the first authorisation times out.',
    type: 'Bug',
    status: 'InProgress',
    priority: 'Medium',
    storyPoints: 3,
    assigneeId: 'demo-member-2',
    assigneeName: 'Tom Whitfield',
    sprintId: 'demo-sprint-current',
    teamId: DEMO_TEAM_ID,
    createdAtUtc: daysAgo(2),
    pickedUpAtUtc: hoursAgo(20),
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: true,
    dodUnitTestsPassed: false,
    dodPeerReviewCompleted: false,
    dodMergedToMaster: false,
    dodStagingVerified: false,
    isEscapedDefect: true,
    defectRootCause: 'Missing idempotency check on the notification path.'
  },
  {
    id: 'demo-item-4',
    key: 'SP-249',
    title: 'Document the refund reconciliation runbook',
    description: 'Support needs a written process for mismatched settlements.',
    type: 'TaskPbi',
    status: 'Backlog',
    priority: 'Low',
    storyPoints: 2,
    sprintId: 'demo-sprint-current',
    teamId: DEMO_TEAM_ID,
    createdAtUtc: daysAgo(1),
    dorAcceptanceCriteriaDefined: false,
    dorDependenciesIdentified: false,
    dorWireframeAvailable: false,
    dodUnitTestsPassed: false,
    dodPeerReviewCompleted: false,
    dodMergedToMaster: false,
    dodStagingVerified: false,
    isEscapedDefect: false
  }
];

export const DEMO_BLOCKERS: Blocker[] = [
  {
    id: 'demo-blocker-1',
    title: 'Awaiting refund policy confirmation from the client',
    description: 'Partial refunds above £500 may need manual approval. Unconfirmed.',
    category: 'ClientClarification',
    slaHoursLimit: 8,
    workItemId: 'demo-item-2',
    workItemKey: 'SP-244',
    raisedById: 'demo-member-3',
    raisedByName: 'Priya Desai',
    sprintId: 'demo-sprint-current',
    raisedAtUtc: hoursAgo(31),
    isResolved: false,
    hoursWaiting: 31,
    isSlaBreached: true
  },
  {
    id: 'demo-blocker-2',
    title: 'Sandbox gateway credentials expired',
    description: 'Payment sandbox rejecting all calls since the key rotation.',
    category: 'EnvironmentAccess',
    slaHoursLimit: 8,
    raisedById: 'demo-member-2',
    raisedByName: 'Tom Whitfield',
    sprintId: 'demo-sprint-current',
    raisedAtUtc: daysAgo(4),
    resolvedAtUtc: daysAgo(4),
    resolutionNotes: 'Platform team reissued the sandbox key.',
    isResolved: true,
    hoursWaiting: 5.5,
    isSlaBreached: false,
    blockedHours: 5.5
  }
];

export const DEMO_STANDUPS: DailyStandup[] = [
  {
    id: 'demo-standup-1',
    teamMemberId: 'demo-member-2',
    teamMemberName: 'Tom Whitfield',
    teamMemberAvatar: 'TW',
    standupDate: DEMO_NOW.toISOString(),
    yesterdaySummary: 'Finished the idempotency key work on SP-241 and got it merged.',
    todayPlan: 'Picking up the duplicate confirmation email bug.',
    moodScore: 4,
    sprintId: 'demo-sprint-current'
  },
  {
    id: 'demo-standup-2',
    teamMemberId: 'demo-member-3',
    teamMemberName: 'Priya Desai',
    teamMemberAvatar: 'PD',
    standupDate: DEMO_NOW.toISOString(),
    yesterdaySummary: 'Raised the PR for partial refunds, waiting on review.',
    todayPlan: 'Chasing the refund policy answer so SP-244 can move.',
    blockersText: 'Still blocked on the client confirming the approval threshold.',
    moodScore: 2,
    sprintId: 'demo-sprint-current'
  },
  {
    id: 'demo-standup-3',
    teamMemberId: 'demo-member-4',
    teamMemberName: 'Марко Ilić',
    teamMemberAvatar: 'MI',
    standupDate: DEMO_NOW.toISOString(),
    yesterdaySummary: 'Verified SP-241 on staging, no regressions on the retry path.',
    todayPlan: 'Writing the regression pack for partial refunds ahead of SP-244.',
    moodScore: 4,
    sprintId: 'demo-sprint-current'
  }
];
