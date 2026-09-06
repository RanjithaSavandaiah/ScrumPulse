import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmationPopupComponent } from './confirmation-popup.component';
import { NotificationService } from '../../services/notification.service';

describe('ConfirmationPopupComponent', () => {
  let component: ConfirmationPopupComponent;
  let fixture: ComponentFixture<ConfirmationPopupComponent>;
  let notificationService: NotificationService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConfirmationPopupComponent],
      providers: [NotificationService]
    }).compileComponents();

    notificationService = TestBed.inject(NotificationService);
    fixture = TestBed.createComponent(ConfirmationPopupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should render popups when notifications are emitted', () => {
    notificationService.showSuccess('Story Created', 'User story was successfully added');
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const card = compiled.querySelector('.confirmation-popup-card');
    expect(card).toBeTruthy();
    expect(card?.classList.contains('popup-success')).toBeTrue();
    expect(compiled.querySelector('.popup-title')?.textContent).toContain('Story Created');
    expect(compiled.querySelector('.popup-message')?.textContent).toContain('User story was successfully added');
  });

  it('should dismiss notification when close button is clicked', () => {
    const id = notificationService.showSuccess('Story Created', 'To be dismissed');
    fixture.detectChanges();

    let compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('.confirmation-popup-card').length).toBe(1);

    const closeBtn = compiled.querySelector('.popup-close-btn') as HTMLButtonElement;
    closeBtn.click();
    fixture.detectChanges();

    compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('.confirmation-popup-card').length).toBe(0);
  });
});
