import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import type { Toast } from './toast';
import type { ToastKind } from './toast-kind';

// Long enough to read two lines, short enough not to pile up (legacy Toaster).
const TOAST_LIFETIME_MS = 5000;
// The exit transition of the toaster's stylesheet (`hx-toast--leaving`), as the legacy one.
const TOAST_EXIT_MS = 220;

/**
 * Transient messages of the whole application, drawn by the shell's toaster. A toast leaves
 * on its own after five seconds, or earlier when dismissed, sliding out before it goes. The
 * server never schedules the departure: a toast raised while rendering stays in the HTML
 * until the browser takes over.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly queue = signal<readonly Toast[]>([]);
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();
  private readonly inBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private lastId = 0;

  readonly toasts = this.queue.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.timers.forEach((timer) => clearTimeout(timer)));
  }

  show(kind: ToastKind, message: string): void {
    const id = ++this.lastId;
    this.queue.update((toasts) => [...toasts, { id, kind, message, leaving: false }]);
    if (this.inBrowser) {
      this.schedule(id, TOAST_LIFETIME_MS, () => this.dismiss(id));
    }
  }

  /** Starts the exit of a toast still on screen; it leaves the queue once the exit is over. */
  dismiss(id: number): void {
    const toast = this.queue().find((candidate) => candidate.id === id);
    if (toast === undefined || toast.leaving) {
      return;
    }
    this.queue.update((toasts) =>
      toasts.map((candidate) =>
        candidate.id === id ? { ...candidate, leaving: true } : candidate,
      ),
    );
    this.schedule(id, TOAST_EXIT_MS, () => this.remove(id));
  }

  private remove(id: number): void {
    this.timers.delete(id);
    this.queue.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }

  // One pending timer per toast: its departure, then its exit, which replaces it.
  private schedule(id: number, delay: number, run: () => void): void {
    clearTimeout(this.timers.get(id));
    this.timers.set(id, setTimeout(run, delay));
  }
}
