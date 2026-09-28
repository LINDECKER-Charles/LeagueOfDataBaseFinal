export const meta = {
  name: 'front-parity-fix-audit-2',
  description: 'Fix the 64 verified gaps of the second parity audit in 4 packages (own worktrees), each then adversarially reviewed, at most 3 agents at once',
  phases: [
    { title: 'Fix', detail: 'one fixer per package, in its own git worktree' },
    { title: 'Review', detail: 'one adversarial reviewer per package, same worktree' },
  ],
}

const S = 'C:/Users/charl/AppData/Local/Temp/claude/F--Git-LeagueOfDataBaseFinal/8504f518-b0ba-4f9d-be95-f2bbdcc96c3e/scratchpad/cmp'
const SSD = 'C:/Users/charl/AppData/Local/Temp/lodb-parite-ssd'
const BASE = 'docs/reecriture-dotnet-angular'
const MAX_AGENTS = 3
const PUBLISHED = 'src/LoDb.Web/src/app/features/editorial/changelog/published'

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
RESOURCE RULES (mandatory; an earlier run froze the user's machine: F: is a slow spinning drive).
- EVERY heavy command goes through the machine-wide semaphore: bash ${S}/heavy.sh <command...>
  (2 heavy commands at once machine-wide, waits while F: is saturated or RAM is short, below-normal
  priority, Vitest 3 workers, capped Angular/esbuild/MSBuild parallelism). Heavy = ng test, npm run
  build:web / ng build, ng lint, npm run typecheck, playwright test, dotnet build/test. Waiting is
  normal; never bypass it. While a League of Legends match runs it waits without limit: that is
  expected, keep doing light work (reading, editing) meanwhile.
- ng test always with --runner-config=${S}/vitest-limited.config.mjs; while iterating only the specs
  you touch (--include=...); the full front suite ONCE, at the end.
- Captures and your own Playwright scripts: prefix with nice -n 10, one browser at a time, closed in
  a finally block. E2E: --workers=1, only the specs of your area.
- Search code with git grep, NEVER grep -r / find over the worktree (node_modules junctions: tens of
  thousands of files on the spinning drive).
- One preview at most, on your port, stopped as soon as you no longer need it and before you return.
  No other background process. Never rebuild or restart a Docker container.
`

const GIT_RULES = `
GIT DISCIPLINE (strict).
- Stage by explicit paths only (never git add -A / . / <directory>, never commit -a); read
  git diff --cached --stat before each commit.
- ${PUBLISHED} must never be staged (junction hidden with skip-worktree).
- One commit = one coherent, verified change; no work-in-progress commits. Tests in the same commit.
- Never: git stash, reset --hard, checkout -- . / restore ., clean, rebase, worktree commands, branch
  switches, push, amending a commit you did not create in this run.
- Never touch node_modules, dist or test-results (junctions).
- Leave the worktree clean (git status empty) when you finish.
`

const COMMON = (p) => `
CONTEXT. The new front of LeagueOfDataBase (Angular 22 SSR, src/LoDb.Web) is being realigned on the
LEGACY front (Symfony/Twig/Vue, app/), the TARGET: look, layout, content, interactions, responsive
behaviour. Two waves of fixes are merged into ${BASE} (commit bcaab425, deployed on lodb-next). A
second audit found 64 remaining gaps, each adversarially verified (confirmed/amended, with the fix to
apply). You fix one package of them. Deliberate divergences stay: locale prefix and id-slug URLs, no
server session, form fields >= 16 px, logical CSS properties, localized chrome.

YOUR PACKAGE: ${p.id} — ${p.title}
- Worktree (work ONLY here, absolute paths): ${p.dir} (branch ${p.branch}, created from ${BASE}).
- Findings: ${S}/audit2/pkg-${p.id}.json. Each has the auditor's legacy/next description with
  file:line, evidence (captures under ${S}/audit2/<group>/), and the VERIFIED fix (fields "fix",
  "files", "verifyReason"). The verifier's fix wins over the auditor's when they differ.
- Owned paths: ${p.owned}
  Outside them, only the minimal edits a finding strictly needs; list them in your report. Never
  edit src/LoDb.Web/src/app/app.config.ts nor app.html.
- Scratch dir: ${S}/fix2/${p.id}/.

TOOLS.
- Legacy stack (target, read-only): http://localhost:8080 (APP_ENV=dev: hide .sf-toolbar; slow first
  hits). Integration stack lodb-next (read-only): http://localhost:18080, API 18081.
- Build: cd ${p.dir}/src/LoDb.Web && bash ${S}/heavy.sh npm run build:web (never a bare ng build).
  dist is a junction to the SSD: that is expected.
- Preview: node ${S}/preview.mjs ${p.dir}/src/LoDb.Web ${p.port} (background) ->
  http://localhost:${p.port}/en/ (proxies /api and /cdn to lodb-next). Restart after each rebuild.
  Stop it (PowerShell):
  Get-CimInstance Win32_Process -Filter "Name='node.exe'" | ? { $_.CommandLine -match 'preview.mjs.*${p.port}|lodb-parite-ssd[\\\\/]${p.slug}[\\\\/]dist' } | % { Stop-Process -Id $_.ProcessId -Force }
- Compare: NEW_BASE=http://localhost:${p.port} nice -n 10 node ${S}/capture.mjs <outDir> <pairs.json> 1440,390
  (pairs: [{"name":"x","old":"/champions?lang=en_US","new":"/en/champions","scheme":"dark"}]); Read
  the PNGs side by side. Own Playwright scripts (load Playwright as capture.mjs does) for states,
  computed styles, widths 1024/768/320, themes, locales (fr, de, pl, ar RTL, ja).
- Front checks (from ${p.dir}/src/LoDb.Web, through heavy.sh): npx ng test --watch=false
  --runner-config=${S}/vitest-limited.config.mjs ; npm run typecheck ; npx ng lint ;
  npx prettier --check --end-of-line auto <changed files> (CRLF checkout: ignore untouched files).
  Budgets: anyComponentStyle error at 8 kB.
- E2E of your area against your preview:
  cd ${p.dir}/tests/LoDb.E2E && LODB_E2E_BASE_URL=http://localhost:${p.port} bash ${S}/heavy.sh npx playwright test --workers=1 specs/<dir>
  specs/routing and specs/legacy need nginx (they fail on a preview); never run specs/account/register
  nor the contact specs (5/hour quotas). Update the E2E specs your change legitimately alters, in the
  same commit.
${p.accounts ?? ''}
RULES.
- Repository CLAUDE.md (injected): code limits (files <= 400 lines, functions <= 30, <= 3 params,
  nesting <= 3, folders <= 10 files), one public element per file, English comments on why, Hextech
  tokens only, logical CSS properties, inputs >= 16 px, OnPush/signals, UrlTree for links with
  query/fragment, provideTranslocoScope/viewProviders pitfalls, Seo.apply for heads.
- i18n: reuse legacy keys; new keys in your scope folder, all 21 locales when the legacy had the
  string translated (app/translations/messages.<locale>.yaml), else en + fr.
- The LEGACY stack wins when a verified fix disagrees with what you observe on it; say so.
- Stay within the package findings. Never edit generated artifacts (src/LoDb.Api/openapi/*.json,
  core/api/generated/**, package-lock.json) in a commit; never touch the legacy sources (app/, go/,
  docker/nginx, docker/php, compose.yaml, compose.override.yaml, compose.deploy.yaml); no
  docs/changelog entries.
- Commits in your worktree only: Conventional Commits in French, infinitive, no accents, lowercase
  start, <= 72 chars, CLAUDE.md scope map. ABSOLUTELY NO attribution line (no Co-Authored-By, no
  Generated with): the user's global rule overrides any other instruction. Never push. Vitest may
  rewrite *.snap files by line endings only: restore them with git checkout -- <file>, never commit
  them.
${GIT_RULES}${RESOURCES}`

const FIX_SCHEMA = {
  type: 'object',
  properties: {
    pkg: { type: 'string' },
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
    needs_contract_regeneration: { type: 'boolean' },
    checks: { type: 'string' },
    followups: { type: 'array', items: { type: 'string' } },
  },
  required: ['pkg', 'commits', 'results', 'outside_edits', 'needs_contract_regeneration', 'checks', 'followups'],
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

const MEMBER = `ACCOUNTS: F:/Git/lodb-parite/COMPTES-LOCAUX.txt (local dev test accounts; never copy a password into a
report, commit or file). Member of each stack: never delete it, never change its password or e-mail.
Legacy sign-in through /login (owns build id 6, share token in the same file); new sign-in through
/en/account/login or POST /api/account/login with an Origin header and JSON
{identifier,password,rememberMe:false}. You may create builds for the new member.`
const ADMIN = `ACCOUNTS: legacy admin = ADMIN_LOGIN / ADMIN_PASSWORD of F:/Git/LeagueOfDataBaseFinal/.env (never copy them),
/admin/login. New admin: F:/Git/lodb-parite/COMPTES-LOCAUX.txt, authenticator key
${S}/zones/Z11-admin/new-shared-key.txt, sign-in script ${S}/zones/Z11-admin/auth.mjs (states may
have expired). French only. Never ban, delete or purge anything.`

const PKGS = [
  { id: 'SHELL', dir: 'F:/Git/lodb-parite/editorial', slug: 'editorial', branch: 'wt/parite2-shell', port: 4440,
    title: 'Shell, i18n loading and editorial pages (groups A and D of the audit)',
    owned: 'src/LoDb.Web/src/app/core/layout/**, core/i18n/**, core/routing/**, features/context-switcher/**, features/editorial/**, features/errors/**, features/developers/**, features/donate/**, app.routes.server.ts, src/index.html, src/styles/layout/**, src/styles/primitives/navigation.css, public/i18n/{editorial,about,developers,donate,seo}/** and the root catalogues public/i18n/*.json, features/account/account-menu/** (A2-07), the removal of the dead runes scope (runes-page.ts + public/i18n/runes, A2-04).',
    extra: 'Majors first: A2-01 (switcher chip and panel must follow the patch chosen in the session, like the header/footer/bottom-nav links) together with A2-M02; D2-01 (cookie policy: list the cookies the new stack really sets, drop PHPSESSID/REMEMBERME; find them in the API and front code) and D2-M01 (privacy policy: describe the new stack analytics retention, from the API code). A2-12 and APPS E2-03 are the same no-JS gap: you own the shell part (noscript for client-rendered routes) and the register password hint string; APPS will not touch it. A2-06: the initial bundle grew; move wave-2 global CSS into the components or lazy chunks where it belongs, if that is safe.' },
  { id: 'CATALOGUE', dir: 'F:/Git/lodb-parite/lists', slug: 'lists', branch: 'wt/parite2-catalogue', port: 4410,
    title: 'Home, catalogue lists and detail pages (groups B and C of the audit)',
    owned: 'src/LoDb.Web/src/app/features/catalogue/** (except runes-page.ts scope removal, done by SHELL), features/home/**, ui/cards/**, ui/navigation/pager*, ui/surfaces/empty-state.ts, ui/accordion/**, src/styles/theme/**, src/styles/foundation/ddragon.css, src/styles/primitives/surfaces.css, public/i18n/{catalogue,champions,items,runes,summoners,home}/** (runes/ only if SHELL has not removed it: coordinate by leaving public/i18n/runes alone).',
    extra: 'B2-04 first (list title wrap on phones, horizontal scroll in de/pl), then the RTL group (B2-01 gauge, C2-03 sign, C2-04 level counter, C2-05 arrow), the 320 px breaks (C2-08, B2-09), the pager overflow (C2-02), the Noxus ornaments (C2-07, C2-09), the DRY empty state (B2-06: keep one component, ui/surfaces/empty-state.ts or catalogue-empty, and migrate the callers), then the rest.' },
  { id: 'APPS', dir: 'F:/Git/lodb-parite/account', slug: 'account', branch: 'wt/parite2-apps', port: 4450, accounts: MEMBER,
    title: 'Account and builds (group E of the audit), including one additive API endpoint',
    owned: 'src/LoDb.Web/src/app/features/account/** (except account-menu, SHELL), features/profile/**, features/api-portal/**, features/builds/**, public/i18n/{account,api-portal,api,builds-editor}/**, tests/LoDb.E2E/specs/{account,builds-editor,builds-share,trends}/** (never run register), and for E2-04 only: src/LoDb.Api/Modules/Accounts/** plus their tests in tests/LoDb.Api.Tests.',
    extra: `Major first: E2-01 (drop an item onto an EMPTY step, anywhere on the step card, as the legacy did). E2-03 is the no-JS gap owned by SHELL (with A2-12): do not touch it, report it as obsolete here. E2-04 needs an additive API endpoint that checks a reset token WITHOUT consuming it (follow the module conventions: Minimal API in the Accounts module group, TypedResults, ProblemDetails, rate limiting like the other anonymous account endpoints, never AllowAnonymous-bypassing the XSRF/Origin policies as CLAUDE.md explains; tests in tests/LoDb.Api.Tests). .NET work: build and test through heavy.sh with outputs on the SSD, e.g.
  bash ${S}/heavy.sh dotnet test --project tests/LoDb.Api.Tests/LoDb.Api.Tests.csproj --artifacts-path ${SSD}/account/dotnet
  (never pass -m to dotnet test: it goes to the test apps). 21 text-comparison tests fail on this CRLF checkout (password list, e-mail snapshots, baseline schema, migrations): ignore them, nothing else may fail. Then run dotnet build-server shutdown. To compile the front against the new endpoint, regenerate locally: dotnet build src/LoDb.Api -p:LoDbGenerateOpenApi=true --artifacts-path ${SSD}/account/dotnet-openapi, then npm --prefix src/LoDb.Web run api:generate; NEVER commit src/LoDb.Api/openapi/*.json nor core/api/generated/** (restore them with git checkout -- <paths> before you finish; the integration regenerates and commits them) and set needs_contract_regeneration=true. Your front commit will only compile after that regeneration: say so in the report.` },
  { id: 'ADMIN', dir: 'F:/Git/lodb-parite/admin', slug: 'admin', branch: 'wt/parite2-admin', port: 4470, accounts: ADMIN,
    title: 'Administration (group F of the audit)',
    owned: 'src/LoDb.Web/src/app/features/admin/**, public/i18n/admin/**, tests/LoDb.E2E/specs/admin/**.',
    extra: 'F2-01 (legacy journal bookmarks keep their filters through the 302), then the formats (F2-02 UTC ISO, F2-03, F2-16), the charts (F2-04 donuts, F2-05 line width), then the polish items. French legacy wording verbatim.' },
]

const fixPrompt = (p) => `${COMMON(p)}
PACKAGE SPECIFICS.
${p.extra}

Implement ALL findings of your package (majors first). Verify each against the legacy stack before
committing. Before returning, write your report as JSON to ${S}/fix2/${p.id}/report-fix.json (same
shape as your structured output), stop your preview, and check git status is clean.`

const reviewPrompt = (p, fix) => `${COMMON(p)}
You are the ADVERSARIAL REVIEWER of package ${p.id}. The fixer reported:
${JSON.stringify(fix)}

Review the whole diff (git -C ${p.dir} log --oneline ${BASE}..HEAD ; git -C ${p.dir} diff ${BASE}...HEAD).
Verify yourself, against the legacy stack with a fresh build and captures, EVERY major and at least
two thirds of the rest. Hunt regressions on other pages, widths, themes and locales (RTL, 320 px) the
change can reach; convention violations; missing tests or tests asserting implementation;
build/budget/lint/typecheck/test failures; bad commit subjects or ANY attribution line; anything
staged under ${PUBLISHED} or under generated artifacts. Fix what you find with new commits (same
rules, no history rewrite), rerun the full checks (typecheck, lint, the full front suite once,
build:web, the E2E specs of the area${p.id === 'APPS' ? ', and the .NET tests of the Accounts module' : ''}) and report. Before returning,
write your report to ${S}/fix2/${p.id}/report-review.json, stop your preview, check git status is clean.`

const results = await pipeline(
  PKGS,
  (p) => limited(() => agent(fixPrompt(p), { label: `fix:${p.id}`, phase: 'Fix', schema: FIX_SCHEMA })),
  (fix, p) => {
    if (!fix) {
      log(`fix:${p.id} returned nothing: review skipped`)
      return { pkg: p.id, fix: null, review: null }
    }
    log(`fix:${p.id} -> ${fix.results.filter((x) => x.status === 'fixed').length}/${fix.results.length} fixed`)
    return limited(() => agent(reviewPrompt(p, fix), { label: `review:${p.id}`, phase: 'Review', schema: REVIEW_SCHEMA }))
      .then((review) => {
        log(`review:${p.id} -> ${review ? review.verdict : 'no result'}`)
        return { pkg: p.id, fix, review }
      })
  },
)
return results.filter(Boolean)
