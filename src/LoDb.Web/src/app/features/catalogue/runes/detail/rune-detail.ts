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
import { EmptyState } from '../../../../ui/surfaces/empty-state';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import { HERO_STYLES } from '../../shared/codex/hero/hero-styles';
import { injectDetailPager } from '../../shared/codex/pager/inject-detail-pager';
import { LoadTime } from '../../shared/codex/timing/load-time';
import { CatalogueImage } from '../../../../ui/cards/catalogue-image';
import { initialsOf } from '../../../../ui/cards/initials-of';
import { pathThemeOf } from '../paths/path-theme-of';
import { injectAnchorOffset } from './anchor/inject-anchor-offset';
import { constellationOf } from './constellation/constellation-of';
import { RuneConstellation } from './constellation/rune-constellation';
import { runeHeadOf } from './rune-head-of';

/** Box of the path's mark in the hero, in CSS pixels. */
const EMBLEM_SIZE = 120;

/**
 * A rune path page, `/{locale}/[{version}/]runes/{key}`: the path's mark and name in its
 * colour, then its constellation of keystones and rows; the list's rune cards link to their
 * card here by anchor, which stops below its row's label. A path without runes shows the
 * framed "no results" of the legacy page.
 */
@Component({
  selector: 'lodb-rune-detail',
  imports: [
    Backdrop,
    CatalogueImage,
    EmptyState,
    LoadTime,
    Pager,
    RuneConstellation,
    TranslocoPipe,
  ],
  templateUrl: './rune-detail.html',
  styleUrl: './rune-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RuneDetail {
  /** The path, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<RuneTreeDetails>>('entry');
  protected readonly hero = HERO_STYLES;
  protected readonly emblemSize = EMBLEM_SIZE;
  /** A path without art is marked by its initials, large and in its colour, as the legacy was. */
  protected readonly initials = initialsOf;
  protected readonly theme = computed(() => pathThemeOf(this.entry().details.profile.key));
  protected readonly constellation = computed(() => constellationOf(this.entry().details));
  protected readonly pager = injectDetailPager(() => ({
    resource: 'runes',
    context: this.entry().context,
    neighbours: this.entry().details.neighbours,
  }));

  constructor() {
    injectAnchorOffset();
    const head = inject(CatalogueHead);
    effect(() => {
      const entry = this.entry();
      untracked(() => head.write(entry.context.locale, (texts) => runeHeadOf(entry, texts)));
    });
  }
}
