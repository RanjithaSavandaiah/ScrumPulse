import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { TeamMember } from '../../../../core/models/scrum.models';

import { CORE_PIPES } from '../../../../core/pipes';

export const SECONDS_PER_MINUTE = 60;
export const SINGLE_DIGIT_SECOND_THRESHOLD = 10;
export const DEFAULT_STANDUP_TIMER_SECONDS = 120;

@Component({
  selector: 'app-standup-timer',
  standalone: true,
  imports: [CommonModule, IconComponent, ...CORE_PIPES],
  templateUrl: './standup-timer.component.html',
  styleUrl: './standup-timer.component.css'
})
export class StandupTimerComponent {
  @Input() seconds: number = DEFAULT_STANDUP_TIMER_SECONDS;
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
    const minutes = Math.floor(seconds / SECONDS_PER_MINUTE);
    const remainingSeconds = seconds % SECONDS_PER_MINUTE;
    const paddedSeconds = remainingSeconds < SINGLE_DIGIT_SECOND_THRESHOLD ? `0${remainingSeconds}` : `${remainingSeconds}`;
    return `${minutes}:${paddedSeconds}`;
  }
}
