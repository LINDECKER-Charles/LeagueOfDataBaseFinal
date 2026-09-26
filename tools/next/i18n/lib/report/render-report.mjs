import { renderAnomalies } from './render-anomalies.mjs';
import { renderMissingKeys } from './render-missing-keys.mjs';
import { renderSummary } from './render-summary.mjs';

/**
 * The whole Markdown report. Deterministic (no date, stable order) so that running it
 * again on unchanged catalogues leaves the committed file untouched.
 */
export function renderReport(measurements, referenceLocale) {
  return [
    '# Complétude des traductions',
    '',
    'Produit par `npm --prefix src/LoDb.Web run i18n:report` à partir des catalogues',
    'Transloco de `src/LoDb.Web/public/i18n/` : ne pas modifier à la main.',
    '',
    `La référence est \`${referenceLocale}\` : à l'exécution, chaque clé manquante s'y replie.`,
    'Le rapport ne bloque rien ; le job `i18n` de la CI le produit pour rendre les trous visibles.',
    '',
    renderSummary(measurements, referenceLocale),
    '',
    renderMissingKeys(measurements),
    renderAnomalies(measurements),
  ].join('\n');
}
