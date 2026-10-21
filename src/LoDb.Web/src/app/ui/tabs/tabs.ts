import { Directionality } from '@angular/cdk/bidi';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  type ElementRef,
  afterNextRender,
  computed,
  contentChildren,
  inject,
  input,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { type TabScroll, tabFade } from './tab-fade';
import { Tab } from './tab';
import { tabIndexAfter } from './tab-index-after';
import { tabMoveForKey } from './tab-move-for-key';
import type { TabSelection } from './tab-selection';
import { TAB_SELECTION } from './tab-selection-token';

/**
 * Tab group after the ARIA tabs pattern with automatic activation: one tab stop, the arrows
 * (mirrored in right-to-left pages), Home and End move the selection and the focus together.
 * The first tab is selected until the reader picks another. A strip wider than its place
 * scrolls, its scroll bar hidden: its edges fade where tabs are out of view (`tabFade`), and
 * the focus, moved by a key, brings its tab in.
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
  private readonly list = viewChild.required<ElementRef<HTMLElement>>('tabList');
  private readonly scroll = signal<TabScroll>({ offset: 0, scrollWidth: 0, clientWidth: 0 });
  private readonly destroyRef = inject(DestroyRef);
  private readonly direction = inject(Directionality);
  private readonly chosen = signal<string | undefined>(undefined);

  protected readonly fade = computed(() =>
    tabFade(this.scroll(), this.direction.valueSignal() === 'rtl'),
  );

  constructor() {
    // The browser alone measures: a server renders the strip without a fade.
    afterNextRender(() => {
      this.measure();
      if (typeof ResizeObserver === 'undefined') {
        return;
      }
      const resizes = new ResizeObserver(() => this.measure());
      resizes.observe(this.list().nativeElement);
      this.destroyRef.onDestroy(() => resizes.disconnect());
    });
  }

  readonly selectedKey = computed(() => {
    const keys = this.tabs().map((tab) => tab.key());
    const chosen = this.chosen();
    return chosen !== undefined && keys.includes(chosen) ? chosen : keys[0];
  });

  protected measure(): void {
    const { scrollLeft, scrollWidth, clientWidth } = this.list().nativeElement;
    this.scroll.set({ offset: scrollLeft, scrollWidth, clientWidth });
  }

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
