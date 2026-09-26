# Jalon du lot 0 — socle

- **Date** : 2026-09-26.
- **Branche** : `docs/reecriture-dotnet-angular`, sur `dd0450c` (L0.1 à L0.4 intégrés, plus
  le lockfile E2E committé par ce jalon).
- **Poste** : macOS arm64, .NET SDK 10.0.400, Node 26.5 / npm 12, Docker 29.8.0.
- **Stack** : `lodb-next`, reconstruite depuis la racine. Les images ont d'abord été
  construites sans cache (`build --no-cache`), puis la stack a été relancée par la
  commande de la méthode.

Plan de référence : [plan maître §8](../../plan-implementation.md#8-jalons-et-critères-de-sortie) ;
critère du lot : [lot 0](../../implementation/lot-00-socle.md).

## 1. Commandes et résultats

Toutes les commandes sont lancées depuis la racine du dépôt.

| Étape | Commande | Résultat |
|---|---|---|
| Lockfile E2E | `npm install --prefix tests/LoDb.E2E` puis `npm ci --prefix tests/LoDb.E2E` | créé (lockfileVersion 3, 8 paquets, registre npmjs seul) ; `npm ci` accepte le fichier ; committé en `dd0450c` |
| Lockfile front | `npm install --prefix src/LoDb.Web` | aucune dérive : le fichier est inchangé, rien à committer |
| Build .NET | `dotnet build LoDb.slnx -c Release` (puis `--no-incremental`) | 9 projets, 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 81 réussis sur 81, 4 projets de test |
| Front | `npm --prefix src/LoDb.Web run lint` | OK (ng lint, Prettier, 9/9 tests des règles d'architecture) |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | 12 fichiers, 153 tests réussis |
| Front | `npm --prefix src/LoDb.Web run build:web` / `build:shell` | OK (0 route prérendue ; `dist/web`, `dist/shell`) |
| Contrat, i18n | `npm --prefix src/LoDb.Web run api:check` / `i18n:report` | bouchons : « not available yet », sortie 0 (L2.2, L3.3) |
| Workflows | `docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint:latest .github/workflows/next-ci.yml .github/workflows/next-build.yml .github/workflows/next-deploy.yml` | actionlint 1.7.12 : code 0 |
| Images | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml build --no-cache` | 3 images construites ; toutes les étapes rejouées (seuls les caches npm et NuGet montés servent) |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build` | api, web-ssr, nginx, postgres et mailpit en `healthy` |
| E2E | `npm --prefix tests/LoDb.E2E test` | 2 réussis sur 2 (1,1 s) |
| E2E (mesure) | `npm --prefix tests/LoDb.E2E test -- --repeat-each=30` | 60 réussis sur 60 (8,4 s), lancé pour relever la mémoire |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |

Avertissements sans effet sur le résultat, repris dans les pièges de `CLAUDE.md` :

- npm 12 bloque les scripts d'installation de 5 paquets (`allowScripts`), sur l'hôte et
  dans l'image `web-ssr` ;
- `build:web` affiche `DEP0205` (`module.register()`) sous Node 26 ;
- Playwright affiche « `NO_COLOR` env is ignored » quand le terminal pose `FORCE_COLOR`.

## 2. Échecs

Chaque échec forme un groupe. Les groupes touchent des fichiers disjoints et peuvent donc
être corrigés en parallèle.

### G1 — API : deux lignes natives hors JSON au premier accès à Postgres

**Reproduire** (stack démarrée) :

```bash
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml restart api
curl -s http://localhost:18081/readyz
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml logs --no-log-prefix --since 1m api | grep -v '^{'
```

**Sortie utile** :

```
Cannot load library libgssapi_krb5.so.2
Error: libgssapi_krb5.so.2: cannot open shared object file: No such file or directory
```

**Cause vérifiée**. À sa première connexion, Npgsql 10 négocie le chiffrement GSS
(`Gss Encryption Mode`, `Prefer` par défaut). Le runtime tente alors de charger
`libgssapi_krb5.so.2`, absente de l'image *chiseled*, et écrit ces deux lignes sur stderr,
hors du formateur JSON. Elles apparaissent une fois par processus. Le mot `Error` fait
aussi classer la ligne en `error` par le collecteur (voir
[`observabilite.md`](../../../guides/observabilite.md), « Le piège du niveau deviné »).

**Contre-épreuve**. J'ai lancé un conteneur jetable de la même image `lodb-next-api`, sur
le réseau de la stack, avec `…;Gss Encryption Mode=Disable` dans `ConnectionStrings__LoDb`.
`/readyz` répond 200 (postgres et storage en `Healthy`), sans aucune ligne hors JSON. Le
conteneur a ensuite été supprimé.

**Fichiers suspects** :

- `compose.next.yaml` (`api.environment.ConnectionStrings__LoDb`) ;
- `compose.next.deploy.yaml` (même clé) ;
- `.env.next.example`, pour documenter le réglage.

**Correction proposée** : ajouter `Gss Encryption Mode=Disable` aux deux chaînes de
connexion. La stack n'utilise pas Kerberos, et ce réglage couvre tous les consommateurs :
sonde, EF Core, puis `migrate` (L1.4). Une variante consiste à forcer le réglage dans le
code, par `NpgsqlConnectionStringBuilder`, dans
`src/LoDb.Api/Hosting/Health/PostgresReadinessCheck.cs` et dans la future source de
données de `Persistence`. Elle ferait sortir le groupe des fichiers Compose : à éviter
pendant ce lot. Vérification : les trois commandes ci-dessus ne doivent plus rien afficher
après `grep -v '^{'`.

Optionnel, dans les mêmes fichiers : les lignes de `docker-entrypoint.sh` de l'image nginx
(10 lignes en texte brut au démarrage) se taisent avec `NGINX_ENTRYPOINT_QUIET_LOGS=1`.
Ce n'est pas un échec du critère : nginx n'a plus de journal d'accès (`access_log off`,
voulu par `observabilite.md`), et son `error_log` ne peut pas s'écrire en JSON.

### G2 — SSR : trace multi-ligne pour tout rendu en erreur

**Reproduire** (stack démarrée) :

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:18080/nimporte-quoi
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml logs --no-log-prefix --since 1m web-ssr | grep -v '^{'
```

**Sortie utile** (le statut 404 est correct, le journal ne l'est pas) :

```
ERROR m [Error]: NG04002: 'nimporte-quoi'
    at Xl.noMatchError (file:///app/server/main.server.mjs:23:77625)
    at Xl.match (file:///app/server/main.server.mjs:23:78371)
    at async Xl.recognize (file:///app/server/main.server.mjs:23:77765)
    at async file:///app/server/main.server.mjs:23:82249 {
  code: 4002
}
```

**Cause**. Pendant un rendu serveur, les erreurs remontent à l'`ErrorHandler` par défaut
d'Angular, qui les écrit par `console.error('ERROR', error)`. Elles passent donc à côté du
journal pino de `server.ts`. Toute URL sans route le déclenche (`/readyz` et `/metrics`
demandés au site, adresses inconnues), et à terme toute erreur de rendu. Sur la stack du
jalon, 14 lignes du journal `web-ssr` sur 236 ne sont pas du JSON.

**Fichiers suspects** :

- `src/LoDb.Web/src/app/app.config.server.ts` (fournisseurs serveur) ;
- `src/LoDb.Web/src/server.ts` ;
- `src/LoDb.Web/src/server/create-logger.ts`.

**Correction proposée**, au choix de l'agent :

- un `ErrorHandler` propre au serveur, dans `app.config.server.ts`, qui écrit un
  enregistrement JSON d'une ligne (événement du type `ssr.render.failed`, nom, message et
  pile dans un seul champ) ;
- ou une redirection de `console.error` et `console.warn` vers pino dans `server.ts`.

`src/server/` contient déjà 10 fichiers : un nouveau fichier doit aller dans un
sous-dossier ou à côté du fournisseur, pour respecter la limite de 10 fichiers par dossier.

**Test** : un test Vitest du gestionnaire (une erreur, puis une seule ligne JSON
analysable). Vérification sur la stack : les deux commandes ci-dessus ne doivent plus rien
afficher après `grep -v '^{'`. Le 404 de `/nimporte-quoi` doit rester un 404.

### G3 — Plan : `autoCsp` prescrit mais impossible en SSR

**Constat** (relayé par L0.2 et confirmé). Angular 22 refuse de combiner `autoCsp` et le
SSR : L0.2 l'a donc retiré de `angular.json`. Le plan le prescrit pourtant encore.

**Reproduire** :

```bash
grep -rn -i autocsp docs/reecriture/implementation
```

Deux occurrences : `lot-00-socle.md` (L0.2, « Option de sécurité `autoCsp` activée pour
`web` ») et `lot-03-web-public.md` (L3.11, « CSP : hash des scripts inline (`autoCsp`) »).

**Fichiers suspects** :

- `docs/reecriture/implementation/lot-00-socle.md` ;
- `docs/reecriture/implementation/lot-03-web-public.md`.

**Correction proposée** : remplacer ces mentions par le nonce rendu par le serveur, que
l'[ADR 0005](../../adr/0005-web-ssr-urls-et-seo.md) permet déjà (« hash des scripts inline
via `autoCsp`, ou nonce »). Aucun écart à l'ADR. Ces documents sont hors du périmètre de ce
jalon : je signale le point, sans le corriger.

## 3. Points ouverts, sans échec

Aucun de ces points n'empêche le critère. Ils sont à trancher, ou à traiter par le
chantier cité.

- **Racine du workspace Angular**. `src/LoDb.Web/` compte 10 fichiers suivis, plus le
  `package-lock.json` : 11 avec lui, déjà au-delà de la limite de 10 fichiers par dossier.
  Le `.postcssrc.json` de L3.2 en ajoutera un douzième. Ces fichiers de configuration sont
  placés là par les outils eux-mêmes. Décision humaine à prendre : les exempter de la
  limite (proposition) ou en déplacer une partie. Faute de décision, `CLAUDE.md` signale
  le point sans poser d'exemption.
- **Clés Data Protection éphémères**. L'API avertit au démarrage
  (`UsingEphemeralFileSystemLocationInContainer`, `NoXMLEncryptor…`). La persistance en
  base revient à L4.1 (zone `DataProtection`), comme prévu par le plan.
- **`migrate` et `up --wait`**. `next-deploy.yml` lance `docker compose run --rm migrate`
  si le service existe, puis `up -d --wait`. Quand L1.4 ajoutera `migrate` comme service
  ponctuel, il faudra vérifier que `--wait` ne l'attend pas indéfiniment : profil ou
  `restart: "no"`, et `depends_on` de l'API en `service_completed_successfully`.
- **`/readyz` par nginx**. `http://localhost:18080/readyz` répond 404 : la route va au
  SSR. C'est voulu, puisque la sonde se lit sur l'API (port 8080 dans le réseau, 18081 en
  dev). Ce même appel déclenche G2.
- **Hydratation**. Le test de fumée charge `/en/` dans Chromium et exige zéro erreur de
  console, ce qui couvre l'hydratation non vérifiée par L0.2.

## 4. Mémoire

Les pics relevés pendant les E2E sont consignés dans [`../memoire.md`](../memoire.md),
section « Pics des E2E ».

## 5. Critère de sortie local du lot 0

| Volet | État | Preuve |
|---|---|---|
| Suites vertes | **vérifié** | tableau du §1 : .NET 81/81, front complet, actionlint, E2E 2/2 |
| `/en/` rendu en SSR par `lodb-next` | **vérifié** | `curl http://localhost:18080/en/` : 200 `text/html`, `<html lang="en">`, `ng-server-context="ssr"`, `<lodb-shell>` présent ; test de fumée vert |
| `/healthz` | **vérifié** | API : 200 `{"status":"Healthy"}` sur 18081 et depuis le réseau (`wget http://api:8080/healthz` dans nginx) ; nginx : 200 `ok` avec les en-têtes constants |
| `/readyz` | **vérifié** | 200, `postgres` et `storage` en `Healthy`, sur 18081 et depuis le réseau de la stack |
| `lodb_build_info` | **vérifié** | depuis le réseau (`docker compose … exec -T nginx wget -qO- http://api:9464/metrics`) : `lodb_build_info{otel_scope_name="LoDb.Api",revision="dev",version="0.1.0"} 1` ; `/metrics` en 404 sur 18080 et 18081, 9464 non publié |
| Logs JSON d'une ligne par enregistrement | **en échec** | API : 7 lignes JSON, puis 2 lignes natives (G1) ; SSR : pino en JSON, mais trace multi-ligne des erreurs de rendu (G2) |

**Critère du lot 0 : en échec**, sur le seul volet des journaux. Les corrections de G1 et G2
suffisent ; G3 ne corrige que la documentation. La vérification doit relancer les
commandes de reproduction de G1 et G2, puis le tableau du §1.

## 6. Vérification

- **Date** : 2026-09-26.
- **Branche** : `docs/reecriture-dotnet-angular`, sur `c950b52` (corrections fusionnées).
- **Fusions** : `wt/corr-l0-g1` (`c36686c`, G1) en `7b4dfa7`, puis `wt/corr-l0-g2`
  (`92e8426`, G2) en `c950b52`. Fichiers disjoints : aucun conflit. Aucune autre branche
  `wt/corr-l0-*`.
- **Stack** : `lodb-next` reconstruite depuis la racine (`up -d --build --wait`) ; les
  conteneurs portent bien les corrections (`Gss Encryption Mode=Disable` dans l'environnement
  de `api`, `NGINX_ENTRYPOINT_QUIET_LOGS=1` dans celui de `nginx`, `ssr.render.failed` dans
  `/app/server/main.server.mjs`).

### 6.1 Commandes et résultats

| Étape | Commande | Résultat |
|---|---|---|
| Lockfiles | `npm install --prefix src/LoDb.Web` et `npm install --prefix tests/LoDb.E2E` | aucune dérive, aucun `package.json` modifié par les corrections : rien à committer |
| Build .NET | `dotnet build LoDb.slnx -c Release` | 0 avertissement, 0 erreur |
| Tests .NET | `dotnet test LoDb.slnx` | 81 réussis sur 81 |
| Front | `npm --prefix src/LoDb.Web run lint` | OK (9/9 tests des règles d'architecture) |
| Front | `npm --prefix src/LoDb.Web run typecheck` | OK |
| Front | `npm --prefix src/LoDb.Web run test` | 13 fichiers, 156 tests réussis (3 nouveaux : `ssr-error-handler.spec.ts`) |
| Front | `npm --prefix src/LoDb.Web run build:web` / `build:shell` | OK |
| Contrat, i18n | `npm --prefix src/LoDb.Web run api:check` / `i18n:report` | bouchons, sortie 0 (L2.2, L3.3) |
| Stack | `docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build --wait` | 5 services en `healthy` |
| E2E | `npm --prefix tests/LoDb.E2E test` | 2 réussis sur 2 (1,1 s) |
| E2E | `npm --prefix tests/LoDb.E2E run typecheck` | OK |

Le dépôt n'a aucun hook de commit : rien à rejouer après les fusions. Le lint et le
formatage du front couvrent les fichiers fusionnés.

### 6.2 Échecs du §2

| Groupe | État | Preuve |
|---|---|---|
| G1 — lignes natives de l'API | **corrigé** | reproduction rejouée : `restart api`, `/readyz` 200 (`postgres` et `storage` en `Healthy`) ; `logs --since 1m api \| grep -v '^{'` ne sort rien ; 14 lignes, toutes JSON, aucune mention de `gssapi` |
| G2 — trace multi-ligne du SSR | **corrigé** | reproduction rejouée : `/nimporte-quoi` et `/readyz` sur 18080 restent en 404 ; `logs --since 1m web-ssr \| grep -v '^{'` ne sort rien ; chaque erreur donne une ligne `{"level":"error",…,"exception":{"class":"Error","code":4002},"msg":"ssr.render.failed"}`, sans l'URL demandée |
| G3 — `autoCsp` dans le plan | **persistant** | hors du périmètre des corrections et de la vérification (`docs/reecriture/implementation/`) ; `grep -rn -i autocsp docs/reecriture/implementation` donne toujours `lot-00-socle.md:96` et `lot-03-web-public.md:340`. Sans effet sur le critère ; à corriger avant L3.11 |

Balayage complet depuis la reconstruction (`logs --since <démarrage>`) : `api` 14 lignes,
`web-ssr` 16 lignes, toutes JSON et analysables par `jq` ; `nginx` 0 ligne (entrypoint
silencieux, pas de journal d'accès).

Les points ouverts du §3 restent ouverts, en particulier la décision humaine sur les
fichiers de configuration à la racine de `src/LoDb.Web/`, à prendre avant L3.2.

### 6.3 Critère de sortie local du lot 0

| Volet | État | Preuve |
|---|---|---|
| Suites vertes | **vérifié** | §6.1 : .NET 81/81, front complet, E2E 2/2 |
| `/en/` rendu en SSR par `lodb-next` | **vérifié** | `curl http://localhost:18080/en/` : 200 `text/html`, `<html lang="en"`, `ng-server-context="ssr"`, `<lodb-shell` ; test de fumée vert |
| `/healthz` | **vérifié** | API : 200 `{"status":"Healthy"}` sur 18081 et depuis le réseau (`wget http://api:8080/healthz` dans `nginx`) ; nginx : 200 `ok` |
| `/readyz` | **vérifié** | 200, `postgres` et `storage` en `Healthy`, sur 18081 et depuis le réseau |
| `lodb_build_info` | **vérifié** | `exec -T nginx wget -qO- http://api:9464/metrics` : `lodb_build_info{otel_scope_name="LoDb.Api",revision="dev",version="0.1.0"} 1` ; `/metrics` en 404 sur 18080 et 18081 ; 9464 non publié |
| Logs JSON d'une ligne par enregistrement | **vérifié** | G1 et G2 corrigés ; balayage ci-dessus sans aucune ligne hors JSON |

**Critère du lot 0 : vérifié.** Reste G3, correction de documentation hors périmètre,
transmise au jalon suivant.
