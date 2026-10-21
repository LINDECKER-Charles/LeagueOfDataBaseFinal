import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { PrerenderPatchesReport } from '../../../../../../../core/api/generated/models/prerender-patches-report';
import { LOCALES } from '../../../../../../../core/i18n/locales';
import { Button } from '../../../../../../../ui/controls/button';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { isHeld } from '../../common/patches/is-held';
import { localeName } from '../../common/patches/locale-name';

/** One locale of the perimeter, and how many of the patches ticked already hold it. */
interface LocaleChip {
  readonly code: string;
  readonly name: string;
  readonly isOn: boolean;
  /** The patches ticked whose cell holds every type chosen: what « Ignorer… » leaves out. */
  readonly complete: number;
}

/**
 * « Langues » (§ 3.4): a toggle per locale of the site, in its order, with its French name
 * and how many of the patches ticked already hold it for the types chosen (« 30/38
 * complets »), the same rule « Ignorer ce qui est déjà complet » follows. One at least.
 */
@Component({
  selector: 'lodb-locale-chips',
  imports: [AdminTextPipe, Button],
  template: `
    <section role="group" aria-labelledby="launch-locales-title">
      <div class="mb-2 flex flex-wrap items-center gap-3">
        <h3 id="launch-locales-title" class="readout-label">
          {{ 'admin.prerender.launcher.locales.title' | adminText }}
        </h3>
        <span class="font-mono text-[0.82rem] text-text-dim" data-locales-count>
          {{
            'admin.prerender.launcher.locales.count'
              | adminText: { selected: selected().length, total: chips().length }
          }}
        </span>
        <span class="ms-auto flex gap-2">
          <button lodbButton="ghost" lodbButtonSize="small" type="button" (click)="choose(all)">
            {{ 'admin.prerender.launcher.locales.all' | adminText }}
          </button>
          <button lodbButton="ghost" lodbButtonSize="small" type="button" (click)="choose([])">
            {{ 'admin.prerender.launcher.locales.none' | adminText }}
          </button>
        </span>
      </div>
      <div class="flex flex-wrap gap-2">
        @for (chip of chips(); track chip.code) {
          <button
            type="button"
            class="inline-flex min-h-11 items-center gap-1.5 border border-gold/28 px-2.5 text-start text-[0.85rem] text-text-muted aria-pressed:border-hex aria-pressed:bg-hex/10 aria-pressed:text-gold-bright"
            [attr.aria-pressed]="chip.isOn"
            [attr.data-locale]="chip.code"
            (click)="toggle(chip.code)"
          >
            <span class="font-mono">{{ chip.code }}</span>
            <span>· {{ chip.name }}</span>
            @if (versions().size > 0) {
              <span class="font-mono text-[0.75rem] text-text-dim">
                {{
                  'admin.prerender.launcher.locales.complete'
                    | adminText: { complete: chip.complete, versions: versions().size }
                }}
              </span>
            }
          </button>
        }
      </div>
      @if (selected().length === 0) {
        <p class="mt-2 text-[0.85rem] text-danger-light" data-locales-required>
          {{ 'admin.prerender.launcher.locales.required' | adminText }}
        </p>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LocaleChips {
  /** The locales chosen. */
  readonly selected = input.required<readonly string[]>();
  /** The coverage of the epoch written; null while it loads. */
  readonly coverage = input.required<PrerenderPatchesReport | null>();
  /** The versions ticked. */
  readonly versions = input.required<ReadonlySet<string>>();
  readonly pageTypes = input.required<readonly string[]>();
  /** The locales chosen once one is toggled, in the site's order. */
  readonly changed = output<readonly string[]>();

  protected readonly all = LOCALES;
  protected readonly chips = computed<LocaleChip[]>(() => {
    const coverage = this.coverage();
    const rows = coverage?.patches.filter((row) => this.versions().has(row.version)) ?? [];
    const chosen = new Set(this.selected());
    return LOCALES.map((code) => {
      const at = coverage?.locales.indexOf(code) ?? -1;
      const held = rows.filter((row) => isHeld(row.types[at] ?? '0', this.pageTypes()));
      return { code, name: localeName(code), isOn: chosen.has(code), complete: held.length };
    });
  });

  protected toggle(code: string): void {
    const chosen = new Set(this.selected());
    if (!chosen.delete(code)) {
      chosen.add(code);
    }
    this.choose(LOCALES.filter((locale) => chosen.has(locale)));
  }

  protected choose(locales: readonly string[]): void {
    this.changed.emit(locales);
  }
}
