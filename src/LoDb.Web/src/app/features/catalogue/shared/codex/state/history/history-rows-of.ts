import type { EntityState } from '../../../../../../core/api/generated/models/entity-state';
import type { StateHistoryEntry } from '../../../../../../core/api/generated/models/state-history-entry';
import type { ValueFormat } from '../changes/value-format';
import { fieldLabelsOf } from '../labels/field-labels-of';
import { listOf } from '../labels/list-of';
import type { SubjectNames } from '../labels/subject-names';
import type { HistoryRow } from './history-row';

type Translate = ValueFormat['translate'];

// The open state's run never names the latest patch: a promotion leaves the page unchanged
// until it closes the state. Versions are written whole, as Data Dragon lists them.
function rangeOf(entry: StateHistoryEntry, translate: Translate): string {
  const first = entry.openerVersion;
  const last = entry.lastVersion;
  if (last === null) {
    return translate('detail.history.since', { version: first });
  }
  return last === first
    ? translate('detail.history.single', { version: first })
    : translate('detail.history.range', { first, last });
}

function kindOf(entry: StateHistoryEntry, translate: Translate): string | null {
  switch (entry.kind) {
    case 'new':
      return translate('detail.history.new');
    case 'returned':
      return translate('detail.history.returned');
    case 'changed':
      return null;
  }
}

/**
 * The rows of the block « Historique »: the entity's states up to the page's, newest first,
 * each with its run of patches, how it opened and the labels of the fields it changed.
 */
export function historyRowsOf(
  entries: readonly StateHistoryEntry[],
  page: EntityState | null,
  format: ValueFormat & { readonly names?: SubjectNames },
): HistoryRow[] {
  return entries.map((entry) => ({
    range: rangeOf(entry, format.translate),
    link: entry.link,
    kind: kindOf(entry, format.translate),
    fields: listOf(
      format.locale,
      fieldLabelsOf(entry.changedFields, format.translate, format.names),
    ),
    isPage: entry.openerVersion === page?.openerVersion,
  }));
}
