import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { FacetDefinition } from '../../facets/model/facet-definition';
import { isRangeSelection } from '../../facets/rules/is-range-selection';
import { rangeSelectionOf } from '../../facets/transitions/range-selection-of';
import { CatalogueFilter } from '../../state/catalogue-filter';

const PERCENT = 100;
// Both thumbs at the far end: the lower one must stay reachable, so it goes on top.
const LOW_ON_TOP_FROM = 99;

/**
 * One numeric facet as a dual-thumb slider and two fields, bounded by what the list
 * carries. A drag only moves the thumbs; the selection is committed on `change` (release,
 * blur), so a drag never floods the URL. The whole span is no selection at all.
 */
@Component({
  selector: 'lodb-facet-range',
  imports: [TranslocoPipe],
  templateUrl: './facet-range.html',
  styleUrls: ['./facet.css', './facet-range.css'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FacetRange {
  readonly facet = input.required<FacetDefinition>();
  protected readonly filter = inject(CatalogueFilter);

  protected readonly bounds = computed(() => this.filter.universe().bounds[this.facet().key]);
  private readonly selection = computed(() => {
    const selection = this.filter.state().facets[this.facet().key];
    return selection !== undefined && isRangeSelection(selection) ? selection : null;
  });
  protected readonly isActive = computed(() => this.selection() !== null);
  protected readonly low = linkedSignal(() => this.selection()?.min ?? this.bounds()?.min ?? 0);
  protected readonly high = linkedSignal(() => this.selection()?.max ?? this.bounds()?.max ?? 0);
  protected readonly lowPercent = computed(() => this.percentOf(this.low()));
  protected readonly highPercent = computed(() => this.percentOf(this.high()));
  protected readonly lowOnTop = computed(
    () => this.highPercent() >= PERCENT && this.lowPercent() >= LOW_ON_TOP_FROM,
  );
  private readonly decimals = computed(() =>
    Math.max(0, Math.ceil(-Math.log10(this.facet().step))),
  );

  protected moveLow(value: number): void {
    this.low.set(Math.min(this.clamp(value), this.high()));
  }

  protected moveHigh(value: number): void {
    this.high.set(Math.max(this.clamp(value), this.low()));
  }

  protected commit(): void {
    const bounds = this.bounds();
    if (bounds !== undefined) {
      this.filter.setRange(this.facet().key, rangeSelectionOf(this.low(), this.high(), bounds));
    }
  }

  protected format(value: number): string {
    const unit = this.facet().unit;
    const text = value.toFixed(this.decimals());
    return unit === null ? text : `${text} ${unit}`;
  }

  private clamp(value: number): number {
    const bounds = this.bounds();
    if (bounds === undefined || !Number.isFinite(value)) {
      return bounds?.min ?? 0;
    }
    return Math.min(bounds.max, Math.max(bounds.min, value));
  }

  private percentOf(value: number): number {
    const bounds = this.bounds();
    if (bounds === undefined) {
      return 0;
    }
    const span = Math.max(bounds.max - bounds.min, this.facet().step);
    return ((value - bounds.min) / span) * PERCENT;
  }
}
