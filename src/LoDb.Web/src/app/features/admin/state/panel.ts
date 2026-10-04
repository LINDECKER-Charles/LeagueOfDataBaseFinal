import type { Signal } from '@angular/core';
import type { PanelFailure } from './panel-failure';

/**
 * The data of an admin panel, loaded when the panel opens and again when its query changes
 * or `reload` is called. The last value stays on screen while the next one loads: a new
 * page, a new range or a refresh never blanks the panel.
 */
export interface Panel<T> {
  /** The latest answer, undefined until the first one arrives or once a load failed. */
  readonly value: Signal<T | undefined>;
  /** Whether a load is in flight, the first one or a later one. */
  readonly busy: Signal<boolean>;
  /** Why the last load failed, null while it has not. */
  readonly failure: Signal<PanelFailure | null>;
  reload(): void;
}
