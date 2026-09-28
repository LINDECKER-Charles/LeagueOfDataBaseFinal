export const meta = {
  name: 'front-parity-audit-2',
  description: 'Second parity audit of the integrated front against the legacy site: 6 page groups, each audited then adversarially verified, at most 3 agents at once, read-only',
  phases: [
    { title: 'Audit', detail: 'one auditor per page group: previous findings, reviewer residuals, fresh comparison' },
    { title: 'Verify', detail: 'one adversarial verifier per group' },
  ],
}

const S = 'C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp'
const MAX_AGENTS = 3

let running = 0
const waiting = []
async function limited(run) {
  if (running >= MAX_AGENTS) await new Promise((resolve) => waiting.push(resolve))
  running++
  try {
    return await run()
  } finally {
    running--
    const next = waiting.shift()
    if (next) next()
  }
}

const RULES = `
CONTEXT. The new front of LeagueOfDataBase (Angular 22 SSR, src/LoDb.Web) was realigned on the LEGACY
front (Symfony/Twig/Vue, app/), which is the TARGET: look, layout, content, interactions, responsive
behaviour. A first audit found 339 verified gaps; two waves of fixes are now merged into the branch
docs/reecriture-dotnet-angular (commit bcaab425) and deployed on the integration stack. You audit the
RESULT. Deliberate divergences that are NOT gaps: the locale prefix and id-slug URLs, no server
session, form fields of at least 16 px (iOS), logical CSS properties (RTL support the legacy lacked),
localized chrome where the legacy was English-only, better accessibility than the legacy when the
look is unchanged.

READ-ONLY AUDIT: never edit, commit, build or run tests in any checkout; never restart or rebuild any
container. You only look, measure and report.

STACKS.
- Legacy (target): http://localhost:8080 (APP_ENV=dev: hide .sf-toolbar, .sf-minitoolbar,
  [id^="sfwdt"] in captures; first hits are slow, up to a minute).
- New (integrated): http://localhost:18080 (API :18081). Source: F:/Git/LeagueOfDataBaseFinal
  (src/LoDb.Web), legacy source: F:/Git/LeagueOfDataBaseFinal/app. Use git grep to search the code,
  NEVER grep -r (node_modules junctions on a slow spinning drive).

TOOLS.
- Page pairs: ${S}/pairs.json (legacy path -> new path). Capture:
  nice -n 10 node ${S}/capture.mjs <outDir> <pairs.json> 1440,390 [names]
  writes <name>-<width>-<old|new>.png/.txt; Read the PNGs side by side. Put everything under
  ${S}/audit2/<group>/.
- Your own Playwright scripts for states, computed styles, other widths (1024, 768, 320), themes
  (data-theme / the theme picker), dark/light, locales (fr, de, ar RTL, ja): load Playwright as
  capture.mjs does (createRequire on F:/Git/LeagueOfDataBaseFinal/tests/LoDb.E2E/package.json), run
  them with nice -n 10, ONE browser at a time, closed in a finally block.
- The first audit's scratch files and scripts, reusable: ${S}/zones/<zone>/ ; its captures of the
  pre-fix new stack: ${S}/shots/.

RESOURCES (the user's machine is shared; an earlier run froze it): one browser at a time, no heavy
command, no background process left when you return. A League of Legends match may start at any
time: capture.mjs then waits by itself; your own scripts should check it too (tasklist
/FI "IMAGENAME eq League of Legends.exe") and wait while it runs.
`

const FINDINGS_SCHEMA = {
  type: 'object',
  properties: {
    group: { type: 'string' },
    previous: {
      type: 'array',
      description: 'every blocker and major of the previous list, plus the minors/polish you re-checked',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          status: { type: 'string', enum: ['fixed', 'still-open', 'regressed', 'not-checked'] },
          note: { type: 'string' },
        },
        required: ['id', 'status', 'note'],
      },
    },
    findings: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string', description: '<group>2-NN' },
          severity: { type: 'string', enum: ['blocker', 'major', 'minor', 'polish'] },
          kind: { type: 'string', enum: ['still-open', 'regression', 'new', 'residual'] },
          previousId: { type: 'string' },
          page: { type: 'string' },
          title: { type: 'string' },
          legacy: { type: 'string', description: 'what the legacy does, with file:line' },
          next: { type: 'string', description: 'what the new front does, with file:line' },
          evidence: { type: 'string', description: 'capture paths, measurements' },
          fix: { type: 'string', description: 'concrete fix, files to touch' },
          files: { type: 'array', items: { type: 'string' } },
        },
        required: ['id', 'severity', 'kind', 'page', 'title', 'legacy', 'next', 'evidence', 'fix', 'files'],
      },
    },
    notes: { type: 'string' },
  },
  required: ['group', 'previous', 'findings', 'notes'],
}

const VERDICT_SCHEMA = {
  type: 'object',
  properties: {
    group: { type: 'string' },
    verdicts: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          verdict: { type: 'string', enum: ['confirmed', 'amended', 'refuted'] },
          severity: { type: 'string', enum: ['blocker', 'major', 'minor', 'polish'] },
          reason: { type: 'string' },
          fix: { type: 'string', description: 'the fix to apply (amended or confirmed)' },
          files: { type: 'array', items: { type: 'string' } },
        },
        required: ['id', 'verdict', 'severity', 'reason', 'fix', 'files'],
      },
    },
    missed: {
      type: 'array',
      description: 'clear gaps the auditor missed that you saw while verifying (same shape as a finding)',
      items: { type: 'object' },
    },
  },
  required: ['group', 'verdicts', 'missed'],
}

const MEMBER = `ACCOUNTS: F:/Git/lodb-parite/COMPTES-LOCAUX.txt (local dev test accounts; never copy a password
into your output or a file). Member of each stack: never delete it, never change its password or
e-mail; legacy sign-in through /login (it owns build id 6, share token in the same file); new sign-in
through /en/account/login. Never use the register or contact forms (5/hour quotas).`
const ADMIN = `ACCOUNTS: legacy admin = ADMIN_LOGIN / ADMIN_PASSWORD of F:/Git/LeagueOfDataBaseFinal/.env (never copy
them into your output), /admin/login. New admin: F:/Git/lodb-parite/COMPTES-LOCAUX.txt, authenticator key
${S}/zones/Z11-admin/new-shared-key.txt, sign-in script ${S}/zones/Z11-admin/auth.mjs (storage
states may have expired: sign in again). The admin is French only; compare in French. Never ban,
delete or purge anything.`

const GROUPS = [
  { id: 'A', title: 'Shell, design system and i18n (header, footer, bottom nav, context switcher, toasts, themes, dark/light, locales, RTL, 320 px)',
    pages: 'home, home-fr, home-light, champions, about, legal-notice, notfound, plus the chrome on every other page you visit',
    residuals: [
      'On /en/?version=<old patch> the header Home link loses aria-current and its lit colour (also on the pre-fix stack); the legacy keeps it.',
      "The ar root catalogue's common.no_result.back is '← رجوع': on an RTL page the arrow should point the other way.",
      "The 'runes' i18n scope is dead: public/i18n/runes/*.json are {} in 21 locales but runes-page.ts still provides the scope (one useless fetch per locale).",
      'Prose fragments (e.g. /en/legal/notice#publisher, cross-page routerLink fragments) land at 0 on direct load instead of below the sticky header (rune path pages were fixed locally).',
      'Initial bundle 684 kB (warning budget 500 kB): report what grew in wave 2 if you can tell (not a parity gap by itself).',
    ] },
  { id: 'B', title: 'Home page and the four catalogue lists (cards, filter console, mobile sheet, empty states, version-pinned list)', accounts: '',
    pages: 'home, home-fr, home-light, champions, items, runes, summoners, champions-v14; also /en/items?q=zzzzqq, /en/runes?path=Domination, /ar/runes, /en/7.20.1/runes, 1024/768/320 widths, noxus/zaun themes',
    residuals: [
      "/en/items logs 'TypeError: Cannot set properties of null (setting __ngContext__)' on load (a hydration error, also on the pre-fix stack); E2E items.spec 'lead from a card to its item, then back to the list', navigation.spec 'filters of /en/items' and catalogue/filters.spec fail because of it (the card click stays on /en/items). Find the root cause (file:line) and the fix.",
      "RTL: the rail gauge '12 / 62' reads '62 / 12' in ar (/ar/runes?path=Domination): needs dir=ltr or unicode-bidi isolate.",
      'Filtered empty panel (/en/items?q=zzzzqq) starts 16 px higher than the legacy.',
      'DRY: features/catalogue/shared/list/catalogue-empty.ts and ui/surfaces/empty-state.ts render the same framed empty state; propose which one stays.',
      'Page weight: portraits, blurbs and item descriptions make the lists heavier; measure the HTML and transfer size of /en/champions and /en/items against the legacy.',
    ] },
  { id: 'C', title: 'Detail pages: champion (Ahri, FiddleSticks, Aphelios, an old version), item (3031, 6672, 1035, 771004), summoner spell (SummonerFlash and _Jade), rune path (all five)',
    pages: 'champion-ahri, champion-ahri-fr, item-3031, item-771004, rune-domination, summoner-flash; plus the pagers, skins lightbox, chroma viewer, load-time badge, 390 and 320 widths, ar',
    residuals: [
      "Plate labels (.hx-plate__label, overflow-wrap:normal + hyphens:auto) let an unhyphenatable word cross the plate border at 320 px in pl, ro, id and tr (item aside and summoner plaques).",
      'The summoner eyebrow is 12 px wider than the legacy one (275 vs 263 px) and the pip sits 6 px to the left.',
      'Between 360 and 400 px a pager neighbour with a very long single word overflows and is clipped (pl YuumiBot_Wersja_OSTATECZNA on /pl/items/9273): the legacy does the same; judge whether to keep.',
    ] },
  { id: 'D', title: 'Editorial pages: about, about/data, faq, changelog, developers, donate, the four legal pages, 404 and failure pages',
    pages: 'about, about-data, faq, changelog, developers, donate, legal-notice, legal-privacy, legal-terms, legal-cookies, notfound',
    residuals: [
      'FragmentLink and SectionNav call preventDefault + scrollIntoView: focus stays on the clicked link instead of moving to the target (a11y; the legacy used native anchors).',
      'The cookie policy still lists PHPSESSID although the new stack has no server session.',
    ] },
  { id: 'E', title: 'Account and builds: sign-in, register, forgot/reset password, account editor, public profile, API portal, trends, my builds, build editor with the armory, shared build', accounts: MEMBER,
    pages: 'login, register, forgot, trends; signed in: account editor and its pickers, public profile, API portal, my builds, the build editor (new and existing build), the shared build /b/{token}; phones 390 and 320, ar',
    residuals: [
      'An item cannot be dropped onto an EMPTY step of the build editor (its CDK list is 0 px wide); the legacy accepted a drop anywhere on the step card.',
      "RTL: the '2 / 8' counters (step hint, armory footer) read '8 / 2' in ar.",
      'The build editor lives under /{locale}/account/** (RenderMode.Client): the legacy <noscript> message build.editor.nojs cannot come from a server render; decide where it can live.',
      'An expired or used reset link is refused only on submit (the legacy refused it on open): note it, the fix needs an API endpoint.',
      'CSS budget warnings: item-armory.css 5.61 kB and step-editor.css 4.06 kB over the 4 kB warning (8 kB is the error).',
    ] },
  { id: 'F', title: 'Administration (French): overview, analytics panels, storage, moderation of users and builds, contacts, donations, API clients, monitoring, audit journal, sign-in and enrolment', accounts: ADMIN,
    pages: 'every admin panel, at 1440 and 390',
    residuals: [
      'The monitoring generation chip reads 27/09/2026 19:49:11 (UTC, no suffix) where the legacy reads 2026-09-27 19:48:56 UTC.',
      'contacts.spec.ts was never run (contact quota); the contacts table could not be compared (no contact message on the new stack): compare the empty state and headings at least.',
    ] },
]

const auditPrompt = (g) => `${RULES}
${g.accounts ?? ''}

YOUR GROUP: ${g.id} — ${g.title}
Pages: ${g.pages}.

1. PREVIOUS FINDINGS: ${S}/audit2/previous-${g.id}.json lists the gaps the first audit found in your
   area, with what each fix claimed (fixStatus, fixNote). Re-check on the integrated stack EVERY
   blocker and major, and every entry whose fixStatus is not "fixed"; re-check minors and polish
   where your captures show them. Report each in "previous" (fixed / still-open / regressed); every
   still-open or regressed one ALSO becomes a finding (kind still-open or regression).
2. RESIDUALS the reviewers left (kind "residual" when confirmed, with root cause and fix):
${g.residuals.map((r) => `   - ${r}`).join('\n')}
3. FRESH COMPARISON: compare every page of your group, legacy vs new, at 1440 and 390 (and 1024, 768,
   320 where layout changes), dark and light, en/fr/ar, and the interactions (hover, focus, open
   states, dialogs, empty states). The fixes of two waves were merged together: hunt merge
   regressions (a page that now combines two packages' changes), console errors, broken links and
   layout overflow. Only report real differences against the legacy (or real defects), with
   evidence.

Severity: blocker = broken or unusable; major = clearly visible divergence or functional gap;
minor = noticeable detail; polish = pixel-level. For each finding give the legacy source
(file:line), the new source (file:line) and a concrete, verified fix.`

const verifyPrompt = (g, audit) => `${RULES}
${g.accounts ?? ''}

You are the ADVERSARIAL VERIFIER of the second audit, group ${g.id} — ${g.title}. The auditor reported:
${JSON.stringify(audit)}

For EACH finding: reproduce it yourself on both stacks (fresh captures or measurements), check the
cited sources, and try to refute it: not reproducible, the legacy does the same, a deliberate
divergence (see CONTEXT), or a wrong root cause. Verdict confirmed / amended (right gap, better fix or
severity) / refuted, with the fix to apply and the files. Also spot-check at least five "previous"
entries the auditor marked fixed. If you see a clear gap the auditor missed, add it to "missed".`

const results = await pipeline(
  GROUPS,
  (g) => limited(() => agent(auditPrompt(g), { label: `audit:${g.id}`, phase: 'Audit', schema: FINDINGS_SCHEMA })),
  (audit, g) => {
    if (!audit) return null
    log(`audit:${g.id} -> ${audit.findings.length} findings, ${audit.previous.filter((p) => p.status !== 'fixed').length} previous not fixed`)
    if (audit.findings.length === 0) return { group: g.id, audit, verify: null }
    return limited(() => agent(verifyPrompt(g, audit), { label: `verify:${g.id}`, phase: 'Verify', schema: VERDICT_SCHEMA }))
      .then((verify) => ({ group: g.id, audit, verify }))
  },
)
return results.filter(Boolean)
