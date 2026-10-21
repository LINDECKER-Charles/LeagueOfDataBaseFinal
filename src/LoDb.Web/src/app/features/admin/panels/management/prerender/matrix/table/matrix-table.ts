import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from '../../../../../../../ui/controls/button';
import { figure } from '../../../../../format/figure';
import { FigurePipe } from '../../../../../format/figure-pipe';
import { AdminTextPipe } from '../../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../../shared/inject-admin-text';
import { decimalBytes } from '../../format/decimal-bytes';
import type { MatrixRow, MatrixSeason } from '../grid/matrix-rows';
import type { MatrixVersion } from '../grid/unit-matrix';

// The figures a version may carry beyond the matrix's own: a pass's progress has its pages
// written, a coverage its pages stored.
interface VersionFigures extends MatrixVersion {
  readonly pagesWritten?: number;
  readonly bytesWritten?: number;
  readonly pages?: number;
  readonly bytes?: number;
}

const COLUMNS = ['version', 'state', 'locales', 'running', 'failed', 'pages', 'volume'] as const;

/**
 * The matrix as a real table, its accessible view (§ 4.4): a `<tbody>` per season headed by a
 * `<th scope="rowgroup">`, a row per version with its state, its locales complete, running
 * and failed, its pages and its volume, and in a pass a button to its detail.
 */
@Component({
  selector: 'lodb-matrix-table',
  imports: [AdminTextPipe, Button, FigurePipe],
  template: `
    <div class="overflow-x-auto">
      <table class="hx-table hx-table--flush admin-tbl">
        <thead>
          <tr>
            @for (column of columns; track column) {
              <th scope="col">
                {{ 'admin.prerender.common.matrix.columns.' + column | adminText }}
              </th>
            }
            @if (mode() === 'pass') {
              <th scope="col">
                <span class="sr-only">{{
                  'admin.prerender.common.matrix.detail' | adminText
                }}</span>
              </th>
            }
          </tr>
        </thead>
        @for (season of seasons(); track season.key) {
          <tbody>
            <tr>
              <th scope="rowgroup" [attr.colspan]="mode() === 'pass' ? 8 : 7">
                {{ seasonOf(season) }}
              </th>
            </tr>
            @for (row of season.rows; track row.version.version) {
              <tr>
                <th scope="row" class="font-mono">{{ row.version.version }}</th>
                <td>{{ 'admin.prerender.common.cells.' + row.status | adminText }}</td>
                <td class="num">{{ row.complete | figure }} / {{ row.total | figure }}</td>
                <td class="num">{{ row.running | figure }}</td>
                <td class="num">{{ row.failed | figure }}</td>
                <td class="num">{{ pagesOf(row) }}</td>
                <td class="num">{{ bytesOf(row) }}</td>
                @if (mode() === 'pass') {
                  <td>
                    <button
                      lodbButton="ghost"
                      lodbButtonSize="small"
                      type="button"
                      (click)="opened.emit(row.version.version)"
                    >
                      {{ 'admin.prerender.common.matrix.detail' | adminText }}
                    </button>
                  </td>
                }
              </tr>
            }
          </tbody>
        }
      </table>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MatrixTable {
  readonly seasons = input.required<readonly MatrixSeason[]>();
  readonly mode = input.required<'pass' | 'coverage'>();
  readonly opened = output<string>();

  private readonly texts = injectAdminText();

  protected readonly columns = COLUMNS;

  protected seasonOf(season: MatrixSeason): string {
    const label = season.isRanked
      ? this.texts.text('prerender.common.matrix.by_rank')
      : this.texts.text(
          `prerender.common.seasons.${season.year === null ? 'label_bare' : 'label'}`,
          { major: season.year === null ? season.key : season.major, year: season.year },
        );
    const { versions, complete, running, failed } = season.counts;
    const counts = { versions, complete, running, failed };
    return `${label} — ${this.texts.text('prerender.common.matrix.season_counts', counts)}`;
  }

  protected pagesOf(row: MatrixRow): string {
    const version: VersionFigures = row.version;
    const pages = version.pagesWritten ?? version.pages;
    return pages === undefined ? '—' : figure(pages);
  }

  protected bytesOf(row: MatrixRow): string {
    const version: VersionFigures = row.version;
    const bytes = version.bytesWritten ?? version.bytes;
    return bytes === undefined ? '—' : decimalBytes(bytes);
  }
}
