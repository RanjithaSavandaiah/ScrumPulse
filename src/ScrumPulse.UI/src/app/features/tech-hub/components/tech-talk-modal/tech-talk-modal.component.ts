import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../../../../core/components/icon/icon.component';
import { TechTalkLog } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';

import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-tech-talk-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './tech-talk-modal.component.html',
  styleUrl: './tech-talk-modal.component.css'
})
export class TechTalkModalComponent implements OnInit {
  @Input() techTalk: TechTalkLog | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<any>();
  @Output() delete = new EventEmitter<string>();

  state = inject(ScrumStateService);
  isSubmitting = signal(false);
  validationError = signal<string | null>(null);

  topic: string = '';
  presenterId: string = '';
  talkDate: string = new Date().toISOString().split('T')[0];
  durationMinutes: number = 30;
  keyTakeaways: string = '';
  slidesUrl: string = '';

  durations = [
    { value: 15, label: '15 Minutes (Lightning Talk)' },
    { value: 30, label: '30 Minutes (Standard Sharing)' },
    { value: 45, label: '45 Minutes (Deep Dive)' },
    { value: 60, label: '60 Minutes (Workshop / Demo)' }
  ];

  ngOnInit(): void {
    if (this.techTalk) {
      this.topic = this.techTalk.topic || '';
      this.presenterId = this.techTalk.presenterId || '';
      this.talkDate = this.techTalk.talkDate ? new Date(this.techTalk.talkDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0];
      this.durationMinutes = this.techTalk.durationMinutes || 30;
      this.keyTakeaways = this.techTalk.keyTakeaways || '';
      this.slidesUrl = this.techTalk.slidesUrl || '';
    } else {
      const activeUser = this.state.currentMember();
      if (activeUser) {
        this.presenterId = activeUser.id;
      }
    }
  }

  onSubmit(): void {
    if (this.isSubmitting()) return;

    if (!this.topic.trim()) {
      this.validationError.set('Tech talk topic is mandatory');
      return;
    }
    if (!this.presenterId) {
      this.validationError.set('Please select a presenter');
      return;
    }
    if (!this.talkDate) {
      this.validationError.set('Talk date is mandatory');
      return;
    }

    this.validationError.set(null);
    this.isSubmitting.set(true);

    const payload: any = {
      topic: this.topic.trim(),
      presenterId: this.presenterId,
      talkDate: new Date(this.talkDate).toISOString(),
      durationMinutes: this.durationMinutes,
      keyTakeaways: this.keyTakeaways.trim(),
      slidesUrl: this.slidesUrl.trim() || null
    };

    if (this.techTalk?.id) {
      payload.id = this.techTalk.id;
    }

    this.save.emit(payload);
  }

  onDelete(): void {
    if (this.techTalk?.id) {
      this.delete.emit(this.techTalk.id);
    }
  }
}
