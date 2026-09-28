export const meta = {
  name: 'front-parity-wave2-resume',
  description: 'Resume wave 2 of the front realignment: finish ENTITYDET/HOME/EDITORIAL fixes, adversarially review all 8 packages, at most 3 agents at once',
  phases: [
    { title: 'Fix', detail: 'resume the interrupted fixers (ENTITYDET, HOME) and run EDITORIAL' },
    { title: 'Review', detail: 'one adversarial reviewer per package, in its worktree' },
  ],
}

const S = 'C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp'
const BASE = 'docs/reecriture-dotnet-angular'
const MAX_AGENTS = 3
const PUBLISHED = 'src/LoDb.Web/src/app/features/editorial/changelog/published'

// The machine is shared with the user's own work (a game client is open): a previous run with
// 4 agents saturated it. FIFO limiter, so the queue order below is the start order.
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
RESOURCE RULES (mandatory; the previous run saturated the user's machine and was stopped for it;
the user is using the machine right now).
- EVERY heavy command goes through the machine-wide semaphore: bash ${S}/heavy.sh <command...>
  It allows ONE heavy command at a time across all agents, waits while free RAM < 12 GB, runs at
  below-normal priority and caps Vitest (3 workers), the Angular build (4 workers), esbuild and
  MSBuild. Heavy = npx ng test, npm run build:web / npx ng build, npm run lint / npx ng lint,
  npm run typecheck, npx playwright test, dotnet build/test. Waiting for the slot is normal:
  never bypass it, never run two heavy commands in parallel yourself.
- ng test always with --runner-config=${S}/vitest-limited.config.mjs; while iterating run only
  the specs you touch (--include=...); the full front suite ONCE, at the end.
- Capture scripts (node ${S}/capture.mjs, your own Playwright scripts): prefix with nice -n 10,
  one browser at a time, closed in a finally block. E2E: --workers=1, only the specs of your area.
- At most ONE preview (your port only), started when you need to look at pages, stopped as soon as
  you no longer need it and in any case before you return. No other background process.
- Do not rebuild or restart any Docker container.
`

const GIT_RULES = `
GIT DISCIPLINE (the previous run broke some of these; they are strict).
- Stage by explicit paths only: never git add -A, git add ., git add <directory>, git commit -a.
  Before every commit, read git diff --cached --stat and check every path is one you meant.
- ${PUBLISHED} must NEVER be staged: it is a symlink in git, replaced on disk by an NTFS junction
  to app/public/changelog and hidden with skip-worktree. A previous agent committed its 18 JSON
  files by mistake. If git status ever shows it, run
  git update-index --skip-worktree ${PUBLISHED} and never stage anything under it.
- No work-in-progress or "sauvegarder" commits: one commit = one coherent change, verified. If
  you run out of time, leave changes uncommitted and say so in the report.
- Never: git stash, git reset --hard, git checkout -- . / git restore ., git clean, git rebase,
  git worktree ..., branch switches, push, amend of a commit you did not create in this run.
- Never touch the node_modules junctions (src/LoDb.Web/node_modules, tests/LoDb.E2E/node_modules):
  no npm install/ci, no deletion under them.
- Leave the worktree clean (git status empty) when you finish.
`

const COMMON = (wp) => `
CONTEXT. The new front of LeagueOfDataBase (Angular 22 SSR, src/LoDb.Web) is being realigned on the
LEGACY front (Symfony/Twig/Vue, app/), which is the visual and functional TARGET (look, layout,
content, interactions, responsive behaviour). The ADR divergences stay (locale prefix, id-slug URLs,
no server session). A page-by-page audit produced verified findings; wave 1 (API contract
additions, design system, shell, i18n) is merged into ${BASE}. Wave 2 packages each live in their
own worktree; this run RESUMES wave 2, which was interrupted. Read ${S}/wave2-notes.md first (what
wave 1 delivered, the new API fields, the i18n keys to reuse, dialog sizes, pitfalls).

YOUR WORK PACKAGE: ${wp.id} — ${wp.title}
- Worktree (work ONLY here, absolute paths): ${wp.dir} (branch ${wp.branch}, based on ${BASE}).
- Findings: ${S}/wp/${wp.id}.json (legacy/next descriptions with file:line refs, evidence, the
  verified fix, files). Line numbers predate wave 1: re-locate the code.
- Owned paths: ${wp.owned}
  Outside them, only the minimal edits a finding strictly needs; list them in your report. Never
  edit src/LoDb.Web/src/app/app.config.ts nor app.html (each owner fills its own folder).
- Scratch dir: ${S}/fix/${wp.id}/ (earlier attempts left files there; reuse what helps).

TOOLS.
- Legacy stack (reference, read-only): http://localhost:8080 (APP_ENV=dev: hide .sf-toolbar in
  captures; first hits are slow). Integration stack lodb-next at wave 1 (read-only):
  http://localhost:18080, API 18081.
- References: ${S}/shots/<pair>-<1440|390>-<old|new>.{png,txt} (the "new" ones predate wave 1),
  ${S}/pairs.json, and the audit scratch files under ${S}/zones/ (legacy measuring scripts).
- Build: cd ${wp.dir}/src/LoDb.Web && bash ${S}/heavy.sh npm run build:web (never a bare ng build:
  sw.js would be missing).
- Preview: node ${S}/preview.mjs ${wp.dir}/src/LoDb.Web ${wp.port} (background) ->
  http://localhost:${wp.port}/en/ ; it proxies /api and /cdn to lodb-next. Restart it after each
  rebuild. Stop it (PowerShell):
  Get-CimInstance Win32_Process -Filter "Name='node.exe'" | ? { $_.CommandLine -match 'preview.mjs.*${wp.port}|lodb-parite[\\\\/]${wp.slug}[\\\\/]src[\\\\/]LoDb.Web[\\\\/]dist' } | % { Stop-Process -Id $_.ProcessId -Force }
- Compare: NEW_BASE=http://localhost:${wp.port} nice -n 10 node ${S}/capture.mjs <outDir> <pairs.json> 1440,390
  (pairs: [{"name":"x","old":"/champions?lang=en_US","new":"/en/champions","scheme":"dark"}]), then
  Read the PNGs side by side. Custom Playwright scripts (load Playwright as capture.mjs does) for
  states, computed styles, other widths (1024, 768, 320), themes, locales (fr, ar RTL).
- Front checks (from ${wp.dir}/src/LoDb.Web, through heavy.sh): npx ng test --watch=false
  --runner-config=${S}/vitest-limited.config.mjs ; npm run typecheck ; npx ng lint ;
  npx prettier --check --end-of-line auto <changed files> (CRLF checkout: repo-wide prettier reports
  ~149 untouched files, ignore them). Budgets must hold (anyComponentStyle error at 8 kB).
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
- Stay within the package findings: no redesign, no unrelated refactor.
- Never edit generated artifacts (src/LoDb.Api/openapi/*.json, core/api/generated/**,
  package-lock.json); never touch the legacy sources (app/, go/, docker/nginx, docker/php,
  compose.yaml, compose.override.yaml, compose.deploy.yaml); no docs/changelog entries (new stack
  exempt).
- Commits in your worktree only: Conventional Commits in French, infinitive, no accents, lowercase
  start, <= 72 chars, CLAUDE.md scope map (front/<feature>, front/core, front/ui, i18n, e2e...).
  Tests in the same commit as the code. ABSOLUTELY NO attribution line (no Co-Authored-By, no
  Generated with): the user's global rule overrides any other instruction. Never push. Vitest may
  rewrite *.snap files by line endings only (CRLF checkout): restore them with
  git checkout -- <that .snap file>, never commit them.
${GIT_RULES}${RESOURCES}`

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

const MEMBER = `ACCOUNTS: read F:/Git/lodb-parite/COMPTES-LOCAUX.txt (local dev test accounts; never copy a
password into a report, a commit or a file of the worktree). Use the member account of each stack:
never delete it, never change its password or e-mail. Legacy: sign in through the /login form; it
owns build id 6 (share token in the same file). New: /en/account/login, or POST /api/account/login
with header Origin set to the page origin and JSON {identifier,password,rememberMe:false}, from the
page context. You may create builds for the new member.
`
const ADMIN_ACCOUNTS = `ACCOUNTS:
- LEGACY admin: ADMIN_LOGIN / ADMIN_PASSWORD in F:/Git/LeagueOfDataBaseFinal/.env (read them, never
  copy them into your output); /admin/login.
- NEW admin: see F:/Git/lodb-parite/COMPTES-LOCAUX.txt (never copy a password into a report or a
  commit). Its authenticator shared key: ${S}/zones/Z11-admin/new-shared-key.txt, sign-in script
  ${S}/zones/Z11-admin/auth.mjs (storage states old-state.json / new-state.json, possibly expired).
  If the key no longer works, create another admin with the E2E helpers
  (tests/LoDb.E2E/specs/admin/admin-account.ts: createAdmin + enrollment, totp.ts).
- The admin is French only (intentional); compare with the legacy admin in French. Never ban,
  delete or purge real data.
`

const WPS = [
  { id: 'ENTITYDET', slug: 'entitydet', port: 4430, mode: 'resume', title: 'Item, summoner spell and rune path detail pages',
    owned: 'src/LoDb.Web/src/app/features/catalogue/{items,summoners,runes}/detail/** and features/catalogue/items/stats/** EXCEPT the pager and load-time badge wiring (DETAIL); public/i18n/{items,summoners,runes}/** detail keys.',
    extra: 'Item: gold coin, evolutions with gold, Tier chip from depth, map chip wording, stat icons, recipe tree hover/focus, Classic badges, RTL signed numbers. Summoner: the page as legacy. Rune path: constellation, emblem without art, medallions without art, slot label anchoring, empty path state. Check 3031, 6672, 1035, 771004, consumables/boots, SummonerFlash and _Jade, all five rune paths, fr/ar, 390.',
    resume: `A PREVIOUS ATTEMPT of this package was interrupted. Its unverified work is in your worktree as
UNCOMMITTED changes (66 modified files and 3 untracked folders: public/icons/, core/routing/anchor/,
features/catalogue/shared/codex/stats/); its scratch files are in ${S}/fix/ENTITYDET/. Its "save"
commit was undone because it had committed the changelog junction by mistake. Start by reading that
diff (git -C ${'${dir}'} diff ; git status), map it to the finding ids, keep what is right, fix what is
wrong or unfinished, then do the remaining findings. Specific points to settle:
- It edits src/LoDb.Web/src/app/app.config.ts (provideAnchorOffset): forbidden. Find an owner-local
  way (e.g. a scroll-margin rule in the page styles, which is what the legacy does) or drop it and
  report the finding as partial with the reason.
- It edits features/catalogue/shared/cards/{catalogue-image,edition-badge}.ts, owned by LISTS
  (branch wt/parite-lists, not merged yet). Compare with git diff ${BASE}...wt/parite-lists --
  <file> and keep your edit minimal and non-conflicting (additive inputs), or move the need into
  your own pages; list it as an outside edit.
- It edits shared ui/media/*, styles/foundation/images.css and styles/primitives/surfaces.css:
  keep only what a finding needs, justify each in outside_edits.
- New PNGs under public/icons/: check their origin (legacy app/public/icons/stats is the source),
  size and that nothing else already serves them.`.replace('${dir}', '') },
  { id: 'EDITORIAL', slug: 'editorial', port: 4440, mode: 'fresh', title: 'Editorial, legal, developers, donate and error pages',
    owned: 'src/LoDb.Web/src/app/features/{editorial,developers,donate,errors}/**, public/i18n/{editorial,about,developers,donate,seo}/**, public/offline.html only if a finding needs it, docs/guides/dev-next.md (one line, see notes).',
    extra: 'Blocker first: the developers h1 breaking mid-word on phones. Then the legal table of contents, 404 and failure pages (framed card, big numeral, status code), about inventory counting rune paths, developers chips and samples and pricing formats, donate errors as toasts, fragment scroll margins (owner-local: never app.config.ts), the cookie policy lod_prefs row (see notes), the changelog page now that its data ships. A previous start left 4 scratch files in the scratch dir and no commit.' },
  { id: 'LISTS', slug: 'lists', port: 4410, mode: 'review', title: 'Catalogue lists, cards and filter console',
    owned: 'src/LoDb.Web/src/app/features/catalogue/shared/** EXCEPT shared/codex/pager, shared/pager and shared/codex/rich-text; features/catalogue/{champions,items,runes,summoners}/list/** and the four list page components; public/i18n/catalogue/** and the list-related keys of public/i18n/{champions,items,runes,summoners}/**.',
    extra: 'Includes the shared entity cards used by the home page and the rune list findings: champion portrait art cards (loadingArt, blurb, See details), filter rail on the right, Copy link, facet group headings, console band/shadow, results counter, count plate, grid columns per width, empty states, mobile filter sheet, item Possible evolutions accordion. Check the four lists at 1440, 1024, 768, 390, 320, dark and light, noxus/zaun themes, en/fr/ar, and a version-pinned list. Also re-measure the weight of the list pages (portraits, blurbs and item descriptions): report the HTML/transfer size of /en/champions and /en/items before (lodb-next) and after (your preview).' },
  { id: 'HOME', slug: 'home', port: 4400, mode: 'resume', title: 'Home page',
    owned: 'src/LoDb.Web/src/app/features/home/**, public/i18n/home/**.',
    extra: 'Rune count = 5 rune paths (hero counter, portal chip, section count), champion preview with the tall loading art (loadingArt), 3 columns between 640 and 1023 px, portal icons = the LoL logo glyph, count chips, empty state, 24 px phone gutter. The shared entity cards are LISTS\'s: do not edit features/catalogue/shared/cards; if a home finding depends on them, note it (they land at integration).',
    resume: `A PREVIOUS ATTEMPT of this package was interrupted. It committed 54163db3 (rune path count) and
left unverified work as UNCOMMITTED changes in 6 files of features/home (preview card/entry/section,
preview-of, sections-of); its "save" commit was undone because it had committed the changelog
junction by mistake. Its scratch: ${S}/fix/HOME/. Read that diff first, keep what is right, fix what
is wrong, then do the remaining findings.` },
  { id: 'DETAIL', slug: 'detail', port: 4420, mode: 'review', title: 'Champion detail page and the shared detail machinery',
    owned: 'src/LoDb.Web/src/app/features/catalogue/champions/detail/**, features/catalogue/shared/codex/pager/**, features/catalogue/shared/pager/**, the load-time badge (features/catalogue/shared/codex/timing/), the champion detail keys of public/i18n/champions/**; plus the minimal wiring of the shared pager and badge into the item, summoner and rune detail pages.',
    extra: 'Four pagers from details().neighbours (SSR, rel prev/next, Classic badge) without browser-only list fetches; the load-time badge on the four detail pages with its compact phone variant; section chips bar (overflow, sticky vs legacy tabs); stat icons; skins lightbox and chroma viewer; skin tiles; pager spacing. Check Ahri, FiddleSticks, Aphelios and a champion of an old version, at 1440/1024/768/390/320, en/fr/ar, dark/light, noxus. Note: ENTITYDET edits the item/summoner/rune pages in parallel; check your wiring there stays minimal so both branches merge.' },
  { id: 'ADMIN', slug: 'admin', port: 4470, mode: 'review', title: 'Administration', accounts: ADMIN_ACCOUNTS,
    owned: 'src/LoDb.Web/src/app/features/admin/**, src/LoDb.Web/public/i18n/admin/**, tests/LoDb.E2E/specs/admin/**.',
    extra: 'Blockers first (overview sections, donut overflow, journal select overflow), then the top bar, palette mapping (good/bad/series tokens), small/danger buttons and ConfirmButton confirmTone, KPI tiles, section rules, tables, badges, each panel against the legacy panel of the same name, French legacy wording verbatim, the new API fields (shareToken, isBanned, riotTagline, geoAvailable, probe objects/bytes).',
    reviewExtra: `HISTORY: the fixer delivered ONE commit of 129 files (2ef72bd2) plus a small one (1e8e5d1d). The
convention is one commit = one coherent change. After your review fixes, split it, on this local,
never-pushed branch, with this exact procedure:
  1. OLD=$(git rev-parse HEAD)  (write it into ${S}/fix/ADMIN/split-old-head.txt first)
  2. git reset --soft ${BASE} && git reset -q && git update-index --skip-worktree ${PUBLISHED}
  3. commit coherent groups (per panel / per shared admin component / i18n / e2e), explicit paths,
     tests with their code;
  4. PROOF: git diff $OLD HEAD must be empty and git status clean; report both.
If the split cannot be made coherent, stop at step 1, restore nothing, and justify the single
commit in your report instead.` },
  { id: 'ACCOUNT', slug: 'account', port: 4450, mode: 'review', title: 'Authentication, account, profile and API portal', accounts: MEMBER,
    owned: 'src/LoDb.Web/src/app/features/account/** (except account-menu and verify-email-banner, done in wave 1), features/profile/**, features/api-portal/**, public/i18n/{account,api-portal,api}/**.',
    extra: 'Pickers (size picker, bottom sheet with handle on phones, focus), profile editor, favorites sockets, identity aside, public profile, API portal panels and grids, toasts where the legacy used toasts, auth forms (autofocus, check-inbox state, terms checkbox, expired reset link).',
    reviewExtra: `A previous reviewer was interrupted before committing anything; its diffs and build log are in
${S}/fix/ACCOUNT/review/ (use them only as hints).` },
  { id: 'BUILDS', slug: 'builds', port: 4460, mode: 'review', title: 'Builds: trends, my builds, editor, shared build', accounts: MEMBER,
    owned: 'src/LoDb.Web/src/app/features/builds/**, public/i18n/builds-editor/** (and builds-related keys), tests/LoDb.E2E/specs/{builds-editor,builds-share,trends}/**.',
    extra: 'Blocker: the shared build header crushing the title on phones (do better than both without changing the desktop look). Then the armory (size wide, sticky head/search/chips), editor typography and labels, game context layout, save row, validation and toasts, trends pagination, my builds empty state, language labels.' },
]
for (const wp of WPS) {
  wp.dir = `F:/Git/lodb-parite/${wp.slug}`
  wp.branch = `wt/parite-${wp.slug}`
  if (wp.resume) wp.resume = wp.resume.replace('git -C  diff', `git -C ${wp.dir} diff`)
}

const fixPrompt = (wp) => `${COMMON(wp)}
PACKAGE SPECIFICS.
${wp.extra}
${wp.resume ? `\nRESUME.\n${wp.resume}\n` : ''}
Implement ALL findings of your package (blockers and majors first, then minors and polish). Verify
each result against the legacy stack (captures at the relevant widths/states) before committing.
Before returning, write your report as JSON to ${S}/fix/${wp.id}/report-fix.json (same shape as your
structured output), stop your preview, and check git status is clean. Report every finding id with
its status.`

const reviewPrompt = (wp, fixSource) => `${COMMON(wp)}
You are the ADVERSARIAL REVIEWER of package ${wp.id}. ${fixSource}
Review the whole diff (git -C ${wp.dir} log --oneline ${BASE}..HEAD ; git -C ${wp.dir} diff ${BASE}...HEAD).
Verify yourself, against the legacy stack with a fresh build and captures, ALL blockers and majors
and at least half of the rest. Hunt regressions on other pages, widths, themes and locales (RTL,
light theme) the change can reach; convention violations (limits, one public element per file,
hard-coded colours, physical CSS properties, app.config.ts edits); missing tests or tests asserting
implementation; build/budget/lint/typecheck/test failures; bad commit subjects or ANY attribution
line; anything staged under ${PUBLISHED}. Fix what you find with new commits (same rules; do not
rewrite history except where told below), rerun the full checks (typecheck, lint, the full front
suite once, build:web, the E2E specs of the area) and report.
${wp.reviewExtra ? `\n${wp.reviewExtra}\n` : ''}
Before returning, write your report as JSON to ${S}/fix/${wp.id}/report-review.json (same shape as
your structured output), stop your preview, and check git status is clean.`

const review = (wp, fixSource) =>
  limited(() => agent(reviewPrompt(wp, fixSource), { label: `review:${wp.id}`, phase: 'Review', schema: REVIEW_SCHEMA }))

const results = await pipeline(
  WPS,
  async (wp) => {
    if (wp.mode === 'review') {
      const r = await review(wp, `The fixer finished in an earlier run; its report is the "fix:${wp.id}" entry of ${S}/wave2-results.json (read it: results per finding, outside edits, followups).`)
      log(`review:${wp.id} -> ${r ? r.verdict : 'no result'}`)
      return { wp: wp.id, review: r }
    }
    const fix = await limited(() => agent(fixPrompt(wp), { label: `fix:${wp.id}`, phase: 'Fix', schema: FIX_SCHEMA }))
    if (!fix) {
      log(`fix:${wp.id} returned nothing: its review is skipped`)
      return { wp: wp.id, fix: null, review: null }
    }
    log(`fix:${wp.id} done, ${fix.results.filter((x) => x.status === 'fixed').length}/${fix.results.length} fixed`)
    const r = await review(wp, `The fixer of this run reported:\n${JSON.stringify(fix)}`)
    log(`review:${wp.id} -> ${r ? r.verdict : 'no result'}`)
    return { wp: wp.id, fix, review: r }
  },
)
return results.filter(Boolean)
