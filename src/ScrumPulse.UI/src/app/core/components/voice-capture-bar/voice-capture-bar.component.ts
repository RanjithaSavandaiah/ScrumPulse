import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { VoiceDictationService } from '../../services/voice-dictation.service';

/**
 * Floating speech-to-text control, in the application shell for interactive workspace screens.
 *
 * Dictating types the microphone into the focused field. Capturing a meeting adds system
 * audio, where the other participants' voices exist only as speaker output, and gates on
 * consent because recording colleagues sends their audio to a transcription service.
 *
 * It is only displayed on application routes (/app, /dashboard, /demo) where text fields
 * and standup meetings exist, and hidden on theory, marketing, and legal pages.
 */
@Component({
  selector: 'app-voice-capture-bar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (isVisible() && dictation.isSupported()) {
      <div class="voice-bar" [class.is-live]="dictation.isCapturing()">
        @if (dictation.isCapturing()) {
          <span class="live-dot" aria-hidden="true"></span>
        }

        <div class="voice-status" aria-live="polite">
          @if (dictation.isCapturing()) {
            <strong>{{ dictation.source() === 'meeting' ? 'Capturing call' : 'Listening' }}</strong>

            <!-- Interim words land here rather than in the field, which cannot show text
                 that gets rewritten as more is heard. -->
            @if (dictation.interimText(); as spokenSoFar) {
              <small class="interim">{{ spokenSoFar }}</small>
            } @else if (dictation.targetLabel(); as fieldName) {
              <small>Text goes to {{ fieldName }}</small>
            } @else {
              <small>Click a text field to choose where text lands</small>
            }
          } @else {
            <strong>Voice to text</strong>
            <small>
              {{ dictation.targetLabel() ? 'Ready - ' + dictation.targetLabel() : 'Click a text field first' }}
            </small>
          }
        </div>

        @if (dictation.isTranscribing()) {
          <span class="transcribing" role="status">Transcribing…</span>
        }

        @if (dictation.isCapturing()) {
          <button type="button" class="voice-button stop" (click)="stop()">Stop</button>
        } @else {
          <button
            type="button"
            class="voice-button"
            [disabled]="!dictation.hasTarget()"
            (click)="dictate()"
            title="Dictate into the selected field"
          >
            <span aria-hidden="true">&#127908;</span> Dictate
          </button>

          <button type="button" class="voice-button secondary" (click)="requestMeetingCapture()">
            Capture call
          </button>
        }
      </div>
    }

    @if (isAwaitingConsent()) {
      <div class="consent-backdrop" (click)="cancelConsent()">
        <div class="consent-box" role="dialog" aria-modal="true" (click)="$event.stopPropagation()">
          <h3>Before capturing the call</h3>
          <p>
            This records the meeting audio and sends it for transcription. Tell your squad you
            are recording, and let anyone who would rather not be send their update in writing.
          </p>
          <p class="consent-hint">
            Choose <strong>Entire Screen</strong> and tick <strong>"Also share system audio"</strong>.
            Sharing a single window cannot capture sound.
          </p>

          <div class="consent-actions">
            <button type="button" class="voice-button secondary" (click)="cancelConsent()">Cancel</button>
            <button type="button" class="voice-button" (click)="confirmMeetingCapture()">
              I have told the squad
            </button>
          </div>
        </div>
      </div>
    }

    @if (isVisible() && dictation.errorMessage(); as error) {
      <div class="voice-error" role="alert">{{ error }}</div>
    }
  `,
  styleUrl: './voice-capture-bar.component.css'
})
export class VoiceCaptureBarComponent {
  protected readonly dictation = inject(VoiceDictationService);
  private readonly router = inject(Router);
  protected readonly isAwaitingConsent = signal(false);

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects || e.url)
    ),
    { initialValue: this.router.url }
  );

  /**
   * Only show the bar on application/workspace screens (/app, /demo, /dashboard)
   * or while an active capture session is ongoing.
   * Suppressed on theory, blog, landing, and marketing pages.
   */
  readonly isVisible = computed(() => {
    if (this.dictation.isCapturing()) {
      return true;
    }
    const url = this.currentUrl() ?? '';
    const cleanPath = url.split('?')[0].split('#')[0];
    return cleanPath.startsWith('/app') || cleanPath === '/dashboard' || cleanPath === '/demo';
  });

  protected async dictate(): Promise<void> {
    await this.dictation.startDictation();
  }

  /** Meeting capture records other people, so it asks first. */
  protected requestMeetingCapture(): void {
    this.isAwaitingConsent.set(true);
  }

  protected async confirmMeetingCapture(): Promise<void> {
    this.isAwaitingConsent.set(false);
    await this.dictation.startMeetingCapture();
  }

  protected cancelConsent(): void {
    this.isAwaitingConsent.set(false);
  }

  protected stop(): void {
    this.dictation.stop();
  }
}
