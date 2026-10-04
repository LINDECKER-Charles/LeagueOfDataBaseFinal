import type { FacetDefinition } from '../../facets/model/facet-definition';
import type { FacetOption } from '../../facets/model/facet-option';
import type { ChoiceRow } from './choice-row';

/** The inputs of a choice's rows: what the cards carry, what is picked, the counts. */
interface ChoiceRowsInput {
  readonly facet: FacetDefinition;
  readonly present: ReadonlySet<string>;
  readonly selected: readonly string[];
  readonly counts: ReadonlyMap<string, number>;
}

/**
 * The values a choice facet offers: only those some card carries, in the schema's order,
 * then any token the schema does not know, sorted and shown as is. A value no card in the
 * current context carries stays visible, disabled, so the reader sees the whole axis.
 */
export function choiceRowsOf({ facet, present, selected, counts }: ChoiceRowsInput): ChoiceRow[] {
  const known = facet.options.filter((option) => present.has(option.value));
  const knownValues = new Set(known.map((option) => option.value));
  const extra: FacetOption[] = [...present]
    .filter((value) => !knownValues.has(value))
    .sort()
    .map((value) => ({ value, label: value }));
  return [...known, ...extra].map((option) => {
    const isOn = selected.includes(option.value);
    const count = counts.get(option.value) ?? 0;
    return { ...option, isOn, count, isDisabled: count === 0 && !isOn };
  });
}
