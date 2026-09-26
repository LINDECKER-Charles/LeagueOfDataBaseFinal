import { measureCompleteness } from './measure-completeness.mjs';
import { readJsonCatalogues } from './read-json-catalogues.mjs';
import { renderReport } from './render-report.mjs';

// The fallback language of the runtime (DEFAULT_LOCALE in src/app/core/i18n).
const REFERENCE_LOCALE = 'en';

/**
 * Measures the catalogues of `catalogueDir` and returns the Markdown report with one
 * summary line per locale that is not complete.
 */
export function runReport(catalogueDir) {
  const measurements = measureCompleteness(readJsonCatalogues(catalogueDir), REFERENCE_LOCALE);
  const summary = measurements.flatMap((scope) =>
    scope.locales
      .filter((entry) => entry.missing.length + entry.extra.length + entry.invalid.length > 0)
      .map(
        (entry) =>
          `${scope.scope || 'root'}/${entry.locale}: ${entry.missing.length} missing, ` +
          `${entry.extra.length} extra, ${entry.invalid.length} invalid`,
      ),
  );
  return { markdown: renderReport(measurements, REFERENCE_LOCALE), summary };
}
