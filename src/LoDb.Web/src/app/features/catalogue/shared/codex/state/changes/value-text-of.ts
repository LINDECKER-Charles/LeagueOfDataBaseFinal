import type { ChangeValue } from '../../../../../../core/api/generated/models/change-value';
import type { FieldChange } from '../../../../../../core/api/generated/models/field-change';
import { codeLabelOf } from './code-label-of';
import type { ValueFormat } from './value-format';

/** The token of a summoner spell's range that reaches the whole map. */
const GLOBAL_RANGE = 'global';
const MAX_FRACTION_DIGITS = 3;
const MAX_PERCENT_DIGITS = 2;

// An invariant number as the locale writes it; a value that is not one stays as it came.
function numberText(value: string, format: ValueFormat, options: Intl.NumberFormatOptions): string {
  const number = Number(value);
  return value.trim() === '' || !Number.isFinite(number)
    ? value
    : new Intl.NumberFormat(format.locale, options).format(number);
}

function flagText(value: string, format: ValueFormat): string {
  return format.translate(value === 'true' ? 'detail.changes.yes' : 'detail.changes.no');
}

// A summoner spell's range: a distance, or the whole map.
function rangeText(value: string, format: ValueFormat): string {
  return value === GLOBAL_RANGE
    ? format.translate('summoner.detail.range_global')
    : numberText(value, format, { maximumFractionDigits: MAX_FRACTION_DIGITS });
}

/**
 * A value of a changed field as the page shows it: numbers and percentages in the locale's
 * writing (they travel invariant), a flag as yes or no, a code through the front's tables,
 * an entity by its name, a range of the whole map by its word; a text as it is.
 */
export function valueTextOf(
  value: ChangeValue,
  change: Pick<FieldChange, 'field' | 'valueKind'>,
  format: ValueFormat,
): string {
  switch (change.valueKind) {
    case 'number':
      return numberText(value.value, format, { maximumFractionDigits: MAX_FRACTION_DIGITS });
    case 'percent':
      return numberText(value.value, format, {
        style: 'percent',
        maximumFractionDigits: MAX_PERCENT_DIGITS,
      });
    case 'flag':
      return flagText(value.value, format);
    case 'code':
      return codeLabelOf(change.field, value.value, format.translate);
    case 'entity':
      return value.label ?? value.value;
    case 'range':
      return rangeText(value.value, format);
    case 'text':
      return value.value;
  }
}
