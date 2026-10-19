import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { TypewriterQueue } from '../utils/typewriter-queue';
import { LiveSpeechRecognitionService } from './live-speech-recognition.service';
import { MeetingAudioCaptureService } from './meeting-audio-capture.service';

export interface DictationTarget {
  readonly label: string;
  append(text: string): void;
}

export type DictationSource = 'microphone' | 'meeting';

/**
 * Speech to text on every page, routed into whichever field is active.
 *
 * Two pipelines. Dictation runs the browser's recogniser, which streams words as they are
 * spoken. A Teams call cannot: remote participants reach this machine only as speaker output,
 * which a microphone-based recogniser never hears, so the call is recorded in chunks and
 * transcribed server side - nothing can come back until a chunk finishes recording. Both feed
 * the same typewriter, which smooths the bursts into steady text.
 */
@Injectable({ providedIn: 'root' })
export class VoiceDictationService {
  private static readonly TranscriptPollMs = 500;

  private readonly capture = inject(MeetingAudioCaptureService);
  private readonly liveRecognition = inject(LiveSpeechRecognitionService);

  private readonly activeTarget = signal<DictationTarget | null>(null);

  private deliveredLength = 0;

  private transcriptPollHandle: ReturnType<typeof setInterval> | null = null;

  private readonly sourceSignal = signal<DictationSource>('microphone');
  private readonly usingLiveRecognition = signal(false);

  // Resolves the target at emit time, so a sentence queued before focus moved still lands
  // in the box the user was looking at.
  private readonly typewriter = new TypewriterQueue((slice) => this.activeTarget()?.append(slice));

  readonly source = this.sourceSignal.asReadonly();

  readonly isCapturing = computed(() => this.liveRecognition.isListening() || this.capture.isCapturing());

  readonly isTranscribing = computed(() => this.capture.isTranscribing());

  readonly errorMessage = computed(() => this.liveRecognition.errorMessage() ?? this.capture.errorMessage());

  readonly transcript = this.capture.transcript;

  /**
   * The phrase being spoken, before the recogniser commits to it. Goes to the capture bar
   * rather than the field, since interim text is revised as more is heard.
   */
  readonly interimText = this.liveRecognition.interimText;

  readonly targetLabel = computed(() => this.activeTarget()?.label ?? null);

  readonly hasTarget = computed(() => this.activeTarget() !== null);

  readonly isSupported = computed(() => this.liveRecognition.isSupported() || this.capture.isSupported);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopTranscriptPoll();
      this.typewriter.dispose();
    });
  }

  setTarget(target: DictationTarget): void {
    this.activeTarget.set(target);
  }

  /** Guarded, because clicking the capture bar's microphone blurs the field it points at. */
  clearTarget(target: DictationTarget): void {
    if (this.activeTarget() === target) {
      this.activeTarget.set(null);
    }
  }

  /** Live recogniser where the browser has one, recorded chunks everywhere else. */
  async startDictation(): Promise<void> {
    this.sourceSignal.set('microphone');

    // If permission has not yet been granted, trigger the browser's native permission prompt via getUserMedia
    if (typeof navigator !== 'undefined' && navigator.mediaDevices?.getUserMedia) {
      try {
        const permStatus = await navigator.permissions?.query?.({ name: 'microphone' as PermissionName }).catch(() => null);
        if (permStatus?.state !== 'granted') {
          const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
          stream.getTracks().forEach((track) => track.stop());
        }
      } catch (err: unknown) {
        const errorName = (err as { name?: string })?.name;
        if (errorName === 'NotAllowedError' || errorName === 'PermissionDeniedError') {
          this.liveRecognition.reportPermissionDenied();
          return;
        }
      }
    }

    if (this.liveRecognition.start((phrase) => this.queueForTyping(`${phrase} `))) {
      this.usingLiveRecognition.set(true);
      return;
    }

    this.usingLiveRecognition.set(false);
    this.deliveredLength = 0;
    await this.capture.start({ includeSystemAudio: false });
    this.watchRecordedTranscript();
  }

  /** Always recorded; the live recogniser hears only this microphone. */
  async startMeetingCapture(): Promise<void> {
    this.sourceSignal.set('meeting');
    this.usingLiveRecognition.set(false);
    this.deliveredLength = 0;
    await this.capture.start({ includeSystemAudio: true });
    this.watchRecordedTranscript();
  }

  stop(): void {
    this.liveRecognition.stop();
    this.usingLiveRecognition.set(false);
    this.capture.stop();
    this.stopTranscriptPoll();
    this.collectRecordedText();

    // Nobody should have to watch the animation finish after pressing Stop.
    this.typewriter.flush();
  }

  reset(): void {
    this.stop();
    this.capture.reset();
    this.deliveredLength = 0;
  }

  /**
   * Queues only the newly returned portion of the transcript; `deliveredLength` keeps each
   * phrase from being typed twice. A poll rather than an effect, since this starts outside
   * any injection context.
   */
  private watchRecordedTranscript(): void {
    this.stopTranscriptPoll();

    this.transcriptPollHandle = setInterval(() => {
      this.collectRecordedText();

      if (!this.capture.isCapturing() && !this.capture.isTranscribing()) {
        this.stopTranscriptPoll();
      }
    }, VoiceDictationService.TranscriptPollMs);
  }

  private stopTranscriptPoll(): void {
    if (this.transcriptPollHandle !== null) {
      clearInterval(this.transcriptPollHandle);
      this.transcriptPollHandle = null;
    }
  }

  private collectRecordedText(): void {
    const fullTranscript = this.transcript();

    if (fullTranscript.length <= this.deliveredLength) {
      return;
    }

    const newText = fullTranscript.slice(this.deliveredLength).trim();
    this.deliveredLength = fullTranscript.length;

    if (newText) {
      this.queueForTyping(`${newText} `);
    }
  }

  private queueForTyping(text: string): void {
    if (this.activeTarget()) {
      this.typewriter.enqueue(text);
    }
  }
}
