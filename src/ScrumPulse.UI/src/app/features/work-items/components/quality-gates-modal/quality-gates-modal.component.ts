import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';
import { QualityGateCriterion, Team, WorkItem } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-quality-gates-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './quality-gates-modal.component.html',
  styleUrl: './quality-gates-modal.component.css'
})
export class QualityGatesModalComponent implements OnInit {
  @Input({ required: true }) item!: WorkItem;
  @Input() team?: Team | null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<WorkItem>();
  @Output() openConfigure = new EventEmitter<void>();

  state = inject(ScrumStateService);

  criteriaChecks: Record<string, boolean> = {};

  ngOnInit(): void {
    this.criteriaChecks = { ...(this.item.qualityGateResults || {}) };

    // Seed baseline legacy criteria if not explicitly recorded yet
    if (this.criteriaChecks['dor-ac'] === undefined) {
      this.criteriaChecks['dor-ac'] = this.item.dorAcceptanceCriteriaDefined ?? true;
    }
    if (this.criteriaChecks['dor-dep'] === undefined) {
      this.criteriaChecks['dor-dep'] = this.item.dorDependenciesIdentified ?? true;
    }
    if (this.criteriaChecks['dor-wireframe'] === undefined) {
      this.criteriaChecks['dor-wireframe'] = this.item.dorWireframeAvailable ?? true;
    }
    if (this.criteriaChecks['dod-tests'] === undefined) {
      this.criteriaChecks['dod-tests'] = this.item.dodUnitTestsPassed ?? false;
    }
    if (this.criteriaChecks['dod-review'] === undefined) {
      this.criteriaChecks['dod-review'] = this.item.dodPeerReviewCompleted ?? false;
    }
    if (this.criteriaChecks['dod-master'] === undefined) {
      this.criteriaChecks['dod-master'] = this.item.dodMergedToMaster ?? false;
    }
    if (this.criteriaChecks['dod-staging'] === undefined) {
      this.criteriaChecks['dod-staging'] = this.item.dodStagingVerified ?? false;
    }
  }

  get dorCriteria(): QualityGateCriterion[] {
    const t = this.team || this.state.currentTeam();
    return t?.dorCriteria && t.dorCriteria.length > 0 ? t.dorCriteria : this.state.currentTeamDorCriteria();
  }

  get dodCriteria(): QualityGateCriterion[] {
    const t = this.team || this.state.currentTeam();
    return t?.dodCriteria && t.dodCriteria.length > 0 ? t.dodCriteria : this.state.currentTeamDodCriteria();
  }

  isCriterionChecked(id: string): boolean {
    return !!this.criteriaChecks[id];
  }

  onToggleCriterion(id: string, checked: boolean): void {
    this.criteriaChecks[id] = checked;

    // Keep legacy boolean properties in sync for compatibility
    if (id === 'dor-ac') this.item.dorAcceptanceCriteriaDefined = checked;
    if (id === 'dor-dep') this.item.dorDependenciesIdentified = checked;
    if (id === 'dor-wireframe') this.item.dorWireframeAvailable = checked;
    if (id === 'dod-tests') this.item.dodUnitTestsPassed = checked;
    if (id === 'dod-review') this.item.dodPeerReviewCompleted = checked;
    if (id === 'dod-master') this.item.dodMergedToMaster = checked;
    if (id === 'dod-staging') this.item.dodStagingVerified = checked;
  }

  getDorMetCount(): number {
    return this.dorCriteria.filter(c => this.isCriterionChecked(c.id)).length;
  }

  getDodMetCount(): number {
    return this.dodCriteria.filter(c => this.isCriterionChecked(c.id)).length;
  }

  onSave(): void {
    this.item.qualityGateResults = { ...this.criteriaChecks };
    this.save.emit(this.item);
  }

  getCriterionDomId(criterion: QualityGateCriterion): string {
    switch (criterion.id) {
      case 'dor-ac': return 'gateDorAcceptanceCriteria';
      case 'dor-dep': return 'gateDorDependencies';
      case 'dor-wireframe': return 'gateDorWireframe';
      case 'dod-tests': return 'gateDodUnitTests';
      case 'dod-review': return 'gateDodPeerReview';
      case 'dod-master': return 'gateDodMergedMaster';
      case 'dod-staging': return 'gateDodStagingVerified';
      default: return `gate-${criterion.id}`;
    }
  }
}
