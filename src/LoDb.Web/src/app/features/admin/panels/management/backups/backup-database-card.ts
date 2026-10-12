import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { BackupDatabaseState } from '../../../../../core/api/generated/models/backup-database-state';
import { Chip } from '../../../../../ui/controls/chip';
import { Frame } from '../../../../../ui/surfaces/frame';
import { FigurePipe } from '../../../format/figure-pipe';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { Badge } from '../../../widgets/badge';
import { backupTone } from './backup-tone';

/**
 * A database the backups save, as a probe card: its name and the state of its backups, then
 * chips of its last success, the size of its dump, its last failure, and its restore tests.
 */
@Component({
  selector: 'lodb-backup-database-card',
  imports: [Badge, Chip, FigurePipe, StampPipe, AdminTextPipe],
  hostDirectives: [Frame],
  template: `
    @let database = state();
    <div class="flex flex-wrap items-center justify-between gap-[0.6rem]">
      <h2 class="font-beaufort text-[0.95rem] font-bold tracking-[0.06em] text-gold-bright">
        {{ name() }}
      </h2>
      <span [lodbBadge]="tone()">
        {{ 'admin.backups.statuses.' + database.status | adminText }}
      </span>
    </div>
    <div class="mt-[0.7rem] flex flex-wrap items-center gap-[0.45rem]">
      <span lodbChip>
        {{
          'admin.backups.card.last_success'
            | adminText: { date: (database.lastSuccessAt | stamp: 'minute') }
        }}
      </span>
      @if (database.lastBytesRead) {
        <span lodbChip>
          {{
            'admin.backups.card.dump'
              | adminText: { size: (database.lastBytesRead | figure: 'bytes') }
          }}
        </span>
      }
      @if (database.lastFailureAt) {
        <span lodbChip>
          {{
            'admin.backups.card.last_failure'
              | adminText: { date: (database.lastFailureAt | stamp: 'minute') }
          }}
        </span>
      }
    </div>
    <p class="mt-[0.8rem] flex flex-wrap items-center gap-[0.5rem] text-[0.8rem] text-text-dim">
      @if (database.restoreStatus; as restore) {
        <span [lodbBadge]="restoreTone()">
          {{ 'admin.backups.statuses.' + restore | adminText }}
        </span>
        {{ 'admin.backups.card.restore' | adminText: { date: (database.lastRestoreAt | stamp) } }}
      } @else {
        {{ 'admin.backups.card.restore_never' | adminText }}
      }
    </p>
  `,
  host: { class: 'block px-[1.35rem] py-5' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BackupDatabaseCard {
  readonly state = input.required<BackupDatabaseState>();

  private readonly texts = injectAdminText();

  protected readonly name = computed(() => this.texts.term('backups.databases', this.state().name));
  protected readonly tone = computed(() => backupTone(this.state().status));
  protected readonly restoreTone = computed(() => backupTone(this.state().restoreStatus ?? ''));
}
