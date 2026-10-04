import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { AuditVocabularyView } from '../../../../../core/api/generated/models/audit-vocabulary-view';
import { Button } from '../../../../../ui/controls/button';
import { Chip } from '../../../../../ui/controls/chip';
import { AdminTextPipe } from '../../../shared/admin-text-pipe';
import { formText } from '../../../shared/form-text';
import { injectAdminText } from '../../../shared/inject-admin-text';
import { injectQuery } from '../../../shared/inject-query';

/** The closed sets a filter picks from, as the vocabulary names them. */
type ClosedSet = 'categories' | 'outcomes' | 'actorTypes';

/** The filters of the journal, each a parameter of the URL of the same name. */
const FILTERS = ['category', 'action', 'outcome', 'actorType', 'actor', 'from', 'to'] as const;
/** The query that clears every filter, from the first page. */
const CLEARED: Readonly<Record<string, null>> = Object.fromEntries(
  [...FILTERS, 'page'].map((name) => [name, null]),
);
/** A filter picked from a closed set: its parameter, its set, the group of its terms. */
interface ClosedFilter {
  readonly name: string;
  readonly label: string;
  readonly set: ClosedSet;
  readonly terms: string;
}

const SELECTS: readonly ClosedFilter[] = [
  { name: 'outcome', label: 'outcome', set: 'outcomes', terms: 'journal.outcomes' },
  { name: 'actorType', label: 'actor_type', set: 'actorTypes', terms: 'journal.actor_types' },
];

/**
 * The filters of the audit journal, the legacy slim toolbar: the category and the days first,
 * then, on the journal, the outcome, the type of actor, the action and the actor. Their
 * choices come from the vocabulary the API serves, never from a copy of its closed sets; the
 * actions are grouped by category. Filtering writes the URL, which the journal reads; the
 * reset chip shows once a filter is set. Content projected in comes first ("← Utilisateurs").
 */
@Component({
  selector: 'lodb-journal-filters',
  imports: [Button, Chip, RouterLink, AdminTextPipe],
  templateUrl: './journal-filters.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JournalFilters {
  /** The vocabulary of the journal; the lists stay empty until it came. */
  readonly vocabulary = input<AuditVocabularyView | undefined>(undefined);
  /** Whether the filters the legacy journal lacked show too; the activity keeps its three. */
  readonly full = input(true);

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
  protected readonly active = computed(() => {
    const params = this.query.params();
    return FILTERS.some((name) => params.has(name));
  });

  protected readonly cleared = CLEARED;

  protected choices(set: ClosedSet): readonly string[] {
    return this.vocabulary()?.[set] ?? [];
  }

  protected apply(event: Event): void {
    const changes = Object.fromEntries(
      FILTERS.map((name) => [name, formText(event, name).trim() || null]),
    );
    this.query.set(changes);
  }
}
