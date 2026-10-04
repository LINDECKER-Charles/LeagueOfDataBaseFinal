import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  afterRenderEffect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { RouterLink, type UrlTree } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Icon } from '../media/icon';
import type { PagerLink } from './pager-link';

/** Lets the links break their names (styles/primitives/navigation.css). */
const TIGHT = 'pager--tight';

/**
 * The codex turning its own pages: the previous and next entity in collection order, with a
 * hub linking back to the list. A missing neighbour leaves an empty cell, so the hub never
 * moves. The links carry `rel` so the order is machine-readable too. The host is a block, so
 * the margin a page sets on it spaces it from the content above.
 *
 * The links keep the legacy tracks, each at least as wide as its longest word, while they fit.
 * Where they would not (the legacy pager then clipped its next link off a phone screen), the
 * nav turns tight and the names break instead.
 */
@Component({
  selector: 'lodb-pager',
  imports: [Icon, RouterLink, TranslocoPipe],
  templateUrl: './pager.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Pager {
  readonly previous = input<PagerLink | null>(null);
  readonly next = input<PagerLink | null>(null);
  /**
   * The list the entity belongs to; its name is not shown, the hub is labelled generically.
   * A UrlTree when it carries a query, as PagerLink's `url`.
   */
  readonly hub = input.required<string | UrlTree>();

  private readonly nav = viewChild.required<ElementRef<HTMLElement>>('nav');
  private readonly view = inject(DOCUMENT).defaultView;

  constructor() {
    // Measured again with each pair of neighbours, whose names may be longer.
    afterRenderEffect((onCleanup) => {
      this.previous();
      this.next();
      const nav = this.nav().nativeElement;
      fitTight(nav);
      const observer = this.refitOnResize(nav);
      onCleanup(() => observer?.disconnect());
    });
  }

  /**
   * The viewport and the web fonts change the widths after the first measure. The class is
   * set a frame later: set within the observer's callback, the heights it changes would be
   * undelivered notifications, reported as a "ResizeObserver loop" error.
   */
  private refitOnResize(nav: HTMLElement): ResizeObserver | null {
    const view = this.view;
    // None where nothing is laid out (jsdom): nothing to fit there.
    if (!view?.ResizeObserver) {
      return null;
    }
    const observer = new view.ResizeObserver(() => view.requestAnimationFrame(() => fitTight(nav)));
    observer.observe(nav);
    for (const cell of Array.from(nav.children)) {
      observer.observe(cell);
    }
    return observer;
  }
}

/** Set directly rather than bound, which would take a second render pass. */
function fitTight(nav: HTMLElement): void {
  nav.classList.remove(TIGHT);
  nav.classList.toggle(TIGHT, nav.scrollWidth > nav.clientWidth);
}
