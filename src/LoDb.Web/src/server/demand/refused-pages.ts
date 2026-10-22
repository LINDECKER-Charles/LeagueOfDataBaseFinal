import { randomUUID } from 'node:crypto';
import type { RefusedPagesReport } from './refused-pages-report';

/** Over the pages a crawl refuses in a day, and some 10 MB of memory at most. */
export const REFUSED_PAGES_CAPACITY = 50_000;
/** Changes one answer carries at most: some hundreds of kilobytes of JSON. */
export const REFUSED_PAGES_PER_REPORT = 10_000;
// A full table drops a tenth of its paths at once: dropping one per new path would sort the
// whole table at each refusal of a crawl.
const EVICTED_SHARE = 10;

interface Entry {
  count: number;
  /** The change that last touched it: what `?after=` compares to. */
  seq: number;
}

/**
 * The pages of older patches the admission refused, each with its cumulative count, for the
 * API's automatic queue of pages to pre-render (docs/produit/pre-rendu-catalogue-cdn.md, « File
 * automatique »). Bounded: past `capacity` paths, the least refused tenth goes, the oldest
 * first among equals. In memory only: a restart starts from zero under a new `instance`.
 */
export class RefusedPages {
  readonly instance = randomUUID();
  private readonly entries = new Map<string, Entry>();
  private seq = 0;
  private evicted = 0;

  constructor(private readonly capacity = REFUSED_PAGES_CAPACITY) {}

  /** Paths kept now. */
  get size(): number {
    return this.entries.size;
  }

  /** Counts one refusal of `path` (already normalised by `refusedPagePathOf`). */
  record(path: string): void {
    this.seq += 1;
    const known = this.entries.get(path);
    if (known !== undefined) {
      known.count += 1;
      known.seq = this.seq;
      return;
    }
    if (this.entries.size >= this.capacity) {
      this.evict();
    }
    this.entries.set(path, { count: 1, seq: this.seq });
  }

  /** The pages changed after `after`, oldest change first, `limit` at most. */
  report(after: number, limit = REFUSED_PAGES_PER_REPORT): RefusedPagesReport {
    const changed = [...this.entries]
      .filter(([, entry]) => entry.seq > after)
      .sort(([, left], [, right]) => left.seq - right.seq);
    const kept = changed.slice(0, limit);
    const isComplete = kept.length === changed.length;
    const last = kept.at(-1)?.[1].seq ?? after;
    return {
      instance: this.instance,
      cursor: isComplete ? Math.max(this.seq, after) : last,
      isComplete,
      evicted: this.evicted,
      pages: kept.map(([path, entry]) => ({ path, count: entry.count })),
    };
  }

  private evict(): void {
    const dropped = Math.max(1, Math.floor(this.capacity / EVICTED_SHARE));
    const leastRefused = [...this.entries]
      .sort(([, left], [, right]) => left.count - right.count || left.seq - right.seq)
      .slice(0, dropped);
    for (const [path] of leastRefused) {
      this.entries.delete(path);
    }
    this.evicted += leastRefused.length;
  }
}
