import { Injectable, Injector, type Signal, inject, signal } from '@angular/core';
import {
  EMPTY,
  type Observable,
  catchError,
  ignoreElements,
  lastValueFrom,
  takeUntil,
  tap,
  timeout,
} from 'rxjs';
import type { WarmUpProgress } from '../../api/generated/models/warm-up-progress';
import { preparingFrame } from './preparing-frame';
import { WarmUpStream } from './warm-up-stream';
import type { WarmUpTarget } from './warm-up-target';

/** Silence after which the visit goes on without the stream; the API beats every 5 s. */
const WATCHDOG_IDLE_MS = 15_000;
/** How long a completed run holds its full bar: long enough to read "ready". */
const READY_HOLD_MS = 450;

function pause(milliseconds: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

/**
 * The legacy "gate-then-visit" loader of a patch or language switch. A modal opens at once,
 * the API warms the chosen catalog (datasets, then the images of the destination's lists,
 * ADR 0003's long tail) while the modal shows each frame it streams, and the visit follows
 * on warm data: the page lands whole instead of on placeholders.
 *
 * The run never holds the visit hostage: a stream that fails or falls silent for the
 * watchdog period, like a visitor dismissing the modal (Escape, the backdrop), lets the
 * visit go at once. The modal stays up during the visit and leaves once it has landed.
 */
@Injectable({ providedIn: 'root' })
export class WarmUpLoader {
  private readonly injector = inject(Injector);
  private readonly stream = inject(WarmUpStream);
  private readonly frame = signal<WarmUpProgress>(preparingFrame([]));

  /** Warms `target` behind the modal, then visits; resolves with the visit's outcome. */
  async gate(target: WarmUpTarget, visit: () => Promise<boolean>): Promise<boolean> {
    this.frame.set(preparingFrame(target.resources));
    const dialog = await this.openModal();
    let dismissed = false;
    dialog.closed.subscribe(() => (dismissed = true));
    await this.warm(target, dialog.closed);
    if (!dismissed && this.frame().stage === 'done') {
      await pause(READY_HOLD_MS);
    }
    try {
      return await visit();
    } finally {
      dialog.close();
    }
  }

  // The header carries the switcher on every page: the CDK overlay and the modal load with
  // the first switch, not with the page.
  private async openModal() {
    const [{ DialogService }, { LoaderDialog }] = await Promise.all([
      import('../../../ui/overlays/dialog-service'),
      import('./dialog/loader-dialog'),
    ]);
    return this.injector
      .get(DialogService)
      .open<InstanceType<typeof LoaderDialog>, Signal<WarmUpProgress>>(LoaderDialog, {
        labelledBy: LoaderDialog.HEADING_ID,
        data: this.frame.asReadonly(),
        size: 'compact',
      });
  }

  // Settles when the stream ends, fails or falls silent, or the modal is dismissed.
  private warm(target: WarmUpTarget, dismissal: Observable<unknown>): Promise<void> {
    return lastValueFrom(
      this.stream.open(target).pipe(
        timeout({ each: WATCHDOG_IDLE_MS }),
        takeUntil(dismissal),
        tap((frame) => this.frame.set(frame)),
        ignoreElements(),
        catchError(() => EMPTY),
      ),
      { defaultValue: undefined },
    );
  }
}
