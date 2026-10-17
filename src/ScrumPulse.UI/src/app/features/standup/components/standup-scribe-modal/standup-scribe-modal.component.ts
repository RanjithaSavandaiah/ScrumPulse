import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MeetingAudioCaptureService } from '../../../../core/services/meeting-audio-capture.service';

/** What the server reports after writing the transcript to the standup feed. */
interface AutoLoggedStandupsResponse {
  savedCount: number;
  savedMemberNames: string[];
  skippedSpeakers: string[];
  wasModelGenerated: boolean;
}

/** Steps the scribe moves through, which decide what the dialog shows. */
type ScribeStage = 'consent' | 'capture' | 'result';

/**
 * Captures a standup from the meeting audio and logs it to the feed.
 *
 * The updates it produces are ordinary standup records: same fields, same entity, same
 * timestamp as one typed by hand. Nothing about the stored row marks it as different except
 * its audit trail, which records that it was transcribed rather than typed so a mistake can
 * be traced and corrected later.
 *
 * Logging is unattended by design — the facilitator is running a ceremony, not filling in a
 * form. The safety is in what the server refuses to write rather than in a review gate: only
 * updates matched to a real roster member are saved, and anyone who could not be matched is
 * reported back so the gap is visible instead of silent.
 *
 * The consent step is not a formality. This records colleagues speaking and sends that audio
 * to a transcription provider, and in most jurisdictions recording people requires telling
 * them first. The dialog cannot start capturing until the facilitator confirms the squad has
 * been told, and a recording indicator stays visible for as long as it runs.
 */
@Component({
  selector: 'app-standup-scribe-modal',
  standalone: true,
  templateUrl: './standup-scribe-modal.component.html',
  styleUrl: './standup-scribe-modal.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StandupScribeModalComponent {
  /** Sprint the logged updates belong to, when one is active. */
  readonly sprintId = input<string | null>(null);

  /** Emitted once standups have been written, so the parent can refresh the feed. */
  readonly standupsLogged = output<number>();

  /** Emitted when the dialog is dismissed. */
  readonly closed = output<void>();

  protected readonly capture = inject(MeetingAudioCaptureService);

  private readonly httpClient = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly stage = signal<ScribeStage>('consent');
  protected readonly hasConfirmedSquadWasTold = signal(false);
  protected readonly isLogging = signal(false);
  protected readonly loggingError = signal<string | null>(null);

  protected readonly savedCount = signal(0);
  protected readonly savedMemberNames = signal<string[]>([]);
  protected readonly skippedSpeakers = signal<string[]>([]);
  protected readonly wasModelGenerated = signal(true);

  /** Shortest transcript worth sending, matching the server-side minimum. */
  private static readonly MinimumTranscriptLength = 40;

  protected get canStartCapture(): boolean {
    return this.hasConfirmedSquadWasTold() && this.capture.isSupported;
  }

  protected get canLogStandups(): boolean {
    return (
      this.capture.transcript().trim().length >= StandupScribeModalComponent.MinimumTranscriptLength &&
      !this.isLogging()
    );
  }

  protected async beginCapture(): Promise<void> {
    if (!this.canStartCapture) {
      return;
    }

    this.stage.set('capture');
    await this.capture.start();
  }

  /** Moves straight to the transcript box, for pasting or for browsers without capture. */
  protected beginManualEntry(): void {
    this.stage.set('capture');
  }

  protected async toggleCapture(): Promise<void> {
    if (this.capture.isCapturing()) {
      this.capture.stop();
      return;
    }

    await this.capture.start();
  }

  protected onTranscriptEdited(text: string): void {
    this.capture.overwriteTranscript(text);
  }

  /** Splits the transcript and writes the standups, with no review step. */
  protected logStandups(): void {
    if (!this.canLogStandups) {
      return;
    }

    // Stop capturing first. Posting while recording continues means the transcript keeps
    // growing behind the request, and the updates describe a stale conversation.
    this.capture.stop();

    this.isLogging.set(true);
    this.loggingError.set(null);

    this.httpClient
      .post<AutoLoggedStandupsResponse>('/api/standup-scribe/auto-log', {
        transcript: this.capture.transcript().trim(),
        sprintId: this.sprintId()
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.savedCount.set(response.savedCount);
          this.savedMemberNames.set(response.savedMemberNames);
          this.skippedSpeakers.set(response.skippedSpeakers);
          this.wasModelGenerated.set(response.wasModelGenerated);
          this.isLogging.set(false);
          this.stage.set('result');
          this.standupsLogged.emit(response.savedCount);
        },
        error: () => {
          this.isLogging.set(false);
          this.loggingError.set(
            'The standups could not be logged. The transcript is still here, so you can try again or log the updates manually.'
          );
        }
      });
  }

  protected close(): void {
    this.capture.reset();
    this.closed.emit();
  }
}
