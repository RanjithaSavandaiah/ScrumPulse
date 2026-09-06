import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, ActionNotification } from '../../services/notification.service';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-confirmation-popup',
  standalone: true,
  imports: [CommonModule, IconComponent],
  templateUrl: './confirmation-popup.component.html',
  styleUrl: './confirmation-popup.component.css'
})
export class ConfirmationPopupComponent {
  notificationService = inject(NotificationService);

  get notifications(): ActionNotification[] {
    return this.notificationService.notifications();
  }

  onDismiss(id: string): void {
    this.notificationService.dismiss(id);
  }

  getVariantColor(variant: string): string {
    switch (variant) {
      case 'success': return 'var(--accent-success)';
      case 'danger': return 'var(--accent-danger)';
      case 'warning': return 'var(--accent-warning)';
      case 'info': return 'var(--accent-secondary)';
      default: return 'var(--accent-primary)';
    }
  }
}
