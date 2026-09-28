import { Directive, inject, input } from '@angular/core';
import { SectionJump } from './section-jump';

/**
 * A link to a section of the same page: `<a lodbFragmentLink="pricing">`. It jumps at once,
 * below the section's scroll margin, and still works without JavaScript; use it instead of a
 * `routerLink` with a `fragment`, whose scrolling ignores that margin.
 */
@Directive({
  selector: 'a[lodbFragmentLink]',
  host: {
    '[attr.href]': 'jump.href(lodbFragmentLink())',
    '(click)': 'jump.follow($event, lodbFragmentLink())',
  },
})
export class FragmentLink {
  /** Id of the section. */
  readonly lodbFragmentLink = input.required<string>();

  protected readonly jump = inject(SectionJump);
}
