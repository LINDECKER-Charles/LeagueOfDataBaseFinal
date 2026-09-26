import type { Signal } from '@angular/core';

/** What a tab panel reads from its tab group: the key of the tab currently shown. */
export interface TabSelection {
  readonly selectedKey: Signal<string | undefined>;
}
