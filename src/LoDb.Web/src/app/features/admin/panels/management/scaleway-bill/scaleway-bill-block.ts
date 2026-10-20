import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Button } from '../../../../../ui/controls/button';
import { Skeleton } from '../../../../../ui/surfaces/skeleton';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminCard } from '../../../layout/admin-card';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { billAmount } from './bill-amount';
import { billMonth } from './bill-month';
import type { BillProject } from './bill-view';
import { injectScalewayBill } from './inject-scaleway-bill';
import { ScalewayBillLines } from './scaleway-bill-lines';

/**
 * What Scaleway really bills for the Object Storage of a project, next to the estimate of
 * its panel: the current month so far, the previous one, every month since the first one
 * billed, and the lines of the current month under a fold. It loads on its own, after the
 * panel, a single line meanwhile, and says when the billing key is not set, when Scaleway
 * has not answered yet, when the amounts shown are those of an earlier reading (both with a
 * way to ask again), and when nothing was consumed yet.
 */
@Component({
  selector: 'lodb-scaleway-bill',
  imports: [AdminCard, AdminTextPipe, Button, ScalewayBillLines, Skeleton, StampPipe],
  templateUrl: './scaleway-bill-block.html',
  host: { class: 'block', '[attr.data-bill]': 'project()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScalewayBillBlock {
  /** The project billed: the backups' bucket, or the page store's. */
  readonly project = input.required<BillProject>();

  protected readonly bill = injectScalewayBill(this.project);
  /** The current month, then the previous one if it was billed. */
  protected readonly current = computed(() => this.bill.view()?.months[0] ?? null);
  protected readonly previous = computed(() => this.bill.view()?.months[1] ?? null);

  protected amount(value: number, currency: string): string {
    return billAmount(value, currency);
  }

  protected month(period: string): string {
    return billMonth(period);
  }
}
