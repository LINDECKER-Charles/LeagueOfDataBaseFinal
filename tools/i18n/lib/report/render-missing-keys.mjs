import { scopeLabel } from './scope-label.mjs';

/**
 * Missing keys, per scope, grouped by the exact set of locales that lack them: the 19
 * incomplete locales miss the same keys today, and one list says it better than nineteen.
 */
export function renderMissingKeys(measurements) {
  const sections = measurements.flatMap((scope) => {
    const groups = groupByLocales(scope);
    if (groups.length === 0) {
      return [];
    }
    return [`### ${scopeLabel(scope.scope)}`, '', ...groups.flatMap(renderGroup)];
  });
  return ['## Clés manquantes', '', ...(sections.length > 0 ? sections : ['Aucune.', ''])].join(
    '\n',
  );
}

function groupByLocales(scope) {
  const groups = new Map();
  for (const key of scope.referenceKeys) {
    const locales = scope.locales.filter((entry) => entry.missing.includes(key));
    if (locales.length > 0) {
      const signature = locales.map((entry) => entry.locale).join(', ');
      groups.set(signature, [...(groups.get(signature) ?? []), key]);
    }
  }
  return [...groups]
    .map(([locales, keys]) => ({ locales, keys }))
    .sort((left, right) => right.keys.length - left.keys.length);
}

function renderGroup({ locales, keys }) {
  return [
    `Clés manquantes : ${keys.length}, dans ${locales}.`,
    '',
    '<details>',
    '<summary>Liste des clés</summary>',
    '',
    ...keys.map((key) => `- \`${key}\``),
    '',
    '</details>',
    '',
  ];
}
