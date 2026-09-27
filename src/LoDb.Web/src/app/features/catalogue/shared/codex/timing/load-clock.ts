import { Injectable, signal } from '@angular/core';
import type { LoadRecord } from './load-record';

/** The last detail page's arrival, from the resolver that fetched it to the badge. */
@Injectable({ providedIn: 'root' })
export class LoadClock {
  readonly last = signal<LoadRecord | null>(null);
}
