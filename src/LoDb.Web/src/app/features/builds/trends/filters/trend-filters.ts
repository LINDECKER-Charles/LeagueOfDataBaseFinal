import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ChampionOption } from '../../../../core/api/generated/models/champion-option';
import type { GameMode } from '../../../../core/api/generated/models/game-mode';
import type { TrendsFilter } from '../../../../core/api/generated/models/trends-filter';
import { Button } from '../../../../ui/controls/button';
import { Field } from '../../../../ui/controls/field';
import { languageLabel } from '../../shared/language/language-label';
import { modeLabelKey } from '../../shared/modes/mode-label-key';
import { filterParamsOf } from './trends-params';

// The query of the page that names the builds' names, carried by a submit without scripts.
const CONTEXT_PARAMS = ['version', 'lang'] as const;

/**
 * The filters of the trends: champion, mode and language, each "all" by default, applied by
 * a button. A plain GET form, so it works before the scripts load; once they have, the submit
 * navigates to the same query instead, and the list starts again at its first page. The
 * applied option is selected twice: by property for the browser, by attribute for the
 * server's HTML, whose DOM gives options no `selected` property.
 */
@Component({
  selector: 'lodb-trend-filters',
  imports: [Button, Field, TranslocoPipe],
  templateUrl: './trend-filters.html',
  styleUrl: './trend-filters.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrendFilters {
  /** The filters the API applied, as it understood them. */
  readonly filters = input.required<TrendsFilter>();
  readonly champions = input.required<readonly ChampionOption[]>();
  readonly languages = input.required<readonly string[]>();
  readonly modes = input.required<readonly GameMode[]>();
  /** Where the form submits without scripts: the trends of the page's locale. */
  readonly action = input.required<string>();

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly modeOptions = computed(() =>
    this.modes().map((mode) => ({ mode, key: modeLabelKey(mode) })),
  );
  protected readonly languageOptions = computed(() =>
    this.languages().map((code) => ({ code, label: languageLabel(code) })),
  );
  protected readonly context = computed(() =>
    CONTEXT_PARAMS.flatMap((name) => {
      const value = this.query().get(name);
      return value === null ? [] : [{ name, value }];
    }),
  );

  protected apply(event: SubmitEvent): void {
    event.preventDefault();
    const form = new FormData(event.target as HTMLFormElement);
    const value = (name: string) => form.get(name)?.toString() ?? null;
    const queryParams = filterParamsOf({
      champion: value('champion'),
      mode: value('mode'),
      language: value('language'),
    });
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      queryParamsHandling: 'merge',
    });
  }
}
