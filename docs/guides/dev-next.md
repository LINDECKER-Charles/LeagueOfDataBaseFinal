# Développer sur la nouvelle stack (`lodb-next`)

Ce guide sert au travail quotidien sur la réécriture .NET 10 + Angular 22 : commandes,
ports, emplacements et dépannage. Les règles de code et les invariants sont dans la section
« Nouvelle stack » de [`CLAUDE.md`](../../CLAUDE.md). Les décisions sont dans
[`docs/reecriture/`](../reecriture/README.md), et le découpage en chantiers dans le
[plan d'implémentation](../reecriture/plan-implementation.md).

> Toutes les commandes se lancent depuis la **racine du dépôt**. La nouvelle stack ne
> partage rien avec l'ancienne (`compose.yaml`, projet `lodb`) : fichiers, projet Compose,
> images, volumes et ports sont distincts.

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

`npm ci` n'écrit pas les lockfiles. Un chantier ne les committe jamais : l'intégration les
régénère par `npm install --prefix <dossier>` puis les committe.

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
| `test` | Vitest, sans mode veille |
| `build:web` | build SSR, dans `dist/web/{browser,server}` |
| `build:shell` | build statique des coquilles, dans `dist/shell/browser` |
| `api:generate`, `api:check` | contrat OpenAPI et client généré : bouchons jusqu'à L2.2 |
| `i18n:convert`, `i18n:report` | conversion des catalogues et complétude : bouchons jusqu'à L3.3 |

Tant que `api:generate` n'est qu'un bouchon, les documents OpenAPI se produisent à la main :

```bash
dotnet build src/LoDb.Api -p:LoDbGenerateOpenApi=true
```

Elle écrit `src/LoDb.Api/openapi/LoDb.Api_app.json` et `LoDb.Api_public-v1.json`. Ce sont
des artefacts générés : un chantier ne les committe pas.

## La stack d'intégration

```bash
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml ps
```

Une seule instance tourne sur le poste. Le premier qui en a besoin la démarre, les autres
la réutilisent. Seuls les agents d'intégration, de jalon et de vérification la
reconstruisent.

| Service | Rôle | Port hôte | Variable |
|---|---|---|---|
| `nginx` | point d'entrée : site, sous-domaine `api.`, `/cdn/blobs/` | 18080 | `LODB_NEXT_HTTP_PORT` |
| `api` | `LoDb.Api` (8080 dans le réseau ; métriques sur 9464, jamais publié) | 18081 | `LODB_NEXT_API_PORT` |
| `web-ssr` | serveur SSR Angular (4000 dans le réseau) | 18082 | `LODB_NEXT_SSR_PORT` |
| `postgres` | PostgreSQL 17, base `lodb` (utilisateur et mot de passe `lodb` en dev) | 15432 | `LODB_NEXT_PG_PORT` |
| `mailpit` | interface des e-mails de dev | 18025 | `LODB_NEXT_MAIL_PORT` |

Vérifier la stack :

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:18080/en/
curl -s http://localhost:18081/healthz
curl -s http://localhost:18081/readyz
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml exec -T nginx wget -qO- http://api:9464/metrics | grep lodb_build_info
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml exec -T postgres psql -U lodb -d lodb -c 'select version()'
```

- `/readyz` se lit sur l'API (18081, ou `api:8080` dans le réseau). Demandé à nginx, il
  part au SSR et répond 404.
- Les métriques se lisent **depuis le réseau de la stack** : 9464 n'est publié ni sur
  l'hôte ni par nginx.

Journaux (JSON, une ligne par enregistrement) :

```bash
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml logs --no-log-prefix --tail 20 api
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml logs --no-log-prefix --tail 20 web-ssr
```

Arrêter sans rien perdre, puis relancer sans reconstruire :

```bash
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml stop
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --wait
```

`--wait` rend la main quand toutes les sondes sont `healthy` : c'est la forme à utiliser
avant des E2E ou un script.

`down -v` supprime aussi la base et le volume `storage` : ne le lancez sur la stack
d'intégration qu'en connaissance de cause.

## Tests E2E

Ils exigent une stack démarrée. Par défaut, ils visent la stack d'intégration
(`http://localhost:18080`).

```bash
npm --prefix tests/LoDb.E2E test
npm --prefix tests/LoDb.E2E run typecheck
```

`LODB_E2E_BASE_URL` pointe la suite vers un autre nginx, par exemple un emplacement. Les
specs vont dans `tests/LoDb.E2E/specs/<feature>/`. Elles s'appuient sur la fixture
`consoleErrors` (`support/test.ts`) et sur `expectNoAccessibilityViolations`
(`support/accessibility.ts`).

## Emplacements

Un emplacement est une seconde stack, isolée par son nom de projet et par ses ports, pour
éprouver une branche sans toucher à la stack d'intégration. On le lance **depuis son
worktree**, et seulement si sa mission l'attribue. Il est supprimé avant de rendre. Au plus
deux emplacements tournent à la fois.

| Emplacement | Projet | HTTP | API | SSR | Postgres | Mailpit |
|---|---|---|---|---|---|---|
| intégration | `lodb-next` | 18080 | 18081 | 18082 | 15432 | 18025 |
| 1 | `lodb-next-e1` | 18180 | 18181 | 18182 | 15532 | 18125 |
| 2 | `lodb-next-e2` | 18280 | 18281 | 18282 | 15632 | 18225 |

Cycle complet de l'emplacement 1 (pour l'emplacement 2, reprendre ses valeurs) :

```bash
LODB_NEXT_HTTP_PORT=18180 LODB_NEXT_API_PORT=18181 LODB_NEXT_SSR_PORT=18182 LODB_NEXT_PG_PORT=15532 LODB_NEXT_MAIL_PORT=18125 docker compose -p lodb-next-e1 -f compose.next.yaml -f compose.next.override.yaml up -d --build --wait
LODB_E2E_BASE_URL=http://localhost:18180 npm --prefix tests/LoDb.E2E test
docker compose -p lodb-next-e1 -f compose.next.yaml -f compose.next.override.yaml down -v --rmi local
```

En dev, les images s'appellent `<projet>-<service>` (`lodb-next-e1-api`…). Un emplacement
n'écrase donc jamais les images de la stack d'intégration, et `--rmi local` ne supprime que
les siennes.

## Variables

Tout a une valeur par défaut en local : aucun fichier `.env` n'est nécessaire. Le modèle
complet est `.env.next.example`.

- Les variables de la nouvelle stack sont préfixées `LODB_` : Compose lit aussi le `.env`
  racine de l'ancienne stack, et les deux ne doivent jamais se lire l'une l'autre.
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
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml config --quiet
```

**Une image ne se construit plus, ou semble périmée.** Reconstruisez-la sans cache et
lisez l'étape qui échoue :

```bash
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml build --no-cache web-ssr
```

Pour `web-ssr`, regardez d'abord l'avertissement `npm warn install-scripts`. npm 12 bloque
les scripts d'installation d'`esbuild`, `lmdb`, `@parcel/watcher` et `msgpackr-extract` ;
les binaires viennent aujourd'hui de leurs paquets optionnels de plateforme. Si un binaire
manque, la piste est `npm install-scripts approve <paquet>`.

**Une ligne de journal n'est pas du JSON.** Isolez-la :

```bash
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml logs --no-log-prefix api | grep -v '^{'
```

Deux causes sont connues au lot 0, consignées dans le
[rapport du jalon](../reecriture/rapports/jalons/lot-00.md) :

- `libgssapi_krb5.so.2`, au premier accès de l'API à Postgres ;
- les traces multi-lignes du SSR sur une erreur de rendu.

Ne les filtrez pas : corrigez-les à la source.

**Les E2E ne trouvent pas de navigateur.** Relancez
`npm --prefix tests/LoDb.E2E run browsers:install`.

**Les tests .NET d'intégration échouent sur Docker.** Testcontainers a besoin de Docker
Desktop démarré. `docker ps` doit répondre.

**`ps` affiche l'image d'un service sous la forme `sha256:…`.** Docker Desktop range les
images dans le magasin containerd. Le conteneur garde l'empreinte propre à sa plateforme,
et l'étiquette pointe vers l'index : c'est le même build. Après une reconstruction,
`up -d --wait` suffit à recréer les services dont l'image a changé.

**Le changelog est vide après un `ng serve` ou un build local sous Windows.**
`src/LoDb.Web/src/app/features/editorial/changelog/published` est un lien symbolique vers
`app/public/changelog` : il faut `git config core.symlinks true` (mode développeur) ou une
jonction à sa place (`mklink /J`). L'image `web-ssr` pose ce lien elle-même.

**Avertissements sans conséquence** :

- `DEP0205` (`module.register()`) pendant `build:web` sous Node 26 ;
- « `NO_COLOR` env is ignored » de Playwright quand le terminal pose `FORCE_COLOR` ;
- `UsingEphemeralFileSystemLocationInContainer` (clés Data Protection) au démarrage de
  l'API, jusqu'à leur persistance en base (L4.1).
