import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { purgeAuditJournal } from '../../../../../core/api/generated/fn/admin-audit/purge-audit-journal';
import { Field } from '../../../../../ui/controls/field';
import { AdminCommand } from '../../../shared/http/admin-command';
import { ConfirmButton } from '../../../widgets/confirm-button';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';

/** What a purge deletes, as the API names it. */
const SCOPES = ['retention', 'before', 'all'] as const;
type PurgeScope = (typeof SCOPES)[number];
const BEFORE: PurgeScope = 'before';

function isScope(value: string): value is PurgeScope {
  return (SCOPES as readonly string[]).includes(value);
}

/**
 * The purge of the audit journal: what the daily retention would delete now, every entry
 * before a day, or everything. It asks twice, and the API journals the purge itself.
 */
@Component({
  selector: 'lodb-journal-purge',
  imports: [ConfirmButton, Field, AdminTextPipe],
  template: `
    <p class="mb-4 text-sm text-text-muted">{{ 'admin.journal.purge.lede' | adminText }}</p>
    <div class="flex flex-wrap items-end gap-3">
      <label class="grid gap-1">
        <span class="eyebrow">{{ 'admin.journal.purge.scope' | adminText }}</span>
        <select lodbField name="scope" (change)="pick($event)">
          @for (choice of scopes; track choice) {
            <option [value]="choice" [selected]="scope() === choice">
              {{ 'admin.journal.purge.scopes.' + choice | adminText }}
            </option>
          }
        </select>
      </label>
      @if (scope() === before) {
        <label class="grid gap-1">
          <span class="eyebrow">{{ 'admin.journal.purge.before' | adminText }}</span>
          <input
            lodbField
            name="before"
            type="date"
            required
            [value]="day()"
            (input)="setDay($event)"
          />
        </label>
      }
      <lodb-confirm-button
        tone="primary"
        [label]="'admin.journal.purge.submit' | adminText"
        [confirmLabel]="'admin.journal.purge.confirm' | adminText"
        [busy]="busy() || !ready()"
        (confirmed)="purge()"
      />
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JournalPurge {
  /** Emitted once entries were deleted, for the journal to read itself again. */
  readonly purged = output<void>();

  private readonly command = inject(AdminCommand);

  protected readonly scopes = SCOPES;
  protected readonly before = BEFORE;
  protected readonly scope = signal<PurgeScope>('retention');
  /** The first UTC day kept by a `before` purge, `yyyy-mm-dd`. */
  protected readonly day = signal('');
  protected readonly busy = signal(false);
  protected readonly ready = computed(() => this.scope() !== BEFORE || this.day() !== '');

  protected pick(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (isScope(value)) {
      this.scope.set(value);
    }
  }

  protected setDay(event: Event): void {
    this.day.set((event.target as HTMLInputElement).value);
  }

  protected async purge(): Promise<void> {
    const scope = this.scope();
    this.busy.set(true);
    try {
      const receipt = await this.command.run(
        purgeAuditJournal,
        { body: { scope, before: scope === BEFORE ? this.day() : null } },
        (done) => ({ key: 'journal.purged', params: { count: done.deleted } }),
      );
      if (receipt !== null) {
        this.purged.emit();
      }
    } finally {
      this.busy.set(false);
    }
  }
}
