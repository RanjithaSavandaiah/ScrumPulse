import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { NotificationService } from '../../core/services/notification.service';
import { RoleType, Team } from '../../core/models/scrum.models';
import { IconComponent } from '../../core/components/icon/icon.component';
import { SmPinModalComponent } from '../../core/components/sm-pin-modal/sm-pin-modal.component';

export const COPY_NOTIFICATION_DURATION_MS = 2000;

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, SmPinModalComponent],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  state = inject(ScrumStateService);
  notification = inject(NotificationService);
  isDark = true;
  showPinModal = signal(false);

  // Multi-Squad Modal State
  showSquadModal = signal(false);
  squadModalTab = signal<'create' | 'join'>('create');
  newSquadName = signal('');
  newSquadDesc = signal('');
  joinCodeInput = signal('');
  squadError = signal<string | null>(null);
  copySuccess = signal(false);
  isSubmittingSquad = signal(false);
  isJoiningSquad = signal(false);

  onRoleChange(changeEvent: Event) {
    const selectEl = changeEvent.target as HTMLSelectElement;
    const selectedRole = selectEl.value as RoleType;

    if (selectedRole === 'ScrumMaster') {
      if (!this.state.isSmAuthenticated()) {
        // Revert dropdown display and prompt for PIN
        selectEl.value = this.state.currentRole();
        this.showPinModal.set(true);
        return;
      }
    }

    this.state.setCurrentRole(selectedRole);
  }

  onSquadChange(event: Event): void {
    const selectEl = event.target as HTMLSelectElement;
    const teamId = selectEl.value;
    if (teamId === '__join__') {
      selectEl.value = this.state.currentTeam()?.id || '';
      this.openSquadModal('join');
      return;
    }

    if (!teamId) {
      this.state.selectTeam(null);
    } else {
      const found = this.state.teams().find(team => team.id === teamId);
      if (found) {
        this.state.selectTeam(found);
      }
    }
  }

  openSquadModal(tab: 'create' | 'join' = 'create'): void {
    this.squadError.set(null);
    // Normal developers cannot create squads; default to join tab
    if (!this.state.canEditOrDelete()) {
      this.squadModalTab.set('join');
    } else {
      this.squadModalTab.set(tab);
    }
    this.showSquadModal.set(true);
  }

  closeSquadModal(): void {
    this.showSquadModal.set(false);
    this.newSquadName.set('');
    this.newSquadDesc.set('');
    this.joinCodeInput.set('');
    this.squadError.set(null);
    this.isSubmittingSquad.set(false);
    this.isJoiningSquad.set(false);
  }

  handleCreateSquad(): void {
    if (this.isSubmittingSquad()) return;

    if (!this.state.canEditOrDelete()) {
      this.squadError.set('Only authenticated Scrum Masters can create new squads.');
      return;
    }

    const name = this.newSquadName().trim();
    if (!name) {
      this.squadError.set('Squad name is mandatory');
      return;
    }

    this.isSubmittingSquad.set(true);

    this.state.createTeam({
      name,
      description: this.newSquadDesc().trim()
    }).subscribe({
      next: (team) => {
        this.isSubmittingSquad.set(false);
        this.closeSquadModal();
        this.notification.showSuccess('Squad Created', `Squad "${team?.name || name}" added successfully.`, 'users');
      },
      error: (err) => {
        this.isSubmittingSquad.set(false);
        console.error('[NavbarComponent] Failed to create squad:', err);
        this.squadError.set(err?.error?.error || err?.message || 'Failed to create squad.');
      }
    });
  }

  handleJoinSquad(): void {
    if (this.isJoiningSquad()) return;

    const code = this.joinCodeInput().trim();
    if (!code) {
      this.squadError.set('Join code is mandatory');
      return;
    }

    this.isJoiningSquad.set(true);

    this.state.joinTeam({ joinCode: code }).subscribe({
      next: (team) => {
        this.isJoiningSquad.set(false);
        this.closeSquadModal();
        this.notification.showSuccess('Squad Joined', `Successfully joined squad "${team?.name || code}".`, 'users');
      },
      error: (err) => {
        this.isJoiningSquad.set(false);
        console.error('[NavbarComponent] Failed to join squad:', err);
        this.squadError.set(err?.message || 'Squad not found with this code.');
      }
    });
  }

  copySquadCode(code: string): void {
    navigator.clipboard?.writeText(code).catch(err => {
      console.warn('[NavbarComponent] Failed to copy squad code to clipboard:', err);
    });
    this.copySuccess.set(true);
    setTimeout(() => this.copySuccess.set(false), COPY_NOTIFICATION_DURATION_MS);
  }

  openSmAuth(): void {
    this.showPinModal.set(true);
  }

  onPinAuthenticated(): void {
    this.showPinModal.set(false);
  }

  onPinCancelled(): void {
    this.showPinModal.set(false);
  }

  lockSm(): void {
    this.state.lockSmSession();
  }

  toggleTheme() {
    this.isDark = !this.isDark;
    document.documentElement.setAttribute('data-theme', this.isDark ? 'dark' : 'light');
  }
}

