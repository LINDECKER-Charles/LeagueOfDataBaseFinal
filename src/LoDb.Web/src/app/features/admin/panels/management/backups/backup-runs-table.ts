import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { BackupRunRow } from '../../../../../core/api/generated/models/backup-run-row';
import { duration } from '../../../format/duration';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { Badge } from '../../../widgets/badge';
import { foldRows } from '../../../widgets/fold-rows';
import { FoldToggle } from '../../../widgets/fold-toggle';

// About the last four nights: a month of steps stays folded under the toggle.
const SHOWN_STEPS = 15;
// The reason of a failure (its column at 18rem at least) wraps on a few lines, never a word
// per line: on a phone the table scrolls sideways (hx-table-scroll) rather than squeeze it.
const MS_PER_SECOND = 1_000;

/**
 * Every step the backup container reported over the window, newest first: when it started,
 * what it was and for which database, how it ended, how long it took, what it read or
 * measured, and why it failed. The latest steps show; the rest fold under a toggle.
 */
@Component({
  selector: 'lodb-backup-runs-table',
  imports: [Badge, FigurePipe, FoldToggle, StampPipe, AdminTextPipe],
  template: `
    <div class="hx-table-scroll">
      <table class="hx-table hx-table--flush admin-tbl">
        <thead>
          <tr>
            <th scope="col">{{ 'admin.backups.columns.started' | adminText }}</th>
            <th scope="col">{{ 'admin.backups.columns.step' | adminText }}</th>
            <th scope="col">{{ 'admin.backups.columns.database' | adminText }}</th>
            <th scope="col">{{ 'admin.backups.columns.result' | adminText }}</th>
            <th scope="col" class="num">{{ 'admin.backups.columns.duration' | adminText }}</th>
            <th scope="col" class="num">{{ 'admin.backups.columns.volume' | adminText }}</th>
            <th scope="col">{{ 'admin.backups.columns.detail' | adminText }}</th>
          </tr>
        </thead>
        <tbody>
          @for (run of fold.shown(); track run.id) {
            <tr [attr.data-run]="run.id">
              <td class="whitespace-nowrap">{{ run.startedAt | stamp: 'minute' }}</td>
              <td>{{ 'admin.backups.steps.' + run.step | adminText }}</td>
              <td>{{ run.database ?? ('admin.backups.repository' | adminText) }}</td>
              <td>
                <span [lodbBadge]="run.succeeded ? 'good' : 'bad'">
                  {{ 'admin.backups.results.' + (run.succeeded ? 'ok' : 'failed') | adminText }}
                </span>
              </td>
              <td class="num whitespace-nowrap">{{ length(run) }}</td>
              <td class="num whitespace-nowrap">
                @if (run.bytesRead !== null && run.bytesRead !== undefined) {
                  {{
                    'admin.backups.volume.backup'
                      | adminText
                        : {
                            read: (run.bytesRead | figure: 'bytes'),
                            added: (run.bytesAdded ?? 0 | figure: 'bytes'),
                          }
                  }}
                } @else if (run.repositoryBytes !== null && run.repositoryBytes !== undefined) {
                  {{
                    'admin.backups.volume.repository'
                      | adminText: { size: (run.repositoryBytes | figure: 'bytes') }
                  }}
                } @else {
                  —
                }
              </td>
              <td class="min-w-[18rem] text-text-dim">{{ run.detail ?? '' }}</td>
            </tr>
          } @empty {
            <tr>
              <td colspan="7" class="t-empty">{{ 'admin.backups.runs_empty' | adminText }}</td>
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
export class BackupRunsTable {
  readonly runs = input.required<readonly BackupRunRow[]>();

  protected readonly fold = foldRows(this.runs, () => SHOWN_STEPS);

  protected length(run: BackupRunRow): string {
    return duration((Date.parse(run.finishedAt) - Date.parse(run.startedAt)) / MS_PER_SECOND);
  }
}
