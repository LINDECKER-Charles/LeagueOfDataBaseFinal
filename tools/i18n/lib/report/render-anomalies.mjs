import { scopeLabel } from './scope-label.mjs';

/**
 * Everything besides missing keys: absent scope files, keys the reference does not know
 * (dead text, never displayed) and messages MessageFormat rejects (a render that throws).
 */
export function renderAnomalies(measurements) {
  const entries = measurements.flatMap((scope) =>
    scope.locales.map((entry) => ({ scope: scopeLabel(scope.scope), ...entry })),
  );
  return [
    section(
      '## Fichiers absents',
      entries.filter((entry) => entry.fileMissing),
      (entry) => [`- ${entry.scope} : \`${entry.locale}\``],
    ),
    section(
      '## Clés hors référence',
      entries.filter((entry) => entry.extra.length > 0),
      (entry) => entry.extra.map((key) => `- ${entry.scope}, \`${entry.locale}\` : \`${key}\``),
    ),
    section(
      '## Messages ICU invalides',
      entries.filter((entry) => entry.invalid.length > 0),
      (entry) =>
        entry.invalid.map(
          ({ key, error }) => `- ${entry.scope}, \`${entry.locale}\`, \`${key}\` : ${error}`,
        ),
    ),
  ].join('\n');
}

function section(title, entries, describe) {
  const lines = entries.flatMap(describe);
  return [title, '', ...(lines.length > 0 ? lines : ['Aucun.']), ''].join('\n');
}
