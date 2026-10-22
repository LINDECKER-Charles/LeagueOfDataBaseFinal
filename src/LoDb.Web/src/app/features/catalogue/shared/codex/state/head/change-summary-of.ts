import type { CatalogueEntry } from '../../../../../../core/routing/catalogue/catalogue-entry';
import type { CatalogueTexts } from '../../head/catalogue-texts';
import { fieldLabelsOf } from '../labels/field-labels-of';
import { listOf } from '../labels/list-of';
import type { StatefulDetails } from '../stateful-details';

/** The fields a description names at most, before its ellipsis. */
const MAX_LABELS = 3;

/**
 * What the patch of a state's opener did to the entity, opening its description: the first
 * fields it changed ("Changed in patch 11.5.1: Health, Armor, Q cooldown…"), or that the
 * entity is new or back. Its blocks, the history and the patch's page name the same fields.
 * Null for a page that opens no state, a state opened at the start of the index (compared to
 * nothing), or a change without a field to name.
 */
export function changeSummaryOf(
  entry: CatalogueEntry<StatefulDetails>,
  texts: CatalogueTexts,
): string | null {
  const { context, details } = entry;
  const state = details.state;
  if (state === null || !state.isOpener || state.kind === 'baseline') {
    return null;
  }
  const version = state.openerVersion;
  if (state.kind !== 'changed') {
    return texts.seo(`state.summary_${state.kind}`, { version });
  }
  const translate = (key: string, params?: Readonly<Record<string, unknown>>) =>
    texts.main(key, params);
  const labels = fieldLabelsOf(details.changes?.fields ?? state.changedFields, translate);
  if (labels.length === 0) {
    return null;
  }
  const fields = listOf(context.locale, labels.slice(0, MAX_LABELS));
  return texts.seo(labels.length > MAX_LABELS ? 'state.summary_more' : 'state.summary', {
    version,
    fields,
  });
}
