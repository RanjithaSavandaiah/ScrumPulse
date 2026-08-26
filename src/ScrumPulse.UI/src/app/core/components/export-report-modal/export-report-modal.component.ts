import { Component, EventEmitter, Input, Output, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../icon/icon.component';
import { ScrumStateService } from '../../services/scrum-state.service';
import { ReportExportService, ExportFilterOptions } from '../../services/report-export.service';

import { generateDynamicMonths, generateDynamicQuarters, getCurrentMonthValue, getCurrentQuarterValue, getDatePresetRange, getThisMonthDateRange, getSprintDateRange } from '../../utils/date-utils';
import { CORE_PIPES } from '../../pipes';

@Component({
  selector: 'app-export-report-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './export-report-modal.component.html',
  styleUrl: './export-report-modal.component.css'
})
export class ExportReportModalComponent implements OnInit {
  state = inject(ScrumStateService);
  private exportService = inject(ReportExportService);

  @Input() initialMemberId: string = 'ALL';
  @Input() initialFormat: 'EXCEL' | 'PDF' = 'EXCEL';
  @Output() close = new EventEmitter<void>();

  selectedMemberId = signal<string>('ALL');
  selectedTimeScope = signal<'SPRINT' | 'MONTH' | 'QUARTER' | 'CUSTOM' | 'ALL'>('SPRINT');
  selectedSprintId = signal<string>('');
  selectedMonth = signal<string>(getCurrentMonthValue());
  selectedQuarter = signal<string>(getCurrentQuarterValue());
  startDate = signal<string>('');
  endDate = signal<string>('');

  monthsList = generateDynamicMonths(11, 2);
  quartersList = generateDynamicQuarters(6, 1);

  ngOnInit(): void {
    if (this.initialMemberId) {
      this.selectedMemberId.set(this.initialMemberId);
    }
    const active = this.state.activeSprint();
    if (active) {
      this.selectedSprintId.set(active.id);
    }
    const now = new Date();
    const twoWeeksAgo = new Date(Date.now() - 14 * 24 * 60 * 60 * 1000);
    this.endDate.set(now.toISOString().split('T')[0]);
    this.startDate.set(twoWeeksAgo.toISOString().split('T')[0]);
  }

  effectiveSprintId = computed(() => {
    const sp = this.selectedSprintId();
    if (sp) return sp;
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

  previewData = computed(() => {
    return this.exportService.filterData(this.currentOptions());
  });

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

  downloadExcel(): void {
    this.exportService.exportToExcel(this.currentOptions());
    this.close.emit();
  }

  downloadPdf(): void {
    this.exportService.exportToPdf(this.currentOptions());
    this.close.emit();
  }
}
