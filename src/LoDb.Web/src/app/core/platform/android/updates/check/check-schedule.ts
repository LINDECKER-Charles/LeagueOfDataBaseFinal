/**
 * The rhythm of ADR 0008 (taken from Bloodborne Legendary Run): a check at start, then on
 * each return to the foreground, at most every 15 minutes.
 */
export const CHECK_INTERVAL_MS = 15 * 60_000;

/** When the last check ran, and whether a return to the foreground warrants another. */
export class CheckSchedule {
  private lastCheck: number | null = null;

  isDue(now: number): boolean {
    return this.lastCheck === null || now - this.lastCheck >= CHECK_INTERVAL_MS;
  }

  record(now: number): void {
    this.lastCheck = now;
  }
}
