# Responsive & mobile

Comment l'interface s'adapte du petit téléphone (320 px) à l'iPad et au desktop.
Ce document décrit la stratégie responsive, les composants spécifiques mobile, les
breakpoints, la méthode d'audit, et les limitations connues.

> Complément de [`architecture.md`](architecture.md) (§ Architecture frontend). Le brief
> [`brief-design-pages.md`](brief-design-pages.md) laisse volontairement le responsive
> hors de son périmètre : ce fichier est la référence.

## Stratégie

- **Mobile-first via Tailwind 4.** Les listes et les pages de détail sont responsives par
  **classes utilitaires** dans les coques Twig (préfixes `md:`/`lg:`, `min-[400px]:`…) —
  `list.css` et `detail.css` n'ont volontairement **aucun `@media`**. Les breakpoints CSS
  custom se concentrent sur la nav, les builds et le profil.
- **Nav pouce-first.** Sous 768 px, la navigation primaire vit dans une **barre fixe basse**
  (`bottom-nav`) ; l'en-tête se densifie (icônes seules, marque courte).
- **Pas d'expansion du layout.** Aucune page ne doit forcer une largeur supérieure à celle
  du device : sur un vrai téléphone cela produit un rendu **dézoomé et pannable** (invisible
  au simple test d'overflow — cf. § Méthode d'audit).

## Breakpoints

Seuils récurrents : **40rem (640 px)** et **48rem (768 px)**, plus quelques seuils ponctuels.

| Zone / fichier | Breakpoints | Bascule |
|---|---|---|
| `nav.css` | `48rem` (768) | bottom-nav visible `<768`, masquée `≥768` ; padding-bas du body |
| `header.html.twig` | `md` (768) / `lg` (1024) | nav desktop dès `md` ; **labels donate/compte dès `lg`** (densification, tient à 768) |
| `builds.css` | `640` | éditeur de build (rangées de runes, pastilles) |
| `profile-*.css` | `30/40/48/64rem` | grilles profil, picker, édition |
| `showcase.css` | `374` | rangée d'onglets de sorts (fiche champion) sur très petit écran |
| `primitives.css` | `480` / `640` | header `.codex-header` (wrap meta) ; inputs anti-zoom iOS |
| `changelog.css` | `480` | timeline |

`@media (pointer: coarse)` (dans `nav.css`, `base.css`, `builds.css`) rehausse les cibles
tactiles primaires (boutons, chips, pagination, bottom-nav) à ~2,5–2,75 rem.

## Composants spécifiques mobile

| Composant | Fichiers | Rôle |
|---|---|---|
| **Bottom navigation** | `templates/partials/bottom_nav.html.twig`, `nav.css` (`.bottom-nav`) | Nav primaire fixe (pouce), `env(safe-area-inset-bottom)`, masquée ≥768. Pur Twig/CSS. |
| **Filtre bottom-sheet** | `assets/vue/components/ResourceFilter.vue`, `templates/components/list_filter.html.twig`, `nav.css` (`.filter-sheet`) | `<dialog>` natif ouvert en `showModal()` sous `md` (chips inline au-dessus) ; poignée + animation `@starting-style`. |
| **Switcher version/langue** | `templates/partials/header.html.twig` | `<details>` natif dans l'en-tête ; selects `.hx-select` (≥16 px sur mobile, anti-zoom iOS). |
| **Dialog contact** | `templates/partials/footer.html.twig` | `<dialog>` plein écran mobile, scroll interne. |
| **Éditeur de build** | `assets/vue/builds/StepEditor.vue`, modal armurerie `ItemArmory`, `builds.css` | Layout single-column, dialog armurerie full-bleed (largeur = viewport), scroll interne. |

`<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">`
(`base.html.twig`) — `viewport-fit=cover` exploité par les `env(safe-area-inset-*)`.

## Méthode d'audit

L'app est auditée sur **8 formats** (320 · 360 · 375 · 390 · 414 · 430 · 768 · 844-paysage)
via Playwright, sur toutes les familles de pages (listes, détails, forge, profil, admin,
auth, dons, légal, i18n).

> ⚠️ **Piège de mesure.** La métrique classique `scrollWidth − innerWidth` est **aveugle**
> à l'expansion du *layout viewport* : sous émulation `isMobile`, quand un contenu a une
> largeur mini supérieure à l'écran, le navigateur **élargit `innerWidth`** au lieu de
> scroller → l'overflow mesuré reste 0 alors que la page est dézoomée. Le signal fiable est
> **`largeur rendue effective > largeur device`** (`max(innerWidth, scrollWidth)` comparé au
> device). C'est cette sonde qui a révélé les dézooms corrigés le 19/07/2026.

Points de contrôle : débordement horizontal, largeur forcée vs device, cibles tactiles,
erreurs JS, `font-size` des inputs (≥16 px sinon zoom iOS), et vérification visuelle des
captures (empilement, chevauchements, troncatures).

Régénérer les captures : `node tools/screenshots/capture.mjs` (desktop 1440 + mobile 390).

## État & limitations

- **Structurellement propre** de 320 à 430 px sur toutes les pages, et jusqu'à l'iPad (768)
  et au-delà. Voir l'entrée changelog `2026-07-19-responsive-mobile-largeur-forcee`.
- **PWA** installable (manifest + service worker) — cf. `public/manifest.webmanifest`,
  `public/sw.js`.
- **RTL (arabe) : non supporté.** `dir`/`lang` restent statiques (`ltr`/`en`) et la structure
  n'est pas mise en miroir ; le texte arabe s'aligne à droite par l'algorithme bidi du
  navigateur uniquement. Aucun débordement, mais UX RTL incorrecte. Limitation connue.
