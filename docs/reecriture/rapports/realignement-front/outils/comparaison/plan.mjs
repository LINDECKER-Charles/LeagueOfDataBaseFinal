// Splits the verified findings into work packages (WP) by file ownership and wave.
import fs from 'node:fs';
const f = JSON.parse(fs.readFileSync('assigned.json', 'utf8'));

const ZONE_DEFAULT = {
  Z01: 'SHELL', Z02: 'DS', Z03: 'HOME', Z04: 'LISTS', Z05: 'DETAIL', Z06: 'ITEMDET',
  Z07: 'RUNES', Z08: 'EDITORIAL', Z09: 'ACCOUNT', Z10: 'BUILDS', Z11: 'ADMIN', Z12: 'I18N',
};
const OVERRIDE = {
  DS: ['Z02-05', 'Z04-20', 'Z05-02', 'Z08-04', 'Z08-05', 'Z09-08', 'Z09-17', 'Z10-02',
    'Z02-04', 'Z04-M01', 'Z07-M01', 'Z08-M01', 'Z02-01', 'Z03-15', 'Z05-06', 'Z07-M02',
    'Z02-13', 'Z06-11', 'Z07-17', 'Z01-08', 'Z01-09', 'Z02-10', 'Z02-11', 'Z11-10', 'Z11-14'],
  SHELL: ['Z01-13', 'Z02-03', 'Z01-03', 'Z12-03', 'Z04-30', 'Z01-04', 'Z11-01', 'Z11-04',
    'Z12-04', 'Z12-05', 'Z12-06', 'Z12-07', 'Z12-08', 'Z12-09', 'Z12-M01', 'Z01-M03', 'Z12-M02',
    'Z01-20', 'Z01-06', 'Z01-18', 'Z02-12', 'Z08-01'],
  I18N: ['Z05-01', 'Z12-01', 'Z12-02', 'Z12-10', 'Z12-12'],
  LISTS: ['Z03-04', 'Z03-06', 'Z03-09', 'Z03-10', 'Z03-13', 'Z12-11', 'Z02-M03', 'Z02-06',
    'Z02-M02', 'Z02-M04', 'Z02-09', 'Z02-15', 'Z02-M01', 'Z02-08', 'Z02-07', 'Z02-14',
    'Z07-04', 'Z07-11', 'Z07-14', 'Z07-25', 'Z07-M04'],
  DETAIL: ['Z06-01', 'Z07-15', 'Z01-15', 'Z05-M01', 'Z06-08', 'Z07-16', 'Z06-14', 'Z07-M03',
    'Z06-06', 'Z07-23', 'Z07-22', 'Z12-M03'],
};
// Rune list findings belong to the list machinery owner.
const RUNE_LIST = /runes? (list|grid|card)|filter rail|copy link|search placeholder|results count|count plate|facet|filter sheet|version without runes|mobile side gutter/i;

const WAVE = { API: 1, DS: 1, SHELL: 1, I18N: 1 };
const byId = new Map();
for (const [wp, ids] of Object.entries(OVERRIDE)) for (const id of ids) byId.set(id, wp);

const wps = {};
for (const x of f) {
  let wp = byId.get(x.id) ?? ZONE_DEFAULT[x.zone.slice(0, 3)];
  if (wp === 'RUNES' && RUNE_LIST.test(x.title) && !/detail|constellation|hero|slot label|pager/i.test(x.title)) wp = 'LISTS';
  if (wp === 'RUNES' || wp === 'ITEMDET') wp = 'ENTITYDET';
  const needsApi = x.owners.includes('API');
  x.wp = wp;
  (wps[wp] ??= []).push(x);
  if (needsApi && wp !== 'API') (wps.API ??= []).push({ ...x, wp: 'API', apiPartOnly: true, frontOwner: wp });
}
fs.mkdirSync('wp', { recursive: true });
for (const [wp, list] of Object.entries(wps)) {
  fs.writeFileSync(`wp/${wp}.json`, JSON.stringify(list, null, 1));
  const sev = list.reduce((a, x) => ((a[x.severity] = (a[x.severity] ?? 0) + 1), a), {});
  console.log(`${wp.padEnd(10)} wave ${WAVE[wp] ?? 2}  ${String(list.length).padStart(3)}  ${JSON.stringify(sev)}`);
}
