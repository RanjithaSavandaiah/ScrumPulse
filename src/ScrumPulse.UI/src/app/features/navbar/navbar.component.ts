import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ScrumStateService } from '../../core/services/scrum-state.service';
import { RoleType } from '../../core/models/scrum.models';
import { IconComponent } from '../../core/components/icon/icon.component';
import { SmPinModalComponent } from '../../core/components/sm-pin-modal/sm-pin-modal.component';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, IconComponent, SmPinModalComponent],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css'
})
export class NavbarComponent {
  state = inject(ScrumStateService);
  isDark = true;
  showPinModal = signal(false);

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

