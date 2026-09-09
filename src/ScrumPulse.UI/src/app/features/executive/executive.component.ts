import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { ReportExportService, ExportFilterOptions } from '../../core/services/report-export.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { AiSuggestionResponse, SprintComparison, SprintHealth, SprintVelocityTrend } from '../../core/models/scrum.models';
import { generateDynamicMonths, generateDynamicQuarters, getCurrentMonthValue, getCurrentQuarterValue, getDatePresetRange, getThisMonthDateRange, getSprintDateRange } from '../../core/utils/date-utils';
import { CORE_PIPES } from '../../core/pipes';

export const DEFAULT_VELOCITY_TREND_SPRINT_COUNT = 6;
export const COPY_NOTIFICATION_TIMEOUT_MS = 2500;
export const TWO_WEEKS_IN_DAYS = 14;
export const HOURS_PER_DAY = 24;
export const MINUTES_PER_HOUR = 60;
export const SECONDS_PER_MINUTE = 60;
export const MILLISECONDS_PER_SECOND = 1000;
export const TWO_WEEKS_IN_MS = TWO_WEEKS_IN_DAYS * HOURS_PER_DAY * MINUTES_PER_HOUR * SECONDS_PER_MINUTE * MILLISECONDS_PER_SECOND;

@Component({
  selector: 'app-executive',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './executive.component.html',
  styleUrl: './executive.component.css'
})
export class ExecutiveComponent implements OnInit {
  protected readonly Math = Math;
  state = inject(ScrumStateService);
  private exportService = inject(ReportExportService);

  selectedMemberId = signal<string>('ALL');
  selectedTimeScope = signal<'SPRINT' | 'MONTH' | 'QUARTER' | 'CUSTOM' | 'ALL'>('SPRINT');
  selectedSprintId = signal<string>('');
  selectedMonth = signal<string>(getCurrentMonthValue());
  selectedQuarter = signal<string>(getCurrentQuarterValue());
  startDate = signal<string>(new Date(Date.now() - TWO_WEEKS_IN_MS).toISOString().split('T')[0]);
  endDate = signal<string>(new Date().toISOString().split('T')[0]);

  aiData = signal<AiSuggestionResponse | null>(null);
  loadingAi = signal<boolean>(false);

  velocityTrend = signal<SprintVelocityTrend | null>(null);
  sprintHealth = signal<SprintHealth | null>(null);
  loadingMetrics = signal<boolean>(false);

  // Sprint Comparison Feature
  showComparison = signal<boolean>(false);
  compareSprintA = signal<string>('');
  compareSprintB = signal<string>('');
  comparisonData = signal<SprintComparison | null>(null);
  loadingComparison = signal<boolean>(false);
  comparisonError = signal<string | null>(null);

  monthsList = generateDynamicMonths(11, 2);
  quartersList = generateDynamicQuarters(6, 1);

  // Error state
  metricsError = signal<string | null>(null);

  ngOnInit(): void {
    this.refreshAiIntelligence();
    this.loadExecutiveMetrics();
  }

  loadExecutiveMetrics(): void {
    this.loadingMetrics.set(true);
    this.metricsError.set(null);
    this.state.getVelocityTrend(DEFAULT_VELOCITY_TREND_SPRINT_COUNT).subscribe({
      next: (data) => this.velocityTrend.set(data),
      error: (err) => {
        console.error('[ExecutiveComponent] Failed to load velocity trend:', err);
        this.metricsError.set('Unable to load velocity trend: ' + (err?.message || 'Network error'));
      }
    });

    const sprintId = this.effectiveSprintId();
    if (sprintId && sprintId !== 'ALL') {
      this.state.getSprintHealth(sprintId).subscribe({
        next: (data) => {
          this.sprintHealth.set(data);
          this.loadingMetrics.set(false);
        },
        error: (err) => {
          console.error('[ExecutiveComponent] Failed to load sprint health:', err);
          this.metricsError.set('Unable to load sprint health: ' + (err?.message || 'Network error'));
          this.loadingMetrics.set(false);
        }
      });
    } else {
      this.sprintHealth.set(null);
      this.loadingMetrics.set(false);
    }
  }

  refreshAiIntelligence(): void {
    this.loadingAi.set(true);
    const memberId = this.selectedMemberId();
    if (memberId !== 'ALL') {
      this.state.getIndividualAi(memberId).subscribe({
        next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
        error: (err) => {
          console.error('[ExecutiveComponent] Failed to load individual AI intelligence:', err);
          this.loadingAi.set(false);
        }
      });
    } else if (this.selectedTimeScope() === 'SPRINT') {
      const sprintId = this.effectiveSprintId();
      if (sprintId && sprintId !== 'ALL') {
        this.state.getProjectAi(sprintId).subscribe({
          next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
          error: (err) => {
            console.error('[ExecutiveComponent] Failed to load project AI intelligence:', err);
            this.loadingAi.set(false);
          }
        });
      } else {
        this.state.getCompanyAi().subscribe({
          next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
          error: (err) => {
            console.error('[ExecutiveComponent] Failed to load company AI intelligence:', err);
            this.loadingAi.set(false);
          }
        });
      }
    } else {
      this.state.getCompanyAi().subscribe({
        next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
        error: (err) => {
          console.error('[ExecutiveComponent] Failed to load company AI intelligence:', err);
          this.loadingAi.set(false);
        }
      });
    }
  }

  cleanName(name: string): string {
    if (!name) return '';
    return name.replace(/\s*\([^)]*\)/g, '').trim();
  }

  getRoleLabel(role: string): string {
    switch (role) {
      case 'ScrumMaster': return 'Scrum Master';
      case 'Developer': return 'Developer';
      case 'QaEngineer': return 'QA Engineer';
      case 'Cdl': return 'CDL';
      case 'ProductOwner':
      case 'ClientStakeholder': return 'Product Owner';
      case 'AgileCoach': return 'Agile Coach';
      default: return role || 'Team Member';
    }
  }

  effectiveSprintId = computed(() => {
    const custom = this.selectedSprintId();
    if (custom) return custom;
    return this.state.activeSprint()?.id || this.state.sprints()[0]?.id || 'ALL';
  });

  currentOptions = computed<ExportFilterOptions>(() => {
    return {
      memberId: this.selectedMemberId(),
      timeScopeType: this.selectedTimeScope(),
      sprintId: this.selectedTimeScope() === 'SPRINT' ? this.effectiveSprintId() : undefined,
      month: this.selectedTimeScope() === 'MONTH' ? this.selectedMonth() : undefined,
      quarter: this.selectedTimeScope() === 'QUARTER' ? this.selectedQuarter() : undefined,
      startDate: this.selectedTimeScope() === 'CUSTOM' ? this.startDate() : undefined,
      endDate: this.selectedTimeScope() === 'CUSTOM' ? this.endDate() : undefined
    };
  });

  filteredPreview = computed(() => {
    return this.exportService.filterData(this.currentOptions());
  });

  previewWorkItems = computed(() => this.filteredPreview().workItems);
  previewPrLogs = computed(() => this.filteredPreview().prLogs);

  previewTotalPoints = computed(() => this.previewWorkItems().reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0));
  previewDonePoints = computed(() =>
    this.previewWorkItems()
      .filter(item => String(item.status).toLowerCase().includes('done'))
      .reduce((accumulatedPoints, item) => accumulatedPoints + (item.storyPoints || 0), 0)
  );
  previewTotalPrs = computed(() => this.previewPrLogs().length);
  previewTotalComments = computed(() => this.previewPrLogs().reduce((accumulatedComments, pr) => accumulatedComments + (pr.totalCommentsCount || 0), 0));
  previewActionableComments = computed(() => this.previewPrLogs().reduce((accumulatedActionable, pr) => accumulatedActionable + (pr.actionableCommentsCount || 0), 0));

  previewStandupCompliance = computed(() => this.filteredPreview().standupCompliance);
  previewMissedStandups = computed(() => this.previewStandupCompliance().missedDays);
  previewComplianceRate = computed(() => this.previewStandupCompliance().complianceRate);
  previewLoggedStandups = computed(() => this.previewStandupCompliance().loggedDays);
  previewExpectedStandups = computed(() => this.previewStandupCompliance().expectedDays);
  previewMissedDates = computed(() => this.previewStandupCompliance().missedDates);

  applyCustomPreset(days: number): void {
    const range = getDatePresetRange(days);
    this.startDate.set(range.startDate);
    this.endDate.set(range.endDate);
  }

  applyThisMonthPreset(): void {
    const range = getThisMonthDateRange();
    this.startDate.set(range.startDate);
    this.endDate.set(range.endDate);
  }

  applySprintDatesPreset(): void {
    const active = this.state.activeSprint();
    const range = getSprintDateRange(active?.startDate, active?.endDate);
    if (range) {
      this.startDate.set(range.startDate);
      this.endDate.set(range.endDate);
    }
  }

  exportExcel(): void {
    this.exportService.exportToExcel(this.currentOptions());
  }

  exportPdf(): void {
    this.exportService.exportToPdf(this.currentOptions());
  }

  copiedSummary = signal<boolean>(false);

  copySummary(): void {
    let summaryText = this.state.executiveReport()?.executiveSummaryMarkdown || '';
    const compliance = this.previewStandupCompliance();
    const missedInfo = `\n\n### Standup Attendance & Updates Telemetry\n` +
      `- Daily Standups Expected (Not on Leave): ${compliance.expectedDays} days\n` +
      `- Daily Standups Logged: ${compliance.loggedDays} updates\n` +
      `- Missed Daily Standups / Updates: ${compliance.missedDays} missed\n` +
      `- Standup Attendance & Logging Compliance: ${compliance.complianceRate}%\n` +
      (compliance.missedDates.length > 0 ? `- Dates with Missed Standup Updates: ${compliance.missedDates.join(', ')}\n` : '- Missed Dates: None (100% compliant)\n');

    summaryText = summaryText ? (summaryText + missedInfo) : missedInfo.trim();
    if (summaryText) {
      navigator.clipboard.writeText(summaryText);
      this.copiedSummary.set(true);
      setTimeout(() => this.copiedSummary.set(false), COPY_NOTIFICATION_TIMEOUT_MS);
    }
  }

  exportSprintCsv(): void {
    const sprintId = this.effectiveSprintId();
    if (sprintId && sprintId !== 'ALL') {
      this.state.exportSprintCsv(sprintId);
    }
  }

  openComparison(): void {
    const sprints = this.state.sprints();
    if (sprints.length >= 2) {
      this.compareSprintA.set(sprints[1].id);
      this.compareSprintB.set(sprints[0].id);
    } else if (sprints.length === 1) {
      this.compareSprintA.set(sprints[0].id);
      this.compareSprintB.set(sprints[0].id);
    }
    this.showComparison.set(true);
    if (this.compareSprintA() && this.compareSprintB()) {
      this.runComparison();
    }
  }

  runComparison(): void {
    const sprintAId = this.compareSprintA();
    const sprintBId = this.compareSprintB();
    if (!sprintAId || !sprintBId) return;

    this.loadingComparison.set(true);
    this.comparisonError.set(null);

    this.state.compareSprints(sprintAId, sprintBId).subscribe({
      next: (data) => {
        this.comparisonData.set(data);
        this.loadingComparison.set(false);
      },
      error: (err) => {
        console.error('[ExecutiveComponent] Sprint comparison failed:', err);
        this.comparisonError.set(err?.message || 'Unable to compare sprints.');
        this.loadingComparison.set(false);
      }
    });
  }
}
