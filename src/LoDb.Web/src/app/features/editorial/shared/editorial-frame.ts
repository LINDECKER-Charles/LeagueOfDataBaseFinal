import { ChangeDetectionStrategy, Component, booleanAttribute, input } from '@angular/core';
import { Backdrop } from '../../../ui/surfaces/backdrop';

/**
 * The column of the about, FAQ and legal pages: codex banner (eyebrow and title), what the
 * page adds under its title (`[lodbLead]`: a lead, a date plate), then the prose. The pages
 * hand in their texts translated. The about pages lay the ambient backdrop under the column
 * (`ambient`); the legal pages never had one.
 */
@Component({
  selector: 'lodb-editorial-frame',
  imports: [Backdrop],
  template: ` <div
    class="relative isolate mx-auto w-full max-w-3xl px-6 pt-10 pb-20 sm:px-8 lg:pt-14"
  >
    @if (ambient()) {
      <lodb-backdrop />
    }
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
  /** Lays the ambient backdrop under the column, clipped to it. */
  readonly ambient = input(false, { transform: booleanAttribute });
}
