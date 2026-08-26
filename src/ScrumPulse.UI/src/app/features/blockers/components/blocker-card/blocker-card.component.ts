import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { Blocker } from '../../../../core/models/scrum.models';

@Component({
  selector: 'app-blocker-card',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './blocker-card.component.html',
  styleUrl: './blocker-card.component.css'
})
export class BlockerCardComponent {
  @Input({ required: true }) blocker!: Blocker;
  @Output() resolve = new EventEmitter<Blocker>();
  @Output() edit = new EventEmitter<Blocker>();
  @Output() delete = new EventEmitter<Blocker>();
}
