import { Component, EventEmitter, Input, OnInit, Output, signal } from '@angular/core';
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
export class ResolveBlockerModalComponent implements OnInit {
  @Input({ required: true }) blocker!: Blocker;
  @Output() close = new EventEmitter<void>();
  @Output() resolve = new EventEmitter<{ id: string; notes: string; blockedHours: number }>();

  isSubmitting = signal(false);
  validationError = signal<string | null>(null);
  notes: string = '';
  blockedHours: number = 0;

  presets: string[] = [
    'Clarified requirements with PO / Client team',
    'Access & credentials provisioned successfully',
    'Technical architecture approved by Tech Lead',
    'Third-party dependency unblocked & verified',
    'Pipeline / Environment configuration restored'
  ];

  ngOnInit(): void {
    if (this.blocker) {
      if (this.blocker.blockedHours && this.blocker.blockedHours > 0) {
        this.blockedHours = this.blocker.blockedHours;
      } else if (this.blocker.hoursWaiting > 0) {
        this.blockedHours = Math.round(this.blocker.hoursWaiting * 10) / 10;
      } else {
        this.blockedHours = 0;
      }
    }
  }

  setPreset(preset: string): void {
    this.notes = preset;
  }

  onConfirm(): void {
    if (this.isSubmitting()) return;
    if (!this.notes.trim()) {
      this.validationError.set('Resolution notes are mandatory');
      return;
    }
    this.validationError.set(null);
    this.isSubmitting.set(true);

    this.resolve.emit({
      id: this.blocker.id,
      notes: this.notes.trim(),
      blockedHours: Math.max(0, Number(this.blockedHours) || 0)
    });
  }
}
