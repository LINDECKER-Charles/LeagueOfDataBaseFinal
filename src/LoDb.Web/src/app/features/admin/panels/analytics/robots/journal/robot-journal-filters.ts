import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../../../../../ui/controls/button';
import { Chip } from '../../../../../../ui/controls/chip';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { formText } from '../../../../shared/form-text';
import { injectQuery } from '../../../../shared/inject-query';
import {
  ROBOT_CATEGORIES,
  ROBOT_SECTIONS,
  ROBOT_VERDICTS,
  STATUS_CLASSES,
} from '../shared/robot-palette';

/** The filters of the journal, each a parameter of the URL of the same name. */
export const JOURNAL_FILTERS = ['robot', 'category', 'status', 'verdict', 'section', 'q'] as const;

/** The query that clears every filter, from the first page. */
const CLEARED: Readonly<Record<string, null>> = Object.fromEntries(
  [...JOURNAL_FILTERS, 'page'].map((name) => [name, null]),
);

/** A filter picked from a closed set: its parameter and its choices, by their term group. */
interface ClosedFilter {
  readonly name: string;
  readonly choices: readonly (string | number)[];
  readonly terms: string;
}

const SELECTS: readonly ClosedFilter[] = [
  { name: 'category', choices: ROBOT_CATEGORIES, terms: 'categories' },
  { name: 'status', choices: STATUS_CLASSES, terms: 'status_classes' },
  { name: 'verdict', choices: ROBOT_VERDICTS, terms: 'verdicts' },
  { name: 'section', choices: ROBOT_SECTIONS, terms: 'sections' },
];

/**
 * The toolbar of the journal of the robots: a robot by its key, a category, a class of
 * statuses, a verdict, a section and a text of the path. Filtering writes the URL, which the
 * journal reads; the reset chip shows once a filter is set.
 */
@Component({
  selector: 'lodb-robot-journal-filters',
  imports: [Button, Chip, RouterLink, AdminTextPipe],
  template: `
    <form class="toolbar" role="search" (submit)="apply($event)">
      <input
        name="robot"
        type="search"
        maxlength="80"
        class="admin-input"
        [value]="query.text('robot')"
        [placeholder]="'admin.robots.journal.robot' | adminText"
        [attr.aria-label]="'admin.robots.journal.robot' | adminText"
      />
      @for (select of selects; track select.name) {
        <select
          class="hx-select admin-input"
          [name]="select.name"
          [attr.aria-label]="'admin.robots.journal.' + select.name | adminText"
        >
          <option value="">{{ 'admin.robots.journal.any_' + select.name | adminText }}</option>
          @for (choice of select.choices; track choice) {
            <option [value]="choice" [selected]="query.text(select.name) === '' + choice">
              {{ 'admin.robots.' + select.terms + '.' + choice | adminText }}
            </option>
          }
        </select>
      }
      <input
        name="q"
        type="search"
        maxlength="200"
        class="admin-input admin-input--wide"
        [value]="query.text('q')"
        [placeholder]="'admin.robots.journal.q' | adminText"
        [attr.aria-label]="'admin.robots.journal.q' | adminText"
      />
      <button lodbButton="ghost" lodbButtonSize="small" type="submit">
        {{ 'admin.actions.filter' | adminText }}
      </button>
      @if (active()) {
        <a lodbChip [routerLink]="[]" [queryParams]="cleared">
          {{ 'admin.actions.reset' | adminText }}
        </a>
      }
    </form>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotJournalFilters {
  protected readonly query = injectQuery();
  protected readonly selects = SELECTS;
  protected readonly cleared = CLEARED;
  protected readonly active = computed(() => {
    const params = this.query.params();
    return JOURNAL_FILTERS.some((name) => params.has(name));
  });

  protected apply(event: Event): void {
    this.query.set(
      Object.fromEntries(
        JOURNAL_FILTERS.map((name) => [name, formText(event, name).trim() || null]),
      ),
    );
  }
}
