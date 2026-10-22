import type { CatalogueEntry } from '../../../../../../core/routing/catalogue/catalogue-entry';
import type { SeoPage } from '../../../../../../core/seo/seo-page';
import type { CatalogueTexts } from '../../head/catalogue-texts';
import type { StatefulDetails } from '../stateful-details';
import { changeSummaryOf } from './change-summary-of';

type StateHead = Pick<Extract<SeoPage, { readonly path: string }>, 'description' | 'titlePatches'>;

/**
 * What the state of a detail adds to its head. The opener of a closed state, the page search
 * engines index for it, names its run of patches after its title ("(patches 11.5.1 to
 * 11.9.1)", instead of its version) and opens its description with what its patch changed.
 * Any other page keeps its title and `description`.
 */
export function stateHeadOf(
  entry: CatalogueEntry<StatefulDetails>,
  texts: CatalogueTexts,
  description: string,
): StateHead {
  const state = entry.details.state;
  if (!entry.context.pinned || state === null || state.isOpen || !state.isOpener) {
    return { description };
  }
  const summary = changeSummaryOf(entry, texts);
  return {
    description: summary === null ? description : `${summary} ${description}`,
    titlePatches: { first: state.openerVersion, last: state.lastVersion ?? state.openerVersion },
  };
}
