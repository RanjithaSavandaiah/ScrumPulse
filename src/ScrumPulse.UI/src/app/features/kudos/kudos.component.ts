import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { IconComponent, IconName } from '../../core/components/icon/icon.component';
import { GiveKudosModalComponent } from './components/give-kudos-modal/give-kudos-modal.component';

import { CORE_PIPES } from '../../core/pipes';

@Component({
  selector: 'app-kudos',
  standalone: true,
  imports: [CommonModule, IconComponent, GiveKudosModalComponent, ...CORE_PIPES],
  templateUrl: './kudos.component.html',
  styleUrl: './kudos.component.css'
})
export class KudosComponent {
  state = inject(ScrumStateService);
  showKudosModal = signal(false);

  filteredKudos = computed(() => {
    const list = this.state.kudos();
    const current = this.state.currentTeam();
    if (!current) return list;
    const squadMemberIds = new Set(this.state.squadMembers().map(m => m.id.toLowerCase().trim()));
    return list.filter(k =>
      (k.senderId && squadMemberIds.has(k.senderId.toLowerCase().trim())) ||
      (k.receiverId && squadMemberIds.has(k.receiverId.toLowerCase().trim()))
    );
  });

  onSaveKudos(kudosData: { senderId: string; receiverId: string; badge: number; message: string }) {
    if (kudosData.senderId && kudosData.receiverId) {
      this.state.sendKudos({
        senderId: kudosData.senderId,
        receiverId: kudosData.receiverId,
        badge: kudosData.badge,
        message: kudosData.message
      });
      this.showKudosModal.set(false);
    }
  }

  getReactionCount(card: any, key: string): number {
    if (!card || !card.reactionEmojis) return 0;
    return card.reactionEmojis[key] || 0;
  }

  getBadgeIcon(badgeType: any): IconName {
    const icons: IconName[] = ['rocket', 'users', 'target', 'shield', 'sparkles', 'trophy'];
    if (typeof badgeType === 'number') return icons[badgeType] || 'award';
    return 'award';
  }
}
