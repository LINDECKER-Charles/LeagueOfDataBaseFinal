// The Markdown report of a diff, docs/reecriture/rapports/diff-seo.md. Written in French, like
// the other reports of the rewrite; everything in it comes from the run and from rules.mjs.

import { FIELDS } from './compare.mjs';
import { RULES } from './rules.mjs';

const FIELD_LABELS = {
  redirect: '301',
  title: 'Titre',
  description: 'Description',
  canonical: 'Canonique',
  types: 'Types JSON-LD',
  fields: 'Champs JSON-LD',
};
const OUTCOMES = ['same', 'explained', 'defect', 'unexplained'];
const OUTCOME_LABELS = {
  same: 'identiques',
  explained: 'écarts expliqués',
  defect: 'défauts consignés',
  unexplained: 'non expliqués',
};
const KIND_LABELS = { decision: 'décision', correction: 'correction', defect: 'défaut' };
const EXAMPLE_LENGTH = 90;

function cell(finding) {
  const ids = finding.rules.map((rule) => `[${rule.id}](#${rule.id})`).join(', ');
  if (finding.outcome === 'same') return '=';
  if (finding.outcome === 'unexplained') return ids === '' ? '**✗**' : `**✗** + ${ids}`;
  return finding.outcome === 'defect' ? `⚠ ${ids}` : `≈ ${ids}`;
}

function shown(value) {
  if (value === undefined || value === null) return '(absent)';
  const text = typeof value === 'string' ? value : JSON.stringify(value);
  const cut = text.length > EXAMPLE_LENGTH ? `${text.slice(0, EXAMPLE_LENGTH)}…` : text;
  return `\`${cut.replaceAll('`', "'").replaceAll('|', '\\|')}\``;
}

function landingPath(result) {
  if (result.nextUrl === null) return '(aucune)';
  const url = new URL(result.nextUrl);
  return `\`${url.pathname}${url.search}\``;
}

function header(meta, results) {
  return [
    '# Diff SEO : la prod face à la réécriture (L3.13)',
    '',
    `- **Date** : ${meta.date}.`,
    `- **Prod** : ${meta.prod}, lue en GET seul, sans cookie, une requête à la fois.`,
    `- **Réécriture** : ${meta.next} (${meta.stack}), dernière version ${meta.latest}.`,
    `- **Échantillon** : ${results.length} URLs de la prod ` +
      '(`tools/seo-diff/lib/sample.mjs`).',
    `- **Commande** : \`${meta.command}\`.`,
    '',
    'Chaque URL de la prod est demandée à la prod, puis à la réécriture sous sa forme héritée :',
    'la 301 de L3.12 désigne la page comparée. Rapport généré : ne pas le modifier à la main,',
    "ajouter une règle argumentée dans `tools/seo-diff/lib/rules.mjs` et relancer l'outil.",
    '',
  ];
}

function method() {
  return [
    '## Méthode',
    '',
    '- **301** : la forme héritée répond en une seule 301, vers une page de même clé (type, id,',
    '  version), qui répond 200.',
    '- **Titre** et **description** : comparés tels quels, entités HTML décodées.',
    '- **Canonique** : réduite à sa clé (type de page, id, version ; la dernière version vaut',
    '  `latest`), l’origine et la grammaire d’URL mises de côté. Côté réécriture, la canonique',
    "  doit en plus être unique, sans query, et dans la locale de la page.",
    '- **Types JSON-LD** : ensemble des `@type`, imbriqués compris.',
    '- **Champs JSON-LD** : chaque bloc est aplati en `chemin → valeur`, après réécriture des URL',
    '  du site en clés de page (`asset:` et le chemin pour un fichier) ; toute feuille différente',
    '  ou absente d’un côté est un écart.',
    '',
  ];
}

function summary(results) {
  const lines = [
    '## Synthèse',
    '',
    `| Champ | ${OUTCOMES.map((outcome) => OUTCOME_LABELS[outcome]).join(' | ')} |`,
    `|---|${OUTCOMES.map(() => '---:').join('|')}|`,
  ];
  for (const field of FIELDS) {
    const findings = results.map((result) => result.findings.find((f) => f.field === field));
    const counts = OUTCOMES.map((outcome) => findings.filter((f) => f.outcome === outcome).length);
    lines.push(`| ${FIELD_LABELS[field]} | ${counts.join(' | ')} |`);
  }
  return [...lines, ''];
}

function verdict(results) {
  const all = results.flatMap((result) => result.findings);
  const unexplained = all.filter((finding) => finding.outcome === 'unexplained').length;
  const defects = all.filter((finding) => finding.outcome === 'defect').length;
  return [
    `**Verdict** : ${unexplained} écart(s) non expliqué(s), ${defects} défaut(s) consigné(s) ` +
      'pour le jalon du lot 3.',
    '',
  ];
}

function table(results) {
  const labels = FIELDS.map((field) => FIELD_LABELS[field]).join(' | ');
  const lines = [
    '## Résultats par URL',
    '',
    '`=` identique, `≈` écart expliqué, `⚠` défaut consigné, `✗` écart non expliqué.',
    '',
    `| URL de la prod | Page comparée | ${labels} |`,
    `|---|---|${FIELDS.map(() => ':---:').join('|')}|`,
  ];
  for (const result of results) {
    const cells = FIELDS.map((field) => cell(result.findings.find((f) => f.field === field)));
    lines.push(`| \`${result.url}\` | ${landingPath(result)} | ${cells.join(' | ')} |`);
  }
  return [...lines, ''];
}

function describe(field, entry) {
  const path = field === 'fields' ? ` \`${entry.path}\`` : '';
  return `${FIELD_LABELS[field]}${path} : ${shown(entry.prod)} → ${shown(entry.next)}`;
}

// Each URL a rule explains, with the first difference it explains there.
function occurrences(results, rule) {
  const lines = [];
  for (const result of results) {
    for (const finding of result.findings) {
      const matched = finding.matches.filter((match) => match.rule.id === rule.id);
      if (matched.length === 0) continue;
      const more = matched.length > 1 ? ` (et ${matched.length - 1} autre(s))` : '';
      lines.push(`- \`${result.url}\` — ${describe(finding.field, matched[0].entry)}${more}`);
    }
  }
  return lines;
}

function explained(results) {
  const lines = ['## Écarts expliqués', ''];
  for (const rule of RULES) {
    const found = occurrences(results, rule);
    if (found.length === 0) continue;
    lines.push(`### \`${rule.id}\``, '', `**${KIND_LABELS[rule.kind]}** — ${rule.reason}`, '');
    lines.push(...found, '');
  }
  return lines;
}

function unexplained(results) {
  const lines = ['## Écarts non expliqués', ''];
  for (const result of results) {
    for (const finding of result.findings.filter((f) => f.unexplained.length > 0)) {
      for (const entry of finding.unexplained) {
        lines.push(`- \`${result.url}\` — ${describe(finding.field, entry)}`);
      }
      if (finding.faults?.length > 0) lines.push(`  - ${finding.faults.join(' ; ')}`);
    }
  }
  return lines.length === 2 ? [...lines, 'Aucun.', ''] : [...lines, ''];
}

function outOfSample() {
  return [
    '## Hors échantillon',
    '',
    '- `/trends`, `/developers`, `/donate` : pages provisoires des lots 5 et 6, sans SEO propre',
    '  à ce stade ; leur diff revient à leurs chantiers.',
    '- `/u/{username}` : les comptes n’existent que dans la base de la prod (répétition du lot 8).',
    '- Pages de compte : jamais indexées (`noindex`), hors diff SEO.',
    '- hreflang : la prod n’en a pas (ADR 0005) ; la suite E2E `specs/public/seo.spec.ts` vérifie',
    '  les 21 locales et `x-default` sur chaque page publique.',
    '',
  ];
}

/** The report of a run: `meta` describes the run, `results` holds each classified pair. */
export function renderReport(meta, results) {
  return [
    ...header(meta, results),
    ...method(),
    ...summary(results),
    ...verdict(results),
    ...table(results),
    ...explained(results),
    ...unexplained(results),
    ...outOfSample(),
  ].join('\n');
}
