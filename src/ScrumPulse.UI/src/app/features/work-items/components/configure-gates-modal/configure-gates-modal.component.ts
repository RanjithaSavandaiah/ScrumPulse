import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { NotificationService } from '../../../../core/services/notification.service';
import { ScrumStateService, DEFAULT_DOR_CRITERIA, DEFAULT_DOD_CRITERIA } from '../../../../core/services/scrum-state.service';
import { QualityGateCriterion, Team } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-configure-gates-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './configure-gates-modal.component.html',
  styleUrl: './configure-gates-modal.component.css'
})
export class ConfigureGatesModalComponent implements OnInit {
  @Input({ required: true }) team!: Team;
  @Output() close = new EventEmitter<void>();
  @Output() saved = new EventEmitter<Team>();

  private state = inject(ScrumStateService);
  private notification = inject(NotificationService);

  activeGateTab = signal<'dor' | 'dod'>('dor');
  dorList = signal<QualityGateCriterion[]>([]);
  dodList = signal<QualityGateCriterion[]>([]);

  newCriterionLabel = signal<string>('');
  newCriterionDesc = signal<string>('');
  newCriterionRequired = signal<boolean>(true);

  isSubmitting = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const dor = this.team.dorCriteria && this.team.dorCriteria.length > 0
      ? this.team.dorCriteria.map(c => ({ ...c }))
      : DEFAULT_DOR_CRITERIA.map(c => ({ ...c }));

    const dod = this.team.dodCriteria && this.team.dodCriteria.length > 0
      ? this.team.dodCriteria.map(c => ({ ...c }))
      : DEFAULT_DOD_CRITERIA.map(c => ({ ...c }));

    this.dorList.set(dor);
    this.dodList.set(dod);
  }

  addCriterion(): void {
    const label = this.newCriterionLabel().trim();
    if (!label) {
      this.errorMessage.set('Please provide a criteria description.');
      return;
    }

    const tab = this.activeGateTab();
    const newId = `${tab}-${Date.now()}`;
    const item: QualityGateCriterion = {
      id: newId,
      label,
      description: this.newCriterionDesc().trim() || undefined,
      isRequired: this.newCriterionRequired()
    };

    if (tab === 'dor') {
      this.dorList.update(list => [...list, item]);
    } else {
      this.dodList.update(list => [...list, item]);
    }

    this.newCriterionLabel.set('');
    this.newCriterionDesc.set('');
    this.newCriterionRequired.set(true);
    this.errorMessage.set(null);
  }

  removeCriterion(tab: 'dor' | 'dod', index: number): void {
    if (tab === 'dor') {
      if (this.dorList().length <= 1) {
        this.errorMessage.set('At least one Definition of Ready criterion must remain.');
        return;
      }
      this.dorList.update(list => list.filter((_, i) => i !== index));
    } else {
      if (this.dodList().length <= 1) {
        this.errorMessage.set('At least one Definition of Done criterion must remain.');
        return;
      }
      this.dodList.update(list => list.filter((_, i) => i !== index));
    }
    this.errorMessage.set(null);
  }

  moveUp(tab: 'dor' | 'dod', index: number): void {
    if (index <= 0) return;
    const target = tab === 'dor' ? this.dorList : this.dodList;
    target.update(list => {
      const copy = [...list];
      const temp = copy[index - 1];
      copy[index - 1] = copy[index];
      copy[index] = temp;
      return copy;
    });
  }

  moveDown(tab: 'dor' | 'dod', index: number): void {
    const target = tab === 'dor' ? this.dorList : this.dodList;
    if (index >= target().length - 1) return;
    target.update(list => {
      const copy = [...list];
      const temp = copy[index + 1];
      copy[index + 1] = copy[index];
      copy[index] = temp;
      return copy;
    });
  }

  toggleRequired(criterion: QualityGateCriterion): void {
    criterion.isRequired = !criterion.isRequired;
  }

  resetToDefaults(): void {
    if (this.activeGateTab() === 'dor') {
      this.dorList.set(DEFAULT_DOR_CRITERIA.map(c => ({ ...c })));
    } else {
      this.dodList.set(DEFAULT_DOD_CRITERIA.map(c => ({ ...c })));
    }
    this.errorMessage.set(null);
    this.notification.showInfo('Reset to Standards', `Reset ${this.activeGateTab().toUpperCase()} criteria to recommended agile defaults.`, 'rotate-ccw');
  }

  onSave(): void {
    if (this.dorList().length === 0) {
      this.errorMessage.set('Please include at least one Definition of Ready criterion.');
      return;
    }
    if (this.dodList().length === 0) {
      this.errorMessage.set('Please include at least one Definition of Done criterion.');
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    this.state.configureTeamQualityGates(this.team.id, this.dorList(), this.dodList()).subscribe({
      next: updatedTeam => {
        this.isSubmitting.set(false);
        this.notification.showSuccess(
          'Quality Gates Configured',
          `DoR and DoD standards saved for ${updatedTeam.name}.`,
          'shield-check'
        );
        this.saved.emit(updatedTeam);
        this.close.emit();
      },
      error: err => {
        this.isSubmitting.set(false);
        const errorMsg = err?.error?.error || 'Failed to save squad quality gates.';
        this.errorMessage.set(errorMsg);
      }
    });
  }
}
