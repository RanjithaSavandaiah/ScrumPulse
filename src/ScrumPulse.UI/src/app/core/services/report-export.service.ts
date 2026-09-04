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
      selectedMember = members.find(m => m.id === options.memberId) || null;
    }

    // Filter Work Items
    let workItems = this.state.workItems();
    if (options.memberId !== 'ALL') {
      workItems = workItems.filter(w => w.assigneeId === options.memberId);
    }
    if (options.timeScopeType === 'SPRINT' && options.sprintId && options.sprintId !== 'ALL') {
      workItems = workItems.filter(w => w.sprintId === options.sprintId);
    } else if (options.timeScopeType === 'MONTH' && options.month) {
      workItems = workItems.filter(w => this.isDateInMonth(w.createdAtUtc || w.completedAtUtc, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      workItems = workItems.filter(w => this.isDateInQuarter(w.createdAtUtc || w.completedAtUtc, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      workItems = workItems.filter(w => this.isDateInRange(w.createdAtUtc || w.completedAtUtc, options.startDate, options.endDate));
    }

    // Filter PR Logs
    let prLogs = this.state.prLogs();
    if (options.memberId !== 'ALL') {
      prLogs = prLogs.filter(p => p.authorId === options.memberId);
    }
    if (options.timeScopeType === 'SPRINT' && options.sprintId && options.sprintId !== 'ALL') {
      prLogs = prLogs.filter(p => p.sprintId === options.sprintId);
    } else if (options.timeScopeType === 'MONTH' && options.month) {
      prLogs = prLogs.filter(p => this.isDateInMonth(p.createdAtUtc, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      prLogs = prLogs.filter(p => this.isDateInQuarter(p.createdAtUtc, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      prLogs = prLogs.filter(p => this.isDateInRange(p.createdAtUtc, options.startDate, options.endDate));
    }

    // Filter Standups
    let standups = this.state.standups();
    if (options.memberId !== 'ALL') {
      standups = standups.filter(s => s.teamMemberId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      standups = standups.filter(s => this.isDateInMonth(s.standupDate, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      standups = standups.filter(s => this.isDateInQuarter(s.standupDate, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      standups = standups.filter(s => this.isDateInRange(s.standupDate, options.startDate, options.endDate));
    }

    // Filter Leaves
    let leaves = this.state.leaves();
    if (options.memberId !== 'ALL') {
      leaves = leaves.filter(l => l.teamMemberId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      leaves = leaves.filter(l => this.isDateInMonth(l.startDate, options.month!) || this.isDateInMonth(l.endDate, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      leaves = leaves.filter(l => this.isDateInQuarter(l.startDate, options.quarter!) || this.isDateInQuarter(l.endDate, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      leaves = leaves.filter(l => this.isDateInRange(l.startDate, options.startDate, options.endDate) || this.isDateInRange(l.endDate, options.startDate, options.endDate));
    }

    // Filter Monthly Reviews
    let reviews = this.state.monthlyFeedbacks();
    if (options.memberId !== 'ALL') {
      reviews = reviews.filter(r => r.teamMemberId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      reviews = reviews.filter(r => r.monthYear === options.month);
    } else if (options.timeScopeType === 'CUSTOM') {
      reviews = reviews.filter(r => this.isDateInRange(r.createdAtUtc, options.startDate, options.endDate));
    }

    // Filter Kudos
    let kudos = this.state.kudos();
    if (options.memberId !== 'ALL') {
      kudos = kudos.filter(k => k.receiverId === options.memberId || k.senderId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      kudos = kudos.filter(k => this.isDateInMonth(k.createdAtUtc, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      kudos = kudos.filter(k => this.isDateInQuarter(k.createdAtUtc, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      kudos = kudos.filter(k => this.isDateInRange(k.createdAtUtc, options.startDate, options.endDate));
    }

    // Filter Tech Talks
    let techTalks = this.state.techTalks();
    if (options.memberId !== 'ALL') {
      techTalks = techTalks.filter(t => t.presenterId === options.memberId);
    }
    if (options.timeScopeType === 'MONTH' && options.month) {
      techTalks = techTalks.filter(t => this.isDateInMonth(t.talkDate, options.month!));
    } else if (options.timeScopeType === 'QUARTER' && options.quarter) {
      techTalks = techTalks.filter(t => this.isDateInQuarter(t.talkDate, options.quarter!));
    } else if (options.timeScopeType === 'CUSTOM') {
      techTalks = techTalks.filter(t => this.isDateInRange(t.talkDate, options.startDate, options.endDate));
    }

    // Determine Scope Label
    let scopeLabel = 'All History';
    if (options.timeScopeType === 'SPRINT') {
      const sp = sprints.find(s => s.id === options.sprintId);
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
    const totalPoints = data.workItems.reduce((acc, w) => acc + (w.storyPoints || 0), 0);
    const donePoints = data.workItems.filter(w => String(w.status).toLowerCase().includes('done'))
      .reduce((acc, w) => acc + (w.storyPoints || 0), 0);
    const totalPrs = data.prLogs.length;
    const totalComments = data.prLogs.reduce((acc, p) => acc + (p.totalCommentsCount || 0), 0);
    const actionableComments = data.prLogs.reduce((acc, p) => acc + (p.actionableCommentsCount || 0), 0);
    const actionabilityRate = totalComments > 0 ? `${Math.round((actionableComments / totalComments) * 100)}%` : '0%';
    const totalLeaveDays = data.leaves.reduce((acc, l) => acc + (l.totalDays || 0), 0);
    const totalTalkMinutes = data.techTalks.reduce((acc, t) => acc + (t.durationMinutes || 0), 0);

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
      ['Say-Do Velocity Rate', totalPoints > 0 ? `${Math.round((donePoints / totalPoints) * 100)}%` : '0%'],
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
      'Actionability %': pr.totalCommentsCount > 0 ? `${Math.round((pr.actionableCommentsCount / pr.totalCommentsCount) * 100)}%` : '0%',
      'Status': pr.reviewStatus,
      'Review Summary & Notes': pr.reviewSummary,
      'Created Date': pr.createdAtUtc ? new Date(pr.createdAtUtc).toLocaleDateString() : ''
    }));
    const wsPrs = XLSX.utils.json_to_sheet(prRows.length > 0 ? prRows : [{ 'Info': 'No pull requests logged for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsPrs, 'Pull Requests & Reviews');

    // 4. Daily Standups Sheet
    const standupRows = data.standups.map(s => ({
      'Date': s.standupDate ? new Date(s.standupDate).toLocaleDateString() : '',
      'Member': s.teamMemberName,
      'Yesterday Accomplishments': s.yesterdaySummary,
      'Today Focus Plan': s.todayPlan,
      'Impediments / Blockers': s.blockersText || 'None',
      'Mood Score (1-10)': s.moodScore
    }));
    const wsStandups = XLSX.utils.json_to_sheet(standupRows.length > 0 ? standupRows : [{ 'Info': 'No standups found for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsStandups, 'Daily Standups');

    // 5. Leaves & Capacity Sheet
    const leaveRows = data.leaves.map(l => ({
      'Member': l.teamMemberName,
      'Leave Category': l.leaveType,
      'Slot': l.leaveSlot === 'FirstHalf' ? '1st Half' : (l.leaveSlot === 'SecondHalf' ? '2nd Half' : 'Full Day'),
      'Start Date': l.startDate ? new Date(l.startDate).toLocaleDateString() : '',
      'End Date': l.endDate ? new Date(l.endDate).toLocaleDateString() : '',
      'Total Working Days': l.totalDays,
      'Approval Status': l.isApproved ? 'Approved' : 'Pending',
      'Reason / Context': l.reason || 'Planned Timeoff'
    }));
    const wsLeaves = XLSX.utils.json_to_sheet(leaveRows.length > 0 ? leaveRows : [{ 'Info': 'No leave records for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsLeaves, 'Capacity & Leaves');

    // 6. Monthly 1:1 Feedback Sheet
    const reviewRows = data.reviews.map(r => ({
      'Team Member': r.teamMemberName,
      'Review Month': r.monthYear,
      'SM Rating (1-5)': r.smRating,
      'Happiness Index (1-10)': r.happinessIndex,
      'Scrum Master Feedback': r.scrumMasterFeedback,
      'CDL Leadership Feedback': r.cdlFeedback,
      'Self Reflection': r.selfReflection,
      'Action Items Agreed': r.actionItems,
      'Next Month Goals': r.nextMonthGoals
    }));
    const wsReviews = XLSX.utils.json_to_sheet(reviewRows.length > 0 ? reviewRows : [{ 'Info': 'No 1:1 monthly feedback reviews for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsReviews, '1on1 Monthly Feedback');

    // 7. Kudos & Peer Recognitions Sheet
    const kudosRows = data.kudos.map(k => ({
      'Recipient': k.receiverName || data.memberLabel,
      'Sender': k.senderName || 'Team Member',
      'Award Category': this.getBadgeLabel(k.badge),
      'Kudos Message': k.message,
      'Date': k.createdAtUtc ? new Date(k.createdAtUtc).toLocaleDateString() : ''
    }));
    const wsKudos = XLSX.utils.json_to_sheet(kudosRows.length > 0 ? kudosRows : [{ 'Info': 'No kudos recognitions found for this selection' }]);
    XLSX.utils.book_append_sheet(wb, wsKudos, 'Kudos Recognitions');

    // 8. Tech Talks & Knowledge Sharing Sheet
    const techTalkRows = data.techTalks.map(t => ({
      'Topic & Subject': t.topic,
      'Presenter': t.presenterName || data.memberLabel,
      'Date Delivered': t.talkDate ? new Date(t.talkDate).toLocaleDateString() : '',
      'Duration (Minutes)': t.durationMinutes,
      'Key Takeaways / Architecture Notes': t.keyTakeaways || 'Technical demo & architectural discussion'
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
    const totalPoints = data.workItems.reduce((acc, w) => acc + (w.storyPoints || 0), 0);
    const donePoints = data.workItems.filter(w => String(w.status).toLowerCase().includes('done'))
      .reduce((acc, w) => acc + (w.storyPoints || 0), 0);
    const totalPrs = data.prLogs.length;
    const totalComments = data.prLogs.reduce((acc, p) => acc + (p.totalCommentsCount || 0), 0);
    const actionableComments = data.prLogs.reduce((acc, p) => acc + (p.actionableCommentsCount || 0), 0);
    const actionabilityRate = totalComments > 0 ? `${Math.round((actionableComments / totalComments) * 100)}%` : '0%';
    const totalLeaveDays = data.leaves.reduce((acc, l) => acc + (l.totalDays || 0), 0);

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
      pr.totalCommentsCount > 0 ? `${Math.round((pr.actionableCommentsCount / pr.totalCommentsCount) * 100)}%` : '0%',
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

      const standupsBody = data.standups.slice(0, 8).map(s => [
        s.standupDate ? new Date(s.standupDate).toLocaleDateString() : '',
        s.yesterdaySummary.length > 35 ? s.yesterdaySummary.substring(0, 32) + '...' : s.yesterdaySummary,
        s.todayPlan.length > 35 ? s.todayPlan.substring(0, 32) + '...' : s.todayPlan,
        s.blockersText || 'None'
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

      const leavesBody = data.leaves.map(l => [
        l.teamMemberName,
        l.leaveType,
        l.leaveSlot === 'FirstHalf' ? '1st Half' : (l.leaveSlot === 'SecondHalf' ? '2nd Half' : 'Full Day'),
        l.startDate ? new Date(l.startDate).toLocaleDateString() : '',
        l.endDate ? new Date(l.endDate).toLocaleDateString() : '',
        `${l.totalDays}d`,
        l.reason || 'Planned PTO'
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

      const reviewsBody = data.reviews.map(r => [
        r.teamMemberName,
        r.monthYear,
        `${r.smRating} / 5`,
        `${r.happinessIndex} / 10`,
        r.scrumMasterFeedback.length > 40 ? r.scrumMasterFeedback.substring(0, 37) + '...' : r.scrumMasterFeedback,
        r.actionItems.length > 35 ? r.actionItems.substring(0, 32) + '...' : r.actionItems
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

      const kudosBody = data.kudos.map(k => [
        k.receiverName || data.memberLabel,
        k.senderName || 'Team Member',
        this.getBadgeLabel(k.badge),
        k.message.length > 45 ? k.message.substring(0, 42) + '...' : k.message,
        k.createdAtUtc ? new Date(k.createdAtUtc).toLocaleDateString() : ''
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

      const talksBody = data.techTalks.map(t => [
        t.topic,
        t.presenterName || data.memberLabel,
        t.talkDate ? new Date(t.talkDate).toLocaleDateString() : '',
        `${t.durationMinutes} mins`,
        (t.keyTakeaways || '').length > 45 ? (t.keyTakeaways || '').substring(0, 42) + '...' : (t.keyTakeaways || 'Technical demo & architectural review')
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
    for (let i = 1; i <= totalPages; i++) {
      doc.setPage(i);
      doc.setFontSize(8);
      doc.setTextColor(148, 163, 184);
      doc.text(
        `ScrumPulse Enterprise v2.0  |  Confidential & Proprietary  |  Page ${i} of ${totalPages}`,
        pageWidth / 2,
        doc.internal.pageSize.getHeight() - 15,
        { align: 'center' }
      );
    }

    const cleanFilename = `ScrumPulse_${data.memberLabel.replace(/[^a-zA-Z0-9]/g, '_')}_${data.scopeLabel.replace(/[^a-zA-Z0-9]/g, '_')}.pdf`;
    doc.save(cleanFilename);
  }
}
