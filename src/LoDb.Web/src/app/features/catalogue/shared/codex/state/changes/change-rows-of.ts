import type { ChangeValue } from '../../../../../../core/api/generated/models/change-value';
import type { FieldChange } from '../../../../../../core/api/generated/models/field-change';
import { fieldLabelOf } from '../labels/field-label-of';
import { listOf } from '../labels/list-of';
import type { SubjectNames } from '../labels/subject-names';
import type { ChangeRow } from './change-row';
import type { ValueFormat } from './value-format';
import { valueTextOf } from './value-text-of';

type Pieces = ChangeRow['pieces'];

const ARROW = '→';

function textsOf(values: readonly ChangeValue[], change: FieldChange, format: ValueFormat) {
  return values.map((value) => valueTextOf(value, change, format));
}

function joined(texts: readonly string[], format: ValueFormat): string {
  return listOf(format.locale, texts);
}

// One value or a list, before then after: struck out, an arrow, inserted. A side the API
// leaves empty is shown alone: the value after when equal in this language or new, the value
// before, struck out, when the field is gone.
function valuePieces(change: FieldChange, format: ValueFormat): Pieces {
  const before = joined(textsOf(change.before, change, format), format);
  const after = joined(textsOf(change.after, change, format), format);
  if (before === '') {
    return [{ kind: 'same', text: after }];
  }
  if (after === '') {
    return [{ kind: 'removed', text: before }];
  }
  return [
    { kind: 'removed', text: before },
    { kind: 'arrow', text: ARROW },
    { kind: 'added', text: after },
  ];
}

// A set: what it lost, then what it gained; the values after when neither (equal here).
function setPieces(change: FieldChange, format: ValueFormat): Pieces {
  const before = new Set(change.before.map((value) => value.value));
  const after = new Set(change.after.map((value) => value.value));
  const removed = change.before.filter((value) => !after.has(value.value));
  const added = change.after.filter((value) => !before.has(value.value));
  if (removed.length === 0 && added.length === 0) {
    return [{ kind: 'same', text: joined(textsOf(change.after, change, format), format) }];
  }
  return [
    ...textsOf(removed, change, format).map((text) => ({ kind: 'removed' as const, text })),
    ...textsOf(added, change, format).map((text) => ({ kind: 'added' as const, text })),
  ];
}

function piecesOf(change: FieldChange, format: ValueFormat): Pieces {
  switch (change.kind) {
    case 'text':
      return change.segments;
    case 'set':
      return setPieces(change, format);
    case 'value':
    case 'list':
      return valuePieces(change, format);
  }
}

/** The rows of the block « Ce qui a changé », one per field the API lists, in its order. */
export function changeRowsOf(
  fields: readonly FieldChange[],
  format: ValueFormat,
  names?: SubjectNames,
): ChangeRow[] {
  return fields.map((change) => ({
    label: fieldLabelOf(change, format.translate, names),
    isText: change.kind === 'text',
    pieces: piecesOf(change, format),
  }));
}
