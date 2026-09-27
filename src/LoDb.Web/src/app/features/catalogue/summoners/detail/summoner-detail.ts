import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  untracked,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { SummonerDetails } from '../../../../core/api/generated/models/summoner-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Reveal } from '../../../../ui/motion/reveal';
import { Pager } from '../../../../ui/navigation/pager';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { Frame } from '../../../../ui/surfaces/frame';
import { EditionCounterpart } from '../../shared/codex/edition/edition-counterpart';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import { HERO_STYLES } from '../../shared/codex/hero/hero-styles';
import { injectDetailPager } from '../../shared/codex/pager/inject-detail-pager';
import { RichText } from '../../shared/codex/rich-text/rich-text';
import { LoadTime } from '../../shared/codex/timing/load-time';
import { injectTranslate } from '../../shared/codex/texts/inject-translate';
import { CatalogueImage } from '../../../../ui/cards/catalogue-image';
import { EditionBadge } from '../../../../ui/cards/edition-badge';
import { modeLabelsOf } from '../modes/mode-labels-of';
import { summonerPlaquesOf } from './plaques/summoner-plaques-of';
import { summonerHeadOf } from './summoner-head-of';

/** Box of the spell's icon in its seal, in CSS pixels. */
const SEAL_ICON_SIZE = 154;

/**
 * A summoner spell page, `/{locale}/[{version}/]summoners/{id}`: the spell in its invocation
 * seal with its cooldown, its facts on engraved plaques, its description and the modes it is
 * allowed in; for a LoL Classic twin, its edition and the link to the other game's spell.
 */
@Component({
  selector: 'lodb-summoner-detail',
  imports: [
    Backdrop,
    CatalogueImage,
    EditionBadge,
    EditionCounterpart,
    Frame,
    LoadTime,
    Pager,
    Reveal,
    RichText,
    TranslocoPipe,
  ],
  templateUrl: './summoner-detail.html',
  styleUrl: './summoner-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SummonerDetail {
  /** The spell, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<SummonerDetails>>('entry');
  protected readonly hero = HERO_STYLES;
  protected readonly sealIconSize = SEAL_ICON_SIZE;

  private readonly translate = injectTranslate();

  protected readonly cooldown = computed(() => this.entry().details.profile.cooldown[0] ?? null);
  protected readonly plaques = computed(() =>
    summonerPlaquesOf(this.entry().details, this.translate()),
  );
  protected readonly modes = computed(() =>
    modeLabelsOf(this.entry().details.profile.modes, this.translate()('edition.classic')),
  );
  protected readonly pager = injectDetailPager(() => ({
    resource: 'summoners',
    context: this.entry().context,
    neighbours: this.entry().details.neighbours,
  }));

  constructor() {
    const head = inject(CatalogueHead);
    effect(() => {
      const entry = this.entry();
      untracked(() => head.write(entry.context.locale, (texts) => summonerHeadOf(entry, texts)));
    });
  }
}
