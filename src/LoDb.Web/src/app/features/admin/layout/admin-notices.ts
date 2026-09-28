import { Injectable, signal } from '@angular/core';
import type { BandTone } from './admin-band';

/** What the last action of the admin said, and in which tone. */
export interface AdminNotice {
  readonly tone: BandTone;
  readonly text: string;
}

/**
 * The outcome of the last action of the admin, as the legacy flash message: shown in a band
 * at the top of the page until the next action or the next page. Only the last one counts,
 * as a legacy page only showed the flashes of the request that led to it.
 */
@Injectable({ providedIn: 'root' })
export class AdminNotices {
  private readonly last = signal<AdminNotice | null>(null);

  readonly current = this.last.asReadonly();

  post(notice: AdminNotice): void {
    this.last.set(notice);
  }

  clear(): void {
    this.last.set(null);
  }
}
