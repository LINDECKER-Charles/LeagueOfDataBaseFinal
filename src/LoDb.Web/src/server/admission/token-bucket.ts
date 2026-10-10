const MS_PER_MINUTE = 60_000;

/**
 * A budget refilled continuously at `perMinute` tokens a minute, up to `capacity`: it starts
 * full, so a quiet server admits a burst at once, then the steady rate only. Asking and
 * spending are apart, so that a render checked against several budgets spends from none of
 * them unless every one has a token.
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

  /** Whether a token is there to spend now. */
  hasToken(): boolean {
    return this.tokensAt(this.now()) >= 1;
  }

  /** Spends one token, which `hasToken` said was there. */
  take(): void {
    const now = this.now();
    this.tokens = this.tokensAt(now) - 1;
    this.refilledAt = now;
  }

  private tokensAt(now: number): number {
    const earned = ((now - this.refilledAt) * this.perMinute) / MS_PER_MINUTE;
    return Math.min(this.capacity, this.tokens + earned);
  }
}
