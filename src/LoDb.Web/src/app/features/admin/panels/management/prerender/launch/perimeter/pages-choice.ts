import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';

/**
 * « Pages » : every page of each patch, or the openers first — the patch page and the pages
 * that open a state at it, the ones that may be indexed — a native radio in each card. With the
 * openers, a golden note says what such a pass leaves to the next one: every other page, the
 * promoted patches (they wait for its end), and an epoch it never makes whole.
 */
@Component({
  selector: 'lodb-pages-choice',
  imports: [AdminTextPipe],
  template: `
    <section class="mt-6">
      <h3 id="launch-pages-title" class="readout-label mb-2">
        {{ 'admin.prerender.launcher.pages.title' | adminText }}
      </h3>
      <div class="grid gap-3 sm:grid-cols-2" role="radiogroup" aria-labelledby="launch-pages-title">
        @for (choice of choices; track choice.isOpeners) {
          <label
            class="flex cursor-pointer flex-col gap-1.5 border border-gold/28 p-3 has-checked:border-hex has-checked:bg-hex/5"
            [attr.data-pages]="choice.key"
          >
            <span class="flex items-center gap-2 text-gold-bright">
              <input
                type="radio"
                name="launch-pages"
                class="size-4 shrink-0 accent-hex scheme-dark"
                [checked]="choice.isOpeners === openersFirst()"
                (change)="changed.emit(choice.isOpeners)"
              />
              {{ 'admin.prerender.launcher.pages.' + choice.key + '_title' | adminText }}
            </span>
            <span class="readout-text">
              {{ 'admin.prerender.launcher.pages.' + choice.key + '_body' | adminText }}
            </span>
          </label>
        }
      </div>
      @if (openersFirst()) {
        <p class="mt-2 text-[0.85rem] text-gold" data-pages-openers>
          {{ 'admin.prerender.launcher.pages.openers_note' | adminText }}
        </p>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PagesChoice {
  /** Whether the openers are rendered alone, before a pass over every page. */
  readonly openersFirst = input.required<boolean>();
  /** The choice once the operator picks a card. */
  readonly changed = output<boolean>();

  protected readonly choices = [
    { key: 'every', isOpeners: false },
    { key: 'openers', isOpeners: true },
  ] as const;
}
