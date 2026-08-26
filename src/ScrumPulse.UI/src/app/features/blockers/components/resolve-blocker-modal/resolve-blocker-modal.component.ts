import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { Blocker } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-resolve-blocker-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './resolve-blocker-modal.component.html',
  styleUrl: './resolve-blocker-modal.component.css'
})
export class ResolveBlockerModalComponent {
  @Input({ required: true }) blocker!: Blocker;
  @Output() close = new EventEmitter<void>();
  @Output() resolve = new EventEmitter<{ id: string; notes: string }>();

  notes: string = '';

  presets: string[] = [
    'Clarified requirements with PO / Client team',
    'Access & credentials provisioned successfully',
    'Technical architecture approved by Tech Lead',
    'Third-party dependency unblocked & verified',
    'Pipeline / Environment configuration restored'
  ];

  setPreset(preset: string): void {
    this.notes = preset;
  }

  onConfirm(): void {
    this.resolve.emit({
      id: this.blocker.id,
      notes: this.notes.trim() || 'Resolved with team'
    });
  }
}
