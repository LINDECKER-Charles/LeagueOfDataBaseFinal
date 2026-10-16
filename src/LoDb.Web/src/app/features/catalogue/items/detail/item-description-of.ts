import type { ItemDetails } from '../../../../core/api/generated/models/item-details';
import type { CatalogueEntry } from '../../../../core/routing/catalogue/catalogue-entry';
import type { CatalogueTexts } from '../../shared/codex/head/catalogue-texts';
import { formatItemStat } from '../stats/format-item-stat';

/** The stats a description names at most: the first rows of the block, in display order. */
const MAX_STATS = 3;
/** Past this length a result page cuts the description: it then names fewer stats. */
const MAX_LENGTH = 160;

/**
 * The search-result description of an item page. Players search an item by its price and
 * its stats ("elixir of sorcery 50 ability power 500 gold"), so both are stated when the
 * item has them; an item that cannot be bought states no price, one without a stat block
 * (a consumable, an Arena item) no stats, and one with neither keeps the plain description.
 */
export function itemDescriptionOf(
  entry: CatalogueEntry<ItemDetails>,
  texts: CatalogueTexts,
  name: string,
): string {
  const { context, details } = entry;
  const gold = details.profile.gold;
  const isPriced = gold.isPurchasable && gold.total > 0;
  const params = isPriced
    ? { name, gold: gold.total, version: context.version }
    : { name, version: context.version };
  const stats = statPhrasesOf(details, texts);
  if (stats.length === 0) {
    return texts.seo(
      isPriced ? 'item.detail.description_priced' : 'item.detail.description',
      params,
    );
  }
  const key = isPriced ? 'item.detail.description_full' : 'item.detail.description_stats';
  const list = new Intl.ListFormat(context.locale, { type: 'conjunction', style: 'long' });
  return fitted(stats.length, (count) =>
    texts.seo(key, { ...params, stats: list.format(stats.slice(0, count)) }),
  );
}

// Each stat as its row shows it (+50) beside its label, in the order the locale reads them.
function statPhrasesOf(details: ItemDetails, texts: CatalogueTexts): string[] {
  return details.profile.stats.slice(0, MAX_STATS).map((row) =>
    texts.seo('item.detail.stat', {
      value: formatItemStat(row),
      label: texts.main(`stat.${row.stat}`),
    }),
  );
}

// The longest description that fits, dropping the last stats first; one stat always stays.
function fitted(count: number, describe: (count: number) => string): string {
  const description = describe(count);
  return count > 1 && description.length > MAX_LENGTH ? fitted(count - 1, describe) : description;
}
