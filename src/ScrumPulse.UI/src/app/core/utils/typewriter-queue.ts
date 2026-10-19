export type TypewriterEmitter = (text: string) => void;

export interface TypewriterOptions {
  readonly tickMs?: number;

  /**
   * Upper bound on how long the queue may take to drain. Speech outruns a comfortable typing
   * speed, so a fixed rate falls further behind the longer someone talks.
   */
  readonly maximumDrainMs?: number;
}

/**
 * Reveals transcribed text a few characters at a time instead of dropping whole phrases in.
 *
 * Recognition emits at pauses rather than on a schedule, so raw output lands in bursts; the
 * queue turns that into steady movement. No Angular here, so fake timers are enough to test it.
 */
export class TypewriterQueue {
  private readonly tickMs: number;
  private readonly maximumDrainMs: number;

  private pending = '';
  private timerHandle: ReturnType<typeof setInterval> | null = null;
  private remainingTicks = 0;

  constructor(
    private readonly emit: TypewriterEmitter,
    options: TypewriterOptions = {}
  ) {
    this.tickMs = options.tickMs ?? 40;
    this.maximumDrainMs = options.maximumDrainMs ?? 2_000;
  }

  get isRevealing(): boolean {
    return this.timerHandle !== null;
  }

  /** Exposed for tests and for the "catching up" indicator. */
  get pendingLength(): number {
    return this.pending.length;
  }

  /** Appends, so a phrase arriving mid-reveal queues behind the current one. */
  enqueue(text: string): void {
    if (!text) {
      return;
    }

    this.pending += text;
    const fullTicks = Math.max(1, Math.floor(this.maximumDrainMs / this.tickMs));
    this.remainingTicks = Math.max(this.remainingTicks, fullTicks);
    this.startRevealing();
  }

  /** Dumps everything queued at once, when capture stops. */
  flush(): void {
    this.stopRevealing();

    if (this.pending) {
      const remaining = this.pending;
      this.pending = '';
      this.emit(remaining);
    }
  }

  /** Teardown: discards queued text without emitting it. */
  dispose(): void {
    this.stopRevealing();
    this.pending = '';
  }

  private startRevealing(): void {
    if (this.timerHandle !== null) {
      return;
    }

    this.timerHandle = setInterval(() => this.releaseNextSlice(), this.tickMs);
  }

  private releaseNextSlice(): void {
    if (!this.pending) {
      this.stopRevealing();
      return;
    }

    const slice = this.pending.slice(0, this.charactersPerTick());
    this.pending = this.pending.slice(slice.length);
    this.remainingTicks = Math.max(1, this.remainingTicks - 1);
    this.emit(slice);

    if (!this.pending) {
      this.stopRevealing();
    }
  }

  // Scaled to the backlog and remaining drain window so the queue drains within maximumDrainMs.
  private charactersPerTick(): number {
    const ticksAvailable = Math.max(1, this.remainingTicks);

    return Math.max(1, Math.ceil(this.pending.length / ticksAvailable));
  }

  private stopRevealing(): void {
    this.remainingTicks = 0;
    if (this.timerHandle !== null) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
  }
}
