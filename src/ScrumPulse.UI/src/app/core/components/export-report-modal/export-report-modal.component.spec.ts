import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideStore } from '@ngrx/store';
import { ExportReportModalComponent } from './export-report-modal.component';
import { ScrumStateService } from '../../services/scrum-state.service';
import { ReportExportService } from '../../services/report-export.service';
import { appReducers } from '../../state';

describe('ExportReportModalComponent', () => {
  let component: ExportReportModalComponent;
  let fixture: ComponentFixture<ExportReportModalComponent>;
  let exportService: ReportExportService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExportReportModalComponent],
      providers: [
        ScrumStateService,
        ReportExportService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ExportReportModalComponent);
    component = fixture.componentInstance;
    exportService = TestBed.inject(ReportExportService);
    fixture.detectChanges();
  });

  it('should create and initialize default dates', () => {
    expect(component).toBeTruthy();
    expect(component.selectedTimeScope()).toBe('SPRINT');
    expect(component.startDate()).toBeDefined();
    expect(component.endDate()).toBeDefined();
  });

  it('should apply presets cleanly', () => {
    component.applyCustomPreset(30);
    expect(component.startDate()).toBeDefined();
    expect(component.endDate()).toBeDefined();

    component.applyThisMonthPreset();
    expect(component.startDate()).toBeDefined();

    component.applySprintDatesPreset();
    expect(component.startDate()).toBeDefined();
  });

  it('should trigger excel download and emit close', () => {
    spyOn(exportService, 'exportToExcel');
    spyOn(component.close, 'emit');

    component.downloadExcel();
    expect(exportService.exportToExcel).toHaveBeenCalled();
    expect(component.close.emit).toHaveBeenCalled();
  });

  it('should trigger pdf download and emit close', () => {
    spyOn(exportService, 'exportToPdf');
    spyOn(component.close, 'emit');

    component.downloadPdf();
    expect(exportService.exportToPdf).toHaveBeenCalled();
    expect(component.close.emit).toHaveBeenCalled();
  });
});
