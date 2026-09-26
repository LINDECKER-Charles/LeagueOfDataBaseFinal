# Jalon du lot 3

- **Date** : 2026-09-27 (horloge des outils : 2026-09-26, UTC).
- **Branche** : `docs/reecriture-dotnet-angular`, sur `ebe34c7`. Toutes les branches du
  lot 3 sont fusionnées, la dernière (L3.13) par l'intégration de la vague E (`b229b2b`).
  La vague F (L5.2, L5.3, L6.3, L6.5, L7.1, L9.0 à L9.3) est aussi intégrée.
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5 / npm 12.
- **Stack** : `lodb-next` reconstruite depuis la racine sur `ebe34c7` (5 services
  `healthy`, `nginx -t` OK). Migration appliquée au démarrage :
  `20260926185409_Lot6BillingAnalyticsApps`. Base chargée, dernière version 16.19.1.

Références : [plan §8](../../plan-implementation.md#8-jalons-et-critères-de-sortie),
[lot 3](../../implementation/lot-03-web-public.md) (critère de sortie, L3.13).

Ce jalon ne corrige rien : il consigne les échecs pour l'agent de correction.

## 1. Commandes et résultats

Toutes les commandes sont lancées depuis la racine du dépôt.

| Étape | Commande | Résultat |
|---|---|---|
| Dépendances | `npm ci --prefix src/LoDb.Web` ; `npm ci --prefix tests/LoDb.E2E` | OK |
| Build .NET | `dotnet build LoDb.slnx -c Release` | 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 2 835 tests : 2 833 réussis, 2 ignorés (run de parité sans `LODB_PARITY_RUN`), 3 min 21 s |
| Front | `npm --prefix src/LoDb.Web run lint` | OK |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | 192 fichiers, 1 699 tests réussis |
| Front | `npm --prefix src/LoDb.Web run build:web` | OK, 168 pages prérendues ; bundle initial **649,08 ko** pour un budget de 500 ko (avertissement) |
| Front | `npm --prefix src/LoDb.Web run build:shell` | OK ; bundle initial 648,59 ko (avertissement) |
| Dérive | `npm --prefix src/LoDb.Web run api:check` | sortie 0 : contrat et client conformes |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | 5 services `healthy` |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |
| E2E (1) | `npm --prefix tests/LoDb.E2E test` | **264 tests : 226 réussis, 37 échecs, 1 ignoré** (1,2 min) |
| E2E (relance) | `npm --prefix tests/LoDb.E2E test -- specs/context-switcher specs/builds-editor specs/builds-share specs/trends` | 15 tests : 7 réussis, 7 échecs, 1 ignoré |
| E2E (2) | `docker restart lodb-next-api-1`, puis `npm --prefix tests/LoDb.E2E test` | 264 tests : 227 réussis, **36 échecs**, 1 ignoré (54 s) |
| Accessibilité | `specs/public/accessibility.spec.ts` (dans les deux passages) | **26 sur 26** : axe sans violation sur 11 pages clés, la 404 et une page RTL, dans les thèmes hextech et spirit-blossom |
| Diff SEO | `node --test 'tools/next/seo-diff/test/*.test.mjs'` | 15 tests réussis |
| Diff SEO | `node tools/next/seo-diff/diff.mjs --stack lodb-next` | sortie 0 : 42 pages, **0 écart non expliqué**, 1 défaut consigné ([rapport](../diff-seo.md)) |
| Lighthouse | `node --test 'tools/next/lighthouse/test/*.test.mjs'` | 5 tests réussis |
| Lighthouse | `node tools/next/lighthouse/run.mjs --stack lodb-next` | sortie 1 : **5 pages sur 5 hors budget** en performance et en LCP ([rapport](../lighthouse.md)) |

Le test ignoré est `trends.spec.ts:71` (« pages through the ranking ») : la stack n'a
qu'une page de builds publics. Les pics mémoire sont consignés dans
[`memoire.md`](../memoire.md). `api` y dépasse sa limite provisoire de 384m (541 Mio).

Les deux passages complets donnent les mêmes échecs, à une exception près :
`context-switcher.spec.ts:101` (« remembers the choice ») échoue au premier passage
seulement. Il passe aussi seul : il est **intermittent** (G3).

Entre deux passages complets, il faut redémarrer `api` (`docker restart
lodb-next-api-1`) : sinon le limiteur d'inscriptions, qui vit en mémoire, fait échouer
les parcours qui créent un compte (G5).

## 2. Échecs du lot 3

Six groupes, G1 à G6, aux fichiers disjoints : on peut les corriger en parallèle. Chaque
commande de reproduction suppose la stack démarrée.

### G1 — l'en-tête déborde à 320 px sur toutes les pages (L3.2 × L3.9)

**Reproduire** :

```bash
npm --prefix tests/LoDb.E2E test -- specs/public/layout.spec.ts -g "320 px"
```

**Sortie utile** : 29 tests en échec sur 29 (toutes les pages publiques, archives, 404
et RTL). Le message est le même partout :

```text
/en/ lays out wider than the phone: span.brand-logo.shrink-0.text-gold [16, 48],
div.flex.shrink-0.items-center [24, 333]      Expected: <= 320  Received: 333
/ar/ lays out wider than the phone: … div.flex.shrink-0.items-center [-1, 308]
```

Sonde Playwright à 320 px sur `/en/` : le groupe d'actions de l'en-tête
(`div.flex.shrink-0.items-center`) mesure 309 px pour 288 px utiles (320 moins les
`px-4`). Il contient le lien de dons (38 px), `lodb-account-menu` (65 px),
`lodb-theme-picker` (40 px) et `lodb-context-switcher` (**141 px** : version, point,
code de langue et chevron), avec 3 écarts de 8 px. Le groupe est en `shrink-0` et la
marque est en `min-w-0` : c'est la marque qui cède et le document s'élargit à 333 px.
Aucun fichier de l'en-tête, du sélecteur ni du menu de compte n'a changé depuis la
fusion de L3.13 (`b229b2b`).

Les champs de 16 px et plus passent partout : seul le débordement échoue.

**Fichiers suspects** : `src/LoDb.Web/src/app/core/layout/header/header.html`,
`src/LoDb.Web/src/app/features/context-switcher/context-switcher.html`,
`src/LoDb.Web/src/styles/layout/chrome.css` (`.switcher`).

**Correction proposée** : sous 360 px, compacter le résumé du sélecteur (ne garder que
la version ou que la langue, l'autre restant dans le panneau et dans l'`aria-label`), ou
resserrer les écarts du groupe d'actions. Ne pas toucher aux fichiers de thème
(`styles/theme/*/type.css`, `frames.css`), réservés à G2. Relancer toute la sonde de
320 px : elle couvre aussi `ar`.

### G2 — le titre de section de `/en/about` ne passe pas à la ligne (L3.10 × L3.2)

**Reproduire** :

```bash
npm --prefix tests/LoDb.E2E test -- specs/public/layout.spec.ts -g "fit /en/about in"
```

**Sortie utile** : `/en/about lays out wider than the phone: h2 [24, 358], …`. Le `h2`
« Where the data comes from » est dans un `div.codex-header`, en
`white-space: nowrap` (`styles/primitives/surfaces.css`, règle `.codex-header h2`) :
358 px pour 320. Le test échoue d'abord à cause de G1 : ce défaut ne se voit qu'une fois
G1 corrigé, ou dans la liste des éléments fautifs.

**Fichiers suspects** : `src/LoDb.Web/src/styles/primitives/surfaces.css`
(`.codex-header h2`, vers la ligne 90), les surcharges `.codex-header h2` de
`src/LoDb.Web/src/styles/theme/{spirit-blossom,noxus,zaun}/type.css`,
`src/LoDb.Web/src/app/features/editorial/about/about-page.html`.

**Correction proposée** : autoriser le retour à la ligne des titres de section sous une
largeur donnée (ou `overflow-wrap` avec `text-wrap: balance`), sans casser la ligne
unique voulue en large. Vérifier les autres pages éditoriales et la galerie.

### G3 — le sélecteur de contexte compte la balise d'analytics comme un POST (L3.9 × L7.1)

**Reproduire** :

```bash
npm --prefix tests/LoDb.E2E test -- specs/context-switcher
```

**Sortie utile** :

```text
context-switcher.spec.ts:60 › pins an older patch on the same page, without a POST
- Array []
+ Array [ "http://localhost:18080/api/analytics/view" ]
```

Le test vérifie que changer de version ne passe pas par un POST de préférences. Depuis
L7.1 (`core/analytics/navigation-beacon.ts`, commit `84e19ea`), chaque navigation interne
envoie une balise `POST /api/analytics/view` : elle est légitime et ne touche pas les
préférences.

Second test, **intermittent** : au premier passage seulement,
`context-switcher.spec.ts:101` (« remembers the choice ») expire sur
`locator('#switcher-remember').check()` avec `element is not visible`. Il passe seul et
au second passage complet. La case est lue alors que le panneau n'est pas encore (ou plus)
ouvert.

**Fichier suspect** : `tests/LoDb.E2E/specs/context-switcher/context-switcher.spec.ts`.

**Correction proposée** : ne compter que les POST qui ne sont pas la balise d'analytics
(filtrer `/api/analytics/`), ou n'accepter que les requêtes vers les préférences. Pour
l'intermittent : attendre que le panneau soit ouvert (`toHaveAttribute('open')` ou case
visible) avant `check()`, sans `force`. Ne pas retirer la balise : elle appartient à L7.1.

### G4 — performance et LCP hors budget sur les 5 pages mesurées (L3.11, puis bundle)

**Reproduire** :

```bash
node tools/next/lighthouse/run.mjs --stack lodb-next --skip-prod --out /tmp/lighthouse.md
js=$(curl -s localhost:18080/en/ | grep -oE '/build/main-[A-Za-z0-9_-]+\.js' | head -1)
curl -s -D - -o /dev/null -H 'Accept-Encoding: gzip, br' "localhost:18080$js" | grep -i '^content-'
```

**Sortie utile** : performance 63 à 69 (budget 90), LCP 5,1 à 7,4 s (budget 2,5 s), FCP
4,8 à 5,1 s, même sur la page prérendue `/en/about`. Accessibilité (98 à 100), SEO
(100) et CLS (au plus 0,011) tiennent. Sur chaque page, Lighthouse relève
**10 à 19 ressources texte servies sans compression** (500 à 642 Kio), toutes des
`/build/*.js`. Le `curl` le confirme : `Content-Type: text/javascript; charset=utf-8`,
sans `Content-Encoding`. Le serveur SSR sert les bundles en `text/javascript`, alors que
`gzip_types` (`docker/next/nginx/nginx.conf`, ligne 31) ne liste que
`application/javascript`. Le manifeste (`application/manifest+json`) manque aussi à la
liste (2 Kio, relevé aussi en prod). Les 14 scripts de `/en/` pèsent 500 Kio bruts et
160 Kio en gzip. Sur la 4G lente simulée (1,6 Mbit/s), l'écart représente environ 1,7 s
de transfert. La prod, mesurée avec les mêmes réglages et à titre indicatif, compresse
ses scripts : 96 à 100 en performance, sauf la liste des champions (60, CLS 0,465).

**Fichiers suspects** : `docker/next/nginx/nginx.conf` (`gzip_types`), puis le bundle
initial de 649 ko (budget de 500 ko dépassé depuis le lot 1 ; + 10 ko avec la vague F).

**Correction proposée** : en premier lieu, ajouter `text/javascript` et
`application/manifest+json` à `gzip_types`, reconstruire `nginx`, puis relancer
Lighthouse. Si la performance reste sous 90, relever la piste suivante dans le rapport
(`unused-javascript`, 114 à 193 Kio, `bootup-time`) : c'est le bundle initial qu'il faut
alléger (routes paresseuses, dépendances chargées au démarrage). Ce second temps touche
des fichiers de `core/` et d'`app.config.ts`, et il se découpe après la nouvelle mesure.
Ne jamais relâcher un budget ni le profil de mesure de `tools/next/lighthouse/`.

### G5 — la suite complète dépasse le quota d'inscriptions (L3.13 × L4.2)

**Reproduire** :

```bash
docker restart lodb-next-api-1   # vide le limiteur
npm --prefix tests/LoDb.E2E test
grep -l "Too many attempts" tests/LoDb.E2E/test-results/*/error-context.md
```

**Sortie utile** : `trends.spec.ts:86` échoue dans `register` (`accounts.ts:35`) :
`Expected pattern: /\/en\/account\/profile$/ · Received: …/en/account/register`. La page
affiche l'alerte « Too many attempts from your connection ». La politique
`registration` (`src/LoDb.Api/Hosting/RateLimitingPolicies.cs`) accorde 5 inscriptions
par heure et par adresse. Toutes les requêtes des E2E partent du même poste et
arrivent à l'API par nginx, sous une seule adresse. Or la suite crée au moins 6 comptes : `account/register.spec.ts`,
`profile/profile.spec.ts`, `builds-editor`, `builds-share` (deux fois) et `trends`. Le
dernier à s'inscrire échoue, et une relance sans redémarrer `api` fait échouer les
précédents.

**Fichiers suspects** : `tests/LoDb.E2E/playwright.config.ts`, `tests/LoDb.E2E/support/`
(nouveau fixture de compte partagé), `tests/LoDb.E2E/specs/account/accounts.ts`.

**Correction proposée** : un compte vérifié par worker, créé une fois par un fixture
(`support/`) et partagé par les parcours qui n'éprouvent pas l'inscription elle-même.
`register.spec.ts` garde sa propre inscription. Ne pas relever le quota en code. Rendre
le quota configurable pour la surcouche locale reste possible, mais ce serait une
décision de L4.2 (`src/LoDb.Api/Hosting/`, `compose.next.override.yaml`), hors fichiers du
lot 3 : à signaler, pas à contourner.

### G6 — `Dataset.inLanguage` de `/about/data` liste les locales du site (L3.10)

**Reproduire** :

```bash
node tools/next/seo-diff/diff.mjs --stack lodb-next --out /tmp/diff-seo.md
grep -A3 "### \`dataset-languages\`" /tmp/diff-seo.md
```

**Sortie utile** : défaut consigné par la règle `dataset-languages` (seul défaut de la
diff) : `Dataset.inLanguage[0]` : `ar_AE` → `ar` (et 27 autre(s)). La page liste les 21
locales du site au lieu des 28 langues de Data Dragon. C'est un écart au « JSON-LD
conservé à l'identique » de l'ADR 0005. La prod les écrit au format `fr_FR`, hors
BCP 47.

**Fichier suspect** : `src/LoDb.Web/src/app/features/editorial/about/about-data-page.ts`
(et son spec).

**Correction proposée** : les 28 langues de Data Dragon converties en BCP 47 (`fr-FR`),
puis retirer ou requalifier la règle `dataset-languages` de
`tools/next/seo-diff/lib/rules.mjs` et relancer l'outil, qui réécrit le rapport. Ne
jamais modifier `diff-seo.md` à la main.

## 3. Échecs hors lot 3 (pour le jalon des lots 5 à 7)

Consignés ici parce que le jalon lance tous les E2E. Leurs fichiers sont ceux des lots 5
et suivants : ils ne relèvent pas de la correction du lot 3.

| Test | Sortie utile | Cause | Fichiers suspects |
|---|---|---|---|
| `builds-share/private.spec.ts:16`, `public.spec.ts:30` | `the picker /api/pickers/champions answers · Expected: true · Received: false` | l'aide appelle `/api/pickers/champions` sans `version` ni `lang`, qui sont obligatoires : 400 `invalid-version` (`PickerRequest.cs`) | `tests/LoDb.E2E/specs/builds-share/builds.ts` (lignes 96 à 98) |
| `builds-editor/builds-editor.spec.ts:43` (étape « leads to the forge on the latest patch ») | `getByLabel('Patch', { exact: true }) · element(s) not found`, alors que l'instantané final montre `combobox "Patch"` avec 16.19.1 sélectionnée | champ rendu après les 5 s d'attente, ou nom accessible différent du texte visible ; stable sur trois exécutions | `src/LoDb.Web/src/app/features/builds/editor/form/build-editor.{html,ts}`, `tests/LoDb.E2E/specs/builds-editor/builds-editor.spec.ts` |
| `trends/trends.spec.ts:22` | canonique attendue `http://localhost:18080/en/trends`, reçue `http://localhost/en/trends` | le test compare l'origine de la page, alors qu'en local toutes les canoniques perdent le port (piège connu de `CLAUDE.md`) | `tests/LoDb.E2E/specs/trends/trends.spec.ts` |
| `trends/trends.spec.ts:58` (« filters without scripts ») | après envoi du formulaire, l'URL porte `mode=arena`, mais `Game mode` vaut `""` | le rendu serveur ne présélectionne pas l'option lue dans la query | `src/LoDb.Web/src/app/features/builds/trends/filters/trend-filters.{html,ts}` |
| `trends/trends.spec.ts:86` | inscription refusée (quota) | G5 | — |

Autres constats, déjà relevés par l'intégration de la vague F :

- donate et trends restent `provisional` dans `tests/LoDb.E2E/support/public-pages.ts`,
  donc hors de `seo.spec` ;
- la rubrique « Hors échantillon » de `diff-seo.md`, écrite par
  `tools/next/seo-diff/lib/report.mjs`, les dit encore sans SEO propre.

Mémoire : `api` culmine à 541 Mio pendant la suite complète, au-dessus de la limite
provisoire de 384m ([`memoire.md`](../memoire.md)). C'est à trancher avant L8.1, avec G2
du jalon 1.

## 4. Critère de sortie local du lot 3

| Élément | État | Preuve |
|---|---|---|
| Diff SEO sur un échantillon face à la prod (rapport) | **tenu** : 42 URLs, 301, canoniques et types JSON-LD identiques partout, 0 écart non expliqué ; 1 défaut consigné (G6) | [`diff-seo.md`](../diff-seo.md) |
| E2E verts | **non tenu** : 30 échecs dans les specs du lot 3 (29 de la sonde de 320 px, G1 et G2 ; 1 stable et 1 intermittent dans le sélecteur de contexte, G3). Les autres specs du lot 3 passent : catalogue, champions, objets, runes, sorts, accueil, éditorial, anciennes URLs, routage, SEO, PWA, cache, médias, navigation, issues | § 1, § 2 |
| Accessibilité verte | **tenu** : axe 26 sur 26 dans deux thèmes ; Lighthouse accessibilité 98 à 100 | § 1, [`lighthouse.md`](../lighthouse.md) |
| Budgets Lighthouse sur la stack locale | **non tenu** : performance 63 à 69 et LCP 5,1 à 7,4 s sur 5 pages sur 5 (G4) ; accessibilité, SEO et CLS tenus | [`lighthouse.md`](../lighthouse.md) |
| Prod à titre indicatif | mesurée : 96 à 100 en performance, sauf la liste des champions (60 ; CLS 0,465, défaut de la prod) | [`lighthouse.md`](../lighthouse.md) |

**Verdict** : critère du lot 3 **non vérifié**. À corriger : G1 à G6 ; puis la
vérification relance la stack, tous les E2E (après `docker restart lodb-next-api-1` tant
que G5 n'est pas corrigé), la diff SEO et Lighthouse.
