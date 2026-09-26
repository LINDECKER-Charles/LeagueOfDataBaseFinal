import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { StepView } from '../../../../core/api/generated/models/step-view';
import { BuildItem } from '../../shared/items/build-item';

/**
 * The purchase order of a shared build, the signature of its page: its steps as diamonds on
 * a gold spine, each with its label, its cost, its note and its items in order, duplicates
 * and ghosts kept; then the cost of the whole order, ghosts left out.
 */
@Component({
  selector: 'lodb-share-order',
  imports: [BuildItem, TranslocoPipe],
  templateUrl: './share-order.html',
  styleUrl: './share-order.css',
  host: { class: 'block mt-12' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShareOrder {
  readonly steps = input.required<readonly StepView[]>();
  readonly totalGold = input.required<number>();
}
