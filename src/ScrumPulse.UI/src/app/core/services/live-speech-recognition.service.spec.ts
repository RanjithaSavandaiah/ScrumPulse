import { TestBed } from '@angular/core/testing';
import { LiveSpeechRecognitionService } from './live-speech-recognition.service';

/**
 * Covers the browser-recogniser dictation path.
 *
 * None of this can be exercised against a real recogniser - it needs a microphone, a network
 * round trip to Google and someone talking - so a fake stands in. The fake is written to the
 * API's real behaviour rather than its documentation, in particular the two places where the
 * documentation misleads: Chrome ends a `continuous` session on silence anyway, and it reports
 * that silence as an `error` rather than a normal end.
 */
describe('LiveSpeechRecognitionService', () => {
  /** Stand-in for the browser recogniser, driven directly by the tests. */
  class FakeRecogniser {
    static instances: FakeRecogniser[] = [];

    continuous = false;
    interimResults = false;
    lang = '';
    maxAlternatives = 0;

    startCallCount = 0;
    abortCallCount = 0;
    stopCallCount = 0;

    /** Set when `start` should throw, mimicking a session that is already running. */
    throwOnStart = false;

    onresult: ((event: unknown) => void) | null = null;
    onerror: ((event: { error: string }) => void) | null = null;
    onend: (() => void) | null = null;

    constructor() {
      FakeRecogniser.instances.push(this);
    }

    start(): void {
      this.startCallCount += 1;

      if (this.throwOnStart) {
        throw new Error('already started');
      }
    }

    stop(): void {
      this.stopCallCount += 1;
    }

    abort(): void {
      this.abortCallCount += 1;
    }

    /** Pushes a recognition result through, exactly as the browser would. */
    emitResult(phrases: { transcript: string; isFinal: boolean }[], resultIndex = 0): void {
      this.onresult?.({
        resultIndex,
        results: phrases.map((phrase) => ({
          isFinal: phrase.isFinal,
          length: 1,
          0: { transcript: phrase.transcript }
        }))
      });
    }
  }

  const browserWindow = window as unknown as Record<string, unknown>;
  let originalSpeechRecognition: unknown;
  let originalWebkitSpeechRecognition: unknown;

  function createService(): LiveSpeechRecognitionService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(LiveSpeechRecognitionService);
  }

  function currentRecogniser(): FakeRecogniser {
    return FakeRecogniser.instances[FakeRecogniser.instances.length - 1];
  }

  beforeEach(() => {
    FakeRecogniser.instances = [];
    originalSpeechRecognition = browserWindow['SpeechRecognition'];
    originalWebkitSpeechRecognition = browserWindow['webkitSpeechRecognition'];
    delete browserWindow['webkitSpeechRecognition'];
    browserWindow['SpeechRecognition'] = FakeRecogniser;
  });

  afterEach(() => {
    if (originalSpeechRecognition === undefined) {
      delete browserWindow['SpeechRecognition'];
    } else {
      browserWindow['SpeechRecognition'] = originalSpeechRecognition;
    }

    if (originalWebkitSpeechRecognition === undefined) {
      delete browserWindow['webkitSpeechRecognition'];
    } else {
      browserWindow['webkitSpeechRecognition'] = originalWebkitSpeechRecognition;
    }
  });

  it('reports support when the browser exposes a recogniser', () => {
    expect(createService().isSupported()).toBeTrue();
  });

  it('reports no support when the browser has no recogniser', () => {
    delete browserWindow['SpeechRecognition'];
    delete browserWindow['webkitSpeechRecognition'];

    expect(createService().isSupported()).toBeFalse();
  });

  it('accepts the prefixed constructor that Chrome and Edge actually ship', () => {
    delete browserWindow['SpeechRecognition'];
    browserWindow['webkitSpeechRecognition'] = FakeRecogniser;

    expect(createService().isSupported()).toBeTrue();

    delete browserWindow['webkitSpeechRecognition'];
  });

  it('refuses to start, rather than throwing, where unsupported', () => {
    // The caller uses this answer to fall back to recorded chunks, so it has to be a return
    // value and not an exception.
    delete browserWindow['SpeechRecognition'];
    delete browserWindow['webkitSpeechRecognition'];

    expect(createService().start(() => undefined)).toBeFalse();
  });

  it('configures the recogniser for continuous, interim recognition', () => {
    // These two flags are the whole feature: without them the recogniser returns one block of
    // text after the user stops talking, which is what this path exists to avoid.
    createService().start(() => undefined);

    expect(currentRecogniser().continuous).toBeTrue();
    expect(currentRecogniser().interimResults).toBeTrue();
  });

  it('follows the browser language so a distributed squad is not forced into one accent', () => {
    createService().start(() => undefined);

    expect(currentRecogniser().lang).toBe(navigator.language || 'en-US');
  });

  it('reports listening once started', () => {
    const service = createService();

    service.start(() => undefined);

    expect(service.isListening()).toBeTrue();
  });

  it('does not open a second session when already listening', () => {
    const service = createService();

    service.start(() => undefined);
    service.start(() => undefined);

    expect(FakeRecogniser.instances.length).toBe(1);
  });

  it('hands over committed phrases', () => {
    const received: string[] = [];
    createService().start((phrase) => received.push(phrase));

    currentRecogniser().emitResult([{ transcript: 'Finished the migration', isFinal: true }]);

    expect(received).toEqual(['Finished the migration']);
  });

  it('publishes uncommitted speech separately instead of handing it over', () => {
    // Interim text is revised as the recogniser hears more. Writing it into the field would
    // make the field rewrite itself mid-sentence.
    const received: string[] = [];
    const service = createService();
    service.start((phrase) => received.push(phrase));

    currentRecogniser().emitResult([{ transcript: 'Blocked on the cert', isFinal: false }]);

    expect(received).toEqual([]);
    expect(service.interimText()).toBe('Blocked on the cert');
  });

  it('clears the uncommitted text once the phrase is committed', () => {
    const service = createService();
    service.start(() => undefined);

    currentRecogniser().emitResult([{ transcript: 'Deploying now', isFinal: false }]);
    currentRecogniser().emitResult([{ transcript: 'Deploying now', isFinal: true }]);

    expect(service.interimText()).toBe('');
  });

  it('delivers only results from the reported index', () => {
    // The results list holds the whole session. Reading from zero would re-deliver every
    // phrase on each event, filling the field with repeats - the single easiest bug to write
    // against this API.
    const received: string[] = [];
    createService().start((phrase) => received.push(phrase));

    currentRecogniser().emitResult(
      [
        { transcript: 'Already delivered', isFinal: true },
        { transcript: 'Newly spoken', isFinal: true }
      ],
      1
    );

    expect(received).toEqual(['Newly spoken']);
  });

  it('ignores a committed phrase that is only whitespace', () => {
    const received: string[] = [];
    createService().start((phrase) => received.push(phrase));

    currentRecogniser().emitResult([{ transcript: '   ', isFinal: true }]);

    expect(received).toEqual([]);
  });

  it('restarts when the browser ends the session on its own', () => {
    // Chrome closes the session after a pause despite `continuous`. Without the restart,
    // dictation would die the first time the user stopped to think.
    createService().start(() => undefined);
    const recogniser = currentRecogniser();

    recogniser.onend?.();

    expect(recogniser.startCallCount).toBe(2);
  });

  it('stops listening when a restart cannot be made', () => {
    const service = createService();
    service.start(() => undefined);
    const recogniser = currentRecogniser();
    recogniser.throwOnStart = true;

    recogniser.onend?.();

    expect(service.isListening()).toBeFalse();
  });

  it('does not restart after a deliberate stop', () => {
    const service = createService();
    service.start(() => undefined);
    const recogniser = currentRecogniser();

    service.stop();
    recogniser.onend?.();

    expect(recogniser.startCallCount).toBe(1);
  });

  it('treats silence as normal rather than as a failure', () => {
    // Chrome reports a pause in speech as `no-speech`. Surfacing it would put a red banner in
    // front of someone who simply stopped talking for a moment.
    const service = createService();
    service.start(() => undefined);

    currentRecogniser().onerror?.({ error: 'no-speech' });

    expect(service.errorMessage()).toBeNull();
    expect(service.isListening()).toBeTrue();
  });

  it('explains a refused microphone in terms of the fix', () => {
    const service = createService();
    service.start(() => undefined);

    currentRecogniser().onerror?.({ error: 'not-allowed' });

    expect(service.errorMessage()).toContain('address bar');
    expect(service.isListening()).toBeFalse();
  });

  it('reports an unexpected failure without losing what was already said', () => {
    const received: string[] = [];
    const service = createService();
    service.start((phrase) => received.push(phrase));

    currentRecogniser().emitResult([{ transcript: 'Said before the failure', isFinal: true }]);
    currentRecogniser().onerror?.({ error: 'network' });

    expect(received).toEqual(['Said before the failure']);
    expect(service.errorMessage()).toContain('kept');
  });

  it('releases the microphone when stopped', () => {
    // An un-aborted recogniser leaves the browser's recording indicator lit, which looks to
    // the user like the application is still listening after they stopped it.
    const service = createService();
    service.start(() => undefined);

    service.stop();

    expect(currentRecogniser().abortCallCount).toBe(1);
    expect(service.isListening()).toBeFalse();
  });

  it('detaches handlers before aborting, so teardown emits nothing', () => {
    const service = createService();
    service.start(() => undefined);
    const recogniser = currentRecogniser();

    service.stop();

    expect(recogniser.onresult).toBeNull();
    expect(recogniser.onerror).toBeNull();
    expect(recogniser.onend).toBeNull();
  });

  it('can be restarted after being stopped', () => {
    const service = createService();
    service.start(() => undefined);
    service.stop();

    expect(service.start(() => undefined)).toBeTrue();
    expect(service.isListening()).toBeTrue();
  });

  it('clears any stale error when restarted', () => {
    const service = createService();
    service.start(() => undefined);
    currentRecogniser().onerror?.({ error: 'network' });

    service.start(() => undefined);

    expect(service.errorMessage()).toBeNull();
  });
});
