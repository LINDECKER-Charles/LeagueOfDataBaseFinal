import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { purgeAuditJournal } from '../../../../../core/api/generated/fn/admin-audit/purge-audit-journal';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { AdminCommand } from '../../../shared/http/admin-command';
import { ConfirmButton } from '../../../widgets/confirm-button';

/** What a purge deletes, as the API names it. */
type PurgeScope = 'retention' | 'before' | 'all';
const BEFORE: PurgeScope = 'before';

/**
 * The purge of the audit journal, the legacy "Purger les journaux" card: what the daily
 * retention would delete now, every entry before a day, or everything, picked by radio. It
 * asks twice, in red, and the API journals the purge itself.
 */
@Component({
  selector: 'lodb-journal-purge',
  imports: [ConfirmButton, AdminTextPipe],
  templateUrl: './journal-purge.html',
  styles: `
    .radio {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.8rem;
      color: var(--color-text-muted);
    }
    .radio input[type='radio'] {
      accent-color: var(--color-hex);
    }
    .submit {
      margin-inline-start: auto;
    }
    @media (width <= 860px) {
      .submit {
        margin-inline-start: 0;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JournalPurge {
  /** The months the retention keeps, as the API states them. */
  readonly months = input.required<number>();
  /** Emitted once entries were deleted, for the journal to read itself again. */
  readonly purged = output<void>();

  private readonly command = inject(AdminCommand);

  protected readonly scope = signal<PurgeScope>('retention');
  /** The first UTC day kept by a `before` purge, `yyyy-mm-dd`. */
  protected readonly day = signal('');
  protected readonly busy = signal(false);
  protected readonly ready = computed(() => this.scope() !== BEFORE || this.day() !== '');

  protected pick(scope: PurgeScope): void {
    this.scope.set(scope);
  }

  protected setDay(event: Event): void {
    this.day.set((event.target as HTMLInputElement).value);
    this.scope.set(BEFORE);
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
