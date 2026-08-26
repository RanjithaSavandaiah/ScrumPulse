import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { WorkItem } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-quality-gates-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './quality-gates-modal.component.html',
  styleUrl: './quality-gates-modal.component.css'
})
export class QualityGatesModalComponent {
  @Input({ required: true }) item!: WorkItem;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<WorkItem>();
}
