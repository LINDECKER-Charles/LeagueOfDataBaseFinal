// The Markdown report of a comparison: one line per page, then each difference.

function cell(value) {
  const shown = typeof value === 'string' ? value : JSON.stringify(value);
  return String(shown).replaceAll('|', '\\|').replaceAll('\n', ' ');
}

/** The report of `results` (`{ build, legacy, next, differences }`), with the run's `meta`. */
export function renderReport(meta, results) {
  const differing = results.filter((result) => result.differences.length > 0);
  const lines = [
    '# Parité des pages /b/{token} (L5.3)',
    '',
    `- Date : ${meta.date}`,
    `- Ancienne stack : ${meta.legacy}`,
    `- Réécriture : ${meta.next}`,
    `- Commande : \`${meta.command}\``,
    `- Pages : ${results.length}, dont ${differing.length} avec un écart`,
    '',
    '| Jeton | Cas | Statut ancien | Statut nouveau | Écarts |',
    '|---|---|---|---|---|',
    ...results.map(
      ({ build, legacy, next, differences }) =>
        `| \`${build.token}\` | ${cell(build.case ?? '')} | ${legacy.status} | ` +
        `${next.status} | ${differences.length} |`,
    ),
  ];
  for (const { build, differences } of differing) {
    lines.push('', `## \`${build.token}\``, '', '| Champ | Ancienne | Nouvelle |', '|---|---|---|');
    for (const { field, legacy, next } of differences) {
      lines.push(`| \`${field}\` | ${cell(legacy)} | ${cell(next)} |`);
    }
  }
  return `${lines.join('\n')}\n`;
}
