import { DOCUMENT, DestroyRef, Injectable, inject } from '@angular/core';

/**
 * The gestures of a person: scrolling by wheel, touch or keys, pointing, clicking. Not the
 * `scroll` event itself, which a restored position or an anchor fires without anyone there.
 */
const PRESENCE_EVENTS: readonly string[] = [
  'wheel',
  'touchstart',
  'keydown',
  'pointerdown',
  'pointermove',
];
/**
 * How long the page must stay visible, in one stretch, for a reader who makes no gesture: far
 * beyond what a headless browser waits after the load before it moves on.
 */
const VISIBLE_DWELL_MS = 10_000;

/**
 * Whether a person is on the page. Robots that run the scripts load a page and leave within
 * seconds, without a gesture; the audience counts a visit only once someone shows: the first
 * gesture, or the page kept visible for ten seconds (a hidden tab does not count down).
 */
@Injectable({ providedIn: 'root' })
export class AudiencePresence {
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly listening = new AbortController();
  private dwell: ReturnType<typeof setTimeout> | undefined;
  private confirm: () => void = () => undefined;

  /** Settles at the first sign of a person, and stays settled. */
  readonly confirmed = new Promise<void>((resolve) => (this.confirm = resolve));

  /** Watches for a person from now on; called once, at startup, before anything is counted. */
  start(): void {
    const options = { capture: true, passive: true, signal: this.listening.signal };
    for (const name of PRESENCE_EVENTS) {
      this.document.addEventListener(name, () => this.present(), options);
    }
    this.document.addEventListener('visibilitychange', () => this.restartDwell(), options);
    this.restartDwell();
    this.destroyRef.onDestroy(() => this.stop());
  }

  private present(): void {
    this.stop();
    this.confirm();
  }

  private stop(): void {
    clearTimeout(this.dwell);
    this.listening.abort();
  }

  private restartDwell(): void {
    clearTimeout(this.dwell);
    if (this.document.visibilityState === 'visible') {
      this.dwell = setTimeout(() => this.present(), VISIBLE_DWELL_MS);
    }
  }
}
