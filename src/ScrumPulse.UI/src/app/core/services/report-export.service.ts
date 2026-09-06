import { Injectable, inject } from '@angular/core';
import * as XLSX from 'xlsx';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import { ScrumStateService } from './scrum-state.service';
import { WorkItem, PullRequestLog, DailyStandup, TeamLeave, TeamMember, MonthlyFeedback, KudosCard, Sprint } from '../models/scrum.models';

export interface ExportFilterOptions {
  memberId: string; // 'ALL' or specific member ID
  timeScopeType: 'SPRINT' | 'MONTH' | 'QUARTER' | 'CUSTOM' | 'ALL';
  sprintId?: string;
  month?: string; // format: 'YYYY-MM' e.g. '2026-08'
  quarter?: string; // format: '2026-Q1', '2026-Q2', '2026-Q3', '2026-Q4'
  startDate?: string; // format: 'YYYY-MM-DD'
  endDate?: string; // format: 'YYYY-MM-DD'
}

import { cleanName, getRoleLabel } from '../utils/format-utils';

const PERCENTAGE_FACTOR = 100;
const MAX_STANDUPS_PDF_PREVIEW_COUNT = 8;

@Injectable({
  providedIn: 'root'
})
export class ReportExportService {
  private state = inject(ScrumStateService);

  private cleanName(name: string): string {
    return cleanName(name);
  }

  private getRoleLabel(role: string): string {
    return getRoleLabel(role);
  }

  private isDateInMonth(dateStr: string | Date | undefined | null, targetYearMonth: string): boolean {
    if (!dateStr || !targetYearMonth) return false;
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return false;
    const yearMonth = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    return yearMonth === targetYearMonth;
  }

  private isDateInQuarter(dateStr: string | Date | undefined | null, targetQuarter: string): boolean {
    if (!dateStr || !targetQuarter) return false;
    const parts = targetQuarter.split('-Q');
    if (parts.length !== 2) return false;
    const year = parseInt(parts[0], 10);
    const q = parseInt(parts[1], 10);
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return false;
    if (d.getFullYear() !== year) return false;
    const month = d.getMonth() + 1;
    const itemQ = Math.ceil(month / 3);
    return itemQ === q;
  }

  private isDateInRange(dateStr: string | Date | undefined | null, startDate?: string, endDate?: string): boolean {
    if (!dateStr) return false;
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return false;
    if (startDate) {
      const start = new Date(startDate);
      start.setHours(0, 0, 0, 0);
      if (d < start) return false;
    }
    if (endDate) {
      const end = new Date(endDate);
      end.setHours(23, 59, 59, 999);
      if (d > end) return false;
    }
    return true;
  }

  public filterData(options: ExportFilterOptions) {
    const members = this.state.squadMembers();
    const sprints = this.state.sprints();
    let selectedMember: TeamMember | null = null;

    if (options.memberId !== 'ALL') {
      selectedMember = members.find(member => member.id === options.memberId) || null;
    }

    // Filter Work Items
    let workItems = this.state.workItems();
    if (options.memberId !== 'ALL') {
      workItems = workItems.filter(item => item.assigneeId === options.memberId);
    }
    if (options.timeScopeType === 'SPRINT' && options.sprintId && options.sprintId !== 'ALL') {
      workItems = workItems.filter(item => item.sprintId === options.sprintId);
    } else if (options.timeScopeType === 'MONTH' && options.month) {
      workItems = workItems.filter(item => this.isDateInMonth(item.createdAtUtc || item.completedAtUtc, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      workItems = workItems.filter(item => this.isDateInQuarter(item.createdAtUtc || item.completedAtUtc, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      workItems = workItems.filter(item => this.isDateInRange(item.createdAtUtc || item.completedAtUtc, options.startDate, options.endDate));
    }

    // Filter PR Logs
    let prLogs = this.state.prLogs();
    if (options.memberId !== 'ALL') {
      prLogs = prLogs.filter(pr => pr.authorId === options.memberId);
    }
    if (options.timeScopeType === 'SPRINT' && options.sprintId && options.sprintId !== 'ALL') {
      prLogs = prLogs.filter(pr => pr.sprintId === options.sprintId);
    } else if (options.timeScopeType === 'MONTH' && options.month) {
      prLogs = prLogs.filter(pr => this.isDateInMonth(pr.createdAtUtc, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      prLogs = prLogs.filter(pr => this.isDateInQuarter(pr.createdAtUtc, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      prLogs = prLogs.filter(pr => this.isDateInRange(pr.createdAtUtc, options.startDate, options.endDate));
    }

    // Filter Standups
    let standups = this.state.standups();
    if (options.memberId !== 'ALL') {
      standups = standups.filter(standup => standup.teamMemberId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      standups = standups.filter(standup => this.isDateInMonth(standup.standupDate, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      standups = standups.filter(standup => this.isDateInQuarter(standup.standupDate, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      standups = standups.filter(standup => this.isDateInRange(standup.standupDate, options.startDate, options.endDate));
    }

    // Filter Leaves
    let leaves = this.state.leaves();
    if (options.memberId !== 'ALL') {
      leaves = leaves.filter(leave => leave.teamMemberId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      leaves = leaves.filter(leave => this.isDateInMonth(leave.startDate, options.month!) || this.isDateInMonth(leave.endDate, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      leaves = leaves.filter(leave => this.isDateInQuarter(leave.startDate, options.quarter!) || this.isDateInQuarter(leave.endDate, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      leaves = leaves.filter(leave => this.isDateInRange(leave.startDate, options.startDate, options.endDate) || this.isDateInRange(leave.endDate, options.startDate, options.endDate));
    }

    // Filter Monthly Reviews
    let reviews = this.state.monthlyFeedbacks();
    if (options.memberId !== 'ALL') {
      reviews = reviews.filter(review => review.teamMemberId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      reviews = reviews.filter(review => review.monthYear === options.month);
    } else if (options.timeScopeType === 'CUSTOM') {
      reviews = reviews.filter(review => this.isDateInRange(review.createdAtUtc, options.startDate, options.endDate));
    }

    // Filter Kudos
    let kudos = this.state.kudos();
    if (options.memberId !== 'ALL') {
      kudos = kudos.filter(kudosCard => kudosCard.receiverId === options.memberId || kudosCard.senderId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      kudos = kudos.filter(kudosCard => this.isDateInMonth(kudosCard.createdAtUtc, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      kudos = kudos.filter(kudosCard => this.isDateInQuarter(kudosCard.createdAtUtc, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      kudos = kudos.filter(kudosCard => this.isDateInRange(kudosCard.createdAtUtc, options.startDate, options.endDate));
    }

    // Filter Tech Talks
    let techTalks = this.state.techTalks();
    if (options.memberId !== 'ALL') {
      techTalks = techTalks.filter(talk => talk.presenterId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      techTalks = techTalks.filter(talk => this.isDateInMonth(talk.talkDate, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      techTalks = techTalks.filter(talk => this.isDateInQuarter(talk.talkDate, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      techTalks = techTalks.filter(talk => this.isDateInRange(talk.talkDate, options.startDate, options.endDate));
    }

    // Determine Scope Label
    let scopeLabel = 'All History';
    if (options.timeScopeType === 'SPRINT') {
      const sp = sprints.find(sprint => sprint.id === options.sprintId);
      scopeLabel = sp ? `Sprint: ${sp.name}` : 'All Sprints';
    } else if (options.timeScopeType === 'MONTH' && options.month) {
      scopeLabel = `Month: ${options.month}`;
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      scopeLabel = `Quarter: ${options.quarter}`;
    } else if (options.timeScopeType === 'CUSTOM') {
      const startText = options.startDate || 'Start';
      const endText = options.endDate || 'Present';
      scopeLabel = `Custom: ${startText} to ${endText}`;
    }

    const memberLabel = selectedMember ? `${this.cleanName(selectedMember.name)} (${this.getRoleLabel(selectedMember.role)})` : 'Entire Squad / All Developers';

    return {
      selectedMember,
      memberLabel,
      scopeLabel,
      workItems,
      prLogs,
      standups,
      leaves,
      reviews,
      kudos,
      techTalks
    };
  }

  private getBadgeLabel(badge: any): string {
    const labels = ['Problem Solver', 'Team Player', 'Goal Crusher', 'Quality Guardian', 'Innovation Star', 'Client Shoutout'];
    if (typeof badge === 'number') return labels[badge] || 'Kudos Recognition';
    return String(badge) || 'Kudos Recognition';
  }

  // ==========================================
  // EXCEL EXPORT (.xlsx Multi-Sheet Workbook)
  // ==========================================
  exportToExcel(options: ExportFilterOptions): void {
    const data = this.filterData(options);
    const wb = XLSX.utils.book_new();

    // 1. Summary Sheet
    const totalPoints = data.workItems.reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
    const donePoints = data.workItems.filter(item => String(item.status).toLowerCase().includes('done'))
      .reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
    const totalPrs = data.prLogs.length;
    const totalComments = data.prLogs.reduce((accumulatedComments, pr) => accumulatedComments + (pr.totalCommentsCount || 0), 0);
    const actionableComments = data.prLogs.reduce((accumulatedActionable, pr) => accumulatedActionable + (pr.actionableCommentsCount || 0), 0);
    const actionabilityRate = totalComments > 0 ? `${Math.round((actionableComments / totalComments) * PERCENTAGE_FACTOR)}%` : '0%';
    const totalLeaveDays = data.leaves.reduce((accumulatedDays, leave) => accumulatedDays + (leave.totalDays || 0), 0);
    const totalTalkMinutes = data.techTalks.reduce((accumulatedMinutes, talk) => accumulatedMinutes + (talk.durationMinutes || 0), 0);

    const summaryData = [
      ['SCRUMPULSE ENTERPRISE AGILE PERFORMANCE REPORT'],
      ['Generated At (UTC)', new Date().toISOString()],
      ['Target Member / Scope', data.memberLabel],
      ['Selected Time Horizon', data.scopeLabel],
      [''],
      ['KEY PERFORMANCE & GROWTH INDICATORS', 'VALUE'],
      ['Total Work Items Handled', data.workItems.length],
      ['Total Story Points Scope', totalPoints],
      ['Delivered Story Points', donePoints],
      ['Say-Do Velocity Rate', totalPoints > 0 ? `${Math.round((donePoints / totalPoints) * PERCENTAGE_FACTOR)}%` : '0%'],
      ['Pull Requests Authored', totalPrs],
      ['Review Discussions Received', totalComments],
      ['Actionable Code Review Feedback', actionableComments],
      ['Review Actionability Index', actionabilityRate],
      ['Daily Standups Logged', data.standups.length],
      ['Leaves / Planned PTO Records', data.leaves.length],
      ['Total Working Days on Leave', `${totalLeaveDays} days`],
      ['Monthly 1-on-1 Feedback Reviews', data.reviews.length],
      ['Peer Kudos Recognitions', data.kudos.length],
      ['Tech Talks Delivered / Knowledge Sessions', data.techTalks.length],
      ['Total Tech Talk Time (Minutes)', `${totalTalkMinutes} mins`]
    ];
    const wsSummary = XLSX.utils.aoa_to_sheet(summaryData);
    XLSX.utils.book_append_sheet(wb, wsSummary, 'Overview Summary');

    // 2. Work Items Sheet
    const workItemsRows = data.workItems.map(item => ({
      'Key': item.key,
      'Title': item.title,
      'Type': String(item.type),
      'Priority': String(item.priority),
      'Story Points': item.storyPoints,
      'Status': String(item.status),
      'Assignee': item.assigneeName || data.memberLabel,
      'PR Number': item.prNumber || 'N/A',
      'PR Branch': item.prBranch || 'N/A',
      'Target Branch': item.targetBranch || 'main',
      'Created At': item.createdAtUtc ? new Date(item.createdAtUtc).toLocaleDateString() : '',
      'Picked Up At': item.pickedUpAtUtc ? new Date(item.pickedUpAtUtc).toLocaleDateString() : '',
      'Completed At': item.completedAtUtc ? new Date(item.completedAtUtc).toLocaleDateString() : '',
      'Pickup Latency (Hours)': item.pickupLatencyHours ?? 'N/A',
      'Dev Cycle Time (Hours)': item.devCycleTimeHours ?? 'N/A',
      'PR Review Latency (Hours)': item.prReviewLatencyHours ?? 'N/A',
      'PR Merge Latency (Hours)': item.prMergeLatencyHours ?? 'N/A',
      'QA Testing Latency (Hours)': item.qaTestingLatencyHours ?? 'N/A',
      'Total Cycle Time (Hours)': item.totalCycleTimeHours ?? 'N/A',
      'DoR Criteria Defined': item.dorAcceptanceCriteriaDefined ? 'Yes' : 'No',
      'DoD Unit Tests Passed': item.dodUnitTestsPassed ? 'Yes' : 'No',
      'DoD Peer Review Done': item.dodPeerReviewCompleted ? 'Yes' : 'No',
      'DoD Merged to Master': item.dodMergedToMaster ? 'Yes' : 'No',
      'DoD Staging Verified': item.dodStagingVerified ? 'Yes' : 'No',
      'Escaped Defect': item.isEscapedDefect ? 'YES' : 'NO'
    }));
    const wsWorkItems = XLSX.utils.json_to_sheet(workItemsRows.length > 0 ? workItemsRows : [{ 'Info': 'No work items found for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsWorkItems, 'Work Items & Lifecycle');

    // 3. Pull Requests Sheet
    const prRows = data.prLogs.map(pr => ({
      'PR Number': pr.prNumber,
      'PR Title & Deliverable': pr.prTitle,
      'Author': pr.authorName,
      'Sprint': pr.sprintName || 'Current Sprint',
      'Total Comments': pr.totalCommentsCount,
      'Actionable Comments': pr.actionableCommentsCount,
      'Actionability %': pr.totalCommentsCount > 0 ? `${Math.round((pr.actionableCommentsCount / pr.totalCommentsCount) * PERCENTAGE_FACTOR)}%` : '0%',
      'Status': pr.reviewStatus,
      'Review Summary & Notes': pr.reviewSummary,
      'Created Date': pr.createdAtUtc ? new Date(pr.createdAtUtc).toLocaleDateString() : ''
    }));
    const wsPrs = XLSX.utils.json_to_sheet(prRows.length > 0 ? prRows : [{ 'Info': 'No pull requests logged for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsPrs, 'Pull Requests & Reviews');

    // 4. Daily Standups Sheet
    const standupRows = data.standups.map(standup => ({
      'Date': standup.standupDate ? new Date(standup.standupDate).toLocaleDateString() : '',
      'Member': standup.teamMemberName,
      'Yesterday Accomplishments': standup.yesterdaySummary,
      'Today Focus Plan': standup.todayPlan,
      'Impediments / Blockers': standup.blockersText || 'None',
      'Mood Score (1-10)': standup.moodScore
    }));
    const wsStandups = XLSX.utils.json_to_sheet(standupRows.length > 0 ? standupRows : [{ 'Info': 'No standups found for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsStandups, 'Daily Standups');

    // 5. Leaves & Capacity Sheet
    const leaveRows = data.leaves.map(leave => ({
      'Member': leave.teamMemberName,
      'Leave Category': leave.leaveType,
      'Slot': leave.leaveSlot === 'FirstHalf' ? '1st Half' : (leave.leaveSlot === 'SecondHalf' ? '2nd Half' : 'Full Day'),
      'Start Date': leave.startDate ? new Date(leave.startDate).toLocaleDateString() : '',
      'End Date': leave.endDate ? new Date(leave.endDate).toLocaleDateString() : '',
      'Total Working Days': leave.totalDays,
      'Approval Status': leave.isApproved ? 'Approved' : 'Pending',
      'Reason / Context': leave.reason || 'Planned Timeoff'
    }));
    const wsLeaves = XLSX.utils.json_to_sheet(leaveRows.length > 0 ? leaveRows : [{ 'Info': 'No leave records for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsLeaves, 'Capacity & Leaves');

    // 6. Monthly 1:1 Feedback Sheet
    const reviewRows = data.reviews.map(review => ({
      'Team Member': review.teamMemberName,
      'Review Month': review.monthYear,
      'SM Rating (1-5)': review.smRating,
      'Happiness Index (1-10)': review.happinessIndex,
      'Scrum Master Feedback': review.scrumMasterFeedback,
      'CDL Feedback': review.cdlFeedback,
      'Self Reflection': review.selfReflection,
      'Action Items Agreed': review.actionItems,
      'Next Month Goals': review.nextMonthGoals
    }));
    const wsReviews = XLSX.utils.json_to_sheet(reviewRows.length > 0 ? reviewRows : [{ 'Info': 'No 1:1 monthly feedback reviews for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsReviews, '1on1 Monthly Feedback');

    // 7. Kudos & Peer Recognitions Sheet
    const kudosRows = data.kudos.map(kudosCard => ({
      'Recipient': kudosCard.receiverName || data.memberLabel,
      'Sender': kudosCard.senderName || 'Team Member',
      'Award Category': this.getBadgeLabel(kudosCard.badge),
      'Kudos Message': kudosCard.message,
      'Date': kudosCard.createdAtUtc ? new Date(kudosCard.createdAtUtc).toLocaleDateString() : ''
    }));
    const wsKudos = XLSX.utils.json_to_sheet(kudosRows.length > 0 ? kudosRows : [{ 'Info': 'No kudos recognitions found for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsKudos, 'Kudos Recognitions');

    // 8. Tech Talks & Knowledge Sharing Sheet
    const techTalkRows = data.techTalks.map(talk => ({
      'Topic & Subject': talk.topic,
      'Presenter': talk.presenterName || data.memberLabel,
      'Date Delivered': talk.talkDate ? new Date(talk.talkDate).toLocaleDateString() : '',
      'Duration (Minutes)': talk.durationMinutes,
      'Key Takeaways / Architecture Notes': talk.keyTakeaways || 'Technical demo & architectural discussion'
    }));
    const wsTechTalks = XLSX.utils.json_to_sheet(techTalkRows.length > 0 ? techTalkRows : [{ 'Info': 'No tech talks hosted for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsTechTalks, 'Tech Talks & Knowledge Hub');

    const cleanFilename = `ScrumPulse_${data.memberLabel.replace(/[^a-zA-Z0-9]/g, '_')}_${data.scopeLabel.replace(/[^a-zA-Z0-9]/g, '_')}.xlsx`;
    XLSX.writeFile(wb, cleanFilename);
  }

  // ==========================================
  // PDF EXPORT (High-Quality Vector PDF)
  // ==========================================
  exportToPdf(options: ExportFilterOptions): void {
    const data = this.filterData(options);
    const doc = new jsPDF({
      orientation: 'portrait',
      unit: 'pt',
      format: 'a4'
    });

    const pageWidth = doc.internal.pageSize.getWidth();
    let currentY = 40;

    const checkPageBreak = (neededHeight: number = 70) => {
      if (currentY + neededHeight > 750) {
        doc.addPage();
        currentY = 40;
      }
    };

    // Header Background
    doc.setFillColor(15, 23, 42); // Navy Dark
    doc.rect(0, 0, pageWidth, 90, 'F');

    // Title
    doc.setTextColor(255, 255, 255);
    doc.setFontSize(18);
    doc.setFont('helvetica', 'bold');
    doc.text('SCRUMPULSE PERFORMANCE REPORT', 30, 45);

    doc.setFontSize(10);
    doc.setFont('helvetica', 'normal');
    doc.setTextColor(148, 163, 184);
    doc.text(`Scope: ${data.scopeLabel}  |  Target: ${data.memberLabel}`, 30, 65);
    doc.text(`Generated: ${new Date().toUTCString()}`, 30, 80);

    currentY = 110;

    // KPI Cards Block
    const totalPoints = data.workItems.reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
    const donePoints = data.workItems.filter(item => String(item.status).toLowerCase().includes('done'))
      .reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0);
    const totalPrs = data.prLogs.length;
    const totalComments = data.prLogs.reduce((accumulatedComments, pr) => accumulatedComments + (pr.totalCommentsCount || 0), 0);
    const actionableComments = data.prLogs.reduce((accumulatedActionable, pr) => accumulatedActionable + (pr.actionableCommentsCount || 0), 0);
    const actionabilityRate = totalComments > 0 ? `${Math.round((actionableComments / totalComments) * PERCENTAGE_FACTOR)}%` : '0%';
    const totalLeaveDays = data.leaves.reduce((accumulatedDays, leave) => accumulatedDays + (leave.totalDays || 0), 0);

    doc.setTextColor(30, 41, 59);
    doc.setFontSize(13);
    doc.setFont('helvetica', 'bold');
    doc.text('Key Performance & Growth Indicators', 30, currentY);
    currentY += 15;

    autoTable(doc, {
      startY: currentY,
      head: [['Metric / Growth Pillar', 'Recorded Value', 'Benchmark / Target']],
      body: [
        ['Work Items Handled', `${data.workItems.length} items`, 'Active Sprint Backlog Scope'],
        ['Delivered / Total Story Points', `${donePoints} / ${totalPoints} Pts`, 'Target >= 85% Say-Do Ratio'],
        ['Pull Requests Created', `${totalPrs} PRs`, 'Continuous Integration Stream'],
        ['Review Discussions Received', `${totalComments} comments`, 'Code Review Engagement'],
        ['Actionable Code Improvements', `${actionableComments} comments`, `Actionability: ${actionabilityRate}`],
        ['Daily Standups Recorded', `${data.standups.length} updates`, 'Daily Cadence & Impediments'],
        ['Planned Leave Days', `${totalLeaveDays} days`, 'Capacity Adjustments'],
        ['Monthly 1-on-1 Reviews', `${data.reviews.length} sessions`, 'Continuous Mentoring & Growth'],
        ['Peer Kudos & Recognitions', `${data.kudos.length} awards`, 'Culture & Collaboration'],
        ['Tech Talks Delivered', `${data.techTalks.length} sessions`, 'Knowledge Sharing & Engineering Impact']
      ],
      theme: 'grid',
      headStyles: { fillColor: [59, 130, 246], textColor: [255, 255, 255], fontStyle: 'bold' },
      styles: { fontSize: 8.5, cellPadding: 4 }
    });

    currentY = (doc as any).lastAutoTable.finalY + 25;

    // Section 1: Work Items Table
    checkPageBreak(100);
    doc.setFontSize(12);
    doc.setFont('helvetica', 'bold');
    doc.text('1. Sprint Work Items Breakdown', 30, currentY);
    currentY += 10;

    const workItemsBody = data.workItems.map(item => [
      item.key,
      item.title.length > 35 ? item.title.substring(0, 32) + '...' : item.title,
      String(item.type),
      `${item.storyPoints} Pts`,
      String(item.status),
      item.totalCycleTimeHours ? `${item.totalCycleTimeHours}h` : 'In Progress'
    ]);

    autoTable(doc, {
      startY: currentY,
      head: [['Key', 'Title & Deliverable', 'Category', 'Points', 'Status', 'Cycle Time']],
      body: workItemsBody.length > 0 ? workItemsBody : [['-', 'No work items found for this selection', '-', '-', '-', '-']],
      theme: 'striped',
      headStyles: { fillColor: [30, 41, 59], textColor: [255, 255, 255] },
      styles: { fontSize: 8, cellPadding: 4 }
    });

    currentY = (doc as any).lastAutoTable.finalY + 25;

    // Section 2: PR & Review Feedback Table
    checkPageBreak(100);
    doc.setFontSize(12);
    doc.setFont('helvetica', 'bold');
    doc.text('2. Pull Requests & Review Feedback Log', 30, currentY);
    currentY += 10;

    const prsBody = data.prLogs.map(pr => [
      pr.prNumber,
      pr.prTitle.length > 30 ? pr.prTitle.substring(0, 27) + '...' : pr.prTitle,
      pr.authorName,
      `${pr.totalCommentsCount} / ${pr.actionableCommentsCount}`,
      pr.totalCommentsCount > 0 ? `${Math.round((pr.actionableCommentsCount / pr.totalCommentsCount) * PERCENTAGE_FACTOR)}%` : '0%',
      pr.reviewStatus
    ]);

    autoTable(doc, {
      startY: currentY,
      head: [['PR #', 'Title & Feature', 'Author', 'Comments (Tot/Act)', 'Actionable %', 'Status']],
      body: prsBody.length > 0 ? prsBody : [['-', 'No pull requests logged for this selection', '-', '-', '-', '-']],
      theme: 'striped',
      headStyles: { fillColor: [139, 92, 246], textColor: [255, 255, 255] },
      styles: { fontSize: 8, cellPadding: 4 }
    });

    currentY = (doc as any).lastAutoTable.finalY + 25;

    // Section 3: Standups & Daily Updates
    if (data.standups.length > 0) {
      checkPageBreak(90);
      doc.setFontSize(12);
      doc.setFont('helvetica', 'bold');
      doc.text('3. Recent Daily Standups & Impediments', 30, currentY);
      currentY += 10;

      const standupsBody = data.standups.slice(0, MAX_STANDUPS_PDF_PREVIEW_COUNT).map(standup => [
        standup.standupDate ? new Date(standup.standupDate).toLocaleDateString() : '',
        standup.yesterdaySummary.length > 35 ? standup.yesterdaySummary.substring(0, 32) + '...' : standup.yesterdaySummary,
        standup.todayPlan.length > 35 ? standup.todayPlan.substring(0, 32) + '...' : standup.todayPlan,
        standup.blockersText || 'None'
      ]);

      autoTable(doc, {
        startY: currentY,
        head: [['Date', 'Completed Tasks', 'Today Plan', 'Blockers']],
        body: standupsBody,
        theme: 'striped',
        headStyles: { fillColor: [16, 185, 129], textColor: [255, 255, 255] },
        styles: { fontSize: 8, cellPadding: 4 }
      });
      currentY = (doc as any).lastAutoTable.finalY + 25;
    }

    // Section 4: Leaves & Planned PTO
    if (data.leaves.length > 0) {
      checkPageBreak(90);
      doc.setFontSize(12);
      doc.setFont('helvetica', 'bold');
      doc.text('4. Leaves & Capacity Allocation', 30, currentY);
      currentY += 10;

      const leavesBody = data.leaves.map(leave => [
        leave.teamMemberName,
        leave.leaveType,
        leave.leaveSlot === 'FirstHalf' ? '1st Half' : (leave.leaveSlot === 'SecondHalf' ? '2nd Half' : 'Full Day'),
        leave.startDate ? new Date(leave.startDate).toLocaleDateString() : '',
        leave.endDate ? new Date(leave.endDate).toLocaleDateString() : '',
        `${leave.totalDays}d`,
        leave.reason || 'Planned PTO'
      ]);

      autoTable(doc, {
        startY: currentY,
        head: [['Member', 'Leave Type', 'Slot', 'Start', 'End', 'Days', 'Reason']],
        body: leavesBody,
        theme: 'striped',
        headStyles: { fillColor: [245, 158, 11], textColor: [255, 255, 255] },
        styles: { fontSize: 8, cellPadding: 4 }
      });
      currentY = (doc as any).lastAutoTable.finalY + 25;
    }

    // Section 5: Monthly 1-on-1 Feedback Reviews
    if (data.reviews.length > 0) {
      checkPageBreak(100);
      doc.setFontSize(12);
      doc.setFont('helvetica', 'bold');
      doc.text('5. Monthly 1-on-1 Reviews & Coaching Feedback', 30, currentY);
      currentY += 10;

      const reviewsBody = data.reviews.map(review => [
        review.teamMemberName,
        review.monthYear,
        `${review.smRating} / 5`,
        `${review.happinessIndex} / 10`,
        review.scrumMasterFeedback.length > 40 ? review.scrumMasterFeedback.substring(0, 37) + '...' : review.scrumMasterFeedback,
        review.actionItems.length > 35 ? review.actionItems.substring(0, 32) + '...' : review.actionItems
      ]);

      autoTable(doc, {
        startY: currentY,
        head: [['Member', 'Month', 'SM Rating', 'Happiness', 'Feedback Summary', 'Action Items']],
        body: reviewsBody,
        theme: 'striped',
        headStyles: { fillColor: [79, 70, 229], textColor: [255, 255, 255] },
        styles: { fontSize: 8, cellPadding: 4 }
      });
      currentY = (doc as any).lastAutoTable.finalY + 25;
    }

    // Section 6: Peer Kudos Recognitions
    if (data.kudos.length > 0) {
      checkPageBreak(90);
      doc.setFontSize(12);
      doc.setFont('helvetica', 'bold');
      doc.text('6. Peer Kudos & Recognitions Received', 30, currentY);
      currentY += 10;

      const kudosBody = data.kudos.map(kudosCard => [
        kudosCard.receiverName || data.memberLabel,
        kudosCard.senderName || 'Team Member',
        this.getBadgeLabel(kudosCard.badge),
        kudosCard.message.length > 45 ? kudosCard.message.substring(0, 42) + '...' : kudosCard.message,
        kudosCard.createdAtUtc ? new Date(kudosCard.createdAtUtc).toLocaleDateString() : ''
      ]);

      autoTable(doc, {
        startY: currentY,
        head: [['Recipient', 'Sender', 'Recognition Badge', 'Message', 'Date']],
        body: kudosBody,
        theme: 'striped',
        headStyles: { fillColor: [236, 72, 153], textColor: [255, 255, 255] },
        styles: { fontSize: 8, cellPadding: 4 }
      });
      currentY = (doc as any).lastAutoTable.finalY + 25;
    }

    // Section 7: Tech Talks & Knowledge Sharing
    if (data.techTalks.length > 0) {
      checkPageBreak(90);
      doc.setFontSize(12);
      doc.setFont('helvetica', 'bold');
      doc.text('7. Tech Talks & Knowledge Sharing Sessions', 30, currentY);
      currentY += 10;

      const talksBody = data.techTalks.map(talk => [
        talk.topic,
        talk.presenterName || data.memberLabel,
        talk.talkDate ? new Date(talk.talkDate).toLocaleDateString() : '',
        `${talk.durationMinutes} mins`,
        (talk.keyTakeaways || '').length > 45 ? (talk.keyTakeaways || '').substring(0, 42) + '...' : (talk.keyTakeaways || 'Technical demo & architectural review')
      ]);

      autoTable(doc, {
        startY: currentY,
        head: [['Topic / Subject', 'Presenter', 'Date', 'Duration', 'Key Takeaways']],
        body: talksBody,
        theme: 'striped',
        headStyles: { fillColor: [6, 182, 212], textColor: [255, 255, 255] },
        styles: { fontSize: 8, cellPadding: 4 }
      });
      currentY = (doc as any).lastAutoTable.finalY + 25;
    }

    // Page Number Footers on All Pages
    const totalPages = (doc.internal as any).getNumberOfPages();
    for (let pageNumber = 1; pageNumber <= totalPages; pageNumber++) {
      doc.setPage(pageNumber);
      doc.setFontSize(8);
      doc.setTextColor(148, 163, 184);
      doc.text(
        `ScrumPulse Enterprise v2.0  |  Confidential & Proprietary  |  Page ${pageNumber} of ${totalPages}`,
        pageWidth / 2,
        doc.internal.pageSize.getHeight() - 15,
        { align: 'center' }
      );
    }

    const cleanFilename = `ScrumPulse_${data.memberLabel.replace(/[^a-zA-Z0-9]/g, '_')}_${data.scopeLabel.replace(/[^a-zA-Z0-9]/g, '_')}.pdf`;
    doc.save(cleanFilename);
  }
}
