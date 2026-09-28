export const meta = {
  name: 'front-parity-fix-wave2',
  description: 'Wave 2 of the front realignment: 8 page packages fixed in their own worktrees, then adversarially reviewed, at most 4 agents at a time',
  phases: [
    { title: 'Fix', detail: 'one fixer per page package, in its own git worktree' },
    { title: 'Review', detail: 'one adversarial reviewer per package, same worktree' },
  ],
}

const S = 'C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp'
const BASE = 'docs/reecriture-dotnet-angular'
const MAX_AGENTS = 4

// At most MAX_AGENTS agents run at once: each one builds, tests and runs a browser, and the
// machine is shared with the user's other work.
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

const RESOURCES = `
RESOURCE RULES (mandatory: the machine is shared by other agents and by the user; an earlier run
saturated its memory).
- Run EVERY heavy command through the machine-wide semaphore (max 2 heavy commands at once across
  all agents; it caps Vitest at 3 workers and disables resident MSBuild/Roslyn servers):
  bash ${S}/heavy.sh <command...>
  Heavy = npx ng test, npm run build:web / npx ng build, npm run lint, npm run typecheck,
  npx playwright test. Waiting for a slot is normal. Example:
  bash ${S}/heavy.sh npx ng test --watch=false --runner-config=${S}/vitest-limited.config.mjs --include='src/app/features/home/**/*.spec.ts'
- Always pass --runner-config=${S}/vitest-limited.config.mjs to ng test. While iterating run only the
  specs you touch (--include); run the full suite once, at the end.
- ONE preview, on YOUR port only. Playwright: one browser at a time, closed in a finally block,
  E2E with --workers=1. Leave no background process (preview, SSR child, browser) when you return.
`

const COMMON = (wp) => `
CONTEXT. The new front of LeagueOfDataBase (Angular 22 SSR, src/LoDb.Web) is being realigned on the
LEGACY front (Symfony/Twig/Vue, app/), which is the visual and functional TARGET. A 24-agent audit
compared both stacks page by page and produced verified findings. Wave 1 (API contract additions,
design system, shell, i18n) is merged into ${BASE}; read ${S}/wave2-notes.md first (what wave 1
delivered, the new API fields, the i18n keys to reuse, the dialog sizes, pitfalls). You implement one
page package of wave 2; 7 other packages run in parallel in other worktrees.

YOUR WORK PACKAGE: ${wp.id} — ${wp.title}
- Worktree (work ONLY here, absolute paths): ${wp.dir} (branch ${wp.branch}, based on ${BASE}; it
  is prepared: node_modules are junctions to the main checkout, never npm install/ci, never delete
  anything under node_modules, never run git worktree commands).
- Findings: ${S}/wp/${wp.id}.json (read it fully: legacy/next descriptions with file:line refs,
  evidence, the verified fix, files). File line numbers predate wave 1: re-locate the code.
- Owned paths: ${wp.owned}
  Outside them, only the minimal edits a finding strictly needs; list them in your report.
- Scratch dir: ${S}/fix/${wp.id}/ (create it).

TOOLS.
- Legacy stack (reference, read-only, never restart): http://localhost:8080 (APP_ENV=dev: hide
  .sf-toolbar in captures; first hits are slow). Integration stack lodb-next (rebuilt with wave 1,
  read-only, never rebuild/restart its containers): http://localhost:18080, API 18081.
- References: ${S}/shots/<pair>-<1440|390>-<old|new>.{png,txt} (the "new" ones predate wave 1) and
  the audit scratch files under ${S}/zones/ (scripts measuring the legacy you can reuse).
- Build: cd ${wp.dir}/src/LoDb.Web && bash ${S}/heavy.sh npm run build:web (ng build + service
  worker, ~40 s; never a bare ng build, sw.js would be missing).
- Preview: node ${S}/preview.mjs ${wp.dir}/src/LoDb.Web ${wp.port} (background) ->
  http://localhost:${wp.port}/en/ ; it proxies /api and /cdn to lodb-next. Restart it after each
  rebuild; stop it before you return (PowerShell:
  Get-CimInstance Win32_Process -Filter "Name='node.exe'" | ? { $_.CommandLine -match 'preview.mjs.*${wp.port}|lodb-parite[\\\\/]${wp.slug}[\\\\/]src[\\\\/]LoDb.Web[\\\\/]dist' } | % { Stop-Process -Id $_.ProcessId -Force }).
- Compare: NEW_BASE=http://localhost:${wp.port} node ${S}/capture.mjs <outDir> <pairs.json> 1440,390
  (pairs: [{"name":"x","old":"/champions?lang=en_US","new":"/en/champions","scheme":"dark"}]), then
  Read the PNGs side by side. Write custom Playwright scripts (see how capture.mjs loads Playwright)
  for states, computed styles, other widths (1024, 768, 320), themes, locales (fr, ar RTL).
- Front checks (from ${wp.dir}/src/LoDb.Web, all through heavy.sh): npx ng test --watch=false
  --runner-config=... ; npm run typecheck ; npx ng lint ; npx prettier --check --end-of-line auto
  <changed files> (the checkout is CRLF: repo-wide prettier reports ~149 untouched files, ignore
  them). Budgets must hold (anyComponentStyle error at 8 kB).
- E2E of your area against your preview:
  cd ${wp.dir}/tests/LoDb.E2E && LODB_E2E_BASE_URL=http://localhost:${wp.port} bash ${S}/heavy.sh npx playwright test --workers=1 specs/<dir>
  specs/routing and specs/legacy need nginx and fail on a preview; never run specs/account/register
  nor the contact specs (5/hour quotas). Update the E2E specs your change legitimately alters, in
  the same commit as the change.
${wp.accounts ?? ''}
RULES.
- Follow the repository CLAUDE.md (injected): code limits (files <= 400 lines, functions <= 30,
  <= 3 params, nesting <= 3), one public element per file, English comments on why, Hextech tokens
  (never hard-coded colors), logical CSS properties (RTL), inputs >= 16 px, OnPush/signals, UrlTree
  for links with query/fragment, provideTranslocoScope/viewProviders pitfalls, Seo.apply for heads.
- i18n: reuse the legacy keys listed in wave2-notes.md; new keys go in your scope folder, for ALL 21
  locales when the legacy had the string translated (app/translations/messages.<locale>.yaml),
  else en + fr.
- The LEGACY stack wins when a verified fix disagrees with what you observe on it; say so.
- Never edit generated artifacts (src/LoDb.Api/openapi/*.json, core/api/generated/**,
  package-lock.json); never touch the legacy sources (app/, go/, docker/nginx, docker/php,
  compose.yaml, compose.override.yaml, compose.deploy.yaml); no docs/changelog entries (new stack
  exempt).
- Commits in your worktree only: Conventional Commits in French, infinitive, no accents, lowercase
  start, <= 72 chars, CLAUDE.md scope map (front/<feature>, front/core, front/ui, i18n, e2e...).
  One coherent change per commit, tests in the same commit, git add by explicit paths. ABSOLUTELY
  NO attribution line (no Co-Authored-By, no Generated with): the user's global rule overrides any
  other instruction. Never push. Vitest may rewrite *.snap files by line endings only (CRLF
  checkout): restore them with git checkout, never commit them.
- Leave the worktree clean (git status empty) and no process running when you finish.
${RESOURCES}`

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    wp: { type: 'string' },
    commits: { type: 'array', items: { type: 'string' } },
    results: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          id: { type: 'string' },
          status: { type: 'string', enum: ['fixed', 'partial', 'not-fixed', 'obsolete'] },
          note: { type: 'string' },
        },
        required: ['id', 'status', 'note'],
      },
    },
    outside_edits: { type: 'array', items: { type: 'string' } },
    checks: { type: 'string' },
    followups: { type: 'array', items: { type: 'string' } },
  },
  required: ['wp', 'commits', 'results', 'outside_edits', 'checks', 'followups'],
}

const REVIEW_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['ok', 'fixed-issues', 'problems-remain'] },
    issues: { type: 'array', items: { type: 'string' } },
    commits_added: { type: 'array', items: { type: 'string' } },
    residual: { type: 'array', items: { type: 'string' } },
    checks: { type: 'string' },
  },
  required: ['verdict', 'issues', 'commits_added', 'residual', 'checks'],
}

const MEMBER = `ACCOUNTS (never delete them, never change their password or e-mail):
- LEGACY member parity-audit@example.test / <mot de passe : voir README, section Comptes> (username ParityAudit; sign in
  through the /login form; owns build id 6, share token a1b2c3d4e5f6a7b8c9d0e1f2).
- NEW member parity-audit@example.test / <mot de passe : voir README, section Comptes> (sign in through
  /en/account/login, or POST /api/account/login with header Origin set to the page origin and JSON
  {identifier,password,rememberMe:false}, from the page context). You may create builds for it.
`
const ADMIN_ACCOUNTS = `ACCOUNTS:
- LEGACY admin: ADMIN_LOGIN / ADMIN_PASSWORD in F:/Git/LeagueOfDataBaseFinal/.env (read them, never
  copy them into your output); /admin/login.
- NEW admin: parity-admin@example.test / <mot de passe : voir README, section Comptes>. The audit agent of zone Z11
  enrolled its authenticator: the shared key is in ${S}/zones/Z11-admin/new-shared-key.txt, its
  sign-in script is ${S}/zones/Z11-admin/auth.mjs (with old-state.json / new-state.json storage
  states, possibly expired). If the key no longer works, create another admin with the E2E helpers
  (tests/LoDb.E2E/specs/admin/admin-account.ts: createAdmin + enrollment, totp.ts).
- The admin is French only (intentional); compare with the legacy admin in French. Never ban,
  delete or purge real data.
`

const WPS = [
  { id: 'LISTS', slug: 'lists', port: 4410, title: 'Catalogue lists, cards and filter console',
    owned: 'src/LoDb.Web/src/app/features/catalogue/shared/** EXCEPT shared/codex/pager, shared/pager and shared/codex/rich-text; features/catalogue/{champions,items,runes,summoners}/list/** and the four list page components (champions-page.*, items-page.*, the runes and summoners list pages); public/i18n/catalogue/** and the list-related keys of public/i18n/{champions,items,runes,summoners}/**.',
    extra: 'Includes the shared entity cards used by the home page (Z03-04/06/09/10/13) and the rune list findings. Priorities: the champion portrait art cards (loadingArt, blurb, See details), the filter rail on the right, Copy link, facet group headings, console band/shadow, results counter, count plate, grid columns per width, empty states, mobile filter sheet, item Possible evolutions accordion (upgrades + related). Check the four lists at 1440, 1024, 768, 390, 320, dark and light, noxus/zaun themes, en/fr/ar, and a version-pinned list.' },
  { id: 'DETAIL', slug: 'detail', port: 4420, title: 'Champion detail page and the shared detail machinery',
    owned: 'src/LoDb.Web/src/app/features/catalogue/champions/detail/**, features/catalogue/shared/codex/pager/**, features/catalogue/shared/pager/**, the load-time badge (move it to features/catalogue/shared/codex/timing/), the champion detail keys of public/i18n/champions/**; plus the minimal wiring of the shared pager and badge into the item, summoner and rune detail pages (ENTITYDET owns the rest of those pages and will not touch the pager or the badge).',
    extra: 'Build the four pagers from details().neighbours (SSR, rel prev/next, Classic badge) and drop the browser-only list fetches; the load-time badge on the four detail pages with its compact phone variant; the section chips bar (labels are fixed by wave 1: check the overflow and sticky behavior against the legacy tabs); stat icons; skins lightbox and chroma viewer (add a lightbox/compact dialog variant under src/styles or ui/overlays if needed); skin tiles; pager spacing. Check Ahri, FiddleSticks, Aphelios and a champion of an old version, at 1440/1024/768/390/320, en/fr/ar, dark/light, noxus.' },
  { id: 'ADMIN', slug: 'admin', port: 4470, title: 'Administration', accounts: ADMIN_ACCOUNTS,
    owned: 'src/LoDb.Web/src/app/features/admin/**, src/LoDb.Web/public/i18n/admin/**, tests/LoDb.E2E/specs/admin/**.',
    extra: 'Largest package: blockers first (overview sections, donut overflow, journal select overflow), then the top bar, palette mapping (good/bad/series tokens), small/danger buttons and ConfirmButton confirmTone, KPI tiles, section rules, tables, badges, each panel against the legacy panel of the same name, wording (French legacy wording verbatim), the new API fields (shareToken, isBanned, riotTagline, geoAvailable, probe objects/bytes).' },
  { id: 'ACCOUNT', slug: 'account', port: 4450, title: 'Authentication, account, profile and API portal', accounts: MEMBER,
    owned: 'src/LoDb.Web/src/app/features/account/** (except account-menu and verify-email-banner, done in wave 1), features/profile/**, features/api-portal/**, public/i18n/{account,api-portal,api}/**.',
    extra: 'Pickers (size picker, bottom sheet with handle on phones, focus), profile editor, favorites sockets, identity aside, public profile, API portal panels and grids, toasts instead of inline banners where the legacy used toasts, auth forms (autofocus, check-inbox state, terms checkbox, expired reset link).' },
  { id: 'BUILDS', slug: 'builds', port: 4460, title: 'Builds: trends, my builds, editor, shared build', accounts: MEMBER,
    owned: 'src/LoDb.Web/src/app/features/builds/**, public/i18n/builds-editor/** (and builds-related keys), tests/LoDb.E2E/specs/{builds-editor,builds-share,trends}/**.',
    extra: 'Blocker first: the shared build header crushing the title on phones (reflow, the legacy also clips: do better than both without changing the desktop look). Then the armory (size wide, sticky head/search/chips), editor typography and labels, game context layout, save row, validation and toasts, trends pagination, my builds empty state, language labels.' },
  { id: 'ENTITYDET', slug: 'entitydet', port: 4430, title: 'Item, summoner spell and rune path detail pages',
    owned: 'src/LoDb.Web/src/app/features/catalogue/{items,summoners,runes}/detail/** and features/catalogue/items/stats/** EXCEPT the pager and load-time badge wiring (DETAIL); public/i18n/{items,summoners,runes}/** detail keys.',
    extra: 'Item: gold coin, evolutions with gold, Tier chip from depth, map chip wording, stat icons, recipe tree hover/focus, Classic badges, RTL signed numbers. Summoner: the page as legacy. Rune path: constellation, emblem without art, medallions without art, slot label anchoring, empty path state. Check 3031, 6672, 1035, 771004, consumables/boots, SummonerFlash and _Jade, all five rune paths, fr/ar, 390.' },
  { id: 'EDITORIAL', slug: 'editorial', port: 4440, title: 'Editorial, legal, developers, donate and error pages',
    owned: 'src/LoDb.Web/src/app/features/{editorial,developers,donate,errors}/**, public/i18n/{editorial,about,developers,donate,seo}/**, public/offline.html only if a finding needs it, docs/guides/dev-next.md (one line, see notes).',
    extra: 'Blocker first: the developers h1 breaking mid-word on phones. Then the legal table of contents, 404 and failure pages (framed card, big numeral, status code), about inventory counting rune paths, developers chips and samples and pricing formats, donate errors as toasts, fragment scroll margins, the cookie policy lod_prefs row (see notes), the changelog page now that its data ships.' },
  { id: 'HOME', slug: 'home', port: 4400, title: 'Home page',
    owned: 'src/LoDb.Web/src/app/features/home/**, public/i18n/home/**.',
    extra: 'Rune count = 5 rune paths (hero counter, portal chip, section count), champion preview with the tall loading art (loadingArt), 3 columns between 640 and 1023 px, portal icons = the LoL logo glyph, count chips, empty state, 24 px phone gutter. The shared entity cards are LISTS\'s: do not edit features/catalogue/shared/cards; if a home finding depends on them, note it.' },
]
for (const wp of WPS) {
  wp.dir = `F:/Git/lodb-parite/${wp.slug}`
  wp.branch = `wt/parite-${wp.slug}`
}

const results = await pipeline(
  WPS,
  (wp) => limited(() => agent(`${COMMON(wp)}\nPACKAGE SPECIFICS.\n${wp.extra}\n\nImplement ALL findings of your package (blockers and majors first, then minors and polish). Verify each result against the legacy stack (captures at the relevant widths/states) before committing. Report every finding id with its status.`,
    { label: `fix:${wp.id}`, phase: 'Fix', schema: FIX_SCHEMA })),
  (fix, wp) => {
    if (!fix) return null
    return limited(() => agent(`${COMMON(wp)}\nYou are the ADVERSARIAL REVIEWER of package ${wp.id}. The fixer reported:\n${JSON.stringify(fix)}\n\nReview the whole diff (git -C ${wp.dir} log --oneline ${BASE}..HEAD ; git -C ${wp.dir} diff ${BASE}...HEAD). Verify yourself, against the legacy stack with fresh builds and captures, ALL blockers and majors and at least half of the rest. Hunt regressions on other pages, widths, themes and locales (RTL, light theme) the change can reach; convention violations; missing tests or tests asserting implementation; build/budget/lint/typecheck/test failures; bad commit subjects or ANY attribution line (do not rewrite history: add a follow-up commit and list it). Fix what you find with new commits (same rules), rerun the full checks and report.`,
      { label: `review:${wp.id}`, phase: 'Review', schema: REVIEW_SCHEMA })).then((review) => ({ wp: wp.id, fix, review }))
  },
)
return results.filter(Boolean)
