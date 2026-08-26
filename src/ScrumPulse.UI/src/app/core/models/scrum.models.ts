export type RoleType = 'ScrumMaster' | 'Developer' | 'QaEngineer' | 'Cdl' | 'ClientStakeholder';
export type WorkItemType = 'UserStory' | 'Bug' | 'TaskPbi' | 'TechDebtSpike';
export type WorkItemStatus = 'Backlog' | 'InProgress' | 'PrCreated' | 'PrApproved' | 'Merged' | 'InQa' | 'Done';
export type PriorityLevel = 'Low' | 'Medium' | 'High' | 'Critical';
export type BlockerCategory = 'ClientClarification' | 'TechLeadArchitecture' | 'EnvironmentAccess' | 'ThirdPartyApi';
export type BadgeType = 'ProblemSolver' | 'TeamPlayer' | 'GoalCrusher' | 'QualityGuardian' | 'InnovationStar' | 'ClientShoutout';
export type RetroCategory = 'WentWell' | 'DidntGoWell' | 'Ideas' | 'ActionItem';

export interface TeamMember {
  id: string;
  name: string;
  email: string;
  role: RoleType;
  location: string;
  timeZone: string;
  avatar: string;
  activeWipLimit: number;
  isActive?: boolean;
}

export interface Sprint {
  id: string;
  name: string;
  goal: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
  committedStoryPoints: number;
  deliveredStoryPoints: number;
  confidenceScore: number;
  confidenceNotes?: string;
}

export interface WorkItem {
  id: string;
  key: string;
  title: string;
  description: string;
  type: WorkItemType;
  status: WorkItemStatus;
  priority: PriorityLevel;
  storyPoints: number;
  assigneeId?: string;
  assigneeName?: string;
  sprintId?: string;
  prNumber?: string;
  prUrl?: string;
  prBranch?: string;
  targetBranch?: string;
  prReviewerId?: string;
  prReviewerName?: string;
  createdAtUtc: string;
  pickedUpAtUtc?: string;
  prCreatedAtUtc?: string;
  prApprovedAtUtc?: string;
  prMergedAtUtc?: string;
  qaStartedAtUtc?: string;
  completedAtUtc?: string;
  dorAcceptanceCriteriaDefined: boolean;
  dorDependenciesIdentified: boolean;
  dorWireframeAvailable: boolean;
  dodUnitTestsPassed: boolean;
  dodPeerReviewCompleted: boolean;
  dodMergedToMaster: boolean;
  dodStagingVerified: boolean;
  isEscapedDefect: boolean;
  defectRootCause?: string;
  pickupLatencyHours?: number;
  devCycleTimeHours?: number;
  prReviewLatencyHours?: number;
  prMergeLatencyHours?: number;
  qaTestingLatencyHours?: number;
  totalCycleTimeHours?: number;
  estimatedHours?: number;
}

export interface Blocker {
  id: string;
  title: string;
  description: string;
  category: BlockerCategory;
  slaHoursLimit: number;
  workItemId?: string;
  workItemKey?: string;
  raisedById: string;
  raisedByName?: string;
  sprintId?: string;
  raisedAtUtc: string;
  resolvedAtUtc?: string;
  resolutionNotes?: string;
  isResolved: boolean;
  hoursWaiting: number;
  isSlaBreached: boolean;
}

export interface DailyStandup {
  id: string;
  teamMemberId: string;
  teamMemberName: string;
  teamMemberAvatar: string;
  standupDate: string;
  yesterdaySummary: string;
  todayPlan: string;
  blockersText?: string;
  moodScore: number;
  sprintId?: string;
}

export interface TeamLeave {
  id: string;
  teamMemberId: string;
  teamMemberName: string;
  startDate: string;
  endDate: string;
  reason: string;
  leaveType: string;
  location: string;
  isApproved: boolean;
  totalDays: number;
  leaveSlot?: string;
}

export interface SprintCapacity {
  sprintId: string;
  sprintName: string;
  totalWorkingDays: number;
  totalTeamMembers: number;
  totalLeaveDays: number;
  totalAvailableHours: number;
  recommendedStoryPoints: number;
  committedStoryPoints: number;
  memberBreakdown: {
    memberId: string;
    memberName: string;
    workingDays: number;
    leaveDays: number;
    availableHours: number;
    suggestedPoints: number;
  }[];
}

export interface MonthlyFeedback {
  id: string;
  teamMemberId: string;
  teamMemberName: string;
  monthYear: string;
  scrumMasterFeedback: string;
  cdlFeedback: string;
  clientFeedback: string;
  selfReflection: string;
  smRating: number;
  happinessIndex: number;
  actionItems: string;
  nextMonthGoals: string;
  aiSynthesizedStrengths?: string;
  aiGrowthRecommendations?: string;
  aiBurnoutRiskAssessment?: string;
  createdAtUtc: string;
}

export interface RetroCard {
  id: string;
  sprintId?: string;
  category: RetroCategory;
  content: string;
  authorId?: string;
  authorName?: string;
  isAnonymous: boolean;
  upvotesCount: number;
}

export interface RetroActionItem {
  id: string;
  sprintId?: string;
  title: string;
  assigneeId?: string;
  assigneeName?: string;
  dueDate?: string;
  isCompleted: boolean;
}

export interface KudosCard {
  id: string;
  senderId: string;
  senderName: string;
  receiverId: string;
  receiverName: string;
  badge: BadgeType;
  message: string;
  reactionEmojis: Record<string, number>;
  createdAtUtc: string;
}

export interface TechDebtItem {
  id: string;
  title: string;
  description: string;
  severity: string;
  estimatedHours: number;
  status: string;
  payoffSprintId?: string;
  assigneeId?: string;
  assigneeName?: string;
}

export interface TechTalkLog {
  id: string;
  topic: string;
  presenterId: string;
  presenterName?: string;
  talkDate: string;
  durationMinutes: number;
  keyTakeaways?: string;
  slidesUrl?: string;
}

export interface AiSuggestionResponse {
  level: 'Individual' | 'Project' | 'Company';
  title: string;
  summary: string;
  keyFindings: string[];
  actionableRecommendations: string[];
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  generatedAtUtc: string;
}

export interface CopilotChatResponse {
  answer: string;
  suggestedFollowUps: string[];
  timestampUtc: string;
}

export interface ExecutiveReport {
  sprintId: string;
  sprintName: string;
  sprintGoal: string;
  sayDoRatioPercentage: number;
  committedPoints: number;
  deliveredPoints: number;
  inFlightPoints: number;
  avgPickupLatencyHours: number;
  avgDevTimeHours: number;
  avgPrReviewHours: number;
  avgPrMergeHours: number;
  avgQaTestingHours: number;
  avgTotalCycleTimeHours: number;
  activeBlockersCount: number;
  avgBlockerResolutionHours: number;
  escapedDefectsCount: number;
  inSprintBugsCount: number;
  executiveSummaryMarkdown: string;
}

export interface PullRequestLog {
  id: string;
  workItemId?: string;
  workItemTitle?: string;
  authorId: string;
  authorName: string;
  reviewerId?: string;
  reviewerName?: string;
  sprintId?: string;
  sprintName?: string;
  prNumber: string;
  prTitle: string;
  prUrl: string;
  totalCommentsCount: number;
  actionableCommentsCount: number;
  reviewSummary: string;
  reviewStatus: string;
  createdAtUtc: string;
  mergedAtUtc?: string;
}

export interface DeveloperPrMetrics {
  developerId: string;
  developerName: string;
  developerRole: string;
  developerAvatar: string;
  totalPrsCreated: number;
  totalCommentsReceived: number;
  actionableCommentsReceived: number;
  actionabilityRatePercentage: number;
  avgCommentsPerPr: number;
  prs: PullRequestLog[];
}

