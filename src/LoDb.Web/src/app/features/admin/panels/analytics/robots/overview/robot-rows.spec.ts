import { robotReport } from '../../../../testing/fixtures/robot-report';
import { type RobotFilter, robotRows } from './robot-rows';

const ROBOTS = robotReport().robots.map((robot) => ({ robot, label: robot.name }));

function keys(filter: Partial<RobotFilter>): string[] {
  return robotRows(ROBOTS, { category: '', search: '', sort: 'hits', ...filter }).map(
    (row) => row.robot.key,
  );
}

describe('robotRows', () => {
  it('sorts by any figure, the largest first, and by name in the alphabet', () => {
    expect(keys({ sort: 'hits' })).toEqual(['googlebot', 'undeclared:example.net']);
    expect(keys({ sort: 'mean' })).toEqual(['undeclared:example.net', 'googlebot']);
    expect(keys({ sort: 'errors' })).toEqual(['googlebot', 'undeclared:example.net']);
    expect(keys({ sort: 'name' })).toEqual(['googlebot', 'undeclared:example.net']);
  });

  it('keeps a category, or a text of the name, the operator, the key or a network', () => {
    expect(keys({ category: 'undeclared' })).toEqual(['undeclared:example.net']);
    expect(keys({ search: 'example.NET' })).toEqual(['undeclared:example.net']);
    expect(keys({ search: 'google' })).toEqual(['googlebot']);
    expect(keys({ search: 'nobody' })).toEqual([]);
  });
});
