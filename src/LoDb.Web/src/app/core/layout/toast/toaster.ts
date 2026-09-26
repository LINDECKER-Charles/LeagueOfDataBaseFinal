import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ToastKind } from './toast-kind';
import { ToastService } from './toast-service';

// Every class is spelt out whole so the stylesheet scan finds it.
const KIND_CLASSES: Readonly<Record<ToastKind, string>> = {
  info: 'hx-toast--info',
  success: 'hx-toast--success',
  warning: 'hx-toast--warning',
  error: 'hx-toast--error',
};

/**
 * Draws the ToastService queue at the top of the inline end. The region is polite; an error
 * interrupts as an alert. Its full text wraps: nothing is truncated behind a modal.
 */
@Component({
  selector: 'lodb-toaster',
  imports: [TranslocoPipe],
  templateUrl: './toaster.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Toaster {
  protected readonly service = inject(ToastService);

  protected kindClass(kind: ToastKind): string {
    return KIND_CLASSES[kind];
  }
}
