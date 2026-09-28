import { scopeLabel } from './scope-label.mjs';

const PERCENT = 100;

/**
 * Summary section: the totals a reader looks for first, then one row per locale with its
 * overall completeness and the keys missing in each scope.
 */
export function renderSummary(measurements, referenceLocale) {
  const referenceTotal = measurements.reduce((sum, scope) => sum + scope.referenceKeys.length, 0);
  const locales = measurements[0].locales.map((entry) => entry.locale);
  const incomplete = locales.filter((locale) =>
    missingPerScope(measurements, locale).some(Boolean),
  );
  const labels = measurements.map((scope) => scopeLabel(scope.scope));
  return [
    '## Synthèse',
    '',
    `${locales.length} locales, ${measurements.length} portées (${labels.join(', ')}), ` +
      `${referenceTotal} clés dans la référence \`${referenceLocale}\`. ` +
      `Locales complètes : ${locales.length - incomplete.length} ; ` +
      `incomplètes : ${incomplete.length}.`,
    '',
    `| Locale | Traduites | Manquantes | Complétude | ${labels.join(' | ')} |`,
    `|---|---:|---:|---:|${labels.map(() => '---:').join('|')}|`,
    ...locales.map((locale) => renderRow(measurements, locale, referenceTotal)),
  ].join('\n');
}

function renderRow(measurements, locale, referenceTotal) {
  const perScope = missingPerScope(measurements, locale);
  const missing = perScope.reduce((sum, count) => sum + count, 0);
  const translated = referenceTotal - missing;
  const cells = [
    locale,
    `${translated} / ${referenceTotal}`,
    missing,
    percent(translated, referenceTotal),
    ...perScope,
  ];
  return `| ${cells.join(' | ')} |`;
}

function missingPerScope(measurements, locale) {
  return measurements.map(
    (scope) => scope.locales.find((entry) => entry.locale === locale).missing.length,
  );
}

function percent(part, whole) {
  const value = whole === 0 ? PERCENT : (part / whole) * PERCENT;
  return `${value.toFixed(1).replace('.', ',')} %`;
}
