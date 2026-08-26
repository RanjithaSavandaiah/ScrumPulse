import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';

export interface FibonacciGuideItem {
  points: number;
  label: string;
  hourRange: string;
  minHours: number;
  maxHours: number;
  complexity: 'Trivial' | 'Low' | 'Moderate' | 'High' | 'Very High' | 'Epic / Decompose';
  riskLevel: 'Negligible' | 'Low' | 'Medium' | 'High' | 'Extreme';
  example: string;
  actionGuidance: string;
  badgeColor: string;
}

@Component({
  selector: 'app-estimation-matrix-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './estimation-matrix-modal.component.html',
  styleUrl: './estimation-matrix-modal.component.css'
})
export class EstimationMatrixModalComponent implements OnInit {
  @Input() initialHours?: number | null = null;
  @Input() initialPoints?: number | null = null;
  @Input() isSelectionMode: boolean = false;
  @Output() close = new EventEmitter<void>();
  @Output() selectEstimation = new EventEmitter<{ points: number; hours: number }>();

  // Interactive Sandbox
  calculatorMode: 'hoursToPoints' | 'pointsToHours' = 'hoursToPoints';
  inputHours: number = 8;
  selectedPoint: number = 3;
  conversionRatio: number = 8.0; // Benchmark hours per story point

  ngOnInit(): void {
    if (this.initialHours !== undefined && this.initialHours !== null && this.initialHours > 0) {
      this.inputHours = this.initialHours;
      this.calculatorMode = 'hoursToPoints';
    } else if (this.initialPoints !== undefined && this.initialPoints !== null) {
      this.selectedPoint = this.initialPoints;
      this.calculatorMode = 'pointsToHours';
    }
  }

  readonly matrixItems: FibonacciGuideItem[] = [
    {
      points: 0,
      label: '0 SP',
      hourRange: '< 1 hour',
      minHours: 0,
      maxHours: 1,
      complexity: 'Trivial',
      riskLevel: 'Negligible',
      example: 'Copy change, typo fix, config flag toggle, documentation update.',
      actionGuidance: 'Immediate pick. Does not require dedicated sprint estimation.',
      badgeColor: '#64748b'
    },
    {
      points: 1,
      label: '1 SP',
      hourRange: '1 – 4 hours',
      minHours: 1,
      maxHours: 4,
      complexity: 'Low',
      riskLevel: 'Low',
      example: 'Simple CSS alignment, minor bug fix with known root cause, minor unit test addition.',
      actionGuidance: 'Straightforward. Single engineer can finish within half a day.',
      badgeColor: '#10b981'
    },
    {
      points: 2,
      label: '2 SP',
      hourRange: '4 – 8 hours',
      minHours: 4,
      maxHours: 8,
      complexity: 'Low',
      riskLevel: 'Low',
      example: 'Standard CRUD API endpoint, basic UI component, extending existing model.',
      actionGuidance: '~1 developer day. Well-understood scope with minimal dependencies.',
      badgeColor: '#06b6d4'
    },
    {
      points: 3,
      label: '3 SP',
      hourRange: '8 – 16 hours',
      minHours: 8,
      maxHours: 16,
      complexity: 'Moderate',
      riskLevel: 'Medium',
      example: 'Full user story with frontend UI, backend controller, service layer & unit tests.',
      actionGuidance: '~2 developer days. Standard feature baseline for sprint delivery.',
      badgeColor: '#6366f1'
    },
    {
      points: 5,
      label: '5 SP',
      hourRange: '16 – 24 hours',
      minHours: 16,
      maxHours: 24,
      complexity: 'Moderate',
      riskLevel: 'Medium',
      example: 'Complex multi-step workflow, third-party API integration, webhook receiver.',
      actionGuidance: '~3 to 4 developer days. Involves some uncertainty; consider pairing.',
      badgeColor: '#8b5cf6'
    },
    {
      points: 8,
      label: '8 SP',
      hourRange: '24 – 40 hours',
      minHours: 24,
      maxHours: 40,
      complexity: 'High',
      riskLevel: 'High',
      example: 'Major feature module, performance refactor, database schema migration with backward compatibility.',
      actionGuidance: '~1 full sprint week. High complexity. Strongly recommend breaking into 3+5 or 5+3.',
      badgeColor: '#f59e0b'
    },
    {
      points: 13,
      label: '13+ SP',
      hourRange: '> 40 hours',
      minHours: 40,
      maxHours: 100,
      complexity: 'Epic / Decompose',
      riskLevel: 'Extreme',
      example: 'New microservice architecture, complete authentication overhaul, cross-team epic.',
      actionGuidance: 'Caution: Too large for a single sprint. Must decompose into smaller stories (DoR requirement).',
      badgeColor: '#ef4444'
    }
  ];

  get calculatedPointFromHours(): FibonacciGuideItem {
    const hrs = Math.max(0, this.inputHours || 0);
    if (hrs <= 1) return this.matrixItems[0];
    if (hrs <= 4) return this.matrixItems[1];
    if (hrs <= 8) return this.matrixItems[2];
    if (hrs <= 16) return this.matrixItems[3];
    if (hrs <= 24) return this.matrixItems[4];
    if (hrs <= 40) return this.matrixItems[5];
    return this.matrixItems[6];
  }

  get calculatedHoursFromPoint(): { min: number; max: number; average: number; item: FibonacciGuideItem } {
    const found = this.matrixItems.find(m => m.points === this.selectedPoint) || this.matrixItems[3];
    const avg = found.points === 0 ? 0.5 : (found.points >= 13 ? 48 : (found.minHours + found.maxHours) / 2);
    return {
      min: found.minHours,
      max: found.maxHours,
      average: avg,
      item: found
    };
  }

  applyToItem(): void {
    if (this.calculatorMode === 'hoursToPoints') {
      const match = this.calculatedPointFromHours;
      this.selectEstimation.emit({ points: match.points, hours: this.inputHours });
    } else {
      const calc = this.calculatedHoursFromPoint;
      this.selectEstimation.emit({ points: this.selectedPoint, hours: calc.average });
    }
    this.close.emit();
  }
}
