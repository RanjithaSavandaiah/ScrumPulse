import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { TechDebtItem } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-tech-debt-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './tech-debt-modal.component.html',
  styleUrl: './tech-debt-modal.component.css'
})
export class TechDebtModalComponent implements OnInit {
  @Input() techDebt: TechDebtItem | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<any>();
  @Output() delete = new EventEmitter<string>();

  state = inject(ScrumStateService);

  title: string = '';
  description: string = '';
  componentArea: string = '';
  severity: 'Critical' | 'High' | 'Medium' | 'Low' = 'Medium';
  estimatedHours: number = 8;
  status: string = 'Identified';
  payoffSprintId: string = '';
  assigneeId: string = '';

  severities = [
    { value: 'Critical', label: 'Critical Severity' },
    { value: 'High', label: 'High Impact' },
    { value: 'Medium', label: 'Medium Effort' },
    { value: 'Low', label: 'Low / Nice to Have' }
  ];

  statuses = [
    { value: 'Identified', label: 'Identified' },
    { value: 'In Progress', label: 'In Progress' },
    { value: 'Resolved', label: 'Resolved / Paid Off' }
  ];

  ngOnInit(): void {
    if (this.techDebt) {
      this.title = this.techDebt.title || '';
      this.description = this.techDebt.description || '';
      this.severity = (this.techDebt.severity as any) || 'Medium';
      this.estimatedHours = this.techDebt.estimatedHours || 8;
      this.status = this.techDebt.status || 'Identified';
      this.payoffSprintId = this.techDebt.payoffSprintId || '';
      this.assigneeId = this.techDebt.assigneeId || '';
    }
  }

  onSubmit(): void {
    if (!this.title.trim()) return;

    const payload: any = {
      title: this.title.trim(),
      description: this.description.trim(),
      severity: this.severity,
      estimatedHours: this.estimatedHours,
      status: this.status,
      payoffSprintId: this.payoffSprintId || null,
      assigneeId: this.assigneeId || null
    };

    if (this.techDebt?.id) {
      payload.id = this.techDebt.id;
    }

    this.save.emit(payload);
  }

  onDelete(): void {
    if (this.techDebt?.id) {
      this.delete.emit(this.techDebt.id);
    }
  }
}
