import { Directive, ElementRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { type DisclosureCue, disclosureClosing } from './disclosure-closing';

/**
 * Header menus are native `<details>`: they open without JavaScript. Once it runs, this
 * directive folds them the way a menu is expected to fold: a press elsewhere, Escape (focus
 * then returns to the summary), or a completed navigation.
 */
@Directive({
  selector: 'details[lodbDisclosure]',
  host: {
    '(document:pointerdown)': 'onPointerDown($event)',
    '(keydown)': 'onKeyDown($event)',
  },
})
export class Disclosure {
  private readonly details = inject<ElementRef<HTMLDetailsElement>>(ElementRef).nativeElement;

  constructor() {
    inject(Router)
      .events.pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.foldOn({ kind: 'navigation' }));
  }

  protected onPointerDown(event: PointerEvent): void {
    const inside = event.target instanceof Node && this.details.contains(event.target);
    this.foldOn({ kind: 'pointer', inside });
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (disclosureClosing(this.details.open, { kind: 'key', key: event.key })) {
      this.details.open = false;
      this.details.querySelector('summary')?.focus();
    }
  }

  private foldOn(cue: DisclosureCue): void {
    if (disclosureClosing(this.details.open, cue)) {
      this.details.open = false;
    }
  }
}
