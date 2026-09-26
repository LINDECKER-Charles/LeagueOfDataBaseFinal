import { DOCUMENT } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  afterRenderEffect,
  computed,
  inject,
  input,
  linkedSignal,
} from '@angular/core';
import { Router } from '@angular/router';
import { activeSection } from './active-section';
import type { SectionLink } from './section-link';

/** A section counts as current while it crosses the upper-middle of the viewport. */
const READING_BAND = '-35% 0px -55% 0px';
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/** A click that asks for nothing but following the link: a modified one opens a new tab. */
function isPlainClick(event: MouseEvent): boolean {
  return event.button === 0 && !(event.metaKey || event.ctrlKey || event.shiftKey || event.altKey);
}

/**
 * Sticky chips that jump to the sections of a long page and mark the one being read
 * (scrollspy). Plain fragment links first: without JavaScript they still jump. In the browser
 * a click scrolls smoothly, unless the reader asked for reduced motion, and leaves a
 * deep-linkable URL; the mark follows the scroll.
 */
@Component({
  selector: 'lodb-section-nav',
  templateUrl: './section-nav.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SectionNav {
  readonly sections = input.required<readonly SectionLink[]>();
  /** Accessible name of the nav. */
  readonly label = input.required<string>();

  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);
  private readonly order = computed(() => this.sections().map((section) => section.id));
  // Before any section crosses the band, the first chip stands for the top of the page.
  protected readonly current = linkedSignal<string | undefined>(() => this.order()[0]);

  constructor() {
    afterRenderEffect((onCleanup) => {
      const observer = this.watch(this.order());
      onCleanup(() => observer.disconnect());
    });
  }

  /** Href of a chip: the page's own path, so the `<base>` element cannot redirect it. */
  protected href(id: string): string {
    return `${this.router.url.split('#')[0]}#${id}`;
  }

  protected jump(event: MouseEvent, id: string): void {
    const target = this.document.getElementById(id);
    if (target === null || !isPlainClick(event)) {
      return;
    }
    event.preventDefault();
    const reduced = this.document.defaultView?.matchMedia(REDUCED_MOTION).matches ?? true;
    target.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'start' });
    this.document.defaultView?.history.replaceState(history.state, '', this.href(id));
    this.current.set(id);
  }

  private watch(order: readonly string[]): IntersectionObserver {
    const crossing = new Set<string>();
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          const id = entry.target.id;
          if (entry.isIntersecting) crossing.add(id);
          else crossing.delete(id);
        }
        this.current.set(activeSection(order, crossing, this.current()));
      },
      { rootMargin: READING_BAND },
    );
    for (const id of order) {
      const section = this.document.getElementById(id);
      if (section !== null) observer.observe(section);
    }
    return observer;
  }
}
