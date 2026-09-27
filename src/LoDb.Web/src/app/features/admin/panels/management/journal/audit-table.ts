import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AuditEntryView } from '../../../../../core/api/generated/models/audit-entry-view';
import { StampPipe } from '../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { Badge, type Tone } from '../../../widgets/badge';

/** Which view of the journal a table is: the whole journal, or the activity of an account. */
export type AuditView = 'journal' | 'activity';

// The columns of each view, in the order of its legacy page: the activity names its actor by
// type only, as the account it follows is the page's own.
const COLUMNS: Readonly<Record<AuditView, readonly string[]>> = {
  journal: ['when', 'actor', 'action', 'target', 'ip', 'outcome'],
  activity: ['when', 'action', 'target', 'ip', 'actor_type', 'outcome'],
};
// The outcomes of the journal, as the API names them.
const OUTCOME_TONES: Readonly<Record<string, Tone>> = {
  success: 'good',
  failure: 'bad',
  denied: 'warn',
};
// The actors that are accounts, whose activity has a page.
const ACCOUNT_ACTORS: readonly string[] = ['user', 'admin'];

function metaText(value: unknown): string {
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}

/**
 * Entries of the audit journal, newest first, in the legacy table: when, who (a link to their
 * own activity when an account acted), what (its category and details under it), on what,
 * from where, how it ended. Shared by the journal and the activity of an account.
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
  readonly view = input<AuditView>('journal');
  /** The key of what an empty table says. */
  readonly empty = input('admin.journal.empty');

  protected readonly texts = injectAdminText();
  protected readonly columns = computed(() => COLUMNS[this.view()]);

  protected outcomeTone(outcome: string): Tone {
    return OUTCOME_TONES[outcome] ?? 'muted';
  }

  protected isAccount(entry: AuditEntryView): boolean {
    return ACCOUNT_ACTORS.includes(entry.actorType) && entry.actorId != null;
  }

  protected targetOf(entry: AuditEntryView): string {
    return entry.target ?? (entry.targetId ? `#${entry.targetId}` : '—');
  }

  /** "Authentification · method=password": the category, then the details that are set. */
  protected detailOf(entry: AuditEntryView): string {
    const category = this.texts.term('journal.categories', entry.category);
    const bits = Object.entries(entry.meta ?? {})
      .filter(([, value]) => value !== null && value !== undefined)
      .map(([key, value]) => `${key}=${metaText(value)}`);
    return bits.length === 0 ? category : `${category} · ${bits.join(', ')}`;
  }
}
