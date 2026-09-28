import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';

/** Marks a section this service made focusable, so base.css hides its focus ring. */
const SECTION_TARGET = 'data-section-target';

/** A click that asks for nothing but following the link: a modified one opens a new tab. */
function isPlainClick(event: MouseEvent): boolean {
  return event.button === 0 && !(event.metaKey || event.ctrlKey || event.shiftKey || event.altKey);
}

// A native `#` link moves the sequential focus starting point to its target, so the next Tab
// enters the section: a section is not focusable, and -1 lets it take the focus.
function focusSection(target: HTMLElement): void {
  if (!target.hasAttribute('tabindex')) {
    target.setAttribute('tabindex', '-1');
    target.setAttribute(SECTION_TARGET, '');
  }
  target.focus({ preventScroll: true });
}

/**
 * Links to the sections of the current page. The router's anchor scrolling puts a section
 * flush against the top of the viewport, under the sticky header: a jump here goes through
 * `scrollIntoView`, which keeps the section's CSS scroll margin, as a native `#` link did.
 * Like that link, a jump moves the focus into the section and adds a history entry, so Back
 * returns to the top of the page rather than leaving it.
 */
@Injectable({ providedIn: 'root' })
export class SectionJump {
  private readonly document = inject(DOCUMENT);
  private readonly router = inject(Router);

  /** Href of section `id`: the page's own path, so the `<base>` element cannot redirect it. */
  href(id: string): string {
    return `${this.router.url.split('#')[0]}#${id}`;
  }

  /**
   * Follows a plain click on a link to section `id`: the page scrolls to it, the focus moves
   * into it and its URL, pushed, stays deep-linkable. Any other click, or a section not on the
   * page, is left to the browser, the event untouched (`defaultPrevented` tells which).
   */
  follow(event: MouseEvent, id: string, behavior: ScrollBehavior = 'auto'): void {
    const target = this.document.getElementById(id);
    const view = this.document.defaultView;
    if (target === null || view === null || !isPlainClick(event)) {
      return;
    }
    event.preventDefault();
    target.scrollIntoView({ behavior, block: 'start' });
    focusSection(target);
    view.history.pushState(view.history.state, '', this.href(id));
  }
}
