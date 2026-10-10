import type { Logger } from 'pino';
import type { RenderCounts } from './render-counts';
import type { RenderLane } from './render-lane';
import type { RenderRefusal } from './render-refusal';

const INTERVAL_SECONDS = 60;
const MS_PER_SECOND = 1_000;

type LaneCounts = Partial<Record<RenderLane, number>>;
type RefusalCounts = Partial<Record<RenderLane, Partial<Record<RenderRefusal, number>>>>;

/**
 * Logs `ssr.admission.summary` once a minute, at `warn`, for a minute in which the admission
 * refused renders: the renders it admitted per lane, and those it refused per lane and per
 * reason, each count only when it is not zero. One line a minute rather than one per refused
 * request: a crawl turned away by the thousand stays a single readable line.
 */
export class AdmissionSummary {
  private logged = new Map<string, number>();

  constructor(
    private readonly counts: RenderCounts,
    private readonly logger: Logger,
  ) {}

  /** Logs the summary of each minute from now on, without keeping the process alive. */
  start(): void {
    setInterval(() => this.flush(), INTERVAL_SECONDS * MS_PER_SECOND).unref();
  }

  /** Logs what the admission decided since the previous call, when it refused any render. */
  flush(): void {
    const admitted: LaneCounts = {};
    const refused: RefusalCounts = {};
    for (const { lane, refusal, count } of this.counts.entries()) {
      const key = `${lane}|${refusal ?? ''}`;
      const increase = count - (this.logged.get(key) ?? 0);
      this.logged.set(key, count);
      if (increase === 0) {
        continue;
      }
      if (refusal === null) {
        admitted[lane] = increase;
      } else {
        refused[lane] = { ...refused[lane], [refusal]: increase };
      }
    }
    if (Object.keys(refused).length > 0) {
      const fields = { interval_seconds: INTERVAL_SECONDS, admitted, refused };
      this.logger.warn(fields, 'ssr.admission.summary');
    }
  }
}
