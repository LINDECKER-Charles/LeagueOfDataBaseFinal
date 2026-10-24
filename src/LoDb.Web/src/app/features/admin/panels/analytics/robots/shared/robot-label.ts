import type { AdminText } from '../../../../shared/inject-admin-text';

/** What names a robot: its key, its name, who runs it, its category. */
export interface NamedRobot {
  readonly key: string;
  readonly name: string;
  readonly operator: string;
  readonly category: string;
}

// The two robots the API names in English, its own words: the catalog's, never shown.
const NO_AGENT = 'no-agent';
const UNDECLARED = 'undeclared';

/**
 * The name of a robot as the admin shows it: the catalog's, but a request without a user
 * agent and a browser at a robot's pace read in French, the second after its network.
 */
export function robotLabel(robot: NamedRobot, texts: AdminText): string {
  if (robot.key === NO_AGENT) {
    return texts.text('robots.no_agent');
  }
  if (robot.category === UNDECLARED) {
    return texts.text('robots.undeclared_name', {
      network: robot.operator || texts.text('robots.unknown_network'),
    });
  }
  return robot.name;
}

/** Who runs a robot, '' for a library and for an undeclared one, whose name says it. */
export function robotOperator(robot: NamedRobot): string {
  return robot.category === UNDECLARED ? '' : robot.operator;
}
