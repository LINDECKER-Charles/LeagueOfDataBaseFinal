import type { RobotSummary } from '../../../../../../core/api/generated/models/robot-summary';

/** What the table of the robots sorts on. */
export type RobotSort = 'hits' | 'name' | 'verified' | 'errors' | 'mean' | 'last';

/** The filters of the table: a category, a text of the name, the operator or a network. */
export interface RobotFilter {
  readonly category: string;
  readonly search: string;
  readonly sort: RobotSort;
}

/** A robot as the table orders it, its name already written. */
export interface LabelledRobot {
  readonly robot: RobotSummary;
  readonly label: string;
}

// A sort on figures puts the largest first; a sort on names, the first in the alphabet.
const SORTS: Readonly<Record<RobotSort, (left: LabelledRobot, right: LabelledRobot) => number>> = {
  hits: (left, right) => right.robot.figures.hits - left.robot.figures.hits,
  name: (left, right) => left.label.localeCompare(right.label, 'fr'),
  verified: (left, right) => verifiedShare(right.robot) - verifiedShare(left.robot),
  errors: (left, right) => errors(right.robot) - errors(left.robot),
  mean: (left, right) => right.robot.figures.meanMs - left.robot.figures.meanMs,
  last: (left, right) => right.robot.lastDay.localeCompare(left.robot.lastDay),
};

/** The share of a robot's hits whose network proved it, from 0 to 1. */
export function verifiedShare(robot: RobotSummary): number {
  return robot.figures.hits > 0 ? robot.figures.verified / robot.figures.hits : 0;
}

/** The hits answered 4xx or 5xx. */
export function errors(robot: RobotSummary): number {
  return robot.figures.clientErrors + robot.figures.serverErrors;
}

/** The robots the filter keeps, in its order. */
export function robotRows(
  robots: readonly LabelledRobot[],
  filter: RobotFilter,
): readonly LabelledRobot[] {
  const search = filter.search.trim().toLocaleLowerCase('fr');
  return [...robots]
    .filter(({ robot }) => !filter.category || robot.category === filter.category)
    .filter(
      ({ robot, label }) =>
        !search ||
        [label, robot.operator, robot.key, ...robot.networks].some((text) =>
          text.toLocaleLowerCase('fr').includes(search),
        ),
    )
    .sort(SORTS[filter.sort]);
}
