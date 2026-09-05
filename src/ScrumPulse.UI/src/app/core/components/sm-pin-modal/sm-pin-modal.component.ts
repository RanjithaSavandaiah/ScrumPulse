import { Component, ElementRef, EventEmitter, HostListener, OnInit, Output, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IconComponent } from '../icon/icon.component';
import { ScrumStateService } from '../../services/scrum-state.service';

@Component({
  selector: 'app-sm-pin-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, IconComponent],
  templateUrl: './sm-pin-modal.component.html',
  styleUrl: './sm-pin-modal.component.css'
})
export class SmPinModalComponent implements OnInit {
  private state = inject(ScrumStateService);

  @Output() authenticated = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  @ViewChild('pinInput') pinInputRef!: ElementRef<HTMLInputElement>;

  pin: string = '';
  errorMessage = signal<string>('');
  isShaking = signal<boolean>(false);
  showDigits = signal<boolean>(false);
  isVerifying = signal<boolean>(false);

  digits = [1, 2, 3, 4, 5, 6, 7, 8, 9];

  ngOnInit(): void {
    setTimeout(() => {
      this.focusInput();
    }, 100);
  }

  focusInput(): void {
    if (!this.isVerifying()) {
      this.pinInputRef?.nativeElement?.focus();
    }
  }

  @HostListener('window:keydown', ['$event'])
  handleKeyDown(event: KeyboardEvent): void {
    if (this.isVerifying()) return;

    if (event.key === 'Escape') {
      this.onCancel();
    } else if (event.key === 'Enter') {
      this.submitPin();
    } else if (event.key >= '0' && event.key <= '9') {
      if (this.pin.length < 4) {
        this.appendDigit(event.key);
      }
    } else if (event.key === 'Backspace') {
      this.backspace();
    }
  }

  appendDigit(digit: number | string): void {
    if (this.isVerifying()) return;

    if (this.pin.length < 4) {
      this.pin += String(digit);
      this.errorMessage.set('');
      if (this.pin.length === 4) {
        // Auto-verify when 4 digits entered
        setTimeout(() => this.submitPin(), 120);
      }
    }
  }

  backspace(): void {
    if (this.isVerifying()) return;

    if (this.pin.length > 0) {
      this.pin = this.pin.slice(0, -1);
      this.errorMessage.set('');
    }
  }

  clear(): void {
    if (this.isVerifying()) return;

    this.pin = '';
    this.errorMessage.set('');
  }

  submitPin(): void {
    if (this.isVerifying()) return;

    if (!this.pin || this.pin.length < 4) {
      this.triggerError('Please enter your 4-digit Scrum Master PIN.');
      return;
    }

    this.isVerifying.set(true);
    this.state.verifyAndUnlockSm(this.pin).subscribe({
      next: (success) => {
        this.isVerifying.set(false);
        if (success) {
          this.authenticated.emit();
        } else {
          this.triggerError('Incorrect Security PIN. Scrum Master access denied.');
          this.pin = '';
        }
      },
      error: (err) => {
        console.error('[SmPinModal] Verification request failed:', err);
        this.isVerifying.set(false);
        this.triggerError('Authentication failed. Server unreachable.');
        this.pin = '';
      }
    });
  }

  private triggerError(msg: string): void {
    this.errorMessage.set(msg);
    this.isShaking.set(true);
    setTimeout(() => this.isShaking.set(false), 500);
  }

  onCancel(): void {
    if (this.isVerifying()) return;
    this.cancelled.emit();
  }
}
