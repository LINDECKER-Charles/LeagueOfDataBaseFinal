import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import type { AuditVocabularyView } from '../../../../../core/api/generated/models/audit-vocabulary-view';
import { Button } from '../../../../../ui/controls/button';
import { Field } from '../../../../../ui/controls/field';
import { formText } from '../../../shared/form-text';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';

/** The closed sets a filter picks from, as the vocabulary names them. */
type ClosedSet = 'categories' | 'outcomes' | 'actorTypes';

/** The filters of the journal, each a parameter of the URL of the same name. */
const FILTERS = ['category', 'action', 'outcome', 'actorType', 'actor', 'from', 'to'] as const;
/** A filter picked from a closed set: its parameter, its set, the group of its terms. */
interface ClosedFilter {
  readonly name: string;
  readonly label: string;
  readonly set: ClosedSet;
  readonly terms: string;
}

const SELECTS: readonly ClosedFilter[] = [
  { name: 'category', label: 'category', set: 'categories', terms: 'journal.categories' },
  { name: 'outcome', label: 'outcome', set: 'outcomes', terms: 'journal.outcomes' },
  { name: 'actorType', label: 'actor_type', set: 'actorTypes', terms: 'journal.actor_types' },
];

/**
 * The filters of the audit journal. Their choices come from the vocabulary the API serves,
 * never from a copy of its closed sets; the actions are grouped by category. Filtering
 * writes the URL, which the journal reads.
 */
@Component({
  selector: 'lodb-journal-filters',
  imports: [Button, Field, AdminTextPipe],
  templateUrl: './journal-filters.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JournalFilters {
  /** The vocabulary of the journal; the lists stay empty until it came. */
  readonly vocabulary = input<AuditVocabularyView | undefined>(undefined);

  protected readonly texts = injectAdminText();
  protected readonly query = injectQuery();
  protected readonly selects = SELECTS;
  protected readonly groups = computed(() => {
    const actions = this.vocabulary()?.actions ?? [];
    return (this.vocabulary()?.categories ?? []).map((category) => ({
      category,
      actions: actions
        .filter((action) => action.category === category)
        .map((action) => action.name),
    }));
  });

  protected choices(set: ClosedSet): readonly string[] {
    return this.vocabulary()?.[set] ?? [];
  }

  protected apply(event: Event): void {
    const changes = Object.fromEntries(
      FILTERS.map((name) => [name, formText(event, name).trim() || null]),
    );
    this.query.set(changes);
  }

  protected clear(): void {
    this.query.set(Object.fromEntries(FILTERS.map((name) => [name, null])));
  }
}
