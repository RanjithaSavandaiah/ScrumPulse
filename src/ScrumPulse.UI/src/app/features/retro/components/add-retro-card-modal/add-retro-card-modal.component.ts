import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { RetroCard, TeamMember } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-add-retro-card-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './add-retro-card-modal.component.html',
  styleUrl: './add-retro-card-modal.component.css'
})
export class AddRetroCardModalComponent implements OnInit {
  @Input() members: TeamMember[] = [];
  @Input() editingCard?: RetroCard | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ category: number; authorId: string; content: string; isAnonymous: boolean }>();

  card = {
    category: 0,
    authorId: '',
    content: '',
    isAnonymous: false
  };

  ngOnInit(): void {
    if (this.editingCard) {
      const catMap: Record<string, number> = {
        'WentWell': 0,
        'DidntGoWell': 1,
        'Ideas': 2,
        'ActionItem': 3
      };
      const catVal = typeof this.editingCard.category === 'string'
        ? (catMap[this.editingCard.category] ?? 0)
        : (this.editingCard.category ?? 0);

      this.card = {
        category: catVal,
        authorId: this.editingCard.authorId || '',
        content: this.editingCard.content || '',
        isAnonymous: !!this.editingCard.isAnonymous
      };
    }
  }

  categories: { value: number; label: string; icon: IconName; color: string; desc: string }[] = [
    { value: 0, label: 'Went Well', icon: 'smile', color: 'var(--accent-success)', desc: 'Wins, smooth workflows, and good teamwork' },
    { value: 1, label: "Didn't Go Well", icon: 'frown', color: 'var(--accent-danger)', desc: 'Roadblocks, PR stalls, requirement gaps' },
    { value: 2, label: 'Ideas & Experiments', icon: 'lightbulb', color: 'var(--accent-secondary)', desc: 'New tools, pairing spikes, process trials' },
    { value: 3, label: 'Action Item', icon: 'check-square', color: 'var(--accent-purple)', desc: 'Committed improvement for next sprint' }
  ];

  presets = [
    'Pair programming during morning overlap hours accelerated PR reviews.',
    'Clear Definition of Ready eliminated mid-sprint requirement rework.',
    'Staging environment pipeline was delayed due to Azure agent restart.',
    'Try Playwright automated end-to-end regression tests next sprint.'
  ];

  applyPreset(preset: string): void {
    this.card.content = preset;
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
      default: return role;
    }
  }
}
