import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { TranslocoPipe, translateSignal } from '@jsverse/transloco';
import type { ChampionDetails } from '../../../../core/api/generated/models/champion-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Reveal } from '../../../../ui/motion/reveal';
import { Pager } from '../../../../ui/navigation/pager';
import { SectionNav } from '../../../../ui/navigation/section-nav';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { Skeleton } from '../../../../ui/surfaces/skeleton';
import { injectDetailPager } from '../../shared/codex/pager/inject-detail-pager';
import { applyChampionsHead } from '../seo/apply-champions-head';
import { championSeo } from '../seo/champion-seo';
import { AbilityShowcase } from './abilities/ability-showcase';
import { CHAMPION_SECTIONS } from './champion-sections';
import { ChampionHero } from './hero/champion-hero';
import { DdragonHtmlPipe } from './rich-text/ddragon-html-pipe';
import { alternateSkins } from './skins/alternate-skins';
import { SkinGallery } from './skins/skin-gallery';
import { StatBoard } from './stats/stat-board';
import { LoadTime } from '../../shared/codex/timing/load-time';

type SectionId = (typeof CHAMPION_SECTIONS)[number]['id'];

function hasText(tip: string): boolean {
  return tip.trim() !== '';
}

/**
 * A champion's page, rendered on the server from the entry its route resolved: the hero,
 * sticky section chips, the ability showcase, the skins (loaded once scrolled to), the lore,
 * the tips, the stat board, then the pager through the list and the load-time badge.
 */
@Component({
  selector: 'lodb-champion-page',
  imports: [
    AbilityShowcase,
    Backdrop,
    ChampionHero,
    DdragonHtmlPipe,
    LoadTime,
    Pager,
    Reveal,
    SectionNav,
    Skeleton,
    SkinGallery,
    StatBoard,
    TranslocoPipe,
  ],
  templateUrl: './champion-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChampionPage {
  private readonly entry = injectRouteData<CatalogueEntry<ChampionDetails>>('entry');
  protected readonly details = computed(() => this.entry().details);
  protected readonly context = computed(() => this.entry().context);
  protected readonly profile = computed(() => this.details().profile);
  protected readonly skinCount = computed(() => alternateSkins(this.details().skins).length);
  protected readonly lore = computed(() => this.details().lore || this.details().blurb);
  protected readonly allyTips = computed(() => this.details().allyTips.filter(hasText));
  protected readonly enemyTips = computed(() => this.details().enemyTips.filter(hasText));
  // Root keys, hence no catalogue scope on this page: under one, translateSignal reads its keys
  // inside that scope (`champions.champion.detail.…`), where the pipe does not.
  private readonly labels = translateSignal(CHAMPION_SECTIONS.map((section) => section.label));
  protected readonly sections = computed(() => {
    const labels = this.labels();
    return CHAMPION_SECTIONS.map((section, index) => ({
      id: section.id,
      label: labels[index] ?? '',
    })).filter((section) => this.isShown(section.id));
  });
  protected readonly pager = injectDetailPager(() => ({
    resource: 'champions',
    context: this.context(),
    neighbours: this.details().neighbours,
  }));

  constructor() {
    applyChampionsHead(() => {
      const entry = this.entry();
      return { locale: entry.context.locale, build: (translate) => championSeo(entry, translate) };
    });
  }

  private isShown(id: SectionId): boolean {
    switch (id) {
      case 'abilities':
        return this.details().abilities.length > 0;
      case 'skins':
        return this.skinCount() > 0;
      case 'lore':
        return this.lore() !== '';
      case 'tips':
        return this.allyTips().length > 0 || this.enemyTips().length > 0;
      case 'stats':
        return true;
    }
  }
}
