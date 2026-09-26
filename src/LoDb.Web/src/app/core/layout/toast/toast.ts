import type { ToastKind } from './toast-kind';

/** One message on screen. The text is plain: a toast never renders markup. */
export interface Toast {
  readonly id: number;
  readonly kind: ToastKind;
  readonly message: string;
}
