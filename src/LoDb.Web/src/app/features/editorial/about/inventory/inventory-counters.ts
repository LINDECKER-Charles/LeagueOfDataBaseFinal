import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { switchMap } from 'rxjs';
import { PageDirection } from '../../../../core/layout/direction/page-direction';
import { Inventory } from './inventory';
import type { InventoryFact } from './inventory-fact';
import { InventoryFacts } from './inventory-facts';

/**
 * The inventory facts with their numbers, loaded in the browser. Only ever rendered inside a
 * `@defer` block, whose placeholder is {@link InventoryFacts} without a snapshot: the
 * prerendered page shows `—` tiles of the same size, which the numbers then fill.
 */
@Component({
  selector: 'lodb-inventory-counters',
  imports: [InventoryFacts],
  template: `<lodb-inventory-facts [facts]="facts()" [snapshot]="snapshot()" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InventoryCounters {
  readonly facts = input.required<readonly InventoryFact[]>();

  private readonly inventory = inject(Inventory);
  protected readonly snapshot = toSignal(
    toObservable(inject(PageDirection).locale).pipe(
      switchMap((locale) => this.inventory.snapshot(locale)),
    ),
    { initialValue: null },
  );
}
