import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { Blocker } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-add-blocker-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './add-blocker-modal.component.html',
  styleUrl: './add-blocker-modal.component.css'
})
export class AddBlockerModalComponent implements OnInit {
  @Input() editBlocker: Blocker | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ title: string; description: string; category: number; slaHoursLimit: number }>();
  @Output() delete = new EventEmitter<string>();

  blocker = {
    title: '',
    description: '',
    category: 0,
    slaHoursLimit: 4
  };

  categories: { value: number; label: string; icon: IconName; desc: string }[] = [
    { value: 0, label: 'Client Requirement', icon: 'building', desc: 'Awaiting PO clarification' },
    { value: 1, label: 'Tech Lead / Arch', icon: 'user-check', desc: 'Design signoff required' },
    { value: 2, label: 'Environment & Access', icon: 'wrench', desc: 'DevOps / KeyVault / CI' },
    { value: 3, label: 'Third-Party Dependency', icon: 'git-merge', desc: 'External microservice API' }
  ];

  slaOptions = [
    { hours: 2, label: '2 Hours (High Severity)' },
    { hours: 4, label: '4 Hours (Standard)' },
    { hours: 8, label: '8 Hours (1 Business Day)' },
    { hours: 24, label: '24 Hours (Next Day)' }
  ];

  presets = [
    'Waiting on DB credentials for PostgreSQL staging',
    'Unclear acceptance criteria for OAuth scopes',
    'Third-party payment gateway mock response needed',
    'Azure Kubernetes pipeline deployment failing'
  ];

  ngOnInit(): void {
    if (this.editBlocker) {
      // Map category string to number if needed
      let catNum = 0;
      if (typeof this.editBlocker.category === 'string') {
        const catMap: Record<string, number> = {
          'ClientRequirement': 0,
          'ClientClarification': 0,
          'TechLead': 1,
          'TechLeadReview': 1,
          'Environment': 2,
          'EnvironmentAccess': 2,
          'ThirdParty': 3,
          'ThirdPartyDependency': 3
        };
        catNum = catMap[this.editBlocker.category] ?? 0;
      } else if (typeof this.editBlocker.category === 'number') {
        catNum = this.editBlocker.category;
      }

      this.blocker = {
        title: this.editBlocker.title || '',
        description: this.editBlocker.description || '',
        category: catNum,
        slaHoursLimit: this.editBlocker.slaHoursLimit || 4
      };
    }
  }

  applyPreset(preset: string): void {
    this.blocker.title = preset;
    this.blocker.description = `Urgent unblocking requested from onshore stakeholders to prevent sprint delay.`;
  }

  onDelete(): void {
    if (this.editBlocker?.id) {
      this.delete.emit(this.editBlocker.id);
    }
  }
}
