import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { ScalewayBillLine } from '../../../../../core/api/generated/models/scaleway-bill-line';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { billAmount } from './bill-amount';
import { billQuantity } from './bill-quantity';

// A free tier's deduction is billed in this unit, always -1 of it: its amount says it all.
const DEDUCTION_UNIT = 'currency';

/**
 * The lines Scaleway billed a project for over the current month, dearest first, under a fold: the
 * product, the resource, the quantity in its unit and the amount, a free tier's deduction
 * negative and green, without its meaningless quantity.
 */
@Component({
  selector: 'lodb-scaleway-bill-lines',
  imports: [AdminTextPipe],
  template: `
    <details class="mt-4" data-bill-lines>
      <summary class="cursor-pointer text-[0.85rem] text-text-muted hover:text-hex">
        {{ 'admin.scaleway_bill.lines' | adminText: { count: lines().length } }}
      </summary>
      <div class="hx-table-scroll mt-3">
        <table class="hx-table hx-table--flush admin-tbl">
          <thead>
            <tr>
              <th scope="col">{{ 'admin.scaleway_bill.columns.product' | adminText }}</th>
              <th scope="col">{{ 'admin.scaleway_bill.columns.resource' | adminText }}</th>
              <th scope="col" class="num">
                {{ 'admin.scaleway_bill.columns.quantity' | adminText }}
              </th>
              <th scope="col" class="num">
                {{ 'admin.scaleway_bill.columns.amount' | adminText }}
              </th>
            </tr>
          </thead>
          <tbody>
            @for (line of lines(); track $index) {
              <tr data-bill-line [attr.data-deduction]="line.amount < 0 ? '' : null">
                <td>{{ line.product }}</td>
                <td class="text-text-dim">{{ line.resource }}</td>
                <td class="num whitespace-nowrap">{{ quantity(line) }}</td>
                <td class="num whitespace-nowrap" [class.text-good]="line.amount < 0">
                  {{ amount(line.amount) }}
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="t-empty">
                  {{ 'admin.scaleway_bill.lines_empty' | adminText }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </details>
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScalewayBillLines {
  readonly lines = input.required<readonly ScalewayBillLine[]>();
  readonly currency = input.required<string>();

  private readonly texts = injectAdminText();

  protected amount(value: number): string {
    return billAmount(value, this.currency());
  }

  protected quantity(line: ScalewayBillLine): string {
    if (line.unit === DEDUCTION_UNIT) {
      return '';
    }
    const unit = this.texts.term('scaleway_bill.units', line.unit);
    return `${billQuantity(line.quantity)} ${unit}`;
  }
}
