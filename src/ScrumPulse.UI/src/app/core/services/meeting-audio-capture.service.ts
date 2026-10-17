import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** What the capture is currently doing, for the UI to render against. */
export type CaptureStatus = 'idle' | 'capturing' | 'stopped' | 'error';

interface AudioChunkTranscriptResponse {
  text: string;
}

/**
 * Captures the audio of a meeting and transcribes it server side.
 *
 * This replaces an earlier attempt built on the browser's `SpeechRecognition`, which could
 * not do the job. That API listens to the microphone, so during a Teams call it hears only
 * the local speaker: remote voices arrive as speaker output, and Teams' echo cancellation is
 * specifically designed to remove speaker output from the microphone signal. On headphones
 * it hears nothing of the call at all.
 *
 * `getDisplayMedia` with audio captures the meeting itself — the tab, or the whole system on
 * Windows — so every participant is picked up. Recognition also stalls in a background tab,
 * whereas `MediaRecorder` keeps running, which matters because the Scrum Master is usually
 * looking at Teams rather than at this page.
 *
 * Audio is recorded in short chunks and each is posted as it completes, so the transcript
 * builds up during the ceremony rather than arriving in one upload at the end, and a failed
 * chunk costs a few seconds instead of the whole standup.
 */
@Injectable({ providedIn: 'root' })
export class MeetingAudioCaptureService {
  /**
   * Length of each recorded chunk.
   *
   * Thirty seconds balances three constraints: Groq's free tier allows about twenty requests
   * a minute, so this uses two; the transcript stays close enough to live to be useful; and
   * chunks stay well inside the upload ceiling. Much shorter would spend quota quickly and
   * cut sentences mid-word, which degrades transcription accuracy at the boundaries.
   */
  private static readonly ChunkDurationMs = 30_000;

  private readonly httpClient = inject(HttpClient);

  private mediaRecorder: MediaRecorder | null = null;
  private capturedStream: MediaStream | null = null;
  private microphoneStream: MediaStream | null = null;
  private mixingContext: AudioContext | null = null;

  /** True when the local microphone was mixed in, so the UI can say whose voice is covered. */
  private readonly microphoneIncluded = signal(false);
  readonly isMicrophoneIncluded = this.microphoneIncluded.asReadonly();

  private readonly transcriptSegments = signal<string[]>([]);
  private readonly statusSignal = signal<CaptureStatus>('idle');
  private readonly errorMessageSignal = signal<string | null>(null);
  private readonly pendingChunkCount = signal(0);

  readonly status = this.statusSignal.asReadonly();
  readonly errorMessage = this.errorMessageSignal.asReadonly();
  readonly isCapturing = computed(() => this.statusSignal() === 'capturing');

  /** True while at least one chunk is still being transcribed. */
  readonly isTranscribing = computed(() => this.pendingChunkCount() > 0);

  /** Everything transcribed so far, in the order it was spoken. */
  readonly transcript = computed(() => this.transcriptSegments().join(' ').trim());

  /** Whether this browser can capture system or tab audio at all. */
  readonly isSupported =
    typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getDisplayMedia === 'function';

  constructor() {
    // A component torn down mid-standup must not leave the capture running. Registering
    // here means the stream is released even on a route change the component never sees.
    inject(DestroyRef).onDestroy(() => this.releaseCapture());
  }

  /**
   * Prompts for a source to capture, then starts recording its audio.
   *
   * The browser's own picker handles consent for the capture itself. The dialog gates on the
   * separate question of whether the squad has been told they are being recorded.
   */
  async start(): Promise<void> {
    if (this.isCapturing()) {
      return;
    }

    if (!this.isSupported) {
      this.failWith('This browser cannot capture meeting audio. Chrome and Edge can, or you can paste the transcript.');
      return;
    }

    let stream: MediaStream;

    try {
      // Video is requested because Chrome will not offer the "share system audio" option for
      // an audio-only request. The video track is discarded immediately below.
      stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: true });
    } catch {
      // Thrown when the picker is dismissed, which is a choice rather than a fault.
      this.statusSignal.set('idle');
      return;
    }

    // No audio track is the single most likely way this goes wrong, and the cause is almost
    // always picking "Window": Chrome and Edge cannot capture audio from a single window on
    // any platform, so the Teams desktop app has to be captured via Entire Screen. The
    // message names the fix rather than just reporting the symptom.
    if (stream.getAudioTracks().length === 0) {
      this.stopStreamTracks(stream);
      this.failWith(
        'No audio was shared, so only the picture was captured. Share again and choose "Entire Screen", ' +
          'then tick "Also share system audio". Sharing a single window cannot capture sound.'
      );
      return;
    }

    // The picture is not wanted and holding it would keep a screen capture alive for the
    // whole ceremony, with the indicator and battery cost that implies.
    for (const videoTrack of stream.getVideoTracks()) {
      videoTrack.stop();
      stream.removeTrack(videoTrack);
    }

    this.capturedStream = stream;
    this.errorMessageSignal.set(null);

    // Fires when the user ends sharing from the browser's own banner rather than this UI.
    stream.getAudioTracks()[0].addEventListener('ended', () => this.stop());

    // System audio carries only what the machine plays, which is everyone else on the call.
    // The local speaker's own voice leaves through the microphone and never reaches the
    // output stream, so without this the Scrum Master's own update is silently missing from
    // the transcript. Mixing both sources covers the whole room.
    const recordedStream = await this.mixInMicrophone(stream);

    const recorder = new MediaRecorder(recordedStream, { mimeType: this.resolveSupportedMimeType() });
    recorder.ondataavailable = (event) => void this.transcribeChunk(event.data);
    recorder.onerror = () => this.failWith('Recording stopped unexpectedly. The transcript so far has been kept.');

    this.mediaRecorder = recorder;
    recorder.start(MeetingAudioCaptureService.ChunkDurationMs);
    this.statusSignal.set('capturing');
  }

  /** Stops recording and releases the captured stream. The transcript is kept. */
  stop(): void {
    this.releaseCapture();

    if (this.statusSignal() !== 'error') {
      this.statusSignal.set('stopped');
    }
  }

  /** Clears the transcript and returns to idle. */
  reset(): void {
    this.stop();
    this.transcriptSegments.set([]);
    this.pendingChunkCount.set(0);
    this.statusSignal.set('idle');
    this.errorMessageSignal.set(null);
  }

  /** Replaces the transcript, so a reviewer can correct a mis-heard passage before drafting. */
  overwriteTranscript(text: string): void {
    this.transcriptSegments.set(text ? [text] : []);
  }

  /**
   * Posts one recorded chunk for transcription and appends the result.
   *
   * A chunk that fails is dropped rather than retried. Retrying would spend quota replaying
   * old audio while the meeting continues, and by the time it succeeded the transcript would
   * be out of order.
   */
  private async transcribeChunk(chunk: Blob): Promise<void> {
    if (chunk.size === 0) {
      return;
    }

    // Reserve the slot before awaiting, so the segment lands in spoken order even though
    // several chunks may be in flight at once.
    const segmentIndex = this.transcriptSegments().length;
    this.transcriptSegments.update((segments) => [...segments, '']);
    this.pendingChunkCount.update((count) => count + 1);

    const formData = new FormData();
    formData.append('audio', chunk, `standup-chunk-${segmentIndex}.webm`);

    try {
      const response = await firstValueFrom(
        this.httpClient.post<AudioChunkTranscriptResponse>('/api/standup-scribe/transcribe', formData)
      );

      this.transcriptSegments.update((segments) =>
        segments.map((segment, index) => (index === segmentIndex ? response.text : segment))
      );
    } catch {
      // Leaves the reserved slot empty rather than inserting a placeholder, so the gap does
      // not end up quoted back as something a colleague said.
      this.errorMessageSignal.set(
        'Part of the audio could not be transcribed. Recording is continuing; check the transcript for gaps.'
      );
    } finally {
      this.pendingChunkCount.update((count) => Math.max(0, count - 1));
    }
  }

  /**
   * Combines the captured system audio with the local microphone into one recorded stream.
   *
   * Uses a Web Audio graph rather than adding both tracks to a single `MediaStream`, because
   * `MediaRecorder` encodes only the first audio track it is given. Routing both through a
   * `MediaStreamAudioDestinationNode` mixes them into one track that carries everyone.
   *
   * Microphone access is best-effort. If the user declines, or no input device exists, the
   * capture continues with system audio alone — a transcript missing the local speaker is
   * far better than no transcript, and the dialog says which case applies.
   *
   * @returns The stream to record: mixed when the microphone is available, otherwise the
   * original system-audio stream.
   */
  private async mixInMicrophone(systemAudioStream: MediaStream): Promise<MediaStream> {
    let microphoneStream: MediaStream;

    try {
      microphoneStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          // Teams applies its own processing to what it sends, but this stream is raw local
          // input. Cancellation matters most when the user is on speakers rather than
          // headphones, where the microphone would otherwise re-record the remote voices
          // already present in the system audio and hand Whisper everything twice.
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true
        }
      });
    } catch {
      this.microphoneIncluded.set(false);
      return systemAudioStream;
    }

    try {
      const mixingContext = new AudioContext();
      const destination = mixingContext.createMediaStreamDestination();

      mixingContext.createMediaStreamSource(systemAudioStream).connect(destination);
      mixingContext.createMediaStreamSource(microphoneStream).connect(destination);

      this.mixingContext = mixingContext;
      this.microphoneStream = microphoneStream;
      this.microphoneIncluded.set(true);

      return destination.stream;
    } catch {
      // Mixing failed, so release the microphone rather than leaving it open and unused,
      // which would keep the browser's recording indicator lit for no reason.
      this.stopStreamTracks(microphoneStream);
      this.microphoneIncluded.set(false);

      return systemAudioStream;
    }
  }

  /**
   * Picks a container the browser can actually record.
   *
   * Chrome and Edge produce WebM/Opus; Safari does not and falls back to MP4. Passing an
   * unsupported type to MediaRecorder throws, so this is checked rather than assumed.
   */
  private resolveSupportedMimeType(): string {
    const candidates = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4'];

    return candidates.find((candidate) => MediaRecorder.isTypeSupported(candidate)) ?? '';
  }

  private failWith(message: string): void {
    this.releaseCapture();
    this.statusSignal.set('error');
    this.errorMessageSignal.set(message);
  }

  /**
   * Stops the recorder and every captured track.
   *
   * Handlers are detached first: stopping a recorder fires a final `dataavailable`, and
   * during teardown that chunk would post against a capture the user has already ended.
   */
  private releaseCapture(): void {
    const recorder = this.mediaRecorder;

    if (recorder) {
      recorder.ondataavailable = null;
      recorder.onerror = null;

      if (recorder.state !== 'inactive') {
        recorder.stop();
      }

      this.mediaRecorder = null;
    }

    if (this.capturedStream) {
      this.stopStreamTracks(this.capturedStream);
      this.capturedStream = null;
    }

    // The microphone is a second, separately acquired stream. Missing it here would leave
    // the browser's recording indicator lit after the standup ended.
    if (this.microphoneStream) {
      this.stopStreamTracks(this.microphoneStream);
      this.microphoneStream = null;
    }

    // An AudioContext holds an audio thread and does not release it when its inputs stop.
    // Leaving one open per standup leaks a thread each time, and browsers cap how many a
    // page may hold before refusing to create more.
    if (this.mixingContext) {
      void this.mixingContext.close().catch(() => {
        // Already closed, or closing during teardown. Nothing further to release.
      });

      this.mixingContext = null;
    }

    this.microphoneIncluded.set(false);
  }

  private stopStreamTracks(stream: MediaStream): void {
    for (const track of stream.getTracks()) {
      track.stop();
    }
  }
}
