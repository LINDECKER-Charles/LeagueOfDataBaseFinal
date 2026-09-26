import { DestroyRef, Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import type { Toast } from './toast';
import type { ToastKind } from './toast-kind';

// Long enough to read two lines, short enough not to pile up (legacy Toaster).
const TOAST_LIFETIME_MS = 5000;

/**
 * Transient messages of the whole application, drawn by the shell's toaster. A toast leaves
 * on its own after five seconds, or earlier when dismissed. The server never schedules the
 * departure: a toast raised while rendering stays in the HTML until the browser takes over.
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
    this.queue.update((toasts) => [...toasts, { id, kind, message }]);
    if (this.inBrowser) {
      this.timers.set(
        id,
        setTimeout(() => this.dismiss(id), TOAST_LIFETIME_MS),
      );
    }
  }

  dismiss(id: number): void {
    clearTimeout(this.timers.get(id));
    this.timers.delete(id);
    this.queue.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }
}
