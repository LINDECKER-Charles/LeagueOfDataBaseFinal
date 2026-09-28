// Gives each finding of a pair its outcome: `same`, explained by rules (their ids), or
// unexplained. A JSON-LD `fields` finding is explained only if each of its leaves is.

import { comparePair } from './compare.mjs';
import { nextKey } from './page-key.mjs';
import { ruleFor } from './rules.mjs';

function contextOf(pair) {
  if (pair.nextUrl === null) return { nextLocale: null, nextKey: null };
  const { key, locale } = nextKey(pair.nextUrl, pair.latest);
  return { nextLocale: locale, nextKey: key };
}

function outcomeOf(rules, unexplained) {
  if (unexplained.length > 0) return 'unexplained';
  return rules.some((rule) => rule.kind === 'defect') ? 'defect' : 'explained';
}

function classifyDifferences(finding, context) {
  const entries = finding.field === 'fields' ? finding.differences : [finding];
  const matches = [];
  const unexplained = [];
  for (const entry of entries) {
    const rule = ruleFor(finding.field, entry, context);
    if (rule === null) unexplained.push(entry);
    else matches.push({ entry, rule });
  }
  const rules = [...new Set(matches.map((match) => match.rule))];
  return { ...finding, outcome: outcomeOf(rules, unexplained), rules, matches, unexplained };
}

/**
 * The findings of a pair (compare.mjs), each with its `outcome`, the `rules` that explain it,
 * each explained leaf with its rule (`matches`), and the leaves no rule explains.
 */
export function classifyPair(pair) {
  const context = contextOf(pair);
  return comparePair(pair).map((finding) =>
    finding.verdict === 'same'
      ? { ...finding, outcome: 'same', rules: [], matches: [], unexplained: [] }
      : classifyDifferences(finding, context),
  );
}
