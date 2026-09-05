import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent, IconName } from '../../core/components/icon/icon.component';
import { TeamPerformanceSummary } from '../../core/models/scrum.models';

@Component({
  selector: 'app-team-performance',
  standalone: true,
  imports: [CommonModule, RouterLink, IconComponent],
  templateUrl: './team-performance.component.html',
  styleUrl: './team-performance.component.css'
})
export class TeamPerformanceComponent implements OnInit {
  protected readonly Math = Math;
  state = inject(ScrumStateService);

  summary = signal<TeamPerformanceSummary | null>(null);
  loading = signal<boolean>(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadPerformance();
  }

  loadPerformance(): void {
    this.loading.set(true);
    this.error.set(null);
    this.state.getTeamPerformanceSummary(6).subscribe({
      next: (data) => {
        this.summary.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('[TeamPerformanceComponent] Failed to load performance summary:', err);
        this.error.set(err?.message || 'Unable to load team performance data.');
        this.loading.set(false);
      }
    });
  }

  hasDataToAnalyze(): boolean {
    const s = this.summary();
    return !!s && s.sprintsAnalyzed > 0 && s.metrics.length > 0;
  }

  getGradeClass(grade: string): string {
    switch (grade) {
      case 'A+': return 'grade-aplus';
      case 'A': return 'grade-a';
      case 'B+': return 'grade-bplus';
      case 'B': return 'grade-b';
      case 'N/A':
      case '--': return 'grade-na';
      default: return 'grade-c';
    }
  }

  getTrendIcon(direction: string): IconName {
    switch (direction) {
      case 'Up': return 'trending-up';
      case 'Down': return 'trending-down';
      default: return 'minus';
    }
  }

  getTrendClass(direction: string): string {
    switch (direction) {
      case 'Up': return 'trend-up';
      case 'Down': return 'trend-down';
      default: return 'trend-stable';
    }
  }

  getMetricCategoryClass(category: string): string {
    switch (category) {
      case 'Delivery': return 'cat-delivery';
      case 'Commitment': return 'cat-commitment';
      case 'Quality': return 'cat-quality';
      case 'Efficiency': return 'cat-efficiency';
      case 'Risk': return 'cat-risk';
      case 'Culture': return 'cat-culture';
      case 'Capacity': return 'cat-capacity';
      case 'Maturity': return 'cat-maturity';
      default: return '';
    }
  }

  getMaxDelivered(): number {
    const snapshots = this.summary()?.sprintSnapshots;
    if (!snapshots || snapshots.length === 0) return 1;
    return Math.max(...snapshots.map(s => Math.max(s.deliveredPoints, s.committedPoints)), 1);
  }

  asIcon(name: string): IconName {
    return (name || 'activity') as IconName;
  }

  getHighlightSentimentClass(sentiment: string): string {
    return sentiment === 'Positive' ? 'sentiment-positive' : 'sentiment-neutral';
  }
}
