# Responsive et mobile

Comment le front Angular s'adapte du petit téléphone (320 px) au desktop, en LTR comme en RTL.
Faisant foi : les règles de code du front sont dans [`AGENTS.md`](../../AGENTS.md) (styles :
jetons Hextech, propriétés logiques, champs à 16 px au moins) ; ce document décrit la
stratégie et la méthode d'audit. L'ancienne version (Twig, Vue) est archivée dans
`legacy/docs/architecture/responsive-mobile.md`.

## Stratégie

- **Un seul front** (`src/LoDb.Web/`) pour le web, le desktop et Android : pas de gabarit par
  plateforme, le layout s'adapte à la largeur.
- **Mobile-first avec Tailwind 4** (`src/LoDb.Web/src/styles/index.css`, `@import 'tailwindcss'`).
  Les composants se règlent par classes utilitaires (`md:`, `max-md:`, `min-[460px]:`…, par
  exemple dans `core/layout/header/header.html`) ; le CSS global ne garde que le châssis.
- **Navigation au pouce.** Sous `md`, la navigation primaire est une barre fixe basse ;
  l'en-tête se densifie (marque et libellés masqués, icône seule).
- **Jamais plus large que l'écran.** Une page ne doit pas imposer une largeur supérieure à
  celle du device (voir § Méthode d'audit).

## Breakpoints

Pas de fichier central de breakpoints : les seuils sont ceux de Tailwind plus quelques seuils
locaux.

| Seuil | Où | Effet |
|---|---|---|
| `48rem` (768 px, `md`) | `src/styles/layout/chrome.css` | bottom-nav visible en dessous, masquée au-dessus ; marge basse du shell |
| `22.5rem` (360 px) | `chrome.css` | interlettrage resserré des libellés de la bottom-nav (six libellés dans 320 px) |
| `640 px` | `src/styles/primitives/dialog.css` (`40rem`) ; `PHONE` dans `features/admin/panels/management/prerender/` | marges des dialogues de formulaire ; `BreakpointObserver` choisit feuille ou dialogue |
| `480 px` / `360 px` | `primitives/surfaces.css`, `primitives/navigation.css` | ajustements ponctuels |
| `pointer: coarse` | `primitives/controls.css` | boutons à `min-block-size: 2.75rem` (cible tactile) |

Les animations respectent `prefers-reduced-motion` (`foundation/motion.css` et les primitives).

## Composants spécifiques mobile

| Composant | Fichiers | Rôle |
|---|---|---|
| Bottom-nav | `src/app/core/layout/bottom-nav/` (`bottom-nav.ts`, `bottom-nav-entries.ts`) | barre fixe sous `md` ; les onglets du catalogue gardent version et variante (`injectChromeLinks`) |
| Filtres en feuille | `src/app/features/catalogue/shared/console/filter-sheet.{ts,html,css}`, ouverte par `list/catalogue-list.ts` | facettes dans une feuille basse sur écran étroit ; le filtrage reste en direct derrière |
| Feuille basse (CDK) | `src/app/ui/overlays/sheet-config.ts`, `.hx-sheet` dans `primitives/dialog.css` | pleine largeur, ancrée en bas, `max-block-size: 85dvh` |
| Dialogues | `src/app/ui/overlays/` (`dialog-service.ts`, `dialog-size.ts`) | `max-block-size: calc(100dvh - 2rem)` |

Le catalogue des composants de `ui/` (`accordion`, `cards`, `controls`, `gallery`, `media`,
`motion`, `navigation`, `overlays`, `surfaces`, `tabs`) est la source des primitives réutilisées.

## RTL

L'arabe (`ar`) est la seule locale RTL des 21 (`src/app/core/layout/direction/text-direction.ts`).
`PageDirection` (`direction/page-direction.ts`) lit `<html lang>` (déjà écrit par le rendu SSR,
donc sans flash LTR à l'hydratation), puis fixe `<html dir>` et le `Directionality` du CDK pour
que les overlays se reflètent avec la page. Le CSS utilise des **propriétés logiques**
(`inline-size`, `padding-block-end`, `inset-inline`, `border-inline`…), règle d'`AGENTS.md`.
La galerie de dev (`/{locale}/dev/gallery`, par exemple `/en/dev/gallery`) est sondée en `en` et
en `ar`.

## Champs, tactile et zones sûres

- Champs de formulaire à **16 px** quel que soit le pointeur (`primitives/controls.css`), pour
  éviter le zoom automatique d'iOS.
- Cibles tactiles : boutons à 2,75 rem sous `pointer: coarse` (`controls.css`).
- `src/index.html` : `viewport` avec `viewport-fit=cover` ; les zones sûres sont consommées par
  `env(safe-area-inset-bottom)` dans la bottom-nav et le shell (`chrome.css`), les feuilles
  (`dialog.css`, `filter-sheet.css`) et `item-armory.css` ; `catalogue-list.css` consomme
  `env(safe-area-inset-top)`.
- `index.html` pose aussi `theme-color` et `apple-mobile-web-app-status-bar-style`.

## PWA et coques

- Manifeste `src/LoDb.Web/public/manifest.webmanifest` (`display: standalone`) et page
  hors ligne `public/offline.html`.
- Service worker : `src/LoDb.Web/src/sw/` ; `sw/registration/register-worker.ts` ajoute les
  liens d'installation puis l'enregistre au `load` (scope `/`, `updateViaCache: 'none'`).
- Coques : `src/app/core/platform/` (`web/`, `desktop/`, `android/`) ; Android passe par
  Capacitor (`src/LoDb.Web/capacitor.config.ts`, plugins dans `platform/android/native/`).

## Méthode d'audit

Deux outils, tous deux alimentés par le Playwright de `tests/LoDb.E2E` (installation :
`npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install`).

**Sonde de la galerie** (`tools/gallery-probe/`). Avec le serveur de dev lancé
(`npm start --prefix src/LoDb.Web`) :

```bash
node tools/gallery-probe/probe.mjs [--no-captures]   # GALLERY_URL change l'adresse (4200 par défaut)
```

Elle parcourt les quatre thèmes en `en` (LTR) et `ar` (RTL) à 320, 390, 768 et 1440 px
(`matrix.mjs`), vérifie la direction, le débordement et l'identité peinte, pilote le sélecteur
de thème, les onglets reflétés et un dialogue, et enregistre une capture pleine page par vue
dans `docs/reecriture/rapports/captures/design-system`. Les phones et tablettes sont émulés en
tactile, pour que le layout viewport puisse s'élargir.

> Piège de mesure. `scrollWidth - innerWidth` est aveugle à l'expansion du layout viewport :
> sous émulation mobile, un contenu trop large fait **élargir `innerWidth`** au lieu de
> défiler, et la page apparaît dézoomée avec un débordement mesuré de 0. Le signal fiable est
> la **largeur rendue effective comparée à la largeur du device** (`overflowFailures` dans
> `tools/gallery-probe/page-checks.mjs`, qui désigne les éléments fautifs).

**Lighthouse** (`tools/lighthouse/`, détail dans son `README.md`). Profil mobile, cinq pages,
budgets de performance, d'accessibilité et de SEO ; rapport dans
`docs/reecriture/rapports/lighthouse.md` :

```bash
node tools/lighthouse/run.mjs --stack lodb-dev       # contre la stack de dev (--next <url>)
node --test 'tools/lighthouse/test/*.test.mjs'       # tests de l'outil
```

À contrôler à la main sur les captures : empilement, chevauchements, troncatures, et en RTL le
reflet des onglets, des overlays et des icônes directionnelles.
