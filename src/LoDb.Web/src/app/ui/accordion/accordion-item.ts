import { CdkAccordionItem } from '@angular/cdk/accordion';
import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Icon } from '../media/icon';

/**
 * One collapsible section, after the ARIA accordion pattern: a heading holding a button that
 * reports and toggles its state, and a labelled region. The body stays in the DOM while
 * folded (`hidden`), so server-rendered content is indexed and the page can still find it.
 */
@Component({
  selector: 'lodb-accordion-item',
  imports: [Icon],
  templateUrl: './accordion-item.html',
  hostDirectives: [
    {
      directive: CdkAccordionItem,
      inputs: ['expanded', 'disabled'],
      outputs: ['expandedChange'],
    },
  ],
  host: { class: 'block border-t border-gold-deep/30' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccordionItem {
  readonly heading = input.required<string>();
  /** Optional counter shown after the heading, such as the number of entries inside. */
  readonly count = input<number>();
  /** Level of the heading in the page outline. */
  readonly level = input(3);

  protected readonly item = inject(CdkAccordionItem);
}
