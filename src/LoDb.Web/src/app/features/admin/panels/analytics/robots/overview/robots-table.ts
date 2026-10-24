import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { RobotSummary } from '../../../../../../core/api/generated/models/robot-summary';
import { FigurePipe } from '../../../../format/figure-pipe';
import { StampPipe } from '../../../../format/stamp-pipe';
import { AdminTextPipe } from '../../../../shared/admin-text-pipe';
import { injectAdminText } from '../../../../shared/inject-admin-text';
import { Badge } from '../../../../widgets/badge';
import { robotLabel, robotOperator } from '../shared/robot-label';
import { categoryColor } from '../shared/robot-palette';
import { StatusBar } from '../shared/status-bar';
import { type RobotSort, errors, robotRows, verifiedShare } from './robot-rows';

const SORTS: readonly RobotSort[] = ['hits', 'name', 'verified', 'errors', 'mean', 'last'];

/**
 * Every robot of the period in one table: who it is, what share of the hits it made, how
 * many of them its network proved, how it was answered, how fast, when last, what it asked
 * most and from where. Filtered by category or a text, sorted by any figure; each robot
 * opens its own page, for the same period.
 */
@Component({
  selector: 'lodb-robots-table',
  imports: [Badge, FigurePipe, RouterLink, StampPipe, StatusBar, AdminTextPipe],
  templateUrl: './robots-table.html',
  host: { class: 'block' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RobotsTable {
  readonly robots = input.required<readonly RobotSummary[]>();
  /** The period of the page, kept by the links to a robot. */
  readonly range = input.required<string>();

  private readonly texts = injectAdminText();

  protected readonly sorts = SORTS;
  protected readonly category = signal('');
  protected readonly search = signal('');
  protected readonly sort = signal<RobotSort>('hits');
  protected readonly categories = computed(() => [
    ...new Set(this.robots().map((robot) => robot.category)),
  ]);
  protected readonly rows = computed(() =>
    robotRows(
      this.robots().map((robot) => ({ robot, label: robotLabel(robot, this.texts) })),
      { category: this.category(), search: this.search(), sort: this.sort() },
    ),
  );

  protected readonly color = categoryColor;
  protected readonly operatorOf = robotOperator;
  protected readonly verifiedShare = verifiedShare;
  protected readonly errors = errors;

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  protected onCategory(event: Event): void {
    this.category.set((event.target as HTMLSelectElement).value);
  }

  protected onSort(event: Event): void {
    this.sort.set((event.target as HTMLSelectElement).value as RobotSort);
  }
}
