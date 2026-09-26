/**
 * Runs an action at most once per window, at the end of it, however many times it was asked
 * for meanwhile: the action reads the latest state when it runs, so nothing is lost.
 */
export class TrailingThrottle {
  private timer: ReturnType<typeof setTimeout> | null = null;

  constructor(
    private readonly windowMs: number,
    private readonly action: () => void,
  ) {}

  request(): void {
    if (this.timer !== null) {
      return;
    }
    this.timer = setTimeout(() => {
      this.timer = null;
      this.action();
    }, this.windowMs);
  }

  cancel(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
