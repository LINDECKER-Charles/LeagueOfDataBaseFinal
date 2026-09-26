import type { FacetKind } from './facet-kind';
import type { FacetOption } from './facet-option';

/**
 * One filter of a list page: how it is presented and combined. A card's values never live
 * here, they come from the page's card adapter; the definition only says how to read them.
 */
export interface FacetDefinition {
  /** Stable and locale-independent: it is the URL parameter too. */
  readonly key: string;
  readonly kind: FacetKind;
  /** Translated label. */
  readonly label: string;
  /** Translated heading of the group the facet sits in; facets group by it. */
  readonly group: string;
  /** Known values in display order; only those some card carries are offered. */
  readonly options: readonly FacetOption[];
  /** A main axis of the list: its group starts unfolded. */
  readonly primary: boolean;
  /** Choice: several values (OR) rather than exactly one. */
  readonly multiple: boolean;
  /** Choice: offers the any/all switch. */
  readonly matchAll: boolean;
  /** Range: display suffix of the bounds. */
  readonly unit: string | null;
  /** Range: granularity of the slider. */
  readonly step: number;
}
