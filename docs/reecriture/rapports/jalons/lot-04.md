# Jalon du lot 4 (et intégration de la vague D)

- **Date** : 2026-09-26.
- **Branche** : `docs/reecriture-dotnet-angular`. Le jalon intègre la vague D sur `76f54ed` :
  L3.6 (`wt/l3-6-champions`, fusion `ee3b0a8`), L3.7 et L3.8
  (`wt/l3-7-l3-8-objets-runes-sorts`, `cb5735b`), L4.6 (`wt/l4-6-pages-compte`,
  `4b87042`), L10.1 (`wt/l10-1-capacitor`, `01631ec`), L9.1 et L9.2
  (`wt/l9-1-l9-2-desktop`, `ae24d9d`), puis le lockfile régénéré (`292e641`). L4.1 à L4.5
  étaient déjà intégrées.
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5 / npm 12, Docker 29.8.0.
- **Stack** : `lodb-next` reconstruite depuis la racine sur `292e641` (5 services
  `healthy`). Aucune migration nouvelle : la base est à `20260926091745_Lot4Accounts`.

Références : [plan §8](../../plan-implementation.md#8-jalons-et-critères-de-sortie),
[lot 4](../../implementation/lot-04-comptes-profil.md).

## 1. Fusion

Les cinq fusions se sont faites sans conflit. Quatre branches partent de `76f54ed`. La
branche desktop part de `3f953e1` et n'ajoute que des fichiers neufs. Fichiers partagés
repris tels quels :

- `src/LoDb.Web/package.json` (L10.1) : `@capacitor/{core,android,app,browser,share}`,
  `@aparajita/capacitor-secure-storage`, `@capacitor/cli` en dev, et le script
  `build:shell:store` ;
- `src/LoDb.Web/angular.json` (L10.1) : la configuration `shell-store`.

Ajouts de l'intégration :

- `LoDb.slnx` : `src/LoDb.Desktop` et `tests/LoDb.Desktop.Tests`, dans le commit de fusion
  `ae24d9d`. Leurs paquets (Photino, Velopack, YARP, TimeProvider.Testing) étaient déjà
  versionnés dans `Directory.Packages.props`.
- `src/LoDb.Web/package-lock.json` régénéré par `npm install` (`292e641`), avec Capacitor
  et ses dépendances transitives (`@capacitor/ios` et `@capacitor/keyboard`, tirées par
  le stockage sécurisé). `tests/LoDb.E2E/package-lock.json` est inchangé.
- `api:generate` ne change rien : aucune branche de la vague ne touche l'API.

Le dépôt n'a aucun hook de commit (ni `core.hooksPath`, ni husky, ni lefthook) : rien à
rejouer sur les fichiers fusionnés.

## 2. Commandes et résultats

Toutes les commandes sont lancées depuis la racine du dépôt.

| Étape | Commande | Résultat |
|---|---|---|
| Lockfile | `npm install --prefix src/LoDb.Web` | Capacitor ajouté au lockfile, commité en `292e641` |
| Lockfile E2E | `npm install --prefix tests/LoDb.E2E` | inchangé |
| Contrat | `npm --prefix src/LoDb.Web run api:generate` | aucun changement |
| Dérive | `npm --prefix src/LoDb.Web run api:check` | sortie 0 |
| Build .NET | `dotnet build LoDb.slnx -c Release` | 0 avertissement, 0 erreur, 12 projets (desktop compris) |
| Tests .NET | `dotnet test LoDb.slnx` | 1 997 tests : 1 995 réussis, 2 ignorés (run de parité sans `LODB_PARITY_RUN`) ; `LoDb.Desktop.Tests` réussi en 7 s |
| Front | `npm --prefix src/LoDb.Web run lint` | OK |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | 138 fichiers, 1 298 tests réussis |
| Front | `npm --prefix src/LoDb.Web run build:web` | OK, 168 pages prérendues ; bundle initial 638,61 ko (§ 4) |
| Front | `npm --prefix src/LoDb.Web run build:shell` | OK ; 637,93 ko |
| Front | `npm --prefix src/LoDb.Web run build:shell:store` | OK (`dist/shell-store`) ; 637,93 ko |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | 5 services `healthy` |
| nginx | `docker exec lodb-next-nginx-1 nginx -t` | OK, `assetlinks.conf` (L10.1) compris ; `/.well-known/assetlinks.json` répond 404, faute de montage (attendu, L10.4) |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |
| E2E | `npm --prefix tests/LoDb.E2E test` | **132 tests : 126 réussis, 6 échecs**, les mêmes sur trois passages |
| E2E (mesure) | `npm --prefix tests/LoDb.E2E test -- specs/champions specs/items specs/runes specs/summoners specs/catalogue specs/home specs/public specs/seo --repeat-each=5` | 200 tests : 180 réussis, 20 échecs (les 4 tests en échec de ces specs, 5 fois chacun) ; aucun échec intermittent ; pics dans [`memoire.md`](../memoire.md) |
| Critère | `node tools/next/accounts/legacy-logins.mjs` | **48 cas sur 48** (§ 5) |

Les E2E de compte lisent bien les e-mails dans Mailpit (18025) : l'inscription, puis la
confirmation par le lien de l'e-mail, passent avant l'échec de G4. Les comptes `e2e_*`
laissés par les tests en échec ont été supprimés de la base de la stack.

## 3. Échecs

Cinq groupes. Leurs fichiers sont disjoints : ils peuvent être corrigés en parallèle.
Chaque commande de reproduction suppose la stack démarrée.

### G1 — le tiroir de filtres ne défile pas sur téléphone (L3.2 × L3.5, révélé par L3.7)

**Reproduire** :

```bash
npm --prefix tests/LoDb.E2E test -- specs/catalogue/filters.spec.ts -g "bottom sheet"
```

**Sortie utile** : `locator.click: Test timeout of 30000ms exceeded … .sheet__done …
element is outside of the viewport`. Chaîne des ancêtres du bouton, relevée dans
Chromium en 390 × 844 sur `/en/items` :

```text
lodb-dialog.hx-dialog-panel     max-height=none  overflow-y=auto     h=990 top=711
lodb-filter-sheet               max-height=none  overflow-y=visible  h=990
cdk-dialog-container            max-height=100%                      h=990
div.cdk-overlay-pane.hx-sheet   max-height=100%                      h=844
```

`.hx-dialog-panel` borne sa hauteur par `max-block-size: inherit`. Or son parent est
l'hôte du composant (`lodb-filter-sheet`), pas le conteneur CDK : il hérite donc de
`none`. Le panneau fait 990 px et ne défile jamais, et le pied `position: sticky`
tombe hors de l'écran. Les facettes des objets de L3.7 dépassent pour la première fois la
hauteur du volet. Le `max-block-size: 85dvh` de `.hx-sheet` est lui aussi écrasé par le
`max-height: 100%` du CDK.

**Fichiers suspects** : `src/LoDb.Web/src/styles/primitives/dialog.css` (`.hx-dialog-panel`,
`.hx-sheet`), `src/LoDb.Web/src/app/features/catalogue/shared/console/filter-sheet.{css,html,ts}`.

**Correction proposée** : borner le panneau sans passer par l'héritage, par exemple
`max-block-size` explicite sur `.hx-sheet .hx-dialog-panel` et `.hx-dialog .hx-dialog-panel`,
ou `display: contents` sur l'hôte des corps de dialogue. Ajouter un test (E2E ou
navigateur) qui ouvre un dialogue plus haut que l'écran et atteint son pied.

### G2 — les visionneuses de skins et de chromas n'entendent pas le clavier (L3.6)

**Reproduire** :

```bash
npm --prefix tests/LoDb.E2E test -- specs/champions/champions.spec.ts -g "skins once scrolled"
```

**Sortie utile** : `Expected: "Red Riding Annie" / Received: " Goth Annie "` après
`ArrowRight`. Sonde Playwright sur `/en/champions/Annie` : après l'ouverture, le focus est
sur `cdk-dialog-container` (`autoFocus: 'dialog'`, `ui/overlays/dialog-config.ts`), qui est
un ancêtre de `lodb-skin-viewer`. L'écouteur `host: {'(keydown)': …}` de la visionneuse
ne voit donc pas la touche. Un `keydown` émis sur l'hôte fait bien passer au skin suivant.
Le test unitaire émet son événement sur l'hôte, et ne voit donc pas le défaut.
`chroma-viewer.ts` suit le même schéma.

**Fichiers suspects** :
`src/LoDb.Web/src/app/features/catalogue/champions/detail/skins/skin-viewer.ts`,
`.../skins/chromas/chroma-viewer.ts`, et leurs specs (`skins.spec.ts`, `chromas.spec.ts`).

**Correction proposée** : écouter `DialogRef.keydownEvents` (ou le conteneur du dialogue)
plutôt que l'hôte ; dans les tests unitaires, émettre la touche depuis l'élément qui a le
focus après l'ouverture. Ne pas changer l'`autoFocus` global de `ui/overlays` : ce n'est
pas à ce groupe de le faire.

### G3 — le lien « retour à la liste » du pager devient une 404 avec `?lang=` (L3.2 × L3.5)

**Reproduire** :

```bash
curl -s "http://localhost:18080/en/champions/Annie?lang=en_GB" | grep -o 'href="[^"]*%3F[^"]*"'
curl -s -o /dev/null -w "%{http_code}\n" "http://localhost:18080/en/champions%3Flang%3Den_GB"
```

**Sortie utile** : `href="/en/champions%3Flang%3Den_GB"` (lien `pager__hub`), qui répond
`404`. `lodb-pager` et `EntityCard` passent une chaîne à `[routerLink]`, qui encode le `?`
(et encoderait de même un `#fragment`). L3.7 et L3.8 contournent le défaut en leur passant
des liens sans `?lang=` : les cartes et le pager des objets et des sorts perdent donc la
variante régionale. L3.6 passe la chaîne avec la query, d'où la 404.

**Fichiers suspects** : `src/LoDb.Web/src/app/ui/navigation/pager.ts` (et son gabarit),
`src/LoDb.Web/src/app/features/catalogue/shared/cards/entity-card.ts`, et leurs appelants :
`features/catalogue/champions/detail/champion-page.{ts,html}`,
`features/catalogue/champions/list/**`, `features/catalogue/items/**`,
`features/catalogue/runes/**`, `features/catalogue/summoners/**`.

**Correction proposée** : faire accepter une `UrlTree` (ou `commands` plus
`queryParams`/`fragment`) au pager et à la carte, et la leur passer partout avec
`injectCatalogueLink` (`items/codex/`). Dans le même groupe, déplacer `items/codex/` vers
`catalogue/shared`, comme le demandent L3.7 et L3.6. Ajouter un test de rendu qui vérifie
le `href` avec `?lang=`.

### G4 — quatre specs E2E se trompent de cible (L3.8, L3.9, L4.6)

Le produit se comporte comme prévu : ce sont les specs qui visent mal.

1. **Déconnexion ambiguë.** `npm --prefix tests/LoDb.E2E test -- specs/account` renvoie
   `strict mode violation: getByRole('button', { name: 'Sign out' }) resolved to 2
   elements`. Le premier est dans le menu du compte (`banner`), le second dans la
   couverture de l'éditeur (`profile-cover.html`, `main`). `signOut()` doit viser le
   bouton à l'intérieur de `lodb-account-menu`.
2. **Sauvegarde du favori jamais vue.** `npm --prefix tests/LoDb.E2E test --
   specs/profile` renvoie `page.waitForResponse: Test timeout`. La trace montre
   `PUT /api/profile/favorites?version=16.19.1 → 200`. `saved()` compare
   `response.url().endsWith(path)` : la query fait échouer la comparaison. Il faut
   comparer `new URL(response.url()).pathname`. `setPublic()` a le même défaut.
3. **Modes « bruts » mal détectés.** `npm --prefix tests/LoDb.E2E test --
   specs/summoners -g "named modes"` renvoie `Expected: false / Received: true`. Les
   modes affichés sont `ARAM`, `URF`, `Tutorial`… : ce sont de vrais libellés en
   majuscules, que la regex `/^[A-Z0-9_]+$/` prend pour des clés. Il faut comparer aux
   clés brutes des modes renvoyées par l'API, ou exiger un `_`.
4. **Titres de l'accueil.** `npm --prefix tests/LoDb.E2E test -- specs/home -g "four
   portals"` renvoie `Expected: 4 / Received: 9` pour les `h2`. Le compte inclut les 5
   titres du pied de page (`core/layout/footer/footer.html`, antérieur à la spec). Il faut
   limiter le compte à `main`. Aucun fichier de l'accueil ni du pied de page n'a changé
   dans la vague D : l'échec est antérieur à ce jalon.

En plus : les deux parcours de compte ne suppriment pas leur compte quand ils échouent.
Il faut une suppression en `finally` ou en `afterEach`.

**Fichiers suspects** : `tests/LoDb.E2E/specs/account/accounts.ts`,
`tests/LoDb.E2E/specs/account/register.spec.ts`, `tests/LoDb.E2E/specs/profile/profile.spec.ts`,
`tests/LoDb.E2E/specs/summoners/summoners.spec.ts`, `tests/LoDb.E2E/specs/home/home.spec.ts`.

### G5 — la variante store garde les paiements (L10.1 × L3.1, L3.2, L6.5)

**Reproduire** :

```bash
grep -rn "\.payments" src/LoDb.Web/src --include='*.ts' | grep -v spec   # aucun lecteur
grep -n "path: 'donate'" src/LoDb.Web/src/app/app.routes.ts               # route toujours là
```

**Sortie utile** : `payments` est déclaré dans `app-environment.ts` et mis à `false` dans
`environment.store.ts`, mais aucun code ne le lit. `/{locale}/donate` reste routé, et le
lien de don reste dans l'en-tête et le pied de page. L10.1 l'a consigné comme contrôle en
échec (« aucun écran de paiement dans la variante store »). D'après son essai, esbuild émet
de toute façon le chunk paresseux : le retrait doit passer par le routage et le masquage
des liens, pas par l'absence du chunk.

**Fichiers suspects** : `src/LoDb.Web/src/app/app.routes.ts` (et sa spec),
`src/LoDb.Web/src/app/core/layout/header/header.html`, `core/layout/footer/site-links.ts`,
les écrans d'achat de crédits de `features/api-portal/**`, et
`src/LoDb.Web/.prettierignore` (ajouter `/android`, pour qu'un `cap sync` lancé sur
l'hôte ne casse pas `lint`).

**Correction proposée** : route `donate` exclue quand `payments` vaut `false` (spread
conditionnel ou `canMatch`), liens de don retirés, test de routes avec l'environnement
store simulé.

## 4. Points ouverts sans échec de suite

Ils ne font échouer aucune commande, mais doivent être suivis. Aucun ne bloque le
critère.

- **Budget du bundle initial** : 638,61 ko (web) pour 500 ko, contre 608,76 ko avant la
  vague D et 549 ko au jalon 1. L'avertissement reste un avertissement ; à trancher avant
  le jalon 3 (Lighthouse).
- **i18n** : 31 clés racine des pages champions et les clés `profile.*` n'existent qu'en
  `en` et `fr`. Les 19 autres locales retombent sur l'anglais (L3.3).
- **Parité, côté API** : `ChampionCard` n'a pas d'`art.loading`, donc la liste montre
  l'icône carrée ; `ItemCard` n'a pas de champ `into`. Les deux demandent un changement du
  contrat (lot 2). Les icônes `public/icons/stats` sont absentes. Les stats en pourcentage
  des facettes d'objets gardent une décimale, là où le site actuel arrondissait à l'entier.
- **assetlinks** : le JSON produit par `tools/next/android/assetlinks.mjs` doit être monté
  sous `/etc/nginx/android/assetlinks.json` (L8.1, L10.4), et `assetlinks.conf` inscrit
  au tableau de `docker/next/nginx/server.d/README.md`.
- **Desktop** : vérifié sur macOS seulement ; Windows (WebView2, DPAPI) et Linux
  (WebKitGTK) restent à vérifier sur des machines dédiées (lot 9).

## 5. Critère de sortie du lot 4

> Connexion de comptes bcrypt et argon2 (hash au format produit par Symfony) ; hash
> ré-écrit vérifié par `password_verify` de PHP.

**État : vérifié en local** sur la stack d'intégration, le 2026-09-26, sur `292e641`.

Commande : `node tools/next/accounts/legacy-logins.mjs` (options `--base`, `--postgres`,
`--keep`). Pour chacun des 48 cas de `tests/fixtures/hashes/hashes.json` (PHP 8.5.11,
sodium 1.0.18), le script :

1. insère un compte en SQL, en n'écrivant que les colonnes de l'entité Doctrine
   `User` (`roles` à `[]`, `is_verified` à vrai) ; les colonnes Identity gardent leur
   défaut, et `security_stamp` et `concurrency_stamp` restent `NULL` ;
2. se connecte par `POST /api/account/login`, à travers nginx, avec l'`Origin` du site ;
3. relit le hash stocké, et vérifie qu'il est réécrit à la cible
   (`$argon2id$v=19$m=19456,t=2,p=1$`) quand l'original était plus faible, ou conservé à
   l'identique quand il était au moins aussi fort ;
4. se reconnecte par l'e-mail avec le hash réécrit, puis vérifie qu'un mauvais mot de
   passe reçoit un 401 ;
5. passe tous les hash stockés à `tests/fixtures/hashes/verify.php`, dans un conteneur
   `php:8.5-cli` jetable, sans réseau : `password_verify()` puis la vérification de
   `NativePasswordHasher` de Symfony (branche sodium comprise).

| Format (6 mots de passe chacun) | Connexion | Hash | Reconnexion, refus | `password_verify` et Symfony |
|---|---|---|---|---|
| `bcrypt` (`$2y$13`, Symfony) | 6/6 | réécrit | 6/6 | 6/6 |
| `bcrypt-cost4` (`$2y$04`) | 6/6 | réécrit | 6/6 | 6/6 |
| `bcrypt-2a` (`$2a$10`) | 6/6 | réécrit | 6/6 | 6/6 |
| `argon2id-sodium` (m=65536, t=4, Symfony avec sodium) | 6/6 | conservé | 6/6 | 6/6 |
| `argon2id` (m=65536, t=4, `password_hash`) | 6/6 | conservé | 6/6 | 6/6 |
| `argon2i` | 6/6 | réécrit | 6/6 | 6/6 |
| `argon2id-weak` (m=10, t=3) | 6/6 | réécrit | 6/6 | 6/6 |
| `argon2id-lanes2` (p=2) | 6/6 | réécrit | 6/6 | 6/6 |

Les six mots de passe couvrent l'ASCII, les accents, des caractères de 4 octets, plus de
72 octets (pré-hachage SHA-512 de Symfony pour bcrypt), un NUL, et la limite de
4 096 octets. Contre-épreuve : pour un hash réécrit, `verify.php` renvoie `true` avec le
bon mot de passe et `false` avec un mauvais. Après la connexion, Identity a bien écrit
`security_stamp` et `concurrency_stamp`. Les comptes `jalon4_c*` sont supprimés à la fin
du script.

Hors du critère local, et toujours ouvert : la connexion de comptes réels du dump de
répétition est une opération hôte (lot 8, plan §11).

## 6. Contrats relayés

- **L3.13 (E2E publics)** : specs par feature sous `tests/LoDb.E2E/specs/<feature>/`. Les
  aides de compte (`accounts.ts`, `mailbox.ts` ; Mailpit via `LODB_E2E_MAIL_URL`, défaut
  `http://localhost:18025`) vivent dans `specs/account/`. Deux inscriptions au plus par
  passage. Le profil public est mis en cache transient par nginx (`s-maxage=60`).
- **L9.4** : implémenter `Lifecycle/IDesktopUpdates` dans `src/LoDb.Desktop/Updates/`, et
  l'enregistrer par une ligne de `Hosting/DesktopServices.AddDesktopHost`, au-dessus de
  `TryAddSingleton<IDesktopUpdates, NoDesktopUpdates>`. Canal et client Google : propriétés
  MSBuild `LoDbDesktopChannel` et `LoDbDesktopGoogleClientId`. Shell copié depuis
  `LoDb.Web/dist/shell/browser` (`/p:LoDbShellDir=`).
- **L10.2** : `appId` `com.leagueofdatabase.app`, App Link vérifié
  `https://{lodbAppLinkHost}/app/oauth/…`, bundle `dist/shell-store/browser` servi depuis
  `https://localhost` (origine CORS couverte par `AndroidOriginTests`), build par
  `tools/next/android/build-debug.sh` en conteneur `linux/amd64`.
- **Lots 5 à 7** : la connexion par mot de passe et le cookie de session sont éprouvés sur
  la stack ; `tools/next/accounts/legacy-logins.mjs` montre comment insérer un compte
  hérité en SQL pour un test de stack.
