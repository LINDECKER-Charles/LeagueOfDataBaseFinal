import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Logo } from '../../../../../ui/media/logo';

/**
 * The head of a catalogue list: the crest, the data source and version it reads, the list's
 * title, and how many entries it holds.
 */
@Component({
  selector: 'lodb-list-heading',
  imports: [Logo],
  template: `<div class="flex items-center gap-4">
      <div class="relative grid size-14 shrink-0 place-items-center text-gold hextech-frame">
        <div class="absolute inset-0 opacity-40 blur-lg hx-logo-halo"></div>
        <lodb-logo class="relative z-10 size-7" />
      </div>
      <div>
        <p class="mb-1.5 eyebrow">Data Dragon · {{ version() }}</p>
        <h1 class="font-beaufort text-3xl tracking-wide text-gold-grad uppercase sm:text-4xl">
          {{ title() }}
        </h1>
      </div>
    </div>
    @if (count() !== null) {
      <p class="flex items-baseline gap-2">
        <span class="font-mono text-xl leading-none text-hex">{{ count() }}</span>
        <span class="font-spiegel text-xs tracking-wider text-text-dim uppercase">
          {{ countLabel() }}
        </span>
      </p>
    }`,
  host: { class: 'mb-8 flex flex-wrap items-end justify-between gap-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ListHeading {
  readonly version = input.required<string>();
  readonly title = input.required<string>();
  /** Null until the list is known. */
  readonly count = input<number | null>(null);
  /** What is counted, such as "items". */
  readonly countLabel = input('');
}
