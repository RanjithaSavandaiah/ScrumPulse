import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { RoleType, Team } from '../../core/models/scrum.models';
import { IconComponent } from '../../core/components/icon/icon.component';
import { SmPinModalComponent } from '../../core/components/sm-pin-modal/sm-pin-modal.component';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent, SmPinModalComponent],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  state = inject(ScrumStateService);
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
    if (!teamId) {
      this.state.selectTeam(null);
    } else {
      const found = this.state.teams().find(t => t.id === teamId);
      if (found) {
        this.state.selectTeam(found);
      }
    }
  }

  openSquadModal(): void {
    this.squadError.set(null);
    this.showSquadModal.set(true);
  }

  closeSquadModal(): void {
    this.showSquadModal.set(false);
    this.newSquadName.set('');
    this.newSquadDesc.set('');
    this.joinCodeInput.set('');
    this.squadError.set(null);
  }

  handleCreateSquad(): void {
    const name = this.newSquadName().trim();
    if (!name) {
      this.squadError.set('Squad name is required.');
      return;
    }

    this.state.createTeam({
      name,
      description: this.newSquadDesc().trim()
    }).subscribe({
      next: () => {
        this.closeSquadModal();
      },
      error: (err) => {
        this.squadError.set(err?.message || 'Failed to create squad.');
      }
    });
  }

  handleJoinSquad(): void {
    const code = this.joinCodeInput().trim();
    if (!code) {
      this.squadError.set('Join code is required.');
      return;
    }

    this.state.joinTeam({ joinCode: code }).subscribe({
      next: () => {
        this.closeSquadModal();
      },
      error: (err) => {
        this.squadError.set(err?.message || 'Squad not found with this code.');
      }
    });
  }

  copySquadCode(code: string): void {
    navigator.clipboard.writeText(code);
    this.copySuccess.set(true);
    setTimeout(() => this.copySuccess.set(false), 2000);
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

