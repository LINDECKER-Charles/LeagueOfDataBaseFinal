import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The column of the about, FAQ and legal pages: codex banner (eyebrow and title), what the
 * page adds under its title (`[lodbLead]`: a lead, a date plate), then the prose. The pages
 * hand in their texts translated.
 */
@Component({
  selector: 'lodb-editorial-frame',
  template: `<div class="relative mx-auto w-full max-w-3xl px-6 pt-10 pb-20 sm:px-8 lg:pt-14">
    <header class="relative mb-10">
      <div class="codex-header mb-6">
        <span class="codex-header__title">{{ eyebrow() }}</span>
      </div>
      <h1
        class="mb-5 font-beaufort text-3xl tracking-[0.06em] text-gold-grad uppercase sm:text-4xl"
      >
        {{ heading() }}
      </h1>
      <ng-content select="[lodbLead]" />
    </header>
    <ng-content />
  </div>`,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditorialFrame {
  readonly eyebrow = input.required<string>();
  readonly heading = input.required<string>();
}
