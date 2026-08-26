import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent, IconName } from '../../../../core/components/icon/icon.component';
import { TeamMember } from '../../../../core/models/scrum.models';
import { ScrumStateService } from '../../../../core/services/scrum-state.service';

import { CORE_PIPES } from '../../../../core/pipes';

@Component({
  selector: 'app-give-kudos-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, ...CORE_PIPES],
  templateUrl: './give-kudos-modal.component.html',
  styleUrl: './give-kudos-modal.component.css'
})
export class GiveKudosModalComponent implements OnInit {
  state = inject(ScrumStateService);

  @Input() members: TeamMember[] = [];
  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<{ senderId: string; receiverId: string; badge: number; message: string }>();

  kudos = {
    senderId: '',
    receiverId: '',
    badge: 0,
    message: ''
  };

  get teamMembers(): TeamMember[] {
    return this.members.length > 0 ? this.members : this.state.members();
  }

  ngOnInit(): void {
    if (!this.kudos.senderId && this.teamMembers.length > 0) {
      this.kudos.senderId = this.teamMembers[0].id;
    }
    if (!this.kudos.receiverId && this.teamMembers.length > 1) {
      this.kudos.receiverId = this.teamMembers[1].id;
    } else if (!this.kudos.receiverId && this.teamMembers.length > 0) {
      this.kudos.receiverId = this.teamMembers[0].id;
    }
  }

  badges: { value: number; label: string; icon: IconName; desc: string }[] = [
    { value: 0, label: 'Problem Solver', icon: 'rocket', desc: 'Resolved a complex technical roadblock' },
    { value: 1, label: 'Team Player', icon: 'users', desc: 'Went above and beyond to support a teammate' },
    { value: 2, label: 'Goal Crusher', icon: 'target', desc: 'Delivered high-velocity sprint commitments' },
    { value: 3, label: 'Quality Guardian', icon: 'shield-check', desc: 'Zero defect mindset & rigorous testing' },
    { value: 4, label: 'Innovation Star', icon: 'zap', desc: 'Introduced an awesome new idea or tool' },
    { value: 5, label: 'Client Shoutout', icon: 'star', desc: 'Received outstanding client demo praise' }
  ];

  presets = [
    'Super fast PR turnaround time! Really appreciate your pairing support.',
    'Amazing job presenting the architectural walkthrough at the tech talk.',
    'Huge shoutout for achieving 100% test coverage and zero QA defects!',
    'Great leadership during sprint demo! The client was super impressed.'
  ];

  setPreset(preset: string): void {
    this.kudos.message = preset;
  }

  getRoleLabel(role: string): string {
    switch (role) {
      case 'ScrumMaster': return 'Scrum Master';
      case 'Developer': return 'Developer';
      case 'QaEngineer': return 'QA Engineer';
      case 'Cdl': return 'CDL';
      default: return role;
    }
  }
}
