// The Markdown summary of a bench run (summary.md beside summary.json), in French like the
// repository's other reports. Everything in it comes from the run.

import { CRITERIA } from './verdict.mjs';

const TITLES = {
  S1: 'Pages épinglées uniques (crawl du 10/10)',
  S2: 'Recherches `?q=` aléatoires sur une liste courante, une adresse',
  S3: '`/api/catalog` balayé (versions × langues)',
  S4: 'URL de l’ancien site',
  S5: 'Boucle de `/warm-up` (SSE)',
};
const SERVICES = ['web-ssr', 'api', 'postgres', 'nginx'];
const MIB = 2 ** 20;
const GIB = 2 ** 30;
const PERCENT = 100;
const CORE_DIGITS = 2;
const NOTHING = '—';

function cores(value) {
  return typeof value === 'number' ? value.toFixed(CORE_DIGITS) : NOTHING;
}

function milliseconds(value) {
  return typeof value === 'number' ? `${Math.round(value)} ms` : NOTHING;
}

function mebibytes(value) {
  return typeof value === 'number' ? `${Math.round(value / MIB)} Mio` : NOTHING;
}

function percent(rate) {
  return typeof rate === 'number' ? `${(rate * PERCENT).toFixed(1)} %` : NOTHING;
}

function statuses({ byStatus }) {
  const entries = Object.entries(byStatus);
  return entries.length === 0 ? NOTHING : entries.map(([code, n]) => `${code}: ${n}`).join(', ');
}

function mark(isMet) {
  return isMet ? '✓' : '**✗**';
}

function header({ run }) {
  return [
    `# Banc de charge k6 — ${run.date}`,
    '',
    `Révision \`${run.revision}\`, projet \`${run.project}\`, ${run.duration} par scénario ` +
      `après ${run.warmUp} de chauffe, sonde humaine à 1 requête/s. Image ` +
      `\`${run.k6Image}\`.`,
    `Moteur Docker : ${run.docker.cpus} CPU, ${(run.docker.memoryBytes / GIB).toFixed(1)} Gio ; ` +
      'quotas et limites mémoire de la prod (`tools/load/compose.bench.yaml`).',
    '',
    `Critères : SSR ≤ ${CRITERIA.ssrCores} cœur et API ≤ ${CRITERIA.apiCores} cœur au 95ᵉ ` +
      `centile, aucun OOM ni redémarrage, sonde HIT p95 < ${CRITERIA.hitP95Ms} ms, MISS du ` +
      `dernier patch p95 < ${CRITERIA.missP95Ms} ms, sonde servie à plus de ` +
      `${(1 - CRITERIA.probeFailedRate) * PERCENT} %.`,
    '',
  ];
}

function synthesis({ scenarios }) {
  const lines = [
    '| Scénario | Requêtes | SSR p50 / p95 | API p50 / p95 | Postgres p95 | nginx p95 | ' +
      'HIT p95 | MISS p95 | Sonde en échec | Verdict |',
    '|---|---:|---:|---:|---:|---:|---:|---:|---:|:---:|',
  ];
  for (const scenario of scenarios) {
    const { containers: c, result } = scenario;
    lines.push(`| ${scenario.name} | ${result.load.total} | ` +
      `${cores(c['web-ssr']?.cores.p50)} / ${cores(c['web-ssr']?.cores.p95)} | ` +
      `${cores(c.api?.cores.p50)} / ${cores(c.api?.cores.p95)} | ` +
      `${cores(c.postgres?.cores.p95)} | ${cores(c.nginx?.cores.p95)} | ` +
      `${milliseconds(result.probe.hit.p95)} | ${milliseconds(result.probe.miss.p95)} | ` +
      `${percent(result.probe.failed.rate)} | ${mark(scenario.verdict.isMet)} |`);
  }
  return [...lines, ''];
}

function stateOf(health) {
  return health
    ? `OOM ${health.oomKilled ? 'oui' : 'non'}, redémarrages ${health.restarts}, ` +
      `processus tués ${health.kills}`
    : NOTHING;
}

// The quota's periods the container was throttled in, and the time it spent so.
function throttlingOf(cgroup) {
  return cgroup
    ? `${percent(cgroup.throttledShare)} (${cgroup.throttledSeconds.toFixed(1)} s)`
    : NOTHING;
}

function containerRows(containers) {
  return SERVICES.map((service) => {
    const usage = containers[service];
    return `| ${service} | ${cores(usage?.cores.p50)} | ${cores(usage?.cores.p95)} | ` +
      `${cores(usage?.cores.max)} | ${cores(usage?.cgroup?.cores)} | ` +
      `${throttlingOf(usage?.cgroup)} | ${mebibytes(usage?.memory?.p95)} | ` +
      `${mebibytes(usage?.memory?.max)} | ${stateOf(usage?.health)} |`;
  });
}

// A criterion's value in its unit, which its name ends with (verdict.mjs).
const FORMATS = [
  [/-cores-p95$/, (value) => `${cores(value)} cœur`],
  [/-ms$/, milliseconds],
  [/-rate$/, percent],
];

function formatted(name, value) {
  if (Array.isArray(value)) {
    return value.join(', ') || 'aucun';
  }
  if (value === null) {
    return 'aucune mesure';
  }
  const format = FORMATS.find(([pattern]) => pattern.test(name));
  return format ? format[1](value) : String(value);
}

function checkLine({ name, measured, limit, isMet }) {
  return `- ${mark(isMet)} \`${name}\` : ${formatted(name, measured)} ` +
    `(borne ${formatted(name, limit)})`;
}

function counts(byKey) {
  const entries = Object.entries(byKey ?? {});
  return entries.length === 0 ? 'aucun' : entries.map(([key, n]) => `${key} ${n}`).join(', ');
}

// The SSR logs its admissions only for a minute in which it refused a render.
function admissionLine(admission) {
  if (Object.keys(admission?.refused ?? {}).length === 0) {
    return '- Admission du SSR : aucun rendu refusé.';
  }
  return `- Admission du SSR (minutes avec refus) : admis ${counts(admission.admitted)} ; ` +
    `refusés ${counts(admission.refused)}.`;
}

// What the generator saw, then what the SSR and the API logged meanwhile.
function trafficLines({ name, result, logs }) {
  const warmUps = name === 'S5'
    ? [`- Flux de warm-up : ${result.warmUps.done} terminés, ${result.warmUps.failed} en ` +
      `échec, ${result.warmUps.cut} coupés.`]
    : [];
  return [
    `- Charge : ${statuses(result.load)} ; itérations abandonnées faute d'utilisateur ` +
      `virtuel : ${result.dropped}.`,
    `- Sonde : ${statuses(result.probeRequests)} ; HIT ${result.probe.hit.count} ` +
      `(p95 ${milliseconds(result.probe.hit.p95)}), MISS ${result.probe.miss.count} ` +
      `(p95 ${milliseconds(result.probe.miss.p95)}).`,
    ...warmUps,
    admissionLine(logs?.admission),
    `- Avertissements et erreurs de l'API : ${counts(logs?.apiEvents)}.`,
  ];
}

function details(scenario) {
  return [
    `## ${scenario.name} — ${TITLES[scenario.name] ?? scenario.name}`,
    '',
    `${scenario.rate} requêtes/s depuis ${scenario.crowd} adresse(s).`,
    '',
    ...trafficLines(scenario),
    '',
    '| Conteneur | CPU p50 | CPU p95 | CPU max | CPU moyen (cgroup) | Bridage (périodes) | ' +
      'Mémoire p95 | Mémoire max | État |',
    '|---|---:|---:|---:|---:|---:|---:|---:|---|',
    ...containerRows(scenario.containers),
    '',
    ...scenario.verdict.checks.map(checkLine),
    '',
  ];
}

/** The Markdown summary of a run: the synthesis table, then each scenario in detail. */
export function renderReport(summary) {
  return [
    ...header(summary),
    ...synthesis(summary),
    ...summary.scenarios.flatMap(details),
  ].join('\n');
}
