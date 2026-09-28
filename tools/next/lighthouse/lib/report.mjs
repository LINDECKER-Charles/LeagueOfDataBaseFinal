// The Markdown report of a measure, docs/reecriture/rapports/lighthouse.md, in French like the
// other reports of the rewrite. Everything in it comes from the run.

import { BUDGETS, checkBudgets } from './budgets.mjs';

const MS_PER_S = 1000;
const KIB = 1024;
const CLS_DIGITS = 3;
const COLUMNS = [
  ['Perf.', (page) => page.performance],
  ['Access.', (page) => page.accessibility],
  ['Bonnes pr.', (page) => page.bestPractices],
  ['SEO', (page) => page.seo],
  ['FCP', (page) => seconds(page.fcp)],
  ['LCP', (page) => seconds(page.lcp)],
  ['TBT', (page) => milliseconds(page.tbt)],
  ['CLS', (page) => page.cls?.toFixed(CLS_DIGITS)],
  ['Speed Index', (page) => seconds(page.speedIndex)],
];

function seconds(value) {
  return typeof value === 'number' ? `${(value / MS_PER_S).toFixed(1)} s` : undefined;
}

function milliseconds(value) {
  return typeof value === 'number' ? `${Math.round(value)} ms` : undefined;
}

// The browser of a user agent (`HeadlessChrome/153.0.0.0`), or the whole string.
function browserOf(userAgent = '') {
  return /(?:Headless)?Chrome\/[\d.]+/.exec(userAgent)?.[0] ?? userAgent;
}

function limitOf(budget) {
  const bound = budget.min === undefined ? `≤ ${budget.max}` : `≥ ${budget.min}`;
  return budget.unit === '' ? bound : `${bound} ${budget.unit}`;
}

function verdictCell(page) {
  const missed = checkBudgets(page).filter((check) => !check.met);
  return missed.length === 0 ? '✓' : `**✗** ${missed.map((check) => check.label).join(', ')}`;
}

function table(pages, withVerdict) {
  const heads = ['Page', ...COLUMNS.map(([label]) => label), ...(withVerdict ? ['Budgets'] : [])];
  const lines = [`| ${heads.join(' | ')} |`, `|---|${COLUMNS.map(() => '---:').join('|')}|`];
  if (withVerdict) lines[1] += ':---|';
  for (const page of pages) {
    const cells = COLUMNS.map(([, value]) => value(page) ?? '—');
    const verdict = withVerdict ? [verdictCell(page)] : [];
    lines.push(`| [${page.name}](${page.url}) | ${[...cells, ...verdict].join(' | ')} |`);
  }
  return [...lines, ''];
}

function header(meta, pages) {
  const environment = pages[0]?.environment ?? {};
  return [
    '# Lighthouse : budgets du lot 3 (L3.13)',
    '',
    `- **Date** : ${meta.date}.`,
    `- **Réécriture** : ${meta.next} (${meta.stack}).`,
    `- **Outil** : Lighthouse ${environment.lighthouse}, ${browserOf(environment.userAgent)}.`,
    '- **Profil** : mobile par défaut de Lighthouse (écran 412 × 823, CPU ralenti 4 fois, 4G',
    '  lente simulée : 150 ms de RTT, 1,6 Mbit/s), stockage vidé avant chaque passe.',
    `- **Passes** : ${meta.runs} par page ; chaque chiffre est la médiane des passes, les`,
    '  détails viennent de la passe de performance médiane.',
    `- **Commande** : \`${meta.command}\`.`,
    '',
    'Rapport généré : ne pas le modifier à la main, relancer `tools/next/lighthouse/run.mjs`.',
    '',
  ];
}

function budgets() {
  return [
    '## Budgets',
    '',
    BUDGETS.map((budget) => `${budget.label} ${limitOf(budget)}`).join(' ; ') + '.',
    '',
  ];
}

function verdict(pages) {
  const missed = pages.filter((page) => checkBudgets(page).some((check) => !check.met));
  const names = missed.map((page) => page.name).join(', ');
  const count = `${missed.length} page(s) sur ${pages.length}`;
  return [
    missed.length === 0
      ? `**Verdict** : budgets tenus sur les ${pages.length} pages.`
      : `**Verdict** : budgets manqués sur ${count} : ${names}.`,
    '',
  ];
}

function details(page) {
  const lines = [`### ${page.name}`, ''];
  for (const audit of page.failing) {
    lines.push(`- ${audit.category} en échec : \`${audit.id}\` (${audit.title}).`);
  }
  for (const lead of page.leads) {
    const value = lead.value === '' ? '' : ` : ${lead.value}`;
    lines.push(`- Piste de performance \`${lead.id}\` (${lead.title})${value}.`);
  }
  const bytes = page.uncompressed.reduce((sum, item) => sum + item.bytes, 0);
  if (page.uncompressed.length > 0) {
    const urls = page.uncompressed.map((item) => `\`${item.url}\``).join(', ');
    lines.push(
      `- ${page.uncompressed.length} ressource(s) texte servie(s) sans compression, ` +
        `${Math.round(bytes / KIB)} Kio : ${urls}.`,
    );
  }
  for (const warning of page.warnings) lines.push(`- Avertissement de Lighthouse : ${warning}`);
  return lines.length === 2 ? [] : [...lines, ''];
}

/** The report: `meta` describes the run, `next` and `prod` hold the measured pages. */
export function renderReport(meta, next, prod) {
  const lines = [...header(meta, next), ...budgets(), '## Réécriture', '', ...table(next, true)];
  lines.push(...verdict(next), '## Détails de la réécriture', '', ...next.flatMap(details));
  if (prod.length > 0) {
    lines.push('## Prod, à titre indicatif', '', `Mesurée sur ${meta.prod}, mêmes réglages.`, '');
    lines.push(...table(prod, false), '## Détails de la prod', '', ...prod.flatMap(details));
  }
  return lines.join('\n');
}
