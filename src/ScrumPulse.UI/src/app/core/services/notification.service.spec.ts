import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { NotificationService, DEFAULT_NOTIFICATION_DURATION_MS, ERROR_NOTIFICATION_DURATION_MS } from './notification.service';

describe('NotificationService', () => {
  let service: NotificationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [NotificationService]
    });
    service = TestBed.inject(NotificationService);
  });

  it('should be created with empty notifications', () => {
    expect(service).toBeTruthy();
    expect(service.notifications()).toEqual([]);
  });

  it('should show success notification and auto-dismiss after duration', fakeAsync(() => {
    const id = service.showSuccess('Squad Added', 'New squad is ready');
    expect(service.notifications().length).toBe(1);
    expect(service.notifications()[0].title).toBe('Squad Added');
    expect(service.notifications()[0].variant).toBe('success');
    expect(service.notifications()[0].icon).toBe('check-circle');

    tick(DEFAULT_NOTIFICATION_DURATION_MS + 10);
    expect(service.notifications().length).toBe(0);
  }));

  it('should show error notification with longer duration', fakeAsync(() => {
    const id = service.showError('Operation Failed', 'Something went wrong');
    expect(service.notifications().length).toBe(1);
    expect(service.notifications()[0].variant).toBe('danger');
    expect(service.notifications()[0].durationMs).toBe(ERROR_NOTIFICATION_DURATION_MS);

    tick(DEFAULT_NOTIFICATION_DURATION_MS + 10);
    expect(service.notifications().length).toBe(1);

    tick(ERROR_NOTIFICATION_DURATION_MS - DEFAULT_NOTIFICATION_DURATION_MS + 10);
    expect(service.notifications().length).toBe(0);
  }));

  it('should manually dismiss a notification by ID', () => {
    const id1 = service.showSuccess('First', 'Message 1');
    const id2 = service.showSuccess('Second', 'Message 2');
    expect(service.notifications().length).toBe(2);

    service.dismiss(id1);
    expect(service.notifications().length).toBe(1);
    expect(service.notifications()[0].id).toBe(id2);
  });

  it('should clear all notifications', () => {
    service.showSuccess('First');
    service.showWarning('Second');
    expect(service.notifications().length).toBe(2);

    service.clearAll();
    expect(service.notifications().length).toBe(0);
  });
});
