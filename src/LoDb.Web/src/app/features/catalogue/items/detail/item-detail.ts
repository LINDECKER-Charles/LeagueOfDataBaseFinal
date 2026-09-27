import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ItemDetails } from '../../../../core/api/generated/models/item-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import { injectRouteData } from '../../../../core/routing/inject-route-data';
import { Icon } from '../../../../ui/media/icon';
import { Pager } from '../../../../ui/navigation/pager';
import { Backdrop } from '../../../../ui/surfaces/backdrop';
import { Frame } from '../../../../ui/surfaces/frame';
import { Reveal } from '../../../../ui/motion/reveal';
import { CatalogueImage } from '../../shared/cards/catalogue-image';
import { EditionBadge } from '../../shared/cards/edition-badge';
import { initialsOf } from '../../shared/cards/initials-of';
import { EditionCounterpart } from '../../shared/codex/edition/edition-counterpart';
import { CatalogueHead } from '../../shared/codex/head/catalogue-head';
import { HERO_STYLES } from '../../shared/codex/hero/hero-styles';
import { injectCatalogueLink } from '../../shared/codex/links/inject-catalogue-link';
import { injectDetailPager } from '../../shared/codex/pager/inject-detail-pager';
import { RichText } from '../../shared/codex/rich-text/rich-text';
import { ItemAside } from './aside/item-aside';
import { itemHeadOf } from './item-head-of';
import { recipeTreeOf } from './recipe/recipe-tree-of';
import { RecipeTree } from './recipe/recipe-tree';

/** Box of the hero's icon, in CSS pixels. */
const HERO_ICON_SIZE = 104;
/** Box of an upgrade's icon, in CSS pixels. */
const UPGRADE_ICON_SIZE = 48;

/**
 * An item page, `/{locale}/[{version}/]items/{id}-{slug}`: the item and its price, its
 * crafting tree and upgrades, Riot's description, its stats, gold ledger and availability,
 * and, for a LoL Classic twin, its edition and the link to the other game's item.
 */
@Component({
  selector: 'lodb-item-detail',
  imports: [
    Backdrop,
    CatalogueImage,
    EditionBadge,
    EditionCounterpart,
    Frame,
    Icon,
    ItemAside,
    Pager,
    RecipeTree,
    Reveal,
    RichText,
    RouterLink,
    TranslocoPipe,
  ],
  templateUrl: './item-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ItemDetail {
  /** The item, resolved by `resolveCatalogueEntry`. */
  protected readonly entry = injectRouteData<CatalogueEntry<ItemDetails>>('entry');
  protected readonly hero = HERO_STYLES;
  protected readonly heroIconSize = HERO_ICON_SIZE;
  protected readonly upgradeIconSize = UPGRADE_ICON_SIZE;
  /** An item without art is marked by the initials of its name, as the legacy hero was. */
  protected readonly initials = initialsOf;
  protected readonly linkOf = injectCatalogueLink();
  protected readonly tree = computed(() => recipeTreeOf(this.entry().details.recipe));
  protected readonly pager = injectDetailPager({
    resource: 'items',
    at: () => ({ context: this.entry().context, key: this.entry().details.profile.id }),
    keyOf: (card) => card.id,
  });

  constructor() {
    const head = inject(CatalogueHead);
    effect(() => {
      const entry = this.entry();
      untracked(() => head.write(entry.context.locale, (texts) => itemHeadOf(entry, texts)));
    });
  }
}
