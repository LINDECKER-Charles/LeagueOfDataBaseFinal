# Lot 3 — Web public

Objectif du lot : toutes les pages publiques en Angular SSR, sous les nouvelles URLs
localisées, avec le design system, l'i18n au runtime, le SEO complet, le cache HTTP
partagé, la PWA et la redirection des anciennes URLs.

Critère de sortie local : diff SEO sur un échantillon face à la prod (rapport) ; E2E et
accessibilité verts ; budgets Lighthouse tenus sur la stack locale (L3.13).

Les **fondations** du lot (L3.1 à L3.4) débloquent aussi les fronts des lots 4, 5, 7, 9 et
10. L3.2 et L3.3 n'ont besoin que du lot 0 et peuvent démarrer pendant les lots 1 et 2.

Faits de l'existant à garder en tête :

- le front actuel n'utilise **pas** PrimeVue : l'interface est faite main (dialogues et
  `<details>` natifs, `Teleport`) sur **Tailwind v4** en configuration CSS (`@theme` dans
  `app/assets/styles/foundation/tokens.css`) ; les deux fichiers `tailwind.config.*` sont
  morts ;
- il n'existe ni page de recherche (la recherche vit dans le filtre des listes), ni page de
  favoris (section du profil) ; le contact est un dialogue du pied de page ;
- les 39 fichiers `*.spec.ts` de `app/assets/vue/**` (environ 257 cas) sont une source de
  cas de test à reprendre.

## L3.1 — Routage localisé, résolution d'URL, coquille SSR

- **Objectif** : le schéma d'URL de l'ADR 0005 de bout en bout — préfixe de locale, segment
  de version, redirections canoniques, vraies 404 —, les modes de rendu par route, les
  en-têtes de cache par route, et la table de routes de toutes les features.
- **À lire** : ADR [0005](../adr/0005-web-ssr-urls-et-seo.md) (URLs, priorités),
  `app/src/EventSubscriber/{VersionedRouteRedirectSubscriber,LocaleSubscriber}.php`,
  `app/src/Service/Client/{PageContextResolver,ClientManager,RememberPreferencesCookie}.php`,
  `Resource/AbstractResourceController.php` (`detailFailure`),
  `app/src/Service/Tools/{ResourceUrlGenerator,UrlGenerator}.php`,
  `app/assets/vue/loader/urls.ts` et `urls.spec.ts` (`destinationForSwitch`),
  `heritage.md` C1, C2, I2.
- **Périmètre** : `src/LoDb.Web/src/app/core/{routing,http,context}/**`, `app.routes.ts`,
  `app.routes.server.ts`, `app.config*.ts`, le composant racine (branchement des
  emplacements), la redirection de `/` dans `src/server.ts`, `features/errors/**`, les
  fichiers de routes des features et les composants provisoires listés plus bas.
- **Conception** :
  - Routes publiques : `/{locale}/`, `/{locale}/{champions|items|runes|summoners}`, leurs
    détails (`/champions/{id}`, `/items/{id}-{slug}`, `/runes/{id}-{slug}`,
    `/summoners/{id}`), les mêmes précédées de `/{version}`, `/{locale}/trends`,
    `/{locale}/u/{username}`, `/{locale}/{about,about/data,faq,changelog,developers,donate}`,
    `/{locale}/legal/{notice,privacy,terms,cookies}`, et `/b/{token}` hors locale.
  - Routes privées, en `RenderMode.Client` et `noindex` : `/{locale}/account/{login,
    register,verify-email,forgot-password,reset-password/:token,profile,profile/preview,api,
    builds,builds/new,builds/:id/edit,builds/:id/import}` ; admin sous `/admin/**`, hors
    locale.
  - Locale : `path: ':locale'` + garde `canMatch` (jamais de `matcher`, incompatible avec
    le prérendu) ; version : segment `:version` validé contre `/api/meta`.
  - Fichiers de routes des features, créés ici avec une page provisoire : un par feature,
    et pour les builds trois fichiers distincts — `features/builds/editor/editor.routes.ts`
    (pages sous `account/builds`, L5.2), `features/builds/share/share.routes.ts`
    (`/b/:token`, L5.3), `features/builds/trends/trends.routes.ts` (`/{locale}/trends`,
    L5.3).
  - Décision canonique en **fonction pure** (`core/routing/canonical.ts`), alimentée par
    `/api/meta` et la réponse de détail (qui porte son `canonicalPath`) : slug faux ou
    absent → 301 vers l'URL canonique (l'id fait foi) ; dernière version épinglée → 301 vers
    l'URL courte ; entité absente sur une version épinglée → 302 vers la liste de cette
    version ; absente sans version épinglée → 404 ; version inconnue → 404. Statut et
    `Location` posés en SSR par `RESPONSE_INIT` ; jamais par un `UrlTree` du routeur, que
    le moteur SSR transformerait en 302.
  - `safeReturnUrl` (fonction pure) : un retour après connexion ou action ne vise que le
    même hôte (invariant de confidentialité), côté front comme côté API (L4.2).
  - `/` → 302 selon `Accept-Language` (`Vary: Accept-Language`), géré par `server.ts`.
  - Commutateur en fonction pure `switchContext(chemin, {locale, version, lang})` : réécrit
    le chemin, garde la query hors paramètres traités ; la version n'entre dans le chemin
    que si elle est épinglée et n'est pas la dernière. Priorité chemin > query > cookie.
    `?lang=` choisit une variante régionale Data Dragon ; la canonique pointe vers l'URL
    sans query.
  - Préférences en cookies lus **côté client seulement** : `lod_theme` (L3.2), `lod_prefs`
    (variante de langue, version retenue). Le serveur SSR ne lit aucun cookie.
  - Cache par classe de réponse, posé en SSR : dernière version `public, max-age=0,
    s-maxage=300, stale-while-revalidate=3600` ; version ancienne épinglée
    `public, max-age=3600, s-maxage=604800` ; **redirections et erreurs**, quelle que soit
    la version, `public, max-age=0, s-maxage=60` (une 301 depuis `/{dernière}/…` ou une 404
    d'une version pas encore ingérée ne doivent pas survivre au patch suivant) ; pages
    privées `private, no-store`.
  - Intercepteurs : en-tête `X-LoDb-Client` (apps, valeur fournie par `PlatformService`) et
    traitement des erreurs. L'origine de l'API vient du jeton `API_BASE_URL` (§5.2 du
    plan) ; l'authentification des requêtes revient à `core/auth` (L4.5).
  - L3.1 branche dans le gabarit racine (L0.2) des composants provisoires aux emplacements
    du §5.2 : `features/context-switcher/` (`switcher`), `features/account/account-menu`
    (`account`), `features/account/verify-email-banner` (`banner`),
    `features/contact/contact-dialog` (`contact`). Chaque chantier concerné remplace son
    fichier provisoire, sans toucher au gabarit racine.
  - Pages d'erreur 404 et 500 avec leur vrai statut, `noindex`.
- **Tests** : fonctions pures pilotées par table (grammaire d'URL, chaque règle canonique
  de l'ADR 0005, commutateur — cas repris de `urls.spec.ts` —, négociation
  `Accept-Language`) ; resolvers et guards avec API simulée ; modes de rendu par route.
- **Acceptation** : un script `curl` sur une table d'URLs vérifie statuts et `Location` en
  SSR ; lint et tests verts.
- **Dépend de** : lot 2. **Taille** : L.

## L3.2 — Design system Hextech, mise en page, thèmes, RTL

- **Objectif** : les couches globales du design system portées dans Angular, les primitives
  d'interface, la coquille de mise en page, les quatre thèmes et le RTL.
- **À lire** : `app/assets/styles/{app.css,foundation,primitives,theme}/**`,
  `app/templates/base.html.twig`, `partials/{header,footer,bottom_nav}.html.twig`,
  `components/ui/theme_picker.html.twig`, `components/**` (cadres, cartes, accordéons,
  puces), `app/src/Service/Client/{Theme,ThemeResolver}.php`,
  `app/assets/vue/fx/{theme,reveal,scrollspy,sectionNav}.ts`,
  `app/assets/vue/components/ui/Toaster.vue`, `app/assets/vue/images/imageLifecycle.ts`,
  [`responsive-mobile.md`](../../architecture/responsive-mobile.md),
  [`brief-design-pages.md`](../../briefs/brief-design-pages.md), `app/public/fonts/`.
- **Périmètre** : `src/LoDb.Web/src/styles/**`, `src/LoDb.Web/src/app/ui/**`,
  `src/LoDb.Web/src/app/core/{layout,theme}/**`, `src/LoDb.Web/public/fonts/**`,
  configuration PostCSS.
- **Conception** :
  - **Tailwind v4 conservé** (configuration CSS : `@theme`, `@utility`, `@layer components`)
    via `@tailwindcss/postcss` dans le build Angular ; un style de composant qui utilise
    `@apply` déclare `@reference` vers la feuille globale ; les configurations v3 mortes ne
    sont pas reprises.
  - Couches globales ici (foundation, primitives, utilitaires `hx-*`, thèmes en dernier) ;
    les styles propres à une feature (codex, showcase, account, builds, community,
    changelog) partent avec elle, portés par le chantier de la feature.
  - Quatre thèmes via `data-theme` : `hextech` (défaut), `zaun`, `noxus`, `spirit-blossom`,
    avec leur `theme-color` ; sélecteur en dialogue ; cookie `lod_theme` écrit côté client
    (365 jours) et validé contre la liste. Couleurs de jeu jamais thématisées (voies de
    runes, types de dégâts, raretés).
  - Fontes auto-hébergées (Beaufort, Spiegel en woff2 ; fontes d'affichage des thèmes
    Grenze, Archivo, Shippori Mincho avec `unicode-range`).
  - Enveloppe `<lodb-shell>` (`core/layout/`), qui garde les emplacements du §5.2 du plan :
    en-tête (marque, puce de version vers le changelog, navigation « Codex », tendances,
    développeurs, dons, emplacements `account` et `switcher`), pied de page (liens,
    mentions, avertissement Riot, emplacement `contact`), barre de navigation basse sous
    `md`, emplacement `banner` pour le bandeau d'e-mail non vérifié, service de toasts.
  - Primitives de présentation : cadres, boutons, puces, champs (16 px au moins), dialogue
    et *bottom sheet* (CDK), accordéon, carte, onglets, squelettes, image (états de
    chargement, repli, `<picture>` WebP), navigation de section (scrollspy + ancres),
    apparition au défilement (respecte `prefers-reduced-motion`), pager.
  - RTL : `dir` selon la locale (`ar`), `Directionality` du CDK, propriétés logiques.
  - Mobile : aucun débordement horizontal dès 320 px ; marque réduite sous 460 px, puce de
    version masquée sous 400 px ; pointeur grossier.
  - Galerie de développement (route absente du build de prod) qui montre chaque primitive.
- **Tests** : service de thème (lecture, écriture, validation), logique des directives en
  fonctions pures, service de toasts.
- **Acceptation** : la galerie s'affiche dans les quatre thèmes, en LTR et en RTL, sans
  débordement à 320 px (sonde Playwright) ; captures 320, 390, 768 et 1440 px jointes au
  compte rendu.
- **Dépend de** : lot 0. **Taille** : L.

## L3.3 — i18n

- **Objectif** : les 84 catalogues YAML convertis une fois pour toutes, Transloco en SSR sans
  double chargement, et un rapport de complétude visible en CI.
- **À lire** : `app/translations/*.yaml` (`messages`, `seo`, `about`, `api` × 21 locales),
  `app/assets/vue/i18n/formatTemplate.ts`, `app/src/Service/I18n/UiLocaleResolver.php`,
  ADR [0006](../adr/0006-front-angular-multi-cibles.md) (i18n).
- **Périmètre** : `tools/next/i18n/**` (dont les fichiers appelés par les scripts
  `i18n:convert` et `i18n:report` déclarés par L0.2), `src/LoDb.Web/public/i18n/**`,
  `src/LoDb.Web/src/app/core/i18n/**`, `docs/reecriture/rapports/i18n-completude.md`.
- **Conception** :
  - Script de conversion : clés conservées (imbriquées), `%param%` → `{{ param }}`, les
    9 pluriels à barres → ICU (`transloco-messageformat`), `zh_Hans`/`zh_Hant` →
    `zh-hans`/`zh-hant`. Sortie `public/i18n/<locale>.json` (`messages`) et scopes `seo`,
    `about`, `api`. Les clés `email.*` partent vers les ressources du serveur (L4.3), pas
    vers le front.
  - Transloco : langues disponibles tirées du tableau de locales généré, repli `en` clé par
    clé ; chargeur **HttpClient** (URL relative des catalogues) : suivi par le rendu SSR et
    le prérendu (sans lecture disque), et repris dans le navigateur par le cache de
    transfert, sans second téléchargement.
  - Complétude : clés manquantes par locale et par scope face à `en` (aujourd'hui 426 clés
    manquent dans chacune des 19 locales incomplètes), rapport non bloquant produit par
    `i18n:report` (job `i18n` de la CI).
- **Tests** : conversion (placeholders, pluriels, imbrication) sur des YAML d'exemple ;
  chargeur en SSR (traductions présentes dans le HTML rendu) ; repli d'une clé manquante.
- **Dépend de** : lot 0. **Taille** : M.

## L3.4 — SEO

- **Objectif** : tout le SEO actuel porté, plus hreflang et les sitemaps par locale.
- **À lire** : `app/src/Twig/SeoExtension.php`, `app/src/Service/Seo/**` (JSON-LD,
  `SitemapBuilder`, `LlmsTxtBuilder`, `CanonicalHost`, `OgLocale`, inventaire),
  `app/src/Controller/Seo/**`, `app/config/packages/seo.yaml`, `app/public/robots.txt`,
  blocs meta de `base.html.twig`, [`seo-indexabilite.md`](../../produit/seo-indexabilite.md),
  `heritage.md` § 6 (SEO).
- **Périmètre** : `src/LoDb.Web/src/app/core/seo/**`, `src/LoDb.Api/Modules/Seo/**`,
  `src/LoDb.Web/public/preview/**`, `docker/next/nginx/server.d/seo.conf`, tests associés.
- **Conception** :
  - Titre « {page} — {site} », suffixe de patch sur les pages versionnées (clé
    `seo.versioned_suffix`) ; variantes des pages de compte et du portail.
  - Canonique = schéma + hôte canonique + chemin, sans query ; hreflang entre les 21
    locales + `x-default` sur les pages indexables ; pages de version ancienne
    auto-canoniques.
  - `robots` : `noindex` sur les pages privées, les erreurs, `/b/*` ; `noindex, follow` sur
    les retours de don.
  - Open Graph : image par page (`/preview/*.png`, icône de rune, splash du skin du profil),
    `og:locale` dérivée de la locale.
  - JSON-LD : `@graph` du site (WebSite, Organization, WebPage) partout ; ItemList (20 au
    plus) sur les listes ; BreadcrumbList + VideoGame sur les détails, champions en
    `character` (Person), objets, voies de runes et sorts en `gameItem` (Thing), stats en
    PropertyValue avec `unitText` ; AboutPage, Dataset, FAQPage ; ProfilePage + ItemList
    sur le profil public ; BreadcrumbList + ItemList sur les tendances. Jamais Product,
    Offer ni SearchAction. Sérialisation sûre contre la XSS, champs vides retirés.
  - Côté API : `/sitemap.xml` (index), `/sitemaps/{locale}/latest.xml` (routes statiques et
    détails en URL courte, chemins tirés de `canonicalPath`), `/sitemaps/{locale}/{version}.xml`
    (immuable un an) ; pas de `lastmod`. Le sitemap d'une version froide ingère ses datasets
    par la file limitée ; en attendant, 503 + `Retry-After`. `/robots.txt` et `/llms.txt`
    générés avec l'hôte canonique.
  - Tout `/sitemaps/` appartient à ce chantier, anciennes formes comprises :
    `/sitemaps/latest.xml` → 301 vers `/sitemap.xml`, `/sitemaps/{version}.xml` → 301 vers
    `/sitemaps/en/{version}.xml`, sitemap de la dernière version → 301 vers `latest`. Ces
    redirections sont en cache court (`s-maxage=60`) : elles changent à chaque patch.
  - `server.d/seo.conf` route ces chemins de nginx vers l'API.
- **Tests** : service SEO par type de page (titre, canonique, hreflang, robots) ;
  constructeurs JSON-LD en instantanés + test d'échappement XSS ; sitemaps valides ;
  contenu de `robots.txt` et `llms.txt`.
- **Dépend de** : lot 2. **Taille** : L.

## L3.5 — Socle du catalogue

- **Objectif** : les briques communes aux quatre listes et aux détails, pour que L3.6 à
  L3.8 avancent en parallèle.
- **À lire** : `components/codex/{list_filter,detail_pager,card_accordion}.html.twig`,
  `Resource/AbstractResourceController::listPage`,
  `app/assets/vue/components/catalog/ResourceFilter.vue` et `facets/**`,
  `app/assets/vue/filter/**` (facettes, comptes, cartes visibles, état d'URL),
  `app/src/Service/Catalog/Facet/**`, `app/assets/vue/search/**`,
  `app/assets/vue/images/pendingImages.ts` (comportement à remplacer),
  `app/assets/styles/codex/{list,filter,filter-layout}.css`, et leurs specs.
- **Périmètre** : `src/LoDb.Web/src/app/features/catalogue/shared/**`.
- **Conception** : gabarit de liste (console de filtres collante sur ordinateur ; barre de
  recherche collante et *bottom sheet* sur mobile) ; facettes choix, intervalle et bascule ;
  filtres actifs ; recherche en direct sans accents avec le raccourci `/` ; paramètres
  d'URL `q`, `page`, `size`, `<clé>`, `<clé>_all` conservés ; première page rendue en SSR et
  lisible par les crawlers, dataset complet chargé ensuite pour un filtrage instantané ;
  grille de cartes ; pager précédent/suivant des détails ; placeholders d'une version froide
  avec **une** relance après `Retry-After`.
- **Tests** : cas des specs `facets`, `facetCounts`, `visibleCards`, `urlState`,
  `useUrlSync` repris sur des fonctions pures ; comportement de la console de filtres.
- **Dépend de** : L3.1 à L3.4. **Taille** : M.

## L3.6 — Pages champions

- **Objectif** : liste et détail des champions à parité, SSR.
- **À lire** : `app/templates/champion/**`, `app/assets/vue/components/codex/{AbilityShowcase,
  SkinGallery,ChromaStrip,StatScaler}.vue`, `kit/`, `chroma/`, `timing/useLoadTiming.ts`,
  `app/assets/styles/showcase/**`, `codex/detail.css`, leurs specs, `heritage.md` H1, H3.
- **Périmètre** : `src/LoDb.Web/src/app/features/catalogue/champions/**`.
- **Conception** : héros avec splash, tags, ressource et barres d'évaluation Riot ;
  navigation de section collante ; sorts avec vidéos **muettes par défaut**, arrêtées en
  quittant la page, repli sur l'icône en cas d'erreur, barre de progression ; galerie de
  skins avec visionneuse et chromas (étiquette de teinte, dégradé) sous `@defer (on
  viewport)` ; lore, conseils ; tableau des stats niveaux 1 à 18 (croissance
  `g × (n − 1) × (0,7025 + 0,0175 × (n − 1))`) ; pager ; données SEO de la page ; temps de
  chargement serveur via `Server-Timing`.
- **Tests** : cas des specs `useVideoPlayback`, `AbilityShowcase`, `SkinGallery`,
  `ChromaStrip`, `chromaLabel`, `chromaRamp`, formule des stats.
- **Dépend de** : L3.5. **Taille** : L.

## L3.7 — Pages objets

- **Objectif** : liste et détail des objets à parité, SSR.
- **À lire** : `app/templates/item/**`, `components/codex/card_accordion.html.twig`,
  `app/assets/styles/codex/recipe.css`, `ItemManager.php` (arbre de recette, index).
- **Périmètre** : `src/LoDb.Web/src/app/features/catalogue/items/**`.
- **Conception** : prix, arbre de recette (from/into), texte riche Data Dragon, stats, bilan
  d'or, cartes, catégories, lien vers le jumeau LoL Classic et badge d'édition.
- **Tests** : projection de l'arbre de recette, affichage du jumeau.
- **Dépend de** : L3.5. **Taille** : M.

## L3.8 — Pages runes et sorts d'invocateur

- **Objectif** : listes et détails des runes et des sorts à parité, SSR.
- **À lire** : `app/templates/{rune,summoner}/**`, `components/_rune_path` (constellation),
  `app/assets/styles/showcase/{constellation,seal}.css`.
- **Périmètre** : `src/LoDb.Web/src/app/features/catalogue/{runes,summoners}/**`.
- **Conception** : liste des runes (bandeau des cinq voies + cartes de runes), détail en
  constellation (clés de voûte et rangées) ; sorts : liste en accordéon, détail avec sceau,
  délai de récupération, modes de jeu (liste blanche des libellés), jumeau Classic.
- **Tests** : construction de la constellation, libellés de modes.
- **Dépend de** : L3.5. **Taille** : M.

## L3.9 — Accueil et sélecteur de version et de langue

- **Objectif** : la page d'accueil et le sélecteur de l'en-tête, sans aucun POST.
- **À lire** : `app/templates/home/home.html.twig`, `HomeController.php` (`/setup-submit`),
  partie sélecteur de `partials/header.html.twig`, `app/assets/vue/fx/switchers.ts`.
- **Périmètre** : `src/LoDb.Web/src/app/features/{home,context-switcher}/**`.
- **Conception** : accueil (héros, compteurs du dataset, quatre portails, aperçus 4 × 4) ;
  sélecteur de patch et de langue qui navigue vers le chemin réécrit par `switchContext`,
  case « retenir » écrite dans `lod_prefs`. Le sélecteur figure aussi sur les pages
  prérendues : ses options (versions, langues) se chargent côté client, jamais pendant un
  rendu. `/working-progress` n'est pas repris.
- **Tests** : logique du sélecteur (options, chemin cible, cookie).
- **Dépend de** : L3.1 à L3.4. **Taille** : M.

## L3.10 — Pages éditoriales prérendues et changelog public

- **Objectif** : à propos, données, FAQ, mentions légales et changelog, prérendus pour les
  21 locales.
- **À lire** : `app/templates/{about,legal,changelog}/**`, `app/src/Controller/Editorial/**`,
  `app/src/Service/Changelog/ChangelogReader.php`, `app/public/changelog/*.json`,
  `app/config/packages/legal_info.yaml`, [`legal-info.md`](../../guides/legal-info.md).
- **Périmètre** : `src/LoDb.Web/src/app/features/editorial/**`, l'entrée d'assets de
  `angular.json` qui copie `app/public/changelog/` au build.
- **Conception** : pages à ancres de section ; FAQ tirée du scope `about` ; contenu
  juridique porté tel quel en français et en anglais (`fr` pour les locales `fr*`, `en`
  sinon), paramètres légaux en configuration ; frise du changelog depuis `manifest.json` et
  les fichiers de version, **lus à leur place actuelle** (`app/public/changelog/`, copiés
  au build, aucune copie dans le dépôt : pas de dérive, et l'outillage du changelog joueur
  reste inchangé jusqu'à la bascule) ; la version affichée dans l'en-tête est
  `manifest[0].version`.
  `RenderMode.Prerender` avec `getPrerenderParams` sur le tableau des 21 locales. Aucune
  page prérendue n'appelle l'API pendant son rendu : les compteurs d'inventaire de
  `/about` et `/about/data` (« X champions, patch Z ») se chargent côté client (`@defer`).
- **Tests** : lecture du changelog, choix de la langue juridique ; le prérendu des
  21 locales réussit sans API démarrée.
- **Dépend de** : L3.1 à L3.4. **Taille** : M.

## L3.11 — Cache HTTP, PWA, en-têtes, durcissement SSR

- **Objectif** : le premier cache HTML partagé du projet, une PWA sans page blanche, et une
  surface SSR durcie.
- **À lire** : `app/public/{sw.js,manifest.webmanifest,offline.html}`,
  `app/src/EventSubscriber/SecurityHeadersSubscriber.php` (CSP actuelle),
  `docker/nginx/default.conf`, ADR 0005 (cache, durcissement), `heritage.md` C4, G2, H4.
- **Périmètre** : `docker/next/nginx/**` (hors fichiers réservés : `server.d/seo.conf`,
  `server.d/legacy-redirects.conf`, `server.d/analytics.conf` et
  `snippets/analytics-mirror.conf`), `src/LoDb.Web/src/sw/**`,
  `src/LoDb.Web/public/{manifest.webmanifest,offline.html,favicon,pwa}/**`, la partie
  durcissement de `src/LoDb.Web/src/server.ts`.
- **Conception** :
  - `proxy_cache` devant `web-ssr` : respecte `s-maxage` et `stale-while-revalidate`,
    clé = révision de déploiement + schéma + hôte + URI (aucune purge manuelle), jamais de
    réponse portant `Set-Cookie`, `/` exclu. La location des pages inclut
    `snippets/analytics-mirror.conf`, créé vide ici et rempli par L7.1.
  - Assets front hachés : cache un an, `immutable`, sous un préfixe réservé (`/build/` si
    le constructeur le permet, sinon le préfixe par défaut, documenté) ; aucune page n'est
    routée sur ce préfixe.
  - Service worker maison en TypeScript, enregistré dans le build `web` seulement :
    network-first pour les pages (délai seulement si une copie existe, puis copie, puis
    page hors ligne, **jamais** `respondWith(undefined)`), cache-first pour `/cdn/blobs/`
    et les assets, contournement de `/api`, `/admin`, `/*/account/**`, `/b/`, `/webhooks`,
    requêtes non GET et `Range`.
  - Manifeste web (raccourcis vers les nouvelles URLs), icônes, page hors ligne bilingue.
  - CSP : hash des scripts inline (`autoCsp`), `style-src 'self' 'unsafe-inline'` (styles
    injectés par Angular, comme aujourd'hui), `img-src` et `media-src` limités à Data
    Dragon, CommunityDragon et au CDN vidéo, `connect-src 'self'`, `form-action 'self'
    https://checkout.stripe.com https://billing.stripe.com` ; `frame-ancestors 'none'` et
    les en-têtes constants à l'edge (HSTS deux ans).
  - Serveur SSR : `allowedHosts`, délais et taille de requête bornés, utilisateur non root,
    limite mémoire.
- **Tests** : routage des stratégies du service worker (fonction pure) ; `nginx -t` ;
  scénario `curl` MISS → HIT → STALE puis changement de révision ; en-têtes présents.
- **Dépend de** : L3.1. **Taille** : M.

## L3.12 — Résolveur des 301 héritées

- **Objectif** : chaque ancienne URL renvoie une 301 vers sa nouvelle forme.
- **À lire** : [`plan-migration.md`](../plan-migration.md) (table des 301),
  `app/src/Controller/**` (anciennes routes et paramètres `{name}`).
- **Périmètre** : `src/LoDb.Api/Modules/Legacy/**`,
  `docker/next/nginx/server.d/legacy-redirects.conf`, tests associés.
- **Conception** : nginx envoie les chemins hérités (premier segment `home`, `champion(s)`,
  `object(s)`, `rune(s)`, `summoner(s)`, version numérique, `about`, `faq`, `changelog`,
  `developers`, `donate`, `legal`, `trends`, `u`, `login`, `register`, `profile`,
  `reset-password`, `builds`) à l'API ; `sitemaps` appartient à L3.4. L'API convertit
  l'ancien `{name}` en id canonique par le catalogue et construit la cible avec son
  `canonicalPath`, la locale depuis `?lang=` (`fr_FR` → `fr`, sinon `en`), la version
  depuis le chemin ou `?version=` ; **une seule redirection** : si la version est la
  dernière, la cible est directement l'URL courte. 301 en cache court ; inconnu → 404.
  `/b/`, `/v1/`, `/webhooks/`, `/cdn/blobs/` et `/admin` ne sont pas touchés.
- **Tests** : chaque ligne de la table des 301, avec et sans `?lang=` et version ; aucune
  chaîne de redirections (une ancienne URL de la dernière version arrive en un saut sur
  l'URL courte).
- **Dépend de** : lot 2. **Taille** : M.

## L3.13 — E2E, accessibilité, diff SEO face à la prod

- **Objectif** : prouver la parité visible et le SEO avant la bascule.
- **À lire** : [`architecture-report.md`](../../architecture/architecture-report.md)
  (audits de mise en page et de contraste), `tools/screenshots/capture.mjs`,
  [`responsive-mobile.md`](../../architecture/responsive-mobile.md).
- **Périmètre** : configuration de `tests/LoDb.E2E/` et `tests/LoDb.E2E/specs/public/**`
  (les specs des autres features restent à leurs chantiers), `tools/next/seo-diff/**`,
  `docs/reecriture/rapports/{diff-seo,lighthouse}.md`.
- **Conception** :
  - Suites Playwright : navigation et sélecteurs ; filtres et état d'URL ; ancres de
    section ; arrêt des médias en quittant une page (H1) ; vidéos muettes (H3) ; table des
    301/302/404 (ADR 0005 et héritées) ; `/` selon `Accept-Language` ; mise en page RTL
    (`ar`) ; sonde de débordement à 320 px sur toutes les pages publiques ; champs ≥ 16 px ;
    axe sur les pages clés dans deux thèmes ; assertions SEO (canonique, hreflang, JSON-LD
    parsé et typé, robots, titres) ; service worker et page hors ligne ; en-têtes de cache.
  - Diff SEO : une quarantaine d'URLs de prod (accueil, listes, détails de chaque type dont
    des jumeaux Classic, pages versionnées, éditoriales) comparées à leurs équivalents
    (titre, canonique normalisée, types et champs clés du JSON-LD, description).
  - Lighthouse (Chrome de Playwright, profil mobile) sur cinq pages de la stack locale, avec
    des budgets fixes : performance ≥ 90, accessibilité ≥ 95, SEO ≥ 95, LCP ≤ 2,5 s,
    CLS ≤ 0,1. La prod est mesurée à titre indicatif seulement : réseau, TLS et CDN
    diffèrent.
- **Acceptation** : suites vertes ; budgets tenus ; rapports sans régression inexpliquée.
- **Dépend de** : L3.6 à L3.12 ; accès réseau à la prod. **Taille** : L.
