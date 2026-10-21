import type { AdminProblem } from '../../../../../shared/http/problem-of';

/** A step of the launch dialog: 1 the patches, 2 the perimeter, 3 the summary. */
export type LaunchStep = 1 | 2 | 3;

/** A text of the dialog: a key of the `admin` scope and its values. */
export interface LaunchMessage {
  readonly key: string;
  readonly params?: Readonly<Record<string, string>>;
}

/** Why the API refused a launch, as the dialog shows it (§ 3.6). */
export interface LaunchErrors {
  /** The first step at fault, where the dialog goes back; null when no field is. */
  readonly step: LaunchStep | null;
  /** The messages of each field refused, by the section they show under. */
  readonly fields: Readonly<Partial<Record<LaunchField, readonly LaunchMessage[]>>>;
  /** The band at the head of the dialog; null when the fields say it all. */
  readonly band: 'active' | 'dormant' | 'session' | 'network' | null;
}

/** The fields of PassRequestReader, and `scope` for the perimeter as a whole. */
export type LaunchField = 'versions' | 'priority' | 'locales' | 'pageTypes' | 'epoch' | 'scope';

const ERRORS = 'prerender.launcher.errors';
const FIELD_STEPS: Readonly<Record<LaunchField, LaunchStep>> = {
  versions: 1,
  priority: 1,
  locales: 2,
  pageTypes: 2,
  epoch: 2,
  scope: 3,
};
// The codes each field may answer (§ 6.2): any other one is shown raw.
const KNOWN_CODES: Readonly<Record<LaunchField, readonly string[]>> = {
  versions: ['malformed', 'too-many', 'required', 'none-pinned'],
  priority: ['malformed', 'too-many'],
  locales: ['malformed', 'required'],
  pageTypes: ['malformed', 'required'],
  epoch: ['out-of-range', 'retired'],
  scope: ['nothing-to-render'],
};
const CONFLICT = 409;
const SESSION_STATUSES: readonly number[] = [401, 403];
const VALIDATION = 400;
const BANDS: Readonly<Record<string, LaunchErrors['band']>> = {
  'prerender-pass-active': 'active',
  'prerender-dormant': 'dormant',
};

function isField(name: string): name is LaunchField {
  return name in FIELD_STEPS;
}

function messagesOf(field: LaunchField, codes: readonly string[]): LaunchMessage[] {
  const known = codes.filter((code) => KNOWN_CODES[field].includes(code));
  const unknown = codes.filter((code) => !known.includes(code));
  const messages: LaunchMessage[] = known.map((code) => ({ key: `${ERRORS}.${field}.${code}` }));
  return unknown.length === 0
    ? messages
    : [...messages, { key: `${ERRORS}.unknown`, params: { codes: unknown.join(', ') } }];
}

// A field the dialog does not know is shown with the perimeter, on the summary.
function fieldsOf(errors: AdminProblem['errors']) {
  const fields: Partial<Record<LaunchField, LaunchMessage[]>> = {};
  for (const [name, codes] of Object.entries(errors)) {
    const field = isField(name) ? name : 'scope';
    const named = isField(name) ? codes : codes.map((code) => `${name}: ${code}`);
    fields[field] = [...(fields[field] ?? []), ...messagesOf(field, named)];
  }
  return fields;
}

function firstStep(fields: Partial<Record<LaunchField, unknown>>): LaunchStep | null {
  const steps = (Object.keys(fields) as LaunchField[]).map((field) => FIELD_STEPS[field]);
  return steps.length > 0 ? (Math.min(...steps) as LaunchStep) : null;
}

/**
 * The messages of a refused launch (§ 3.6): a validation names each field under its section
 * and sends the dialog back to the first step at fault; a conflict, an expired session or
 * no answer at all (the pass may exist) raise a band instead.
 */
export function launchErrorsOf(problem: AdminProblem): LaunchErrors {
  if (problem.status === CONFLICT) {
    return { step: null, fields: {}, band: BANDS[problem.code ?? ''] ?? 'active' };
  }
  if (SESSION_STATUSES.includes(problem.status)) {
    return { step: null, fields: {}, band: 'session' };
  }
  if (problem.status !== VALIDATION) {
    return { step: null, fields: {}, band: 'network' };
  }
  const fields = fieldsOf(problem.errors);
  if (Object.keys(fields).length === 0) {
    fields.scope = messagesOf('scope', [problem.code ?? String(VALIDATION)]);
  }
  return { step: firstStep(fields), fields, band: null };
}
