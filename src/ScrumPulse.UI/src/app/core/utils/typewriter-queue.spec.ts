import { TypewriterQueue } from './typewriter-queue';

/**
 * Covers the progressive reveal that sits between transcription and the form field.
 *
 * The risk this guards is drift. A reveal that runs at a fixed rate falls further behind the
 * longer someone talks, so by the end of a standup the field would be showing a sentence from
 * minutes ago. The drain bound is the mechanism that prevents it, and most of these tests exist
 * to prove it still holds.
 */
describe('TypewriterQueue', () => {
  /** Matches the production tick, so character budgets here reflect real behaviour. */
  const tickMs = 40;
  const maximumDrainMs = 2_000;

  let received: string[];
  let queue: TypewriterQueue;

  function written(): string {
    return received.join('');
  }

  beforeEach(() => {
    jasmine.clock().install();
    received = [];
    queue = new TypewriterQueue((slice) => received.push(slice), { tickMs, maximumDrainMs });
  });

  afterEach(() => {
    queue.dispose();
    jasmine.clock().uninstall();
  });

  it('reveals text gradually rather than all at once', () => {
    queue.enqueue('Finished the migration');

    jasmine.clock().tick(tickMs);

    expect(written().length).toBeGreaterThan(0);
    expect(written().length).toBeLessThan('Finished the migration'.length);
  });

  it('emits nothing until the first tick', () => {
    // Enqueueing must not write synchronously, otherwise the first phrase would appear in a
    // block and only later text would be revealed - visibly inconsistent.
    queue.enqueue('Deployed to staging');

    expect(received).toEqual([]);
  });

  it('delivers the whole text, in order, once drained', () => {
    const phrase = 'Blocked on the certificate renewal';
    queue.enqueue(phrase);

    jasmine.clock().tick(maximumDrainMs * 2);

    expect(written()).toBe(phrase);
  });

  it('drains within the configured window however long the text', () => {
    // The anti-drift guarantee. A fixed rate would take proportionally longer as text grows;
    // this must not.
    queue.enqueue('a'.repeat(5_000));

    jasmine.clock().tick(maximumDrainMs);

    expect(queue.pendingLength).toBe(0);
  });

  it('speeds up when a backlog builds', () => {
    // The anti-drift mechanism itself: the rate is derived from how much is waiting, so a long
    // passage catches up while a few words still arrive at reading speed.
    function charactersInFirstTick(text: string): number {
      const emitted: string[] = [];
      const measured = new TypewriterQueue((slice) => emitted.push(slice), { tickMs, maximumDrainMs });

      measured.enqueue(text);
      jasmine.clock().tick(tickMs);
      measured.dispose();

      return emitted.join('').length;
    }

    expect(charactersInFirstTick('x'.repeat(2_000))).toBeGreaterThan(charactersInFirstTick('short'));
  });

  it('appends text arriving mid-reveal without interleaving it', () => {
    // Speech recognition commits a phrase whenever the speaker pauses, which routinely lands
    // while the previous one is still being typed.
    queue.enqueue('First phrase. ');
    jasmine.clock().tick(tickMs);
    queue.enqueue('Second phrase.');

    jasmine.clock().tick(maximumDrainMs * 2);

    expect(written()).toBe('First phrase. Second phrase.');
  });

  it('ignores empty text', () => {
    queue.enqueue('');

    jasmine.clock().tick(tickMs * 5);

    expect(received).toEqual([]);
    expect(queue.isRevealing).toBeFalse();
  });

  it('releases everything still queued when flushed', () => {
    // Clicking Stop must hand over the text immediately. Making the user watch the animation
    // finish would be the effect serving itself rather than them.
    queue.enqueue('Everything said during the call');

    queue.flush();

    expect(written()).toBe('Everything said during the call');
  });

  it('stops revealing once flushed', () => {
    queue.enqueue('Some text');
    queue.flush();
    const afterFlush = written();

    jasmine.clock().tick(maximumDrainMs);

    expect(written()).toBe(afterFlush);
    expect(queue.isRevealing).toBeFalse();
  });

  it('emits nothing on flush when there is nothing queued', () => {
    queue.flush();

    expect(received).toEqual([]);
  });

  it('discards queued text when disposed', () => {
    // Teardown happens on navigation away. Emitting into a destroyed component's field would
    // throw, or worse, silently write into a field the user can no longer see.
    queue.enqueue('Text that should never land');

    queue.dispose();
    jasmine.clock().tick(maximumDrainMs);

    expect(received).toEqual([]);
  });

  it('stops its timer when disposed', () => {
    queue.enqueue('Running');
    jasmine.clock().tick(tickMs);

    queue.dispose();

    expect(queue.isRevealing).toBeFalse();
  });

  it('can be reused after draining naturally', () => {
    queue.enqueue('One. ');
    jasmine.clock().tick(maximumDrainMs * 2);

    queue.enqueue('Two.');
    jasmine.clock().tick(maximumDrainMs * 2);

    expect(written()).toBe('One. Two.');
  });
});
