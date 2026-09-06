import { Injectable, signal } from '@angular/core';
import { IconName } from '../components/icon/icon.component';

export type NotificationVariant = 'success' | 'info' | 'warning' | 'danger';

export interface ActionNotification {
  id: string;
  title: string;
  message?: string;
  variant: NotificationVariant;
  icon: IconName;
  durationMs: number;
  timestamp: number;
}

export const DEFAULT_NOTIFICATION_DURATION_MS = 3500;
export const ERROR_NOTIFICATION_DURATION_MS = 5000;

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  readonly notifications = signal<ActionNotification[]>([]);

  show(params: {
    title: string;
    message?: string;
    variant?: NotificationVariant;
    icon?: IconName;
    durationMs?: number;
  }): string {
    const id = `notif-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;
    const variant = params.variant ?? 'success';
    const defaultIcon: IconName =
      variant === 'success' ? 'check-circle' :
      variant === 'danger' ? 'trash-2' :
      variant === 'warning' ? 'alert-triangle' : 'sparkles';
    const icon: IconName = params.icon ?? defaultIcon;
    const durationMs = params.durationMs ?? (variant === 'danger' ? ERROR_NOTIFICATION_DURATION_MS : DEFAULT_NOTIFICATION_DURATION_MS);

    const notif: ActionNotification = {
      id,
      title: params.title,
      message: params.message,
      variant,
      icon,
      durationMs,
      timestamp: Date.now()
    };

    this.notifications.update(list => [...list, notif]);

    if (durationMs > 0) {
      setTimeout(() => {
        this.dismiss(id);
      }, durationMs);
    }

    return id;
  }

  showSuccess(title: string, message?: string, icon: IconName = 'check-circle'): string {
    return this.show({ title, message, variant: 'success', icon });
  }

  showError(title: string, message?: string, icon: IconName = 'alert-circle'): string {
    return this.show({ title, message, variant: 'danger', icon, durationMs: ERROR_NOTIFICATION_DURATION_MS });
  }

  showWarning(title: string, message?: string, icon: IconName = 'alert-triangle'): string {
    return this.show({ title, message, variant: 'warning', icon });
  }

  showInfo(title: string, message?: string, icon: IconName = 'sparkles'): string {
    return this.show({ title, message, variant: 'info', icon });
  }

  dismiss(id: string): void {
    this.notifications.update(list => list.filter(item => item.id !== id));
  }

  clearAll(): void {
    this.notifications.set([]);
  }
}
