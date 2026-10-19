import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export type CaptureStatus = 'idle' | 'capturing' | 'stopped' | 'error';

interface AudioChunkTranscriptResponse {
  text: string;
}

/**
 * Captures meeting audio via `getDisplayMedia` and transcribes it server side in chunks.
 * `SpeechRecognition` cannot do this job: it listens to the microphone, where remote voices
 * arrive as speaker output and Teams' echo cancellation exists to strip exactly that.
 */
@Injectable({ providedIn: 'root' })
export class MeetingAudioCaptureService {
  /**
   * Sets the floor on transcript lag, since nothing is transcribed until its chunk finishes.
   * Shorter chunks cut more sentences mid-word and cost accuracy for little latency gain.
   */
  private static readonly ChunkDurationMs = 15_000;

  private readonly httpClient = inject(HttpClient);

  private mediaRecorder: MediaRecorder | null = null;
  private capturedStream: MediaStream | null = null;
  private microphoneStream: MediaStream | null = null;
  private mixingContext: AudioContext | null = null;

  private readonly microphoneIncluded = signal(false);
  readonly isMicrophoneIncluded = this.microphoneIncluded.asReadonly();

  private readonly transcriptSegments = signal<string[]>([]);
  private readonly statusSignal = signal<CaptureStatus>('idle');
  private readonly errorMessageSignal = signal<string | null>(null);
  private readonly pendingChunkCount = signal(0);

  readonly status = this.statusSignal.asReadonly();
  readonly errorMessage = this.errorMessageSignal.asReadonly();
  readonly isCapturing = computed(() => this.statusSignal() === 'capturing');

  readonly isTranscribing = computed(() => this.pendingChunkCount() > 0);

  readonly transcript = computed(() => this.transcriptSegments().join(' ').trim());

  readonly isSupported =
    typeof navigator !== 'undefined' && typeof navigator.mediaDevices?.getDisplayMedia === 'function';

  constructor() {
    // Releases the stream even on a route change the component never sees.
    inject(DestroyRef).onDestroy(() => this.releaseCapture());
  }

  async start(options: { includeSystemAudio?: boolean } = {}): Promise<void> {
    if (this.isCapturing()) {
      return;
    }

    // Dictation needs only the microphone; a screen-share prompt for one person speaking
    // would deter anyone from using it.
    if (options.includeSystemAudio === false) {
      await this.startMicrophoneOnly();
      return;
    }

    if (!this.isSupported) {
      this.failWith('This browser cannot capture meeting audio. Chrome and Edge can, or you can paste the transcript.');
      return;
    }

    let stream: MediaStream;

    try {
      // Video is requested only because Chrome hides "share system audio" on an audio-only
      // request. The track is discarded below.
      stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: true });
    } catch {
      // Picker dismissed, which is a choice rather than a fault.
      this.statusSignal.set('idle');
      return;
    }

    // Almost always means the user picked "Window", which Chrome and Edge cannot capture
    // audio from on any platform, so the message names the fix.
    if (stream.getAudioTracks().length === 0) {
      this.stopStreamTracks(stream);
      this.failWith(
        'No audio was shared, so only the picture was captured. Share again and choose "Entire Screen", ' +
          'then tick "Also share system audio". Sharing a single window cannot capture sound.'
      );
      return;
    }

    // Holding the picture keeps a screen capture alive for the whole ceremony.
    for (const videoTrack of stream.getVideoTracks()) {
      videoTrack.stop();
      stream.removeTrack(videoTrack);
    }

    this.capturedStream = stream;
    this.errorMessageSignal.set(null);

    // Fires when sharing is ended from the browser's own banner rather than this UI.
    stream.getAudioTracks()[0].addEventListener('ended', () => this.stop());

    // System audio carries everyone else on the call but not the local speaker, whose voice
    // leaves through the microphone and never reaches the output stream.
    const recordedStream = await this.mixInMicrophone(stream);

    const recorder = new MediaRecorder(recordedStream, { mimeType: this.resolveSupportedMimeType() });
    recorder.ondataavailable = (event) => void this.transcribeChunk(event.data);
    recorder.onerror = () => this.failWith('Recording stopped unexpectedly. The transcript so far has been kept.');

    this.mediaRecorder = recorder;
    recorder.start(MeetingAudioCaptureService.ChunkDurationMs);
    this.statusSignal.set('capturing');
  }

  private async startMicrophoneOnly(): Promise<void> {
    let microphoneStream: MediaStream;

    try {
      microphoneStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true }
      });
    } catch {
      this.failWith(
        'Microphone access was refused. Allow it in the browser address bar to dictate.'
      );
      return;
    }

    this.capturedStream = microphoneStream;
    this.microphoneStream = microphoneStream;
    this.microphoneIncluded.set(true);
    this.errorMessageSignal.set(null);

    const recorder = new MediaRecorder(microphoneStream, { mimeType: this.resolveSupportedMimeType() });
    recorder.ondataavailable = (event) => void this.transcribeChunk(event.data);
    recorder.onerror = () => this.failWith('Recording stopped unexpectedly. The text so far has been kept.');

    this.mediaRecorder = recorder;
    recorder.start(MeetingAudioCaptureService.ChunkDurationMs);
    this.statusSignal.set('capturing');
  }

  stop(): void {
    this.releaseCapture();

    if (this.statusSignal() !== 'error') {
      this.statusSignal.set('stopped');
    }
  }

  reset(): void {
    this.stop();
    this.transcriptSegments.set([]);
    this.pendingChunkCount.set(0);
    this.statusSignal.set('idle');
    this.errorMessageSignal.set(null);
  }

  /** Lets a reviewer correct a mis-heard passage. */
  overwriteTranscript(text: string): void {
    this.transcriptSegments.set(text ? [text] : []);
  }

  /**
   * Posts one chunk and appends the result. Failures are dropped rather than retried: a retry
   * spends quota on old audio and lands out of order.
   */
  private async transcribeChunk(chunk: Blob): Promise<void> {
    if (chunk.size === 0) {
      return;
    }

    // Reserve the slot before awaiting; several chunks can be in flight at once.
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
      // The slot stays empty; a placeholder could be quoted back as something someone said.
      this.errorMessageSignal.set(
        'Part of the audio could not be transcribed. Recording is continuing; check the transcript for gaps.'
      );
    } finally {
      this.pendingChunkCount.update((count) => Math.max(0, count - 1));
    }
  }

  /**
   * Mixes system audio and microphone through a Web Audio graph, because `MediaRecorder`
   * encodes only the first audio track of a stream given two.
   *
   * @returns The mixed stream, or the original system audio if the microphone is unavailable.
   */
  private async mixInMicrophone(systemAudioStream: MediaStream): Promise<MediaStream> {
    let microphoneStream: MediaStream;

    try {
      microphoneStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          // On speakers rather than headphones, the microphone would otherwise re-record
          // the remote voices already in the system audio and hand Whisper both copies.
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
      // Release the microphone rather than leave it open with the indicator lit.
      this.stopStreamTracks(microphoneStream);
      this.microphoneIncluded.set(false);

      return systemAudioStream;
    }
  }

  /** Chrome and Edge do WebM/Opus, Safari MP4, and an unsupported type throws. */
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
   * Handlers come off first: stopping a recorder fires a final `dataavailable`, which would
   * post a chunk against a capture the user already ended.
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

    // Separately acquired, so missing it leaves the recording indicator lit after the
    // standup ends.
    if (this.microphoneStream) {
      this.stopStreamTracks(this.microphoneStream);
      this.microphoneStream = null;
    }

    // An AudioContext holds an audio thread and keeps it after its inputs stop, so one leaks
    // per standup and browsers cap how many a page may hold.
    if (this.mixingContext) {
      void this.mixingContext.close().catch(() => {
        // Already closed, or closing during teardown. Nothing left to release.
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
