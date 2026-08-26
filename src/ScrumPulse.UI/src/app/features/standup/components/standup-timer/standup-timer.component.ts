import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { TeamMember } from '../../../../core/models/scrum.models';

import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-standup-timer',
  standalone: true,
  imports: [CommonModule, IconComponent, ...CORE_PIPES],
  templateUrl: './standup-timer.component.html',
  styleUrl: './standup-timer.component.css'
})
export class StandupTimerComponent {
  @Input() seconds: number = 120;
  @Input() isRunning: boolean = false;
  @Input() currentSpeakerIndex: number = 0;
  @Input() members: TeamMember[] = [];
  @Output() toggle = new EventEmitter<void>();
  @Output() reset = new EventEmitter<void>();
  @Output() nextSpeaker = new EventEmitter<void>();
  @Output() selectSpeaker = new EventEmitter<number>();

  get currentSpeaker(): TeamMember | null {
    if (this.members.length === 0) return null;
    return this.members[this.currentSpeakerIndex] || this.members[0] || null;
  }

  formatTimer(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}:${remainingSeconds < 10 ? '0' : ''}${remainingSeconds}`;
  }
}
