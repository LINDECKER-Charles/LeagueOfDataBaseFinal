import { Directionality } from '@angular/cdk/bidi';
import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  contentChildren,
  inject,
  input,
  signal,
  viewChildren,
} from '@angular/core';
import { Tab } from './tab';
import { tabIndexAfter } from './tab-index-after';
import { tabMoveForKey } from './tab-move-for-key';
import type { TabSelection } from './tab-selection';
import { TAB_SELECTION } from './tab-selection-token';

/**
 * Tab group after the ARIA tabs pattern with automatic activation: one tab stop, the arrows
 * (mirrored in right-to-left pages), Home and End move the selection and the focus together.
 * The first tab is selected until the reader picks another.
 */
@Component({
  selector: 'lodb-tabs',
  templateUrl: './tabs.html',
  providers: [{ provide: TAB_SELECTION, useExisting: Tabs }],
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Tabs implements TabSelection {
  /** Accessible name of the tab list. */
  readonly label = input.required<string>();

  protected readonly tabs = contentChildren(Tab);
  private readonly buttons = viewChildren<ElementRef<HTMLButtonElement>>('tabButton');
  private readonly direction = inject(Directionality);
  private readonly chosen = signal<string | undefined>(undefined);

  readonly selectedKey = computed(() => {
    const keys = this.tabs().map((tab) => tab.key());
    const chosen = this.chosen();
    return chosen !== undefined && keys.includes(chosen) ? chosen : keys[0];
  });

  protected select(key: string): void {
    this.chosen.set(key);
  }

  protected onKeydown(event: KeyboardEvent, index: number): void {
    const move = tabMoveForKey(event.key, this.direction.valueSignal());
    const tabs = this.tabs();
    if (move === null || tabs.length === 0) {
      return;
    }
    event.preventDefault();
    const target = tabIndexAfter(move, index, tabs.length);
    this.chosen.set(tabs[target].key());
    this.buttons()[target]?.nativeElement.focus();
  }
}
