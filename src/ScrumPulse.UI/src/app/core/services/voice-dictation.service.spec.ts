import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { DictationTarget, VoiceDictationService } from './voice-dictation.service';
import { LiveSpeechRecognitionService } from './live-speech-recognition.service';
import { MeetingAudioCaptureService } from './meeting-audio-capture.service';

/**
 * Covers the routing between the two speech pipelines and the field they write into.
 *
 * The decisions worth protecting are which pipeline runs for which job, and the fact that no
 * phrase reaches a field twice. Both have obvious failure modes: routing a call through the
 * microphone recogniser silently transcribes only the local speaker, and re-delivering the
 * whole transcript on each poll fills the field with repeats.
 */
describe('VoiceDictationService', () => {
  /** Captures what a field receives, standing in for a real form control. */
  class RecordingTarget implements DictationTarget {
    readonly received: string[] = [];

    constructor(readonly label: string) {}

    append(text: string): void {
      this.received.push(text);
    }

    get text(): string {
      return this.received.join('');
    }
  }

  class FakeLiveRecognition {
    readonly isListening = signal(false);
    readonly errorMessage = signal<string | null>(null);
    readonly interimText = signal('');
    readonly isSupported = signal(true);

    startCallCount = 0;
    stopCallCount = 0;

    private onFinalText: ((text: string) => void) | null = null;

    start(onFinalText: (text: string) => void): boolean {
      this.startCallCount += 1;

      if (!this.isSupported()) {
        return false;
      }

      this.onFinalText = onFinalText;
      this.isListening.set(true);

      return true;
    }

    stop(): void {
      this.stopCallCount += 1;
      this.isListening.set(false);
    }

    reportPermissionDenied(): void {
      this.errorMessage.set('Microphone access was refused. Allow it in the browser address bar to dictate.');
      this.isListening.set(false);
    }

    /** Commits a phrase, as the browser recogniser would. */
    commit(phrase: string): void {
      this.onFinalText?.(phrase);
    }
  }

  class FakeMeetingCapture {
    readonly isCapturing = signal(false);
    readonly isTranscribing = signal(false);
    readonly errorMessage = signal<string | null>(null);
    readonly transcript = signal('');
    readonly isSupported = true;

    startCallCount = 0;
    lastOptions: { includeSystemAudio?: boolean } | null = null;

    async start(options: { includeSystemAudio?: boolean } = {}): Promise<void> {
      this.startCallCount += 1;
      this.lastOptions = options;
      this.isCapturing.set(true);
    }

    stop(): void {
      this.isCapturing.set(false);
    }

    reset(): void {
      this.transcript.set('');
      this.isCapturing.set(false);
    }

    /** Appends a transcribed chunk, as a completed upload would. */
    appendChunk(text: string): void {
      this.transcript.update((existing) => (existing ? `${existing} ${text}` : text));
    }
  }

  let liveRecognition: FakeLiveRecognition;
  let meetingCapture: FakeMeetingCapture;
  let service: VoiceDictationService;
  let target: RecordingTarget;

  /** Runs long enough for the typewriter to drain whatever it is holding. */
  function drainTypewriter(): void {
    jasmine.clock().tick(5_000);
  }

  beforeEach(() => {
    jasmine.clock().install();

    liveRecognition = new FakeLiveRecognition();
    meetingCapture = new FakeMeetingCapture();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: LiveSpeechRecognitionService, useValue: liveRecognition },
        { provide: MeetingAudioCaptureService, useValue: meetingCapture }
      ]
    });

    service = TestBed.inject(VoiceDictationService);
    target = new RecordingTarget('Blocker description');
  });

  afterEach(() => {
    service.stop();
    jasmine.clock().uninstall();
  });

  it('names the field that will receive text', () => {
    service.setTarget(target);

    expect(service.targetLabel()).toBe('Blocker description');
    expect(service.hasTarget()).toBeTrue();
  });

  it('gives the most recently focused field the text', () => {
    const second = new RecordingTarget('Retro note');

    service.setTarget(target);
    service.setTarget(second);

    expect(service.targetLabel()).toBe('Retro note');
  });

  it('keeps the target when a different field releases it', () => {
    // Focus moves to the capture bar's own button when it is clicked. Clearing on that blur
    // would drop the target at the exact moment dictation starts.
    const second = new RecordingTarget('Retro note');
    service.setTarget(target);

    service.clearTarget(second);

    expect(service.targetLabel()).toBe('Blocker description');
  });

  it('clears the target when the active field releases it', () => {
    service.setTarget(target);

    service.clearTarget(target);

    expect(service.hasTarget()).toBeFalse();
  });

  it('dictates through the live recogniser rather than recording', async () => {
    // The point of the live path: no upload, no chunk window, no transcription quota spent.
    service.setTarget(target);

    await service.startDictation();

    expect(liveRecognition.startCallCount).toBe(1);
    expect(meetingCapture.startCallCount).toBe(0);
  });

  it('falls back to recording where the browser has no recogniser', async () => {
    liveRecognition.isSupported.set(false);
    service.setTarget(target);

    await service.startDictation();

    expect(meetingCapture.startCallCount).toBe(1);
    expect(meetingCapture.lastOptions?.includeSystemAudio).toBeFalse();
  });

  it('never routes a call through the microphone recogniser', async () => {
    // It would hear only the local speaker, and the whole purpose of capturing a call is
    // everyone else on it. Failing this test means silently losing the squad's updates.
    service.setTarget(target);

    await service.startMeetingCapture();

    expect(liveRecognition.startCallCount).toBe(0);
    expect(meetingCapture.lastOptions?.includeSystemAudio).toBeTrue();
  });

  it('reports which pipeline is running', async () => {
    await service.startDictation();
    expect(service.source()).toBe('microphone');

    service.stop();
    await service.startMeetingCapture();
    expect(service.source()).toBe('meeting');
  });

  it('reports capturing for either pipeline', async () => {
    await service.startDictation();
    expect(service.isCapturing()).toBeTrue();

    service.stop();
    expect(service.isCapturing()).toBeFalse();

    await service.startMeetingCapture();
    expect(service.isCapturing()).toBeTrue();
  });

  it('writes committed speech into the active field', async () => {
    service.setTarget(target);
    await service.startDictation();

    liveRecognition.commit('Finished the migration');
    drainTypewriter();

    expect(target.text.trim()).toBe('Finished the migration');
  });

  it('reveals text gradually rather than in one block', async () => {
    service.setTarget(target);
    await service.startDictation();

    liveRecognition.commit('Finished the migration');
    jasmine.clock().tick(40);

    expect(target.received.length).toBeGreaterThan(0);
    expect(target.text.length).toBeLessThan('Finished the migration'.length);
  });

  it('separates consecutive phrases', async () => {
    service.setTarget(target);
    await service.startDictation();

    liveRecognition.commit('Finished the migration');
    liveRecognition.commit('Starting on the report');
    drainTypewriter();

    expect(target.text.trim()).toBe('Finished the migration Starting on the report');
  });

  it('writes nothing when no field is selected', async () => {
    // Dictating with nothing focused must be inert, not write into whatever was last used.
    await service.startDictation();

    liveRecognition.commit('Text with nowhere to go');
    drainTypewriter();

    expect(target.received).toEqual([]);
  });

  it('does not write uncommitted speech into the field', async () => {
    service.setTarget(target);
    await service.startDictation();

    liveRecognition.interimText.set('half a sent');
    drainTypewriter();

    expect(target.received).toEqual([]);
    expect(service.interimText()).toBe('half a sent');
  });

  it('delivers each recorded chunk exactly once', async () => {
    // The repeat bug: queueing the whole transcript on every poll rather than the new tail.
    service.setTarget(target);
    await service.startMeetingCapture();

    meetingCapture.appendChunk('First chunk of the call');
    jasmine.clock().tick(500);
    jasmine.clock().tick(500);
    drainTypewriter();

    expect(target.text.trim()).toBe('First chunk of the call');
  });

  it('appends later chunks without repeating earlier ones', async () => {
    service.setTarget(target);
    await service.startMeetingCapture();

    meetingCapture.appendChunk('First chunk');
    jasmine.clock().tick(500);
    meetingCapture.appendChunk('second chunk');
    jasmine.clock().tick(500);
    drainTypewriter();

    expect(target.text.trim()).toBe('First chunk second chunk');
  });

  it('stops polling once recording and transcription have both finished', async () => {
    service.setTarget(target);
    await service.startMeetingCapture();

    meetingCapture.stop();
    jasmine.clock().tick(500);

    meetingCapture.appendChunk('Arrived after everything stopped');
    jasmine.clock().tick(2_000);
    drainTypewriter();

    expect(target.text).not.toContain('after everything stopped');
  });

  it('keeps polling while a chunk is still being transcribed', async () => {
    // Recording ends when the user clicks Stop, but chunks already uploaded are still in
    // flight. Stopping the poll there would drop the last part of the standup.
    service.setTarget(target);
    await service.startMeetingCapture();

    meetingCapture.isCapturing.set(false);
    meetingCapture.isTranscribing.set(true);
    meetingCapture.appendChunk('The final answer');
    jasmine.clock().tick(500);
    drainTypewriter();

    expect(target.text.trim()).toBe('The final answer');
  });

  it('hands over queued text immediately when stopped', async () => {
    // The user has finished; making them watch the animation catch up would be the effect
    // serving itself rather than them.
    service.setTarget(target);
    await service.startDictation();
    liveRecognition.commit('A reasonably long sentence that is still being revealed');

    service.stop();

    expect(target.text.trim()).toBe('A reasonably long sentence that is still being revealed');
  });

  it('stops both pipelines regardless of which was running', async () => {
    service.setTarget(target);
    await service.startDictation();

    service.stop();

    expect(liveRecognition.stopCallCount).toBe(1);
    expect(service.isCapturing()).toBeFalse();
  });

  it('collects a chunk that landed just before stopping', async () => {
    service.setTarget(target);
    await service.startMeetingCapture();
    meetingCapture.appendChunk('Said right at the end');

    service.stop();

    expect(target.text.trim()).toBe('Said right at the end');
  });

  it('starts a fresh transcript after a reset', async () => {
    service.setTarget(target);
    await service.startMeetingCapture();
    meetingCapture.appendChunk('Old call');
    jasmine.clock().tick(500);
    drainTypewriter();
    target.received.length = 0;

    service.reset();
    await service.startMeetingCapture();
    meetingCapture.appendChunk('New call');
    jasmine.clock().tick(500);
    drainTypewriter();

    expect(target.text.trim()).toBe('New call');
  });

  it('surfaces a recogniser failure', async () => {
    await service.startDictation();
    liveRecognition.errorMessage.set('Microphone access was refused.');

    expect(service.errorMessage()).toContain('refused');
  });

  it('surfaces a recorded-capture failure', async () => {
    await service.startMeetingCapture();
    meetingCapture.errorMessage.set('Part of the audio could not be transcribed.');

    expect(service.errorMessage()).toContain('could not be transcribed');
  });

  it('is supported when either pipeline is available', () => {
    liveRecognition.isSupported.set(false);

    expect(service.isSupported()).toBeTrue();
  });
});
