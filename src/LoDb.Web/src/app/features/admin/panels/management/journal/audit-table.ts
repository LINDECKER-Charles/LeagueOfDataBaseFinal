import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AuditEntryView } from '../../../../../core/api/generated/models/audit-entry-view';
import { StampPipe } from '../../../format/stamp-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../widgets/badge';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';

// The outcomes of the journal, as the API names them.
const OUTCOME_TONES: Readonly<Record<string, Tone>> = {
  success: 'good',
  failure: 'bad',
  denied: 'warn',
};
// The actors that are accounts, whose activity has a page.
const ACCOUNT_ACTORS: readonly string[] = ['user', 'admin'];

/**
 * Entries of the audit journal, newest first: when, what, how it ended, who (a link to their
 * own activity when an account acted), on what, from where. Shared by the journal and the
 * activity of an account.
 */
@Component({
  selector: 'lodb-audit-table',
  imports: [Badge, RouterLink, StampPipe, AdminTextPipe],
  templateUrl: './audit-table.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditTable {
  readonly entries = input.required<readonly AuditEntryView[]>();

  protected readonly texts = injectAdminText();

  protected outcomeTone(outcome: string): Tone {
    return OUTCOME_TONES[outcome] ?? 'muted';
  }

  protected isAccount(entry: AuditEntryView): boolean {
    return ACCOUNT_ACTORS.includes(entry.actorType) && entry.actorId != null;
  }

  protected metaOf(entry: AuditEntryView): string {
    const meta = entry.meta ?? {};
    return Object.keys(meta).length === 0 ? '' : JSON.stringify(meta);
  }
}
