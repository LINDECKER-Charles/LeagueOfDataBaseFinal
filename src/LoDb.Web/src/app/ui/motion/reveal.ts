import { DOCUMENT } from '@angular/common';
import { DestroyRef, Directive, ElementRef, afterNextRender, inject } from '@angular/core';
import { shouldRevealImmediately } from './should-reveal-immediately';

const ARMED_CLASS = 'js-reveal';
const REVEALED_CLASS = 'reveal-in';
const REVEAL_BAND: IntersectionObserverInit = { rootMargin: '0px 0px -8% 0px', threshold: 0.05 };
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/**
 * Scroll reveal: the host rises into view once (foundation/motion.css). The CSS only arms
 * itself under `.js-reveal` on `<html>`, stamped here in the browser, so server-rendered
 * content is never hidden without JavaScript; a host already on screen, or any host under
 * reduced motion, is revealed in the frame it is armed.
 */
@Directive({
  selector: '[lodbReveal]',
  host: { 'data-reveal': '' },
})
export class Reveal {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    afterNextRender(() => this.arm());
  }

  private arm(): void {
    this.document.documentElement.classList.add(ARMED_CLASS);
    const view = this.document.defaultView;
    const reduced = view?.matchMedia(REDUCED_MOTION).matches ?? true;
    const top = this.host.getBoundingClientRect().top;
    if (shouldRevealImmediately(reduced, top, view?.innerHeight ?? 0)) {
      this.host.classList.add(REVEALED_CLASS);
      return;
    }
    const observer = new IntersectionObserver(([entry]) => {
      if (entry?.isIntersecting) {
        this.host.classList.add(REVEALED_CLASS);
        observer.disconnect();
      }
    }, REVEAL_BAND);
    observer.observe(this.host);
    this.destroyRef.onDestroy(() => observer.disconnect());
  }
}
