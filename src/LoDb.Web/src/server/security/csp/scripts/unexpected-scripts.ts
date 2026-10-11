import type { Logger } from 'pino';

const INTERVAL_SECONDS = 60;
const MS_PER_SECOND = 1_000;

/**
 * Logs `ssr.csp.unexpected_scripts` once a minute, at `warn`, for a minute in which rendered
 * pages held inline scripts the build never wrote (`KnownInlineScripts`): their policy left
 * them out, so no browser ran them, but something put them into the HTML. One line a minute
 * rather than one per render; `lodb_ssr_csp_unexpected_scripts_total` counts them too.
 */
export class UnexpectedScripts {
  private seen = 0;
  private logged = 0;

  constructor(private readonly logger: Logger) {}

  record(count: number): void {
    this.seen += count;
  }

  /** Logs the summary of each minute from now on, without keeping the process alive. */
  start(): void {
    setInterval(() => this.flush(), INTERVAL_SECONDS * MS_PER_SECOND).unref();
  }

  /** Logs the scripts left out since the previous call, when there were any. */
  flush(): void {
    const increase = this.seen - this.logged;
    this.logged = this.seen;
    if (increase > 0) {
      const fields = { interval_seconds: INTERVAL_SECONDS, scripts: increase };
      this.logger.warn(fields, 'ssr.csp.unexpected_scripts');
    }
  }
}
