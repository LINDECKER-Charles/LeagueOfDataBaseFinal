import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { StoredObject } from '../../../../../core/api/generated/models/stored-object';
import { FigurePipe } from '../../../format/figure-pipe';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { foldRows } from '../../../widgets/fold-rows';
import { FoldToggle } from '../../../widgets/fold-toggle';

// The legacy table showed eight objects before its toggle.
const LIMIT = 8;

/**
 * The heaviest objects of the storage, the legacy two-column table: the key and its weight,
 * eight before the rest folds away. A long key is cut, its whole path in its title, so that
 * the weight stays in sight (the legacy table pushed it out of the card).
 */
@Component({
  selector: 'lodb-largest-objects',
  imports: [FigurePipe, FoldToggle, AdminTextPipe],
  template: `
    <div class="hx-table-scroll">
      <table class="hx-table hx-table--flush admin-tbl table-fixed">
        <thead>
          <tr>
            <th scope="col">{{ 'admin.storage.columns.key' | adminText }}</th>
            <th scope="col" class="num w-28">{{ 'admin.storage.columns.weight' | adminText }}</th>
          </tr>
        </thead>
        <tbody>
          @for (object of fold.shown(); track object.path) {
            <tr>
              <td>
                <code class="block truncate font-mono text-[0.78rem]" [title]="object.path">
                  {{ object.path }}
                </code>
              </td>
              <td class="num">{{ object.bytes | figure: 'bytes' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="2" class="t-empty">{{ 'admin.storage.largest_empty' | adminText }}</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    <lodb-fold-toggle [folded]="fold.folded()" [(open)]="fold.open" />
  `,
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LargestObjects {
  readonly objects = input.required<readonly StoredObject[]>();

  protected readonly fold = foldRows(
    () => this.objects(),
    () => LIMIT,
  );
}
