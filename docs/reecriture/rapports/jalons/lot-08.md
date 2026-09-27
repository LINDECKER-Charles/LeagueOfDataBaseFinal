# Jalon du lot 8 (bascule)

- **Date** : 2026-09-27.
- **Branche** : `docs/reecriture-dotnet-angular`, sur `fed0c64`. L8.1 et L8.2 étaient déjà
  fusionnés (`20e39de`, `5855ffb`), puis corrigés à l'intégration (`8e97cc4`, `4fe0c23`), et
  L8.3 était commité (`fed0c64`). Ce jalon ne fusionne rien et ne régénère rien :
  `api:check` est vert, aucun `package.json` n'a changé.
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5 / npm 12, Docker 29 (11,67 Gio,
  dont environ 4,3 Gio occupés par les autres projets au départ).
- **Stack `lodb-next`** : reconstruite depuis la racine sur `fed0c64`, avec
  `IMAGE_TAG=APP_REVISION=fed0c64` (piège du jalon 3). Ses 5 services sont `healthy` et ses
  images portent `org.opencontainers.image.revision=fed0c64`. Aucune migration nouvelle :
  la base est à `20260926185409_Lot6BillingAnalyticsApps`.
- **Ancienne stack** (projet `lodb`) : arrêtée au départ, depuis ce même dossier. Elle a été
  démarrée puis arrêtée par la répétition (`--stop-legacy`), puis son seul `postgres` a été
  relancé un instant pour vérifier la remise en état (§ 4.3). Aucun fichier suivi touché,
  `.env` inchangé (identique à `.env.example`).

Références : [plan §8](../../plan-implementation.md#8-jalons-et-critères-de-sortie),
[lot 8](../../implementation/lot-08-bascule.md), [runbook](../../bascule.md) § 3.1,
[outils de bascule](../../../../tools/next/cutover/README.md).

## 1. Commandes et résultats

Toutes les commandes sont lancées depuis la racine du dépôt.

| Étape | Commande | Résultat |
|---|---|---|
| Build .NET | `dotnet build LoDb.slnx -c Release` | 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 2 936 tests : 2 934 réussis, 2 ignorés (parité sans `LODB_PARITY_RUN`), 0 échec (3 min 41 s) |
| Front | `npm ci --prefix src/LoDb.Web` | OK |
| Front | `npm --prefix src/LoDb.Web run lint` | OK |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | 233 fichiers, 2 035 tests réussis |
| Front | `npm --prefix src/LoDb.Web run build:web` | OK, 168 pages prérendues ; initial 654,84 ko, avertissement de budget (500 ko) connu (G4 du jalon 3) |
| Front | `npm --prefix src/LoDb.Web run build:shell` | OK ; 654,01 ko, même avertissement |
| Dérive | `npm --prefix src/LoDb.Web run api:check` | sortie 0, aucune dérive |
| Stack | `IMAGE_TAG=fed0c64 APP_REVISION=fed0c64 docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build --wait` | sortie 0, 5 services `healthy` |
| CLI de l'hôte | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml exec -T api dotnet LoDb.Api.dll analytics import --source /x --dry-run` | `No such directory: /x` : l'hôte se construit (piège des lots 5 à 7) |
| E2E | `npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install` | OK (runbook § 3.1) |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |
| E2E (suite complète) | `npm --prefix tests/LoDb.E2E test` | **302 tests : 284 réussis, 10 échecs, 7 non lancés, 1 ignoré** (1,4 min) ; § 3 |
| E2E (diagnostic) | `docker restart lodb-next-api-1` puis `npm --prefix tests/LoDb.E2E test -- specs/admin specs/context-switcher` | 33 tests : 23 réussis, 3 échecs (G5 du lot 7), 7 non lancés ; ne compte pas pour le critère |
| Outils de bascule | `node --test 'tools/next/cutover/test/*.test.mjs'` | 16 réussis |
| *Contract* | `tools/next/contract/check.sh` | `CONTRACT OK` : appliqué sur une base jetable, instants stables sous Europe/Paris, second passage sans effet, garde qui refuse sans historique EF |
| **Répétition locale** | `tools/next/cutover/rehearse.sh --slot 2 --anonymize --stop-legacy 2>&1 \| tee /tmp/lodb-rehearsal.log` | **15 étapes `ok`**, « The rehearsal passes » ; § 4 |

Pics mémoire de la suite complète : [`memoire.md`](../memoire.md), section du lot 8.

## 2. Écarts entre le runbook et ce qu'il a fallu faire

La répétition a suivi le § 3.1 du [runbook](../../bascule.md) **tel qu'écrit**, dans
l'ordre : `.env` présent, installation de la suite E2E, tests des outils,
`contract/check.sh`, puis `rehearse.sh --slot 2 --anonymize --stop-legacy`. Aucune commande
n'a été ajoutée, retirée ni modifiée ; aucun écart de procédure. Le runbook n'a donc pas été
corrigé par ce jalon.

Remarques sur le texte, à trancher par la correction (groupe G5, § 3) :

1. **Code de sortie masqué par `tee`.** Le runbook dit que le script « réussit (code 0) »,
   mais sa commande se termine par `| tee /tmp/lodb-rehearsal.log` : `$?` est celui de
   `tee`. Sous zsh (shell par défaut de macOS), `${PIPESTATUS[0]}` est vide ; il faut
   `$pipestatus[1]`, ou `set -o pipefail` avant la commande. Ce jalon a jugé la réussite
   sur le résumé (15 lignes `ok` et « The rehearsal passes », qu'imprime seulement la
   branche de sortie 0).
2. **Base de dev sans compte à mot de passe.** La base `lodb` du poste compte 1 utilisateur,
   sans mot de passe (`password IS NULL`) : l'option `--anonymize` n'y trouve aucun des
   « cinq comptes de la base » qu'elle devait connecter, sans le signaler. Les comptes
   existants vérifiés sont les 8 comptes écrits dans la copie **avant** `migrate`, comme
   Doctrine les écrit (un par format de hash de `tests/fixtures/hashes`). C'est conforme au
   critère (comptes présents avant la migration), mais le runbook ne le dit pas.
3. **Emplacement `lodb-next-e2` et non `lodb-next`.** Le critère parle de « la nouvelle
   stack » ; le runbook fait servir la copie par l'emplacement 2 et laisse la stack
   d'intégration intacte. Ce jalon a suivi le runbook : `lodb-next` est restée sur sa base
   pendant toute la répétition, il n'y avait donc rien à y remettre.
4. En-tête de `tools/next/cutover/pre-ingest.sh` : l'exemple nomme le projet
   `lodb-prod-next` ; le vrai nom est `lodb-next-prod` (déjà signalé par L8.3).

## 3. Échecs

Cinq groupes, aux fichiers disjoints : ils peuvent être corrigés en parallèle. Chaque
commande de reproduction suppose `lodb-next` démarrée depuis la racine. Le quota
d'inscriptions (5 par heure et par adresse) impose `docker restart lodb-next-api-1` entre
deux passages.

Aucun échec ne touche le critère du lot 8 (§ 4) : les E2E `@readonly` passent tous sur la
copie migrée.

### G1 — rapport de surveillance vide par intermittence (L7.3) — nouveau

**Tests** : `admin/analytics.spec.ts:7` (vue d'ensemble) et `admin/operations.spec.ts:6`
(première assertion) dans la suite complète. Au passage de diagnostic, la vue d'ensemble
réussit : l'échec est intermittent.

**Reproduire** :

```bash
npm --prefix tests/LoDb.E2E test      # suite complète, 4 workers
docker logs lodb-next-api-1 2>&1 | grep admin.monitoring.database_unreadable
```

**Sortie utile** :

```text
analytics.spec.ts:18  expect(getByText('Comptes', { exact: true })).toBeVisible() — element(s) not found
operations.spec.ts:15 expect(getByRole('heading', { level: 2, name: 'Versions Data Dragon' })) — element(s) not found

04:15:36.300Z admin.monitoring.database_unreadable
  Npgsql.NpgsqlException: Received backend message BindComplete while expecting ParseCompleteMessage.
  at LoDb.Api.Modules.Admin.Monitoring.DatabaseFigures.OutboxAsync (DatabaseFigures.cs:50)
04:15:36.682Z … ObjectDisposedException: Cannot access a disposed context instance ('LoDbDbContext')
  at DatabaseFigures.CountersAsync (DatabaseFigures.cs:23)
04:15:36.685Z … same, at LoDbDbContext.get_DdragonVersions() (VersionOverview)
04:15:36.694Z … same, at DatabaseFigures.TablesAsync (DatabaseFigures.cs:64)
```

**Analyse** : dans un même rapport, toutes les lectures en base échouent. `TryAsync`
avale l'erreur et le rapport part avec `Counters`, `Versions` et `Tables` vides. Le cache
hybride le garde 30 s : la vue d'ensemble perd ses chiffres (« Comptes », « Requêtes API du
jour »), et les opérations leur carte « Versions Data Dragon ». Les deux erreurs, protocole
Npgsql puis contexte disposé, montrent un `LoDbDbContext` utilisé par deux flux, puis
libéré pendant la lecture. Hypothèse : la fabrique de `HybridCache.GetOrCreateAsync`
(`MonitoringReporter.ReportAsync`) capture le `MonitoringReporter` **scoped** de la
première requête, donc son contexte. Les requêtes concurrentes partagent cette fabrique.
Si la requête d'origine se termine ou est annulée, sa portée libère le contexte en pleine
lecture ; un `refresh`, qui retire la clé pendant qu'une fabrique tourne, peut aussi
lancer une seconde lecture sur le même contexte. Pistes : une portée propre à la fabrique
(`IServiceScopeFactory`) ou `IDbContextFactory`. Aucun test .NET n'exerce deux appels
concurrents.

**Fichiers suspects** : `src/LoDb.Api/Modules/Admin/Monitoring/MonitoringReporter.cs`,
`DatabaseFigures.cs`, `VersionOverview.cs`, les enregistrements de
`src/LoDb.Api/Modules/Admin/AdminModule.cs` (l. 88-90), un test de concurrence dans
`tests/LoDb.Api.Tests/Admin/`.

**Groupe proposé** : G1 = `src/LoDb.Api/Modules/Admin/**`, `tests/LoDb.Api.Tests/Admin/**`.

### G2 — l'admin repasse en anglais après une action (G5 du lot 7) — persistant

**Tests** : `admin/contacts.spec.ts:61`, `admin/moderation.spec.ts:107` (puis les 6 tests
suivants de son mode série, non lancés), `admin/operations.spec.ts:6` après
« Actualiser », et `contacts.spec.ts:74`, non lancé. Même état qu'à la vérification des
lots 5 à 7.

**Reproduire** :

```bash
docker restart lodb-next-api-1
npm --prefix tests/LoDb.E2E test -- specs/admin
```

**Sorties utiles** (passage de diagnostic, instantanés Playwright) :

- `contacts.spec.ts:69` : `locator.click: Test timeout of 30000ms exceeded` sur
  `getByRole('button', { name: 'Rouvrir' })` ; la carte montre `button "Reopen"`.
- `moderation.spec.ts:115` : `getByText('Privé', { exact: true })` introuvable ; la ligne
  montre `Private`.
- `operations.spec.ts` : `getByText(/^\s*Relevé du /)` introuvable ; la page montre
  `Read at 27/09/2026 04:26:10 (UTC)`.

**Analyse** : celle du [jalon des lots 5 à 7](lots-05-06-07.md) (§ 7.2, G5) tient.
L'admin impose le français par `provideTranslocoLang`, mais le `TranslocoPipe` revient à la
langue active du site quand sa clé ou ses paramètres changent.

**Fichiers suspects** : `src/LoDb.Web/src/app/features/admin/**` (gabarits dont la clé
ou les paramètres du pipe changent, ou un mécanisme commun de l'admin).

**Groupe proposé** : G2 = `src/LoDb.Web/src/app/features/admin/**`.

### G3 — quota d'inscriptions de la suite complète (G4 des lots 5 à 7) — persistant

**Tests** : `builds-editor.spec.ts:33`, `builds-share/private.spec.ts:6`,
`builds-share/public.spec.ts:19`, `profile/profile.spec.ts:32`, `trends/trends.spec.ts:82`.

**Reproduire** :

```bash
docker restart lodb-next-api-1
npm --prefix tests/LoDb.E2E test
```

**Sortie utile** (identique pour les cinq) :

```text
Error: expect(page).toHaveURL(expected) failed
Expected pattern: /\/en\/account\/profile$/
Received string:  "http://localhost:18080/en/account/register"
  at register (specs/account/accounts.ts:35:22)
  at registerVerified (support/worker-account.ts:44:5)
```

**Analyse** : celle de la vérification des lots 5 à 7. `register.spec.ts`, `api-portal`
et `admin/moderation.spec.ts` inscrivent encore leurs propres comptes, en plus de celui de
chaque worker : la limite de 5 par heure est dépassée. Derrière le quota restent deux
défauts de specs, déjà décrits dans ce même rapport : `forge.ts` (sélecteur de champion
déjà ouvert) et `public.spec.ts` (le vote n'est pas attendu avant le rechargement). Ne
jamais relever le quota pour faire passer les tests.

**Fichiers suspects** : `tests/LoDb.E2E/support/worker-account.ts`,
`tests/LoDb.E2E/specs/account/**`, `tests/LoDb.E2E/specs/api-portal/**`,
`tests/LoDb.E2E/specs/admin/admin-account.ts`, `tests/LoDb.E2E/specs/builds-editor/forge.ts`,
`tests/LoDb.E2E/specs/builds-share/public.spec.ts`.

**Groupe proposé** : G3 = ces fichiers. `admin/moderation.spec.ts` n'en fait partie que
pour l'inscription de son membre ; G2 ne touche pas aux specs.

### G4 — `context-switcher.spec.ts:118` avant l'hydratation (signalé par L8.2) — instable

**Reproduire** : `npm --prefix tests/LoDb.E2E test` (suite complète) ; seul
(`… test -- specs/context-switcher`), il réussit, et il réussit aussi dans la répétition
(`@readonly`, 4 workers).

**Sortie utile** :

```text
locator.check: Test timeout of 30000ms exceeded.
  - locator resolved to <input … type="checkbox" class="hx-check" jsaction="change:;" id="switcher-remember"/>
  56 × waiting for element to be visible, enabled and stable — element is not visible
> 129 |     await remember.check();
```

**Analyse** : `jsaction="change:;"` montre une page pas encore hydratée : la case existe
dans le HTML du serveur et n'est pas encore visible. `trends.spec.ts:35` a le même défaut
(L8.2). Relancer n'est pas une correction.

**Fichiers suspects** : `tests/LoDb.E2E/specs/context-switcher/context-switcher.spec.ts`,
`tests/LoDb.E2E/specs/trends/trends.spec.ts` (l. 35 seulement ; la l. 82 relève de G3,
par la fixture).

**Groupe proposé** : G4 = `tests/LoDb.E2E/specs/context-switcher/**`,
`tests/LoDb.E2E/specs/trends/**`.

### G5 — textes du runbook et des outils (§ 2) et restes de L8.1

Rien n'échoue, mais la répétition et les contrôles montrent des manques de texte :

- `docs/reecriture/bascule.md` § 3.1 : lire le code du script à travers `tee`
  (`set -o pipefail`, ou `$pipestatus[1]` sous zsh) ; dire que la base de dev peut n'avoir
  aucun compte à mot de passe et que les comptes semés tiennent lieu de comptes existants.
- `tools/next/cutover/rehearse.sh` : avertir quand `--anonymize` ne trouve aucun compte à
  connecter (aujourd'hui : silence) ; `pre-ingest.sh`, en-tête : `lodb-next-prod`.
- `.env.next.example` (L8.1) : ni `LODB_PUBLIC_API_ORIGIN` ni ligne `LoDb__*` (relevé :
  0 occurrence de chacun ; `LODB_DB_*` y figure, 3 occurrences). Pas de variante prod.
- Redirection 301 de `www.` (L8.1) : **non reproduite** en local. `curl -sI -H 'Host:
  www.league-of-data-base.com' http://localhost:18080/en/` renvoie une 301 avec
  `Strict-Transport-Security`, `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy` et `Cross-Origin-Resource-Policy`. Seule la CSP manque, ce qui est sans
  effet sur une redirection. À reverser comme constat, pas comme défaut.

**Groupe proposé** : G5 = `docs/reecriture/bascule.md`, `tools/next/cutover/{rehearse.sh,
pre-ingest.sh,README.md}`, `.env.next.example`.

## 4. Critère de sortie du lot 8

### 4.1 Résumé de la répétition (runbook § 3.1)

Commande : `tools/next/cutover/rehearse.sh --slot 2 --anonymize --stop-legacy 2>&1 | tee /tmp/lodb-rehearsal.log`
(journal complet hors dépôt). Durée totale : environ 6 min.

```text
ok    3s  Copy lodb into lodb_rehearsal, anonymized
ok    0s  Existing accounts of the copy
ok    3s  Build the new stack (lodb-next-e2)
ok    8s  migrate on lodb_rehearsal (Baseline marked, additive migrations)
ok    189s  Pre-ingestion (ingest --latest 3 --languages all)
ok    18s  New stack up on lodb_rehearsal
ok    3s  Smoke tests of the new stack
ok    43s  301 of the former sitemaps
ok    45s  Read-only E2E (@readonly)
ok    5s  Existing accounts on the new stack, API key
ok    0s  New stack stopped (rollback)
ok    14s  Legacy stack on lodb_rehearsal
ok    1s  Legacy key pages on the migrated schema
ok    5s  Legacy sign-in and /v1/usage after the new stack
ok    13s  Legacy stack back on lodb
The rehearsal passes: migrated copy served by the new stack, then by the legacy one.
```

### 4.2 Détail

| Exigence du critère | Preuve |
|---|---|
| Copie de la base de dev, base à part | `lodb` → `lodb_rehearsal` (même PostgreSQL), anonymisée. Comptes de lignes égaux : `users` 1, `builds` 0, `api_keys` 4, `api_usage` 4, `doctrine_migration_versions` 11 ; `lodb` jamais écrite |
| Comptes existants | 8 comptes `cutover_c01` à `c08` écrits dans la copie avant `migrate` : bcrypt `2y`, coût 4, `2a`, argon2id sodium, argon2id, argon2i, argon2id faible, deux voies |
| Marquage de `Baseline`, migrations additives | 1ᵉʳ passage : `3 migration(s) applied, baseline marked: True, now at 20260926185409_Lot6BillingAnalyticsApps (234 ms)`. 2ᵉ passage : `0 migration(s) applied, baseline marked: False (124 ms)`. Historique : `Baseline`, `Lot1DataDragon`, `Lot4Accounts`, `Lot6BillingAnalyticsApps` |
| Pré-ingestion | `ingest --latest 3 --languages all` : code 0 au 1ᵉʳ passage, en 189 s (16.19.1 : 112 datasets, 2 009 images) |
| La nouvelle stack sert la copie | `lodb-next-e2` sur `lodb_rehearsal` par `host.docker.internal:5432` ; smoke : 32 contrôles `ok` (pages, 301 d'anciennes URLs, 404, 401 de `/v1`) |
| 301 des anciens sitemaps | 2 175/2 175 anciennes URLs en une 301 puis 200 (`/sitemap.xml`, `sitemaps/latest.xml` 1 093, `sitemaps/16.18.1.xml` 1 079) |
| E2E en lecture seule | `@readonly` : 260 tests, 259 réussis, 1 ignoré, 0 instable (une reprise permise) |
| Connexion de comptes existants | les 8 se connectent (200, puis 200 à nouveau) ; hash réécrit en `$argon2id$v=19$m=19456,t=2,p=1`, ou gardé (`m=65536,t=4`) pour les argon2id plus forts ; clé d'API émise, `/v1/usage` 200 |
| Retour arrière : ancienne stack relancée sur le même schéma | `POSTGRES_DB=lodb_rehearsal docker compose up -d --wait` (sans toucher au `.env`) ; `php` et `go-api` lisent `lodb_rehearsal` |
| Pages clés de l'ancienne stack | 17 pages 200, `/profile` 302, `/b/…` 404, go-api `/healthz` 200 et `/v1/usage` sans clé 401 |
| Connexion à l'ancienne stack | les 8 comptes se connectent par le formulaire Symfony **avec le hash écrit par la nouvelle stack** (302 vers `/profile`, profil 200) |
| `/v1/usage` de l'ancienne stack | go-api répond 200 avec la clé émise par la nouvelle stack |

### 4.3 Remise en état

- Ancienne stack remise sur `lodb` par le script (dernière étape), puis arrêtée
  (`--stop-legacy`) : 6 conteneurs `Exited (0)`.
- Emplacement supprimé (`down -v --rmi local`) : aucun conteneur, volume ni image
  `lodb-next-e2*`.
- Copie effacée. Vérifié en relançant le seul `postgres` de l'ancienne stack
  (`docker compose start postgres`, puis `psql -l`, puis `docker compose stop postgres`) :
  bases `lodb`, `lodb_j567` (copie laissée par le jalon des lots 5 à 7, hors de ce jalon),
  `postgres`, `template0`, `template1`. Pas de `lodb_rehearsal`.
- `lodb-next` : jamais touchée par la répétition, toujours sur sa propre base, sur
  `fed0c64`.
- `.env` racine identique à `.env.example` ; `git status` propre.

### 4.4 Ce que la répétition locale ne couvre pas

Ce que le runbook exclut du § 3.1 : dump réel chiffré (§ 3.2, opération hôte), reprise des
agrégats et de l'audit, edge, surveillance. Il exclut aussi la connexion à l'ancienne stack
d'un compte **créé** par la nouvelle, non jouée ici (`--keep` et essai à la main,
facultatifs selon le runbook).

### 4.5 État

**Critère du lot 8 : vérifié.** La copie migrée de la base de dev a été servie par la
nouvelle stack (E2E `@readonly` et comptes existants verts). L'ancienne stack a ensuite
été relancée sur le même schéma : pages clés, connexion et `/v1/usage` répondent. Le retour
arrière est prouvé.

Les échecs du § 3 ne touchent pas ce critère, mais la suite E2E complète n'est pas verte :
G1 (nouveau) et G2 à G4 (hérités des lots 5 à 7 et de L8.2) restent à corriger. Le lot 7
reste non vérifié tant que G2 tient.

## 5. Vérification

- **Date** : 2026-09-27, depuis la racine, sur la branche `docs/reecriture-dotnet-angular`.
- **Fusions** (sans conflit, fichiers disjoints) : `wt/corr-l8-api-runbook` (G1, G5 ;
  `d52fd4c`, `b642889`, `8ead54f`) en `5c7d65e`, `wt/corr-l8-admin-i18n` (G2 ; `6d82177`)
  en `719f20e`, `wt/corr-l8-e2e` (G3, G4 ; `8a3bdff`, `7fd8725`) en `813b169`. Aucune autre
  branche `wt/corr-l8-*` ; `git branch --no-merged` ne liste plus aucune branche `wt/*`.
  Aucun ajout aux fichiers partagés, aucun `package.json` modifié, aucun nouveau projet :
  `api:check` sans dérive, rien à régénérer. Le dépôt n'a aucun hook de commit : rien à
  rejouer sur les fichiers fusionnés.
- **Commits de la vérification** : `817aa27` (secrets des releases dans le guide des
  secrets : A3 du [jalon des lots 9 et 10](lots-09-10.md)), `8fba30a` (piège du quota
  d'inscriptions de `CLAUDE.md`, rendu inexact par G3), `810366d` (spec de l'éditeur de
  builds, ci-dessous), `279ad8b` (rapport de complétude i18n régénéré), `005cb57`
  (Lighthouse remesuré).
- **Stack** : `lodb-next` reconstruite depuis la racine sur `8fba30a`
  (`IMAGE_TAG=8fba30a APP_REVISION=8fba30a docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build --wait`),
  5 services `healthy`, images `org.opencontainers.image.revision=8fba30a`. Les commits
  suivants ne touchent que des specs et de la documentation.

### 5.1 Commandes et résultats

| Étape | Commande | Résultat |
|---|---|---|
| Build .NET | `dotnet build LoDb.slnx -c Release` | 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 2 939 tests : 2 937 réussis, 2 ignorés (parité sans `LODB_PARITY_RUN`), 0 échec ; dont `AdminMonitoringConcurrencyTests` (G1) |
| Front | `npm ci --prefix src/LoDb.Web` puis `lint`, `typecheck` | OK, OK |
| Front | `npm --prefix src/LoDb.Web run test` | 233 fichiers, 2 039 tests réussis |
| Front | `build:web` / `build:shell` | OK, 168 pages prérendues ; initial 654,79 ko et 653,96 ko (avertissement de budget connu) |
| Dérive | `npm --prefix src/LoDb.Web run api:check` | sortie 0 |
| i18n | `npm --prefix src/LoDb.Web run i18n:report` | sortie 0 ; rapport périmé depuis L3.3, régénéré (`279ad8b`) |
| CLI de l'hôte | `docker compose -p lodb-next … exec -T api dotnet LoDb.Api.dll analytics import --source /x --dry-run` | `No such directory: /x` |
| E2E | `npm ci --prefix tests/LoDb.E2E` puis `run typecheck` | OK |
| E2E, passage 1 | `npm --prefix tests/LoDb.E2E test` (API recréée par la reconstruction) | 302 tests : 300 réussis, **1 échec** (`builds-editor.spec.ts:33`), 1 ignoré |
| E2E, diagnostic | `npm --prefix tests/LoDb.E2E test -- specs/builds-editor --repeat-each=3` | 3 échecs sur 3 avant `810366d`, puis 6 réussis sur 6 |
| **E2E, passage 2** | `docker restart lodb-next-api-1` puis `npm --prefix tests/LoDb.E2E test` | **302 tests : 301 réussis, 0 échec, 1 ignoré** (54,9 s) |
| E2E, passage 3 | idem, **sans** redémarrer l'API | 298 réussis, 1 échec (`admin/contacts.spec.ts:42`, 429), 2 non lancés : quota de contact, ci-dessous |
| G1 | `docker logs lodb-next-api-1 2>&1 \| grep -c admin.monitoring.database_unreadable` | 0 après les trois passages |
| Lighthouse | `node tools/next/lighthouse/run.mjs --stack lodb-next` | sortie 1 : 5 pages sur 5 hors budget ([rapport](../lighthouse.md)) ; critère du lot 3 |
| Outils de bascule | `node --test 'tools/next/cutover/test/*.test.mjs'` | 16 réussis |
| *Contract* | `tools/next/contract/check.sh` | `CONTRACT OK` |
| **Répétition locale** | `set -o pipefail; tools/next/cutover/rehearse.sh --slot 2 --anonymize 2>&1 \| tee /tmp/lodb-rehearsal-verif.log; echo "code $?"` | **`code 0`, 15 étapes `ok`**, « The rehearsal passes » ; § 5.3 |

Le passage 3 ne compte pas pour le critère : il vérifie que le quota d'inscriptions tient
deux passages de suite (G3). Il bute sur un autre quota, celui des messages de contact
(5 par heure, `RateLimitingPolicies.Contact`), que `contacts.spec.ts` consomme à chaque
passage. C'est la règle déjà consignée : redémarrer l'API entre deux passages complets ;
jamais relever le quota.

### 5.2 Échecs initiaux

| Groupe | État | Preuve |
|---|---|---|
| G1 — rapport de surveillance vide par intermittence | **corrigé** | `analytics.spec.ts:7` et `operations.spec.ts:6` réussis aux passages 1 et 2 ; aucune `admin.monitoring.database_unreadable` dans les journaux ; `AdminMonitoringConcurrencyTests` vert |
| G2 — l'admin repasse en anglais après une action | **corrigé** | 29 tests admin sur 29 aux passages 1 et 2, dont `contacts.spec.ts:61` et `:74`, `moderation.spec.ts:103` à `:189` (le mode série entier), `operations.spec.ts:6` après « Actualiser » |
| G3 — quota d'inscriptions de la suite complète | **corrigé** | `builds-editor.spec.ts:33` (après `810366d`), `builds-share/private.spec.ts:6`, `builds-share/public.spec.ts:19`, `profile.spec.ts:32`, `trends.spec.ts:90` réussis ; aucun refus d'inscription au passage 3, enchaîné sans redémarrage |
| G4 — `context-switcher.spec.ts:118` avant l'hydratation | **corrigé** | réussi aux trois passages complets, en 4 workers |
| G5 — textes du runbook et des outils | **corrigé**, sauf `.env.next.example` | runbook § 1 et § 3.1 (`pipefail`, comptes semés, emplacement 2) suivis tels quels ; `WARNING: --anonymize found no account with a password…` émis ; en-tête de `pre-ingest.sh` à `lodb-next-prod`. **Persistant** : `.env.next.example` sans `LODB_PUBLIC_API_ORIGIN`, sans ligne `LoDb__*`, sans variante prod (fichier hors du périmètre des corrections et de la vérification ; le runbook § 1 le garde ouvert avant J-3) |

**Nouveau, corrigé par la vérification** (`810366d`) : `builds-editor.spec.ts:33`,
masqué jusqu'ici par le quota puis par le sélecteur de champion, échouait 3 fois sur 3.
Trois défauts de la spec, aucun du produit :

1. `pickRunes` comptait les rangées juste après le clic (`count()` n'attend pas) : 0
   rangée, puis la sortie `locator('lodb-rune-board .rune-slot').first() … Expected: 0,
   Received: 1`. La fonction attend maintenant le chemin pressé et sa première rangée.
2. `getByRole('button', { name: 'All' })` dans l'armurerie : *strict mode violation*,
   3 éléments (« Crystalline Bracer », « Executioner's Calling »). `exact: true`.
3. L'import vers le patch précédent attendait « Save changes ». L'ancienne stack
   (`BuildImportController` : « opening a fresh (create-mode) editor … the source is
   untouched ») ouvre un éditeur de création, comme la nouvelle : la spec forge
   maintenant le brouillon importé, attend deux builds, puis supprime les deux.

### 5.3 Répétition locale (runbook § 3.1, rejouée)

Les corrections touchent le runbook (§ 1, § 3.1) et `rehearse.sh` : la répétition est
rejouée telle qu'écrite, `--stop-legacy` en moins (l'ancienne stack devait rester
démarrée pour ses tests, § 5.5 ; le runbook prévoit ce cas).

```text
ok    3s  Copy lodb into lodb_rehearsal, anonymized
ok    0s  Existing accounts of the copy
ok    3s  Build the new stack (lodb-next-e2)
ok    8s  migrate on lodb_rehearsal (Baseline marked, additive migrations)
ok    190s  Pre-ingestion (ingest --latest 3 --languages all)
ok    19s  New stack up on lodb_rehearsal
ok    1s  Smoke tests of the new stack
ok    44s  301 of the former sitemaps
ok    40s  Read-only E2E (@readonly)
ok    4s  Existing accounts on the new stack, API key
ok    0s  New stack stopped (rollback)
ok    14s  Legacy stack on lodb_rehearsal
ok    1s  Legacy key pages on the migrated schema
ok    5s  Legacy sign-in and /v1/usage after the new stack
ok    13s  Legacy stack back on lodb
The rehearsal passes: migrated copy served by the new stack, then by the legacy one.
code 0
```

- `code 0` est celui du script (`set -o pipefail`), et non celui de `tee`.
- `--anonymize` écrit l'avertissement attendu : la base de dev n'a aucun compte à mot de
  passe ; les 8 comptes `cutover_c01` à `c08` sont écrits dans la copie avant `migrate`.
- `@readonly` : 259 réussis, 1 ignoré ; anciens sitemaps : `latest.xml` 1 093/1 093,
  `16.18.1.xml` 1 079/1 079 ; l'ancienne go-api accepte la clé émise par la nouvelle stack
  (200) et refuse l'absence de clé (401).
- Pré-ingestion : `1 of 2007 fetches got no verdict` pour 16.18.1 (avertissement réseau,
  image reprise au passage suivant) ; l'étape sort en 0.
- Remise en état : aucun conteneur ni volume `lodb-next-e2*` ; bases de l'ancienne stack
  `lodb`, `lodb_j567`, `postgres`, `template0`, `template1` (pas de `lodb_rehearsal`).

### 5.4 Critère de sortie du lot 8

| Exigence | État | Preuve |
|---|---|---|
| Base de l'ancienne stack migrée (copie) | **vérifié** | § 5.3, étapes 1 à 4 |
| Nouvelle stack servie dessus | **vérifié** | smoke, 301 des anciens sitemaps, comptes existants et clé d'API |
| E2E en lecture seule | **vérifié** | `@readonly` 259 réussis, 1 ignoré |
| Ancienne stack relancée sur le même schéma | **vérifié** | pages clés, connexion Symfony avec le hash réécrit, `/v1/usage` 200 |

**Critère du lot 8 : vérifié**, et la suite E2E complète est verte (passage 2). Reste
ouvert, hors critère : `.env.next.example` (G5), avant J-3 du runbook.

### 5.5 Ancienne stack

Démarrée par la répétition depuis ce dossier, puis :

| Commande | Résultat |
|---|---|
| `docker compose exec -T -u www-data php php vendor/bin/phpunit tests/Unit` | OK (695 tests, 1 616 assertions) |
| `cd app && npm ci && npm test` (hôte, Vitest) | 42 fichiers, 290 tests réussis ; `app/node_modules` retiré ensuite |
| `docker run --rm -v "$PWD/go:/src:ro" golang:1.26 sh -c 'cd /src/api && go test ./...; cd /src/fetcher && go test ./...'` | tous les paquets `ok` (api : 7, fetcher : 3) |
| `curl` sur 8080 et 8090 | `/`, `/champions`, `/objects`, `/runes` 200 ; `/builds` 302 ; go-api `/healthz` 200 |
| `docker compose stop` | 6 conteneurs `Exited (0)` |

Aucun fichier de l'ancienne stack modifié ; `.env` identique à `.env.example`.
