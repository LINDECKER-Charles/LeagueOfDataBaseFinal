# Wave 2 — handover notes from wave 1

Wave 1 (merged into the integration branch before wave 2 starts) delivered:

- **API**: additive contract changes, and the client regenerated at integration.
- **DS**: primitives, themes, backdrop, dialogs and rich text.
- **SHELL**: header, footer, bottom nav, toasts, progress bar, context switcher, and the admin shown outside the public chrome.
- **I18N**: raw keys, SSR scope race and plurals.

The full wave-1 reports are the `fix:*` entries of `wave1-results.json`, with the reviews next to them.

## Common to every package

- **Build.** Build with `npm run build:web`, not with a bare `ng build`. The postbuild step writes `sw.js`, and without it every E2E spec that asserts no console errors fails.
- **E2E on the preview.**
  - `specs/routing/**` and `specs/legacy/**` exercise nginx-level redirects, which the preview does not serve. Ignore their failures there.
  - Never run the register or contact specs. Their quotas are 5 per hour.
- **Transloco scopes.** A component that receives translated inputs or projected templates from a page with another catalogue scope provides its own scope in `viewProviders`, not in `providers`. The pipe on its host element resolves that component's `providers`. `translateSignal` under a scope provider reads keys inside that scope.
- **Legacy strings.** Reuse the legacy keys that are still in the catalogues, never new wording:
  - `common.detail`
  - `common.search`
  - `common.no_result.text` and `common.no_result.back`
  - `filter.copy_link`, `filter.copied` and `filter.copy_error`
  - `item.list.evolutions`
  - `item.detail.tier`
  - `homepage.champions.no_data`
  - `auth.register.password_help`
  - `build.editor.nojs`

  They are translated in 21 locales, except `filter.*` and `build.editor.nojs`, which have en and fr only.
- **Themes.** Theme reach relies on host-scoped selectors in `src/styles/theme/{zaun,noxus,spirit-blossom}/{frames,ornament}.css`, for example `lodb-filter-console .console`, `:is(lodb-filter-console, lodb-filter-sheet, lodb-active-filters) .marker`, and `lodb-catalogue-list :is(.notice, .empty)` with `.empty__mark`.
  - A renamed class or a new marker must be added to those selectors.
  - Keep component rules at single-class specificity, or the theme loses.
- **Dialog sizes (DS).** `size: 'wide'` gives `hx-dialog--wide`, which is `min(56rem, 94vw)`. `size: 'picker'` gives `hx-dialog--picker` / `hx-sheet--picker`, with the width only. The contact dialog size is already applied. There is no lightbox or compact variant yet: extend `ui/overlays/dialog-size.ts` or `pane-class.ts`. The native `<dialog>` has a size cap; the legacy lightboxes were not native dialogs.
- **Buttons (DS).** `lodbButton="danger"` and `lodbButtonSize="small"` exist. The tokens `good`, `bad`, `series-blue`, `series-green`, `series-red`, `series-cyan` and `track` exist as utilities.
- **Ambient backdrop.** The backdrop primitive is already applied, by DS, on the champions list, the champion detail, the editorial frame (`ambient` opt-in), the developers page, trends, the build editor, the share page, the API portal and the profile editor. Apply the same primitive where your pages still lack it; do not create another.
- **CRLF checkout.** The worktree is checked out with CRLF (`core.autocrlf=true`). Judge formatting with `npx prettier --check --end-of-line auto <files>`. The about 149 repo-wide Prettier hits and 21 .NET text-comparison tests are environment noise that predates these branches.

## New API fields (camelCase, `/api/catalog/{version}/{lang}` unless stated)

**Champions**

- `ChampionCard` (list `entries[]` and `details.profile`):
  - `loadingArt`: a uri, required, hotlinked from Data Dragon.
  - `blurb`: required.
- `ChampionDetails.neighbours`: a `DetailNeighbours`, required.

**Items**

- `ItemList.related`: an `EntityLink[]`, one per distinct upgrade target of the returned entries. Both the full list and the first SSR page carry it.
- `ItemCard.upgrades`: a `string[]` of ids of listed items, keyed into `related`.
- `ItemCard.summary`: falls back to the description when the plaintext is empty, on list cards only. The item page lead stays the plaintext alone, as in the legacy.
- `ItemDetails.upgrades[]`: now an `ItemUpgrade` = {id, name, canonicalPath, image, edition, gold}. `gold` is the total cost.
- `ItemDetails.depth`: `int | null`. Show the chip "Tier {depth}" (`item.detail.tier`) when depth > 1.
- `ItemDetails.neighbours`.

**Summoners and runes**

- `SummonerDetails.neighbours` and `RuneTreeDetails.neighbours`.

**Neighbour records**

- `DetailNeighbours` = {previous: `DetailNeighbour | null`, next: `DetailNeighbour | null`}.
- `DetailNeighbour` = {id, name, canonicalPath (without the locale prefix), edition: 'modern' | 'classic'}.
- The order is exactly the list endpoint's, with no wrap-around. It was checked against the legacy rel=prev/next: 69/69.

**Admin**

- `AdminBuildRow.shareToken`: the share page is `/b/{shareToken}`.
- `AdminUserRef.isBanned`: on build owners, API-client owners, contact users and donors.
- `AdminUserRow.riotTagline` and `AuditSubjectView.riotTagline`: display `username#tag`.
- `AnalyticsReport.geoAvailable`.
- `ServiceProbe.objects` and `ServiceProbe.bytes`: `int64 | null`, set on the `storage` probe only.

## Per package

### HOME

- Preview art = `champion.loadingArt`, from the champions list endpoint, the paged SSR call included.
- Empty champions preview: `homepage.champions.no_data`, plus the framed empty state (`common.no_result.*`).
- The shared entity card findings (Z03-04/06/09/10/13) belong to LISTS. Do not edit `features/catalogue/shared/cards/**`; check them after LISTS lands.

### LISTS

- The champion card reads `card.loadingArt` and `card.blurb`, with "See details" (`common.detail`).
- The item card reads `card.upgrades` and resolves names and icons through `list.related`, as a `Map` keyed by id.
- "Copy link" (`filter.copy_link`) goes at the foot of the filter console and of the mobile sheet. `ui/controls/copy-link.ts` exists.
- Search placeholder: `common.search`.
- Empty states: `common.no_result.*`.
- The console `box-shadow` and head band (Z04-27, Z07-11) are still missing. The facet group heading (Z02-06, Z04-10) is not done: `primitives/filter.css` was not created.

### DETAIL (champion detail + shared detail machinery)

- Build the four pagers (champion, item, summoner, rune path) from `details().neighbours`: reuse `injectDetailPager`, fed from the payload.
- Remove the browser-only list fetches: `shared/pager/inject-catalogue-neighbours.ts` and runes `inject-path-pager.ts`.
- Add link `rel=prev/next`. Show the Classic badge when `edition === 'classic'`.
- Move the load-time badge to a shared place and wire it into the four detail pages. Give it the compact phone variant.
- Skin lightbox and chroma viewer: see the DS note on dialog sizes.

### ENTITYDET (items, summoners, rune paths)

- Show `upgrade.gold` under each evolution.
- Tier chip from `details.depth`.
- Empty rune path: the framed empty state with `common.no_result.*` (Z07-24).
- The pager and the load-time badge belong to DETAIL: do not edit `shared/codex/pager`, `shared/pager` nor the timing badge. Your pages receive them from DETAIL.

### EDITORIAL

- Z12-M02: rewrite the `lod_prefs` row in `features/editorial/legal/texts/cookies-en.html` and `cookies-fr.html`, and in the other locales if they exist. It should say that the cookie is:
  - unsigned and readable by scripts;
  - SameSite=Lax, and Secure on HTTPS;
  - kept for 1 year;
  - used to remember the locale, the Data Dragon language and the version;
  - read by nginx (`loc=`) to open `/`.

  The per-session choice is kept in sessionStorage, not in a cookie.
- Add a line to `docs/guides/dev-next.md`: on Windows, a local ng serve or build needs git `core.symlinks=true` or a junction at `src/app/features/editorial/changelog/published`. The Docker image now links it itself.
- Z08-02, the developers h1 breaking at 390, is still open. The backdrop wrapper is already on the developers host.

### ACCOUNT

- Open the pickers with `size: 'picker'`. Add `max-block-size`, padding, the handle and the close button under `.hx-dialog--picker` / `.hx-sheet--picker`.
- Z09-18: the portal host already has the backdrop; only the padding remains.
- Register password hint: `auth.register.password_help`.

### BUILDS

- Z10-07 is already done by DS, in the `editor-page.ts` wrapper.
- Armory: use `size: 'wide'`, and put the armory rules under `.hx-dialog--wide` / `.hx-sheet--wide`.
- Z10-30, the English language labels: reuse `core/api/meta/language-name.ts`.
- `build.editor.nojs`: `/{locale}/account/**` is `RenderMode.Client`, so the `<noscript>` must live in markup that the server sends.
- The share page now takes its locale from `?lang=` (SHELL, I18N).

### ADMIN

- The admin now renders without the public chrome and is pinned to Hextech (`data { chrome: 'bare' }`, SHELL). Its own top bar and main column (Z11-02, Z11-49) and `<html lang>` fr (Z11-05) remain.
- Map `good` to the new tokens in `badge.ts` and `kpi.ts`. Colour the charts with `series-*` and gold.
- Use `lodbButton="danger"` for Supprimer, Révoquer and Purger, and `lodbButtonSize="small"` for row and toolbar actions. Add a `confirmTone` input to `ConfirmButton`. Legacy `.btn` is 0.78rem and 40.5px tall.
- Wire the new API fields: `shareToken` ("Voir"), `isBanned` ("banni" badge), `riotTagline`, `geoAvailable` (geo notice) and probe `objects` / `bytes` (storage chip).
