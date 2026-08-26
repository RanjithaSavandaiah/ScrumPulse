import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { ReportExportService, ExportFilterOptions } from '../../core/services/report-export.service';
import { IconComponent } from '../../core/components/icon/icon.component';
import { AiSuggestionResponse } from '../../core/models/scrum.models';
import { generateDynamicMonths, generateDynamicQuarters, getCurrentMonthValue, getCurrentQuarterValue, getDatePresetRange, getThisMonthDateRange, getSprintDateRange } from '../../core/utils/date-utils';
import { CORE_PIPES } from '../../core/pipes';

@Component({
  selector: 'app-executive',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './executive.component.html',
  styleUrl: './executive.component.css'
})
export class ExecutiveComponent implements OnInit {
  state = inject(ScrumStateService);
  private exportService = inject(ReportExportService);

  selectedMemberId = signal<string>('ALL');
  selectedTimeScope = signal<'SPRINT' | 'MONTH' | 'QUARTER' | 'CUSTOM' | 'ALL'>('SPRINT');
  selectedSprintId = signal<string>('');
  selectedMonth = signal<string>(getCurrentMonthValue());
  selectedQuarter = signal<string>(getCurrentQuarterValue());
  startDate = signal<string>(new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]);
  endDate = signal<string>(new Date().toISOString().split('T')[0]);

  aiData = signal<AiSuggestionResponse | null>(null);
  loadingAi = signal<boolean>(false);

  monthsList = generateDynamicMonths(11, 2);
  quartersList = generateDynamicQuarters(6, 1);

  ngOnInit(): void {
    this.refreshAiIntelligence();
  }

  refreshAiIntelligence(): void {
    this.loadingAi.set(true);
    const mId = this.selectedMemberId();
    if (mId !== 'ALL') {
      this.state.getIndividualAi(mId).subscribe({
        next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
        error: () => this.loadingAi.set(false)
      });
    } else if (this.selectedTimeScope() === 'SPRINT') {
      const spId = this.effectiveSprintId();
      if (spId && spId !== 'ALL') {
        this.state.getProjectAi(spId).subscribe({
          next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
          error: () => this.loadingAi.set(false)
        });
      } else {
        this.state.getCompanyAi().subscribe({
          next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
          error: () => this.loadingAi.set(false)
        });
      }
    } else {
      this.state.getCompanyAi().subscribe({
        next: (data) => { this.aiData.set(data); this.loadingAi.set(false); },
        error: () => this.loadingAi.set(false)
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

  previewTotalPoints = computed(() => this.previewWorkItems().reduce((acc, w) => acc + (w.storyPoints || 0), 0));
  previewDonePoints = computed(() =>
    this.previewWorkItems()
      .filter(w => String(w.status).toLowerCase().includes('done'))
      .reduce((acc, w) => acc + (w.storyPoints || 0), 0)
  );
  previewTotalPrs = computed(() => this.previewPrLogs().length);
  previewTotalComments = computed(() => this.previewPrLogs().reduce((acc, p) => acc + (p.totalCommentsCount || 0), 0));
  previewActionableComments = computed(() => this.previewPrLogs().reduce((acc, p) => acc + (p.actionableCommentsCount || 0), 0));

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
    const summaryText = this.state.executiveReport()?.executiveSummaryMarkdown;
    if (summaryText) {
      navigator.clipboard.writeText(summaryText);
      this.copiedSummary.set(true);
      setTimeout(() => this.copiedSummary.set(false), 2500);
    }
  }
}
