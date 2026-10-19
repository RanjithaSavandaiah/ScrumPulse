import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';

/** Local declaration: TypeScript's DOM library still ships no Web Speech API types. */
interface SpeechRecognitionLike {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  maxAlternatives: number;
  start(): void;
  stop(): void;
  abort(): void;
  onresult: ((event: SpeechRecognitionResultEventLike) => void) | null;
  onerror: ((event: { error: string }) => void) | null;
  onend: (() => void) | null;
}

interface SpeechRecognitionResultEventLike {
  readonly resultIndex: number;
  readonly results: ArrayLike<{
    readonly isFinal: boolean;
    readonly length: number;
    readonly [index: number]: { readonly transcript: string };
  }>;
}

type SpeechRecognitionConstructor = new () => SpeechRecognitionLike;

/**
 * Live speech to text for the user's own voice, streaming results as the words are spoken and
 * costing no transcription quota.
 *
 * Only the microphone is heard, so meeting capture keeps its recorded path. Chrome also sends
 * the audio to Google's servers, so this runs for the user's own voice after an explicit click
 * and never for a colleague's; see the README privacy notes. Falls back to recorded chunks
 * where {@link isSupported} is false.
 */
@Injectable({ providedIn: 'root' })
export class LiveSpeechRecognitionService {
  /**
   * Chrome ends a session on a long pause even with `continuous` set, so sessions are
   * restarted until this much silence has passed. A pause to think survives; walking away does
   * not.
   */
  private static readonly SilenceTimeoutMs = 60_000;

  private recogniser: SpeechRecognitionLike | null = null;
  private lastResultAtMs = 0;

  /** Lets the `onend` restart tell a requested stop from one the browser decided on. */
  private isStopping = false;

  private readonly listeningSignal = signal(false);
  private readonly errorMessageSignal = signal<string | null>(null);
  private readonly interimTextSignal = signal('');

  readonly isListening = this.listeningSignal.asReadonly();
  readonly errorMessage = this.errorMessageSignal.asReadonly();

  /**
   * Uncommitted text, for the capture bar to show as a hint. Writing it into the field would
   * make it flicker between guesses as the recogniser revises.
   */
  readonly interimText = this.interimTextSignal.asReadonly();

  private onFinalText: ((text: string) => void) | null = null;

  readonly isSupported = computed(() => LiveSpeechRecognitionService.resolveConstructor() !== null);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.release());
  }

  /**
   * @param onFinalText Receives finalised text only; interim guesses go to {@link interimText},
   * so the caller is never handed a phrase that later changes.
   */
  start(onFinalText: (text: string) => void): boolean {
    const RecogniserConstructor = LiveSpeechRecognitionService.resolveConstructor();

    if (!RecogniserConstructor) {
      return false;
    }

    if (this.listeningSignal()) {
      return true;
    }

    this.onFinalText = onFinalText;
    this.errorMessageSignal.set(null);
    this.isStopping = false;
    this.lastResultAtMs = Date.now();

    const recogniser = new RecogniserConstructor();

    recogniser.continuous = true;
    recogniser.interimResults = true;
    recogniser.maxAlternatives = 1;
    recogniser.lang = LiveSpeechRecognitionService.resolveLanguage();

    recogniser.onresult = (event) => this.handleResult(event);
    recogniser.onerror = (event) => this.handleError(event.error);
    recogniser.onend = () => this.handleEnd();

    this.recogniser = recogniser;

    try {
      recogniser.start();
    } catch {
      // Already running, usually a previous session still tearing down. It does the job.
      return true;
    }

    this.listeningSignal.set(true);

    return true;
  }

  stop(): void {
    this.isStopping = true;
    this.release();
  }

  private handleResult(event: SpeechRecognitionResultEventLike): void {
    this.lastResultAtMs = Date.now();

    let interim = '';

    // From `resultIndex` onward only; reading from zero re-delivers the whole session.
    for (let index = event.resultIndex; index < event.results.length; index += 1) {
      const result = event.results[index];
      const transcript = result[0]?.transcript ?? '';

      if (result.isFinal) {
        const committed = transcript.trim();

        if (committed) {
          this.onFinalText?.(committed);
        }
      } else {
        interim += transcript;
      }
    }

    this.interimTextSignal.set(interim.trim());
  }

  private handleError(error: string): void {
    // Chrome reports a pause in speech as an error and ends the session; the restart in
    // handleEnd covers it, and a red banner for someone who stopped to think does not.
    if (error === 'no-speech' || error === 'aborted') {
      return;
    }

    this.errorMessageSignal.set(
      error === 'not-allowed' || error === 'service-not-allowed'
        ? 'Microphone access was refused. Allow it in the browser address bar to dictate.'
        : 'Speech recognition stopped unexpectedly. The text so far has been kept.'
    );

    this.isStopping = true;
    this.release();
  }

  /** Restarts a session the browser closed on its own, bounded by the silence window. */
  private handleEnd(): void {
    if (this.isStopping || !this.recogniser) {
      this.listeningSignal.set(false);
      return;
    }

    const silentForMs = Date.now() - this.lastResultAtMs;

    if (silentForMs >= LiveSpeechRecognitionService.SilenceTimeoutMs) {
      this.release();
      return;
    }

    try {
      this.recogniser.start();
    } catch {
      // Unrevivable, so settle into stopped rather than claiming to listen.
      this.release();
    }
  }

  private release(): void {
    const recogniser = this.recogniser;

    if (recogniser) {
      recogniser.onresult = null;
      recogniser.onerror = null;
      recogniser.onend = null;

      try {
        recogniser.abort();
      } catch {
        // Already finished; the microphone is released either way.
      }

      this.recogniser = null;
    }

    this.listeningSignal.set(false);
    this.interimTextSignal.set('');
  }

  /** Chrome and Edge expose this prefixed; the unprefixed name is the eventual standard. */
  private static resolveConstructor(): SpeechRecognitionConstructor | null {
    if (typeof window === 'undefined') {
      return null;
    }

    const candidate = window as unknown as {
      SpeechRecognition?: SpeechRecognitionConstructor;
      webkitSpeechRecognition?: SpeechRecognitionConstructor;
    };

    return candidate.SpeechRecognition ?? candidate.webkitSpeechRecognition ?? null;
  }

  /** Follows the browser, so a distributed squad is not forced into one accent model. */
  private static resolveLanguage(): string {
    return typeof navigator !== 'undefined' && navigator.language ? navigator.language : 'en-US';
  }
}
