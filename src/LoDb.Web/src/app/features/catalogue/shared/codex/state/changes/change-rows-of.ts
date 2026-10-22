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
const SPACE = ' ';

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

// The values of `side` that `other` lacks.
function onlyIn(side: readonly ChangeValue[], other: readonly ChangeValue[]) {
  const others = new Set(other.map((value) => value.value));
  return side.filter((value) => !others.has(value.value));
}

// A set: what it lost, then what it gained, each as one list; the values after when neither
// (equal here).
function setPieces(change: FieldChange, format: ValueFormat): Pieces {
  const removed = joined(textsOf(onlyIn(change.before, change.after), change, format), format);
  const added = joined(textsOf(onlyIn(change.after, change.before), change, format), format);
  if (removed === '' && added === '') {
    return [{ kind: 'same', text: joined(textsOf(change.after, change, format), format) }];
  }
  return [
    ...(removed === '' ? [] : [{ kind: 'removed' as const, text: removed }]),
    ...(added === '' ? [] : [{ kind: 'added' as const, text: added }]),
  ];
}

// Pieces read apart, by eye, by a screen reader and in the page's text: a space between two of
// them, which a text's diff already carries in its own runs.
function spaced(pieces: Pieces): Pieces {
  return pieces.flatMap((piece, index) =>
    index === 0 ? [piece] : [{ kind: 'same' as const, text: SPACE }, piece],
  );
}

// A field the index marks changed that neither side holds in this language: its label alone.
function hasNoValue(change: FieldChange): boolean {
  return change.before.length === 0 && change.after.length === 0 && change.segments.length === 0;
}

function piecesOf(change: FieldChange, format: ValueFormat): Pieces {
  if (hasNoValue(change)) {
    return [];
  }
  switch (change.kind) {
    case 'text':
      return change.segments;
    case 'set':
      return spaced(setPieces(change, format));
    case 'value':
    case 'list':
      return spaced(valuePieces(change, format));
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
