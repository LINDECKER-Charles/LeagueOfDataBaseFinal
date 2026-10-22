# Développer sur la nouvelle stack (`lodb-dev`)

Ce guide sert au travail quotidien sur la stack .NET 10 + Angular 22 : commandes, ports,
emplacements et dépannage. Les règles de code et les invariants sont dans
[`AGENTS.md`](../../AGENTS.md). Les décisions sont dans
[`docs/reecriture/`](../reecriture/README.md), et le découpage en chantiers dans le
[plan d'implémentation](../reecriture/plan-implementation.md).

> Toutes les commandes se lancent depuis la **racine du dépôt**. En local, la nouvelle
> stack ne partage rien avec l'ancienne, archivée sous `legacy/` (`legacy/compose.yaml`,
> projet `lodb`) : fichiers, projet Compose, images, volumes et ports sont distincts. Sur
> les hôtes, elle prend la place de l'ancienne ([`configuration.md`](configuration.md)).

## Prérequis

| Outil | Version | Où |
|---|---|---|
| .NET SDK | 10.0.400 (`global.json`, `rollForward: latestFeature`) | hôte |
| Node / npm | Node `^22.22.3`, `^24.15.0` ou `>= 26` ; npm 12 accepté | hôte |
| Docker | Docker Desktop, socket par défaut (`/var/run/docker.sock`) | hôte |

Go, le JDK et le SDK Android ne sont pas nécessaires sur l'hôte. Les images Linux de la
stack embarquent leurs propres outils.

Première installation :

```bash
npm ci --prefix src/LoDb.Web
npm ci --prefix tests/LoDb.E2E
npm --prefix tests/LoDb.E2E run browsers:install
```

`npm ci` n'écrit pas les lockfiles. Ils sont générés, jamais édités à la main : un
changement de dépendance passe par `npm install --prefix <dossier>`, et le lockfile part
dans le commit de ce changement. Dependabot commite les siens de la même façon.

## Build et tests sur l'hôte

```bash
dotnet build LoDb.slnx -c Release
dotnet test LoDb.slnx
dotnet test --project tests/LoDb.Api.Tests
```

- Le build Release ne tolère aucun avertissement (`TreatWarningsAsErrors`).
- `dotnet test` compile en **Debug** : `bin/Debug` et `bin/Release` coexistent. Il passe
  par Microsoft.Testing.Platform (réglé dans `global.json`, xUnit v3).
- Testcontainers démarre ses propres conteneurs (`postgres:17-alpine`), sans variable
  d'environnement ; Docker doit tourner.
- `tests/LoDb.Parity`, outil de bascule, est hors de la solution : ni `dotnet build` ni
  `dotnet test LoDb.slnx` ne le touchent. À la demande :
  `dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj`
  ([`tools/parity/README.md`](../../tools/parity/README.md)).

Front (workspace `src/LoDb.Web`) :

```bash
npm --prefix src/LoDb.Web run lint
npm --prefix src/LoDb.Web run typecheck
npm --prefix src/LoDb.Web run test
npm --prefix src/LoDb.Web run build:web
npm --prefix src/LoDb.Web run build:shell
```

| Script | Rôle |
|---|---|
| `lint` | ESLint (dont les règles d'architecture), Prettier, tests des règles de lint |
| `typecheck` | `tsc --noEmit` sur `tsconfig.app.json` et `tsconfig.spec.json` |
| `test` | Vitest, sans mode veille, puis les tests du build des catalogues (`test:i18n`) |
| `i18n:build` | catalogues servis (`dist/i18n`) : complétés par `en`, messages ICU compilés ; lancé avant chaque build et par `npm start` |
| `build:web` | build SSR, dans `dist/web/{browser,server}` |
| `build:shell` | build statique des coquilles, dans `dist/shell/browser` |
| `api:generate` | reconstruit l'API avec la génération OpenAPI (`src/LoDb.Api/openapi/*.json`), puis le client TypeScript du document `app` (`src/app/core/api/generated/`) |
| `api:check` | régénère les mêmes fichiers et échoue s'ils diffèrent de ceux du commit ; job `contract` de la CI |
| `i18n:convert` | conversion ponctuelle des catalogues YAML de l'ancienne stack (`legacy/app/translations`) vers `public/i18n` ; la relancer écrase les catalogues JSON, désormais la source |
| `i18n:report` | complétude des catalogues face à `en`, dans `docs/reecriture/rapports/i18n-completude.md` ; jamais bloquant, job `i18n` de la CI |

Les documents OpenAPI et le client sont des artefacts générés, jamais édités à la main : un
changement de contrat lance `npm --prefix src/LoDb.Web run api:generate` et committe ce
qu'il écrit dans le même commit.

## La stack d'intégration

```bash
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml up -d --build
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml ps
```

Une seule instance tourne sur le poste. Le premier qui en a besoin la démarre, les autres
la réutilisent. Avant de rendre un travail, elle se reconstruit avec
`up -d --build --wait` (garde-fous d'[`AGENTS.md`](../../AGENTS.md)).

| Service | Rôle | Port hôte | Variable |
|---|---|---|---|
| `nginx` | point d'entrée : site, sous-domaine `api.`, `/cdn/blobs/` (8080 dans le réseau) | 18080 | `LODB_HTTP_PORT` |
| `api` | `LoDb.Api` (8080 dans le réseau ; métriques sur 9464, jamais publié) | 18081 | `LODB_API_PORT` |
| `web-ssr` | serveur SSR Angular (4000 dans le réseau) | 18082 | `LODB_SSR_PORT` |
| `web-ssr-prerender` | le même SSR, pour la passe de précalcul seule (4000 et métriques 9464 dans le réseau, alias `lodb-web-ssr-prerender`), sans quota en local | aucun | — |
| `postgres` | PostgreSQL 17, base `lodb` (utilisateur et mot de passe `lodb` en dev) | 15432 | `LODB_PG_PORT` |
| `mailpit` | interface des e-mails de dev | 18025 | `LODB_MAIL_PORT` |
| `pages-store` | stockage objet simulé des pages pré-rendues (moto, l'API S3 en mémoire ; 5000 dans le réseau, alias `lodb-pages-store`), bucket `lodb-pages-dev` ([`configuration.md`](configuration.md), § 7) | aucun | — |

Deux conteneurs éphémères passent avant l'API, qui attend leur réussite : `migrate`
(migrations, rôles) puis `states` (`catalog states build`, l'index des états d'entité,
[`configuration.md`](configuration.md) § 4.12). Avant toute promotion, `states` ne fait rien ;
la première promotion construit alors l'index dans l'API. Il ne couvre ensuite que les
versions consécutives dont l'en_US est stocké, jusqu'au dernier patch : sur une pile neuve,
souvent la seule dernière version, sans page de patch ni historique.

Vérifier la stack :

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:18080/en/
curl -s http://localhost:18081/healthz
curl -s http://localhost:18081/readyz
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml exec -T nginx wget -qO- http://lodb-api:9464/metrics | grep lodb_build_info
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml exec -T postgres psql -U lodb -d lodb -c 'select version()'
```

- `/readyz` se lit sur l'API (18081, ou `lodb-api:8080` dans le réseau), ou par nginx sur le
  sous-domaine `api.` (`curl -H 'Host: api.localhost' http://localhost:18080/readyz`, une
  requête par seconde et par adresse). Demandé au site, il part au SSR et répond 404.
- Les métriques se lisent **depuis le réseau de la stack** : 9464 n'est publié ni sur
  l'hôte ni par nginx. Celles du SSR :
  `… exec -T web-ssr node -e "fetch('http://127.0.0.1:9464/metrics').then(r => r.text()).then(console.log)"`.
  Noms et labels : [`metriques.md`](metriques.md).

Journaux (JSON, une ligne par enregistrement) :

```bash
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml logs --no-log-prefix --tail 20 api
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml logs --no-log-prefix --tail 20 web-ssr
```

Arrêter sans rien perdre, puis relancer sans reconstruire :

```bash
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml stop
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml up -d --wait
```

`--wait` rend la main quand toutes les sondes sont `healthy` : c'est la forme à utiliser
avant des E2E ou un script.

`down -v` supprime aussi la base et le volume `ddragon` (blobs Data Dragon) : ne le lancez
sur la stack d'intégration qu'en connaissance de cause.

## Tests E2E

Où placer un test et ce que chaque couche coûte : [`tests.md`](tests.md).

Ils exigent une stack démarrée. Par défaut, ils visent la stack d'intégration
(`http://localhost:18080`).

Une stack neuve n'ingère que le dernier patch : son index des états ne couvre que lui, et les
specs qui choisissent une page d'ancien patch par son état (`support/states.ts`) ne trouvent
aucun état clos. Avant elles, et avant `tools/routing/check-urls.sh`, indexer les patchs
d'avant, comme la CI :

```bash
bash tools/states/index-past-patches.sh
```

Le script ingère l'en_US des 5 patchs qui précèdent le dernier (`LODB_STATES_PATCHES`, une
minute chacun, images comprises), puis relance le service `states`, qui reconstruit l'index à
partir du plus ancien, et attend que l'API le relise (une minute au plus, sur une lecture) :
lancé juste après, `check-urls.sh` ne lit jamais l'ancien et ne saute pas ses lignes d'états.

```bash
npm --prefix tests/LoDb.E2E test
npm --prefix tests/LoDb.E2E run typecheck
```

`LODB_E2E_BASE_URL` pointe la suite vers un autre nginx, par exemple un emplacement. Les
specs vont dans `tests/LoDb.E2E/specs/<feature>/`. Elles s'appuient sur la fixture
`consoleErrors` (`support/test.ts`) et sur `expectNoAccessibilityViolations`
(`support/accessibility.ts`).

La suite ne garde que les parcours critiques : ce qu'aucune couche basse ne voit (un vrai
navigateur, le rendu serveur puis l'hydratation, nginx de bout en bout), un parcours par
fonctionnalité, ses variantes en `test.step`. Une règle déjà couverte par Vitest, xUnit ou les
outils n'y revient pas. L'accessibilité (axe) et la largeur de 320 px y restent sur un
échantillon : une page par gabarit, plus les langues à risque (de, pl, ar). Les vérifications
de requête (en-têtes, caches, 404 de nginx, anciennes URLs, grammaire d'URL de l'ADR 0005)
sont celles de `tools/nginx` et de `tools/routing/check-urls.sh`, que le job `e2e` de la CI
lance avant Playwright. Les tests marqués `@readonly`, qui n'écrivent rien, servent aussi de
fumée au staging et à la production (`tools/cutover/readonly-e2e.sh`).

## Emplacements

Un emplacement est une seconde stack, isolée par son nom de projet et par ses ports, pour
éprouver une branche sans toucher à la stack d'intégration. On le lance **depuis son
worktree**, quand une branche doit tourner à côté de la stack d'intégration. Il est
supprimé (`down -v`) avant de rendre. Au plus
deux emplacements tournent à la fois.

| Emplacement | Projet | HTTP | API | SSR | Postgres | Mailpit |
|---|---|---|---|---|---|---|
| intégration | `lodb-dev` | 18080 | 18081 | 18082 | 15432 | 18025 |
| 1 | `lodb-dev-e1` | 18180 | 18181 | 18182 | 15532 | 18125 |
| 2 | `lodb-dev-e2` | 18280 | 18281 | 18282 | 15632 | 18225 |

Cycle complet de l'emplacement 1 (pour l'emplacement 2, reprendre ses valeurs) :

```bash
LODB_HTTP_PORT=18180 LODB_API_PORT=18181 LODB_SSR_PORT=18182 LODB_PG_PORT=15532 LODB_MAIL_PORT=18125 docker compose -p lodb-dev-e1 -f compose.yaml -f compose.override.yaml up -d --build --wait
LODB_E2E_COMPOSE_PROJECT=lodb-dev-e1 bash tools/states/index-past-patches.sh http://localhost:18180
LODB_E2E_BASE_URL=http://localhost:18180 LODB_E2E_COMPOSE_PROJECT=lodb-dev-e1 \
  LODB_E2E_MAIL_URL=http://localhost:18125 npm --prefix tests/LoDb.E2E test
docker compose -p lodb-dev-e1 -f compose.yaml -f compose.override.yaml down -v --rmi local
```

Les trois variables des E2E vont ensemble : les parcours qui écrivent créent leurs comptes
par `docker compose -p <projet> exec` et lisent leurs e-mails dans Mailpit. Sans
`LODB_E2E_COMPOSE_PROJECT` ni `LODB_E2E_MAIL_URL`, ils visent la stack d'intégration et
échouent quand seul l'emplacement tourne.

En dev, les images s'appellent `<projet>-<service>` (`lodb-dev-e1-api`…). Un emplacement
n'écrase donc jamais les images de la stack d'intégration, et `--rmi local` ne supprime que
les siennes.

## Variables

Tout a une valeur par défaut en local : aucun fichier `.env` n'est nécessaire. Un `.env`
racine ne sert qu'à surcharger une variable locale (un port, par exemple) ; ne jamais y
copier un modèle d'environnement servi. L'inventaire complet des secrets et des variables
est dans [`configuration.md`](configuration.md).

- Les variables de la nouvelle stack sont préfixées `LODB_`. L'ancienne stack a son propre
  `.env`, `legacy/.env` : les deux ne se lisent jamais l'une l'autre.
- Côté API, la configuration passe par `LoDb__<Section>__<Clé>` et
  `ConnectionStrings__LoDb`.
- Côté SSR : `LODB_API_ORIGIN`, `LODB_ALLOWED_HOSTS` et `LODB_TRUST_PROXY_HEADERS`.

## Dépannage

**Quelles stacks tournent, et depuis quel dossier ?**

```bash
docker compose ls --all --filter name=lodb
```

Le projet `lodb`, l'ancienne stack, peut avoir été lancé depuis le checkout principal. Ne
le recréez pas depuis un autre dossier.

**Un port est déjà pris.** Cherchez qui l'occupe :

```bash
lsof -nP -iTCP:18080 -sTCP:LISTEN
```

Les ports 3307, 33306 et 21200 à 21213 appartiennent à d'autres projets du poste, et
8080, 8085, 8090, 5432, 8025 et 1025 à l'ancienne stack. Un emplacement se choisit dans
le tableau ci-dessus, jamais au hasard.

**La configuration Compose est-elle valide ?**

```bash
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml config --quiet
```

**Une image ne se construit plus, ou semble périmée.** Reconstruisez-la sans cache et
lisez l'étape qui échoue :

```bash
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml build --no-cache web-ssr
```

Pour `web-ssr`, regardez d'abord l'avertissement `npm warn install-scripts`. npm 12 bloque
les scripts d'installation d'`esbuild`, `lmdb`, `@parcel/watcher` et `msgpackr-extract` ;
les binaires viennent aujourd'hui de leurs paquets optionnels de plateforme. Si un binaire
manque, la piste est `npm install-scripts approve <paquet>`.

**Une ligne de journal n'est pas du JSON.** Isolez-la :

```bash
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml logs --no-log-prefix api | grep -v '^{'
```

Les deux causes connues sont corrigées ([rapport du jalon](../reecriture/rapports/jalons/lot-00.md)) :
`libgssapi_krb5.so.2` au premier accès de l'API à Postgres, neutralisé par
`Gss Encryption Mode=Disable` dans les chaînes de connexion (`compose.yaml`), et les traces
multi-lignes du SSR, qu'écrit désormais en une ligne JSON son dernier gestionnaire
d'erreurs (`src/server/failure-handler.ts`). Une nouvelle ligne hors JSON ne se filtre pas :
corrigez-la à la source.

**L'API ne démarre pas, `states` a échoué.** `docker compose -p lodb-dev -f compose.yaml -f
compose.override.yaml logs states` : `states.build.failed` dit que la liste des versions de
Data Dragon est illisible alors qu'aucun index n'existe (réseau coupé) ; relancer une fois le
réseau rétabli. Un historique plus long s'obtient en ingérant l'en_US de chaque version listée
depuis la plus ancienne voulue (`ingest --version <v> --languages en_US`), puis en relançant
`states`, qui reconstruit l'index sur le nouveau début de couverture.

**Les E2E ne trouvent pas de navigateur.** Relancez
`npm --prefix tests/LoDb.E2E run browsers:install`.

**Les tests .NET d'intégration échouent sur Docker.** Testcontainers a besoin de Docker
Desktop démarré. `docker ps` doit répondre.

**`ps` affiche l'image d'un service sous la forme `sha256:…`.** Docker Desktop range les
images dans le magasin containerd. Le conteneur garde l'empreinte propre à sa plateforme,
et l'étiquette pointe vers l'index : c'est le même build. Après une reconstruction,
`up -d --wait` suffit à recréer les services dont l'image a changé.

**Avertissements sans conséquence** :

- `DEP0205` (`module.register()`) pendant `build:web` sous Node 26 ;
- « `NO_COLOR` env is ignored » de Playwright quand le terminal pose `FORCE_COLOR`.

Les clés Data Protection sont gardées en base (`PersistKeysToDbContext`) : un avertissement
`UsingEphemeralFileSystemLocationInContainer` au démarrage de l'API n'est plus attendu.
