import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { PrerenderPassView } from '../../../../../../core/api/generated/models/prerender-pass-view';
import { FigurePipe } from '../../../../format/figure-pipe';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../../widgets/badge';
import { foldRows } from '../../../../widgets/fold-rows';
import { FoldToggle } from '../../../../widgets/fold-toggle';
import { DecimalBytesPipe } from '../format/decimal-bytes-pipe';
import { prerenderTone } from '../prerender-tone';
import { scopeSummary } from './scope-summary';

// The latest passes show; the API sends 50 at most, folded under the toggle.
const SHOWN_PASSES = 10;

/**
 * The latest passes of the pre-rendering, newest first, the active one included: the epoch
 * each wrote, how it ended, over what, the pages it wrote of those it listed, its volume, who
 * launched it (a promotion or the CLI has no author) and when it started and ended.
 */
@Component({
  selector: 'lodb-pass-history-table',
  imports: [AdminTextPipe, Badge, DecimalBytesPipe, FigurePipe, FoldToggle, StampPipe],
  template: `
    <div class="hx-table-scroll">
      <table class="hx-table hx-table--flush admin-tbl">
        <thead>
          <tr>
            <th scope="col">{{ 'admin.prerender.history.columns.pass' | adminText }}</th>
            <th scope="col" class="num">
              {{ 'admin.prerender.history.columns.epoch' | adminText }}
            </th>
            <th scope="col">{{ 'admin.prerender.history.columns.state' | adminText }}</th>
            <th scope="col">{{ 'admin.prerender.history.columns.scope' | adminText }}</th>
            <th scope="col" class="num">
              {{ 'admin.prerender.history.columns.pages' | adminText }}
            </th>
            <th scope="col" class="num">
              {{ 'admin.prerender.history.columns.volume' | adminText }}
            </th>
            <th scope="col">{{ 'admin.prerender.history.columns.author' | adminText }}</th>
            <th scope="col">{{ 'admin.prerender.history.columns.started' | adminText }}</th>
            <th scope="col">{{ 'admin.prerender.history.columns.finished' | adminText }}</th>
          </tr>
        </thead>
        <tbody>
          @for (pass of fold.shown(); track pass.id) {
            <tr [attr.data-pass]="pass.id">
              <td>#{{ pass.id }}</td>
              <td class="num">{{ pass.epoch }}</td>
              <td>
                <span [lodbBadge]="tone(pass)">
                  {{ 'admin.prerender.states.' + pass.state | adminText }}
                </span>
              </td>
              <td class="min-w-[14rem]">{{ scope(pass) }}</td>
              <td class="num whitespace-nowrap">
                {{
                  'admin.prerender.history.pages'
                    | adminText
                      : {
                          written: (pass.pagesWritten | figure),
                          total: (pass.pagesTotal | figure),
                        }
                }}
              </td>
              <td class="num whitespace-nowrap">{{ pass.bytesWritten | decimalBytes }}</td>
              <td>
                {{ pass.authorName ?? ('admin.prerender.history.automatic' | adminText) }}
              </td>
              <td class="whitespace-nowrap">{{ pass.createdAt | stamp: 'minute' }}</td>
              <td class="whitespace-nowrap">{{ pass.finishedAt | stamp: 'minute' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="9" class="t-empty">
                {{ 'admin.prerender.history.empty' | adminText }}
              </td>
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
export class PassHistoryTable {
  readonly passes = input.required<readonly PrerenderPassView[]>();

  private readonly texts = injectAdminText();

  protected readonly fold = foldRows(this.passes, () => SHOWN_PASSES);

  protected tone(pass: PrerenderPassView): Tone {
    return prerenderTone(pass.state);
  }

  protected scope(pass: PrerenderPassView): string {
    return scopeSummary(pass.scope, this.texts);
  }
}
