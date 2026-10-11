// What `npm audit --json` reports, against the allow-list: an advisory at or above the audit
// level blocks unless an entry of the allow-list names it, gives its reason and has not expired.
// An entry lasts at most MAX_ALLOWANCE_DAYS, so that an accepted advisory is looked at again:
// a fix may have shipped, or the reason may no longer hold.

export const SEVERITIES = Object.freeze(['info', 'low', 'moderate', 'high', 'critical']);
export const MAX_ALLOWANCE_DAYS = 90;
const DAY_MS = 24 * 60 * 60 * 1000;
const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;
const ADVISORY_ID = /\/(GHSA(?:-[23456789cfghjmpqrvwx]{4}){3})$/;
const ENTRY_FIELDS = Object.freeze(['advisory', 'package', 'reason', 'expires']);

const rankOf = (severity) => SEVERITIES.indexOf(severity);

/** The advisories of a report (npm 7+), one per advisory and package, with their GHSA id. */
export function advisoriesOf(report) {
  const advisories = new Map();
  for (const vulnerability of Object.values(report.vulnerabilities ?? {})) {
    for (const via of vulnerability.via ?? []) {
      if (typeof via !== 'object') {
        continue; // the name of another vulnerable package: its own entry holds the advisory
      }
      const id = ADVISORY_ID.exec(via.url ?? '')?.[1] ?? String(via.source);
      const advisory = { id, package: via.name, severity: via.severity, title: via.title };
      advisories.set(`${id} ${via.name}`, { ...advisory, url: via.url, range: via.range });
    }
  }
  return [...advisories.values()];
}

// Midnight UTC of a YYYY-MM-DD date, or NaN when it is not a real calendar date.
function dayOf(date) {
  const time = ISO_DATE.test(date) ? Date.parse(`${date}T00:00:00Z`) : Number.NaN;
  return !Number.isNaN(time) && new Date(time).toISOString().startsWith(date) ? time : Number.NaN;
}

function entryProblemOf(entry, today) {
  const isFilled = (field) => typeof entry?.[field] === 'string' && entry[field] !== '';
  const missing = ENTRY_FIELDS.filter((field) => !isFilled(field));
  if (missing.length > 0) {
    return `missing ${missing.join(', ')}`;
  }
  const expires = dayOf(entry.expires);
  if (Number.isNaN(expires)) {
    return `expires '${entry.expires}' is not a YYYY-MM-DD date`;
  }
  const days = Math.round((expires - dayOf(today)) / DAY_MS);
  return days > MAX_ALLOWANCE_DAYS
    ? `expires ${days} days ahead, more than ${MAX_ALLOWANCE_DAYS}`
    : null;
}

/** The malformed entries of an allow-list (a JSON array), each with its problem. */
export function allowListProblemsOf(entries, today) {
  if (!Array.isArray(entries)) {
    return [{ index: -1, problem: 'the allow-list is not a JSON array' }];
  }
  return entries.flatMap((entry, index) => {
    const problem = entryProblemOf(entry, today);
    return problem ? [{ index, problem }] : [];
  });
}

const names = (entry, advisory) =>
  entry.advisory === advisory.id && entry.package === advisory.package;

/**
 * Splits the advisories at or above `level`: allowed by a live entry, or blocking (`expired`
 * set when only an expired entry names it). `stale` lists the entries that name nothing left.
 */
export function auditOutcomeOf(report, entries, { level, today }) {
  const relevant = advisoriesOf(report).filter((item) => rankOf(item.severity) >= rankOf(level));
  const outcome = { allowed: [], blocking: [], stale: [] };
  for (const advisory of relevant) {
    const entry = entries.find((candidate) => names(candidate, advisory));
    if (entry && entry.expires >= today) {
      outcome.allowed.push({ ...advisory, expires: entry.expires });
    } else {
      outcome.blocking.push({ ...advisory, expired: entry?.expires ?? null });
    }
  }
  outcome.stale = entries.filter((entry) => !relevant.some((advisory) => names(entry, advisory)));
  return outcome;
}
