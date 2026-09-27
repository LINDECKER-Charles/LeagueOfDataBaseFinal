import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  untracked,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RuneTreeDetails } from '../../../../core/api/generated/models/rune-tree-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Pager } from '../../../../ui/navigation/pager';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import { HERO_STYLES } from '../../shared/codex/hero/hero-styles';
import { LoadTime } from '../../shared/codex/timing/load-time';
import { CatalogueImage } from '../../shared/cards/catalogue-image';
import { pathThemeOf } from '../paths/path-theme-of';
import { constellationOf } from './constellation/constellation-of';
import { RuneConstellation } from './constellation/rune-constellation';
import { injectPathPager } from './pager/inject-path-pager';
import { runeHeadOf } from './rune-head-of';

/** Box of the path's mark in the hero, in CSS pixels. */
const EMBLEM_SIZE = 120;

/**
 * A rune path page, `/{locale}/[{version}/]runes/{key}`: the path's mark and name in its
 * colour, then its constellation of keystones and rows; the list's rune cards link to their
 * card here by anchor.
 */
@Component({
  selector: 'lodb-rune-detail',
  imports: [Backdrop, CatalogueImage, LoadTime, Pager, RuneConstellation, TranslocoPipe],
  templateUrl: './rune-detail.html',
  styleUrl: './rune-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RuneDetail {
  /** The path, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<RuneTreeDetails>>('entry');
  protected readonly hero = HERO_STYLES;
  protected readonly emblemSize = EMBLEM_SIZE;
  protected readonly theme = computed(() => pathThemeOf(this.entry().details.profile.key));
  protected readonly constellation = computed(() => constellationOf(this.entry().details));
  protected readonly pager = injectPathPager(() => ({
    context: this.entry().context,
    key: this.entry().details.profile.key,
  }));

  constructor() {
    const head = inject(CatalogueHead);
    effect(() => {
      const entry = this.entry();
      untracked(() => head.write(entry.context.locale, (texts) => runeHeadOf(entry, texts)));
    });
  }
}
