import { TestBed } from '@angular/core/testing';
import { ToastService } from '../../../../core/layout/toast/toast-service';

/** The toasts shown so far, as `kind: message`. */
export function toasts(): string[] {
  return TestBed.inject(ToastService)
    .toasts()
    .map((toast) => `${toast.kind}: ${toast.message}`);
}
