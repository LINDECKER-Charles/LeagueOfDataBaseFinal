const MS_PER_MINUTE = 60_000;

/**
 * A budget refilled continuously at `perMinute` tokens a minute, up to `capacity`: it starts
 * full, so a quiet server admits a burst at once, then the steady rate only.
 */
export class TokenBucket {
  private tokens: number;
  private refilledAt: number;

  constructor(
    private readonly perMinute: number,
    private readonly capacity: number,
    private readonly now: () => number = () => performance.now(),
  ) {
    this.tokens = capacity;
    this.refilledAt = now();
  }

  /** Spends one token when there is one; tells whether it did. */
  tryTake(): boolean {
    const now = this.now();
    const earned = ((now - this.refilledAt) * this.perMinute) / MS_PER_MINUTE;
    this.tokens = Math.min(this.capacity, this.tokens + earned);
    this.refilledAt = now;
    if (this.tokens < 1) {
      return false;
    }
    this.tokens -= 1;
    return true;
  }
}
