import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';

/** A click that asks for nothing but following the link: a modified one opens a new tab. */
function isPlainClick(event: MouseEvent): boolean {
  return event.button === 0 && !(event.metaKey || event.ctrlKey || event.shiftKey || event.altKey);
}

/**
 * Links to the sections of the current page. The router's anchor scrolling puts a section
 * flush against the top of the viewport, under the sticky header: a jump here goes through
 * `scrollIntoView`, which keeps the section's CSS scroll margin, as a native `#` link did.
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
   * Follows a plain click on a link to section `id`: the page scrolls to it and its URL
   * stays deep-linkable. Any other click, or a section not on the page, is left to the
   * browser, the event untouched (`defaultPrevented` tells which).
   */
  follow(event: MouseEvent, id: string, behavior: ScrollBehavior = 'auto'): void {
    const target = this.document.getElementById(id);
    const view = this.document.defaultView;
    if (target === null || view === null || !isPlainClick(event)) {
      return;
    }
    event.preventDefault();
    target.scrollIntoView({ behavior, block: 'start' });
    view.history.replaceState(view.history.state, '', this.href(id));
  }
}
