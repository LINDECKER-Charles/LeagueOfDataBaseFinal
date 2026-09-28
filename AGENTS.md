# LeagueOfDataBase — instructions projet

Encyclopédie League of Legends (champions, objets, runes, sorts d'invocateur) pour chaque
version et chaque langue de **Data Dragon**, plus CommunityDragon pour les chromas. Un seul
front sert le web, le desktop et Android. Ce fichier fixe l'architecture, les règles de code
et les pièges connus du dépôt ; il complète les préférences globales de l'agent sans les
redire. Il est lu par tous les agents (`CLAUDE.md` ne fait que l'importer).

L'ancienne version (Symfony + Go + Vue) est **archivée sous `legacy/`** et reste en
production jusqu'à la bascule ([runbook](docs/reecriture/bascule.md)). Ses règles sont dans
[`legacy/AGENTS.md`](legacy/AGENTS.md) ; on n'y touche pas, sauf correctif exigé par la prod
avant la bascule.

## Changelog : une entrée par changement visible

Chaque feature, correctif ou gain perceptible par le joueur crée une entrée dans
`docs/changelog/YYYY/YYYY-MM-DD-slug.md` (date de livraison, slug kebab-case), au format de
[`docs/changelog/TEMPLATE.md`](docs/changelog/TEMPLATE.md) : frontmatter `date`, `type`,
`scope` (`front`, `back`, `apps`, `infra`, `full-stack`), `title`, `summary`, `tags` ; corps
orienté joueur, contexte technique en fin sous `## Technique`.

- ✅ feature, bug visible, perf ou UX perceptible, devops visible (disponibilité, sécurité).
- ❌ refacto interne, tests, lint, formatage, dépendances sans effet.
- L'entrée part **dans le commit** du changement. Deux changements distincts, deux entrées.
- Avant une release : synthèse manuelle en JSON publiés dans
  `src/LoDb.Web/src/app/features/editorial/changelog/published/` (+ `manifest.json`), puis
  archivage des entrées dans `docs/changelog/archived/YYYY/`
  ([`docs/changelog/README.md`](docs/changelog/README.md)).
- Les entrées de la bascule sont des brouillons datés du jour J :
  [`docs/reecriture/changelog-bascule/`](docs/reecriture/changelog-bascule/README.md).

## Stack

| Couche | Techno |
|---|---|
| API, ingestion, tâches de fond | ASP.NET Core 10, un seul hôte `LoDb.Api` (`/api`, `/v1`, `/webhooks`) |
| Données | PostgreSQL 17 + EF Core ; blobs Data Dragon adressés par contenu (volume `storage`) |
| Web | Angular 22 en SSR (Node 24), locale dans l'URL (`/{locale}/…`), 21 langues |
| Desktop / Android | Photino + Velopack / Capacitor 8 + live update signé |
| Frontal | nginx (cache des pages, `/cdn/blobs/`) derrière l'edge Caddy du VPS (`infra-vps`) |
| Observabilité | OpenTelemetry, logs JSON d'une ligne, `/metrics` (port 9464) |

Décisions : [`docs/reecriture/README.md`](docs/reecriture/README.md) et ses ADR 0001 à 0010.

### Arborescence

```
LoDb.slnx  global.json  Directory.Build.props  Directory.Packages.props  .editorconfig
compose.yaml  compose.override.yaml (dev)  compose.deploy.yaml (hôtes)
.env.preprod.example  .env.production.example
docker/{api,web-ssr,nginx,android-build}/
src/LoDb.Domain/          pur, sans I/O : versions, langues, éditions, chemins canoniques
src/LoDb.Ingestion/       Egress/ Ddragon/ Pipeline/ Catalog/ …
src/LoDb.Infrastructure/  Persistence/ Storage/ Jobs/ Outbox/ Audit/ Analytics/ DataProtection/
src/LoDb.Api/             Program.cs Hosting/ Cli/<Zone>/ Workers/<Zone>/ Modules/<M>/ openapi/
src/LoDb.Web/             Angular : src/app/{core,ui,features}  src/server/  public/i18n/
src/LoDb.Desktop/         coquille desktop
tests/LoDb.*.Tests/  tests/LoDb.Testing/  tests/LoDb.E2E/  tests/LoDb.Parity/  tests/fixtures/
tools/                    contrat, i18n, fixtures, releases, bascule, parité
legacy/                   ancienne stack, archivée
```

Références : `Api` → `Ingestion` → `Infrastructure` → `Domain`. Les modules
(`Add<Module>`/`Map<Module>`) et les zones (`AddLoDb<Zone>`) sont pré-enregistrés :
`Program.cs` ne change pas, un chantier ne remplit que son fichier.

### Environnements

| Environnement | Projet Compose | Où | Déploiement |
|---|---|---|---|
| local | `lodb-dev` (emplacements `lodb-dev-e1`, `lodb-dev-e2`) | poste | `docker compose` |
| `preprod` | `lodb-preprod`, tag `:preprod` | VPS, `test.league-of-data-base.com` | à chaque push de `dev` (`ci.yml`) |
| `production` | `lodb-production`, tag `:production` | VPS, `league-of-data-base.com` | à la main (`promote.yml`, approbation) |

Un environnement porte **un seul nom** : environnement GitHub, projet `lodb-<env>`, tag
d'image, dossier `/opt/lodb-<env>`. Jusqu'à la décommission, l'ancienne stack occupe encore
`lodb-prod`, `lodb-staging`, le projet local `lodb` et le tag `:prod` de `lodb/nginx` :
**jamais** réutilisés. Secrets et `.env` : [`configuration.md`](docs/guides/configuration.md).

## Invariants d'architecture (ADR)

- **Un seul hôte `LoDb.Api`** (ADR 0002). L'egress Data Dragon et CommunityDragon passe
  par le client nommé `ddragon` et son allow-list (https, deux hôtes, chaque redirection
  re-vérifiée, plafond de taille). 403/404 = **absence définitive**, rendue comme une
  valeur ; 5xx/timeout = transitoire, jamais persisté.
- **`Add*` et `Map*` ne font aucune I/O et ne lèvent rien** : la génération OpenAPI exécute
  `Program` sans configuration. Sous-commandes : `ICliCommand` sous `Cli/<Zone>/` ; tâches
  de fond : `BackgroundService` sous `Workers/<Zone>/`, découvertes par convention.
  `LoDb:Workers:Enabled=false` coupe toutes les tâches.
- **Tâches périodiques** (ADR 0003) : `PeriodicTimer` + verrou consultatif Postgres, files
  `Channel<T>` bornées, **une ligne de synthèse par lot**, jamais une par URL.
- **Stockage** (ADR 0004) : blobs derrière `IBlobStore`, adressés par contenu, écrits de
  façon **atomique** (fichier temporaire sur le même volume, puis `File.Move`) ; nginx ne
  sert que `/cdn/blobs/`. Manifeste, analytics, audit et clés Data Protection en Postgres
  (`absent` = absence définitive). Rien n'empêche N instances : **aucune session serveur**.
- **Objets et sorts indexés par id, jamais par nom** : Data Dragon livre des jumeaux
  homonymes (édition Classic).
- **SSR** (ADR 0005) : le serveur ne lit **aucun cookie** et ne détient **aucun secret**.
  Locale en préfixe d'URL ; l'id fait foi, le slug est décoratif (301 vers la canonique).
  Thème par script inline ; `www.` et `.fr` redirigés par nginx.
- **Front** (ADR 0006) : il ne consomme que le **client généré** depuis OpenAPI, sans copie
  manuelle des contrats. Aucun `if (platform)` hors de `core/platform/`, aucun kit UI, pas
  de NgRx.
- **Persistance** : migrations EF **additives** tant que l'ancienne stack partage la base
  de prod, c'est-à-dire jusqu'au *contract* (runbook § 9). Une base Doctrine est d'abord
  marquée à `Baseline` par `migrate`.
- **Identité** (ADR 0009) : hash argon2id PHC, lisibles par `password_verify` de PHP.
  Autorisation : jamais `AllowAnonymous`, les politiques portent le garde XSRF et `Origin`.
- **Observabilité** (ADR 0010) : logs JSON d'**une ligne par enregistrement** avec
  `trace_id`, `EventName` en `domaine.sujet.resultat`, **aucune clé de contexte `error`**,
  aucune donnée personnelle. `/metrics` sur 9464, **jamais routé ni publié**. `/healthz` ne
  dépend de rien ; `/readyz` vérifie Postgres et le stockage.
- **Apps** (ADR 0007, 0008) : Photino derrière `IDesktopShell`, versions épinglées ;
  Capacitor 8 et live update signé ; `X-LoDb-Client` et `426` : l'API décide.
- **Artefacts générés** (`src/LoDb.Api/openapi/*.json`, `core/api/generated/`,
  `package-lock.json`) : jamais édités à la main. `npm run api:generate` les régénère et le
  commit qui change le contrat les inclut.

## Conventions de code

Les limites chiffrées sont des plafonds ; les principes sont des défauts à suivre sauf
raison explicite.

### Principes

- **DRY** : une règle vit à un seul endroit ; pas d'abstraction avant la 3ᵉ répétition.
- **KISS** : la solution la plus simple qui résout réellement le problème.
- **SOLID** : responsabilité unique, ouvert/fermé, substitution, interfaces ciblées,
  dépendance aux abstractions.
- **CQS** : une fonction modifie l'état **ou** retourne une valeur, jamais les deux.

### Limites

| Règle | Limite |
|---|---|
| Fichier | ≤ 300 lignes (alerte), 400 maximum |
| Fichiers par dossier | ≤ 10 ; au-delà, sous-dossiers par domaine |
| Fonction / méthode | ≤ 30 lignes |
| Paramètres | ≤ 3, au-delà un objet ; constructeurs d'injection exemptés |
| Imbrication | ≤ 3 niveaux |
| Complexité cyclomatique | ≤ 10 |
| Ligne | ≤ 100 caractères |

Exceptions assumées : dossiers de données (releases du changelog, fixtures, catalogues
i18n) et racine de `src/LoDb.Web/` (fichiers imposés par l'outillage).

- **Un seul élément public par fichier**, nommé comme lui.
- **Pas de nombre ni de chaîne magique** : constantes nommées.
- Noms explicites révélant l'intention, casse idiomatique, pas d'abréviation cryptique
  (`id`, `url`, `http`, `sha` tolérés), booléens en `is`/`has`/`should`/`can`.
- Une fonction fait une chose ; fonctions pures par défaut, effets de bord explicites ;
  *guard clauses* plutôt qu'imbrication ; pas de paramètre drapeau (sauf opt-in orthogonal
  documenté).
- **Commentaires en anglais**, sur le *pourquoi* (décision, piège, invariant), jamais la
  paraphrase du code.

### C# (.NET 10)

- `net10.0`, `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended` :
  **zéro avertissement**. Espaces de noms au niveau du fichier (`LoDb.<Projet>.<Dossier>`).
- Classes `sealed` par défaut, `record` pour les DTO et objets valeur, membres `required`,
  constructeurs primaires pour l'injection.
- Aucun état statique mutable : `TimeProvider` injecté, jamais `DateTime.UtcNow` ;
  `CancellationToken` sur toute méthode asynchrone ; async de bout en bout (jamais
  `.Result` ni `.Wait()`).
- `[LoggerMessage]`, exception passée en objet. Exceptions typées pour le transitoire ;
  l'absence définitive est une valeur, pas une exception.
- `/api` répond en ProblemDetails ; `/v1` garde `{error:{code,message}}` et le snake_case.
- Minimal APIs : `MapGroup` par module, `TypedResults`. Options `LoDb` liées et validées au
  démarrage (`ValidateOnStart`) ; configuration par `LoDb__<Section>__<Clé>`, jamais de
  secret dans `appsettings*.json`.
- Versions NuGet dans `Directory.Packages.props` seul, jamais flottantes.

### TypeScript / Angular 22

- Composants standalone, signals, zoneless, `OnPush`, `inject()`, `input()`/`output()`,
  blocs `@if`/`@for`/`@defer`. TypeScript et templates stricts, pas de `any`.
- Un composant = présentation + câblage mince ; l'orchestration vit dans des services et
  des fonctions pures, testables sans monter le composant.
- ESLint bloquant : `@capacitor/*` et `@capawesome/*` seulement sous `core/platform/` ; une
  feature n'importe que `core/`, `ui/` et elle-même.
- Styles : jetons Hextech, rien en dur, propriétés logiques (RTL), champs de saisie à 16 px
  au moins.
- Une page ne touche jamais au `<head>` : elle appelle `inject(Seo).apply(SeoPage)`.
  Données de page par `injectRouteData(clé)` ; liens avec query ou ancre par `UrlTree`
  (`injectCatalogueLink`), jamais une chaîne passée à `[routerLink]`.

### Tests

- **Toute feature livre ses tests** dans le même commit : le comportement nominal et les
  cas limites qu'elle introduit, rien de plus. Un bon test casse quand le comportement
  casse, pas quand l'implémentation change. On ne teste ni le framework ni les bibliothèques.
- .NET : xUnit v3 dans `tests/LoDb.<Projet>.Tests/`, Postgres réel par Testcontainers
  (`tests/LoDb.Testing`), réponses Data Dragon rejouées depuis `tests/fixtures/`.
- Front : Vitest en `*.spec.ts` à côté du code. E2E Playwright dans
  `tests/LoDb.E2E/specs/<feature>/` ; nouvelles clés i18n dans `public/i18n/<feature>/`.
- Outils : `node --test` dans `tools/<outil>/test/`.

## Garde-fous (avant de rendre un travail)

```bash
dotnet build LoDb.slnx -c Release            # 0 avertissement
dotnet test LoDb.slnx                        # Testcontainers : Docker démarré
npm ci --prefix src/LoDb.Web
npm --prefix src/LoDb.Web run lint           # puis typecheck, test, build:web, build:shell
npm --prefix src/LoDb.Web run api:check      # le client généré suit le contrat
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml up -d --build --wait
npm --prefix tests/LoDb.E2E test             # stack démarrée (LODB_E2E_BASE_URL sinon)
bash tools/routing/check-urls.sh             # grammaire d'URL de l'ADR 0005
```

Selon ce qui est touché : `node --test 'tools/<outil>/test/*.test.mjs'` ;
`docker run --rm -v "$PWD:/repo" -w /repo rhysd/actionlint` pour les workflows ;
`docker compose --env-file .env.preprod.example -f compose.yaml -f compose.deploy.yaml
config --quiet` pour la surcouche des hôtes. Commandes détaillées :
[`docs/guides/developpement.md`](docs/guides/developpement.md).

- **Stack locale `lodb-dev`**, une seule instance, depuis la racine : 18080 nginx, 18081
  API, 18082 SSR, 15432 Postgres, 18025 Mailpit. Emplacements `lodb-dev-e1` et `-e2`
  (181xx, 182xx) pour un worktree, supprimés (`down -v`) avant de rendre.
- **Ne jamais toucher** aux conteneurs `chewb-*`, `laforce-*` et `grafana/mcp-grafana`, ni à
  leurs ports (3307, 33306, 21200 à 21213). Le port 9464 n'est jamais publié : les métriques
  se lisent depuis le réseau de la stack (`wget http://api:9464/metrics` dans nginx).

## Pièges connus (ne pas « corriger » par erreur)

### Build et outillage

- `dotnet test` compile en **Debug**, le build en **Release** : les deux coexistent. Il ne
  prend pas `-m` (code 5, aucun test) ; `--artifacts-path` fait échouer
  `PhotinoIsolationTests`. `bin/` et `obj/` ne sont ignorés que sous `/src` et `/tests`.
- OpenAPI : `dotnet build src/LoDb.Api -p:LoDbGenerateOpenApi=true` écrit
  `openapi/LoDb.Api_app.json` et `LoDb.Api_public-v1.json` (noms imposés par le SDK).
  `MapOpenApi` n'est actif qu'en Development. Sous Windows, normaliser les `\r\n` avant
  `api:check`.
- **npm 12** bloque les scripts d'installation (`allowScripts`) d'`esbuild`, `lmdb`,
  `@parcel/watcher` et `msgpackr-extract` ; les binaires viennent des paquets optionnels de
  plateforme. Si l'image `web-ssr` échoue au build, c'est la première piste.
- `npm install --prefix src/LoDb.Web` tire `@capacitor/ios` et `@capacitor/keyboard` par
  `@aparajita/capacitor-secure-storage` : attendu, il n'y a pas de plateforme iOS.
- Relever le budget `initial` de `build:web` est une décision du propriétaire.
- **CLI en `Development`** : l'hôte de `CliRunner` y valide tout le conteneur. Un service
  qui dépend de l'hôte web fait tomber **toutes** les sous-commandes. Après un ajout à un
  `Add<Module>` : `docker compose -p lodb-dev … exec -T api dotnet LoDb.Api.dll analytics
  import --source /x --dry-run` doit répondre `No such directory: /x`.

### Front, SSR et SEO

- **`autoCsp` est incompatible avec le SSR** ; `withFetch` et `withIncrementalHydration`
  sont omis (défauts en v22).
- Issues de navigation **rendues en place** : 301, 302, 404 et 5xx s'affichent à l'URL
  demandée (`PageResponse`), jamais d'`UrlTree` de redirection côté serveur.
  `/{locale}/account`, `/{locale}/u` et `/b` ont une route 404 explicite (`bareNotFound`).
  Les routes `RenderMode.Client` (compte, `/admin`) ne posent pas de statut.
- La liaison des entrées de composant par le routeur reste coupée : une query forgée
  remplirait des entrées.
- `app.routes.server.spec.ts` passe par `ɵextractRoutesAndCreateRouteTree`, API privée
  qu'une montée de version peut déplacer.
- Points d'accroche de `app.config.ts` : chaque propriétaire remplit son dossier
  (`core/auth`, `core/analytics`, `core/update`) sans toucher `app.config.ts` ni `app.html`.
- Une page qui oublie `Seo.apply` n'a ni canonique ni hreflang. Une clé `about.*`, `api.*`
  ou `seo.*` n'est traduite que si un ancêtre fournit son scope (`provideTranslocoScope`) ;
  sinon la clé brute sort dans le HTML, sans erreur.
- **Grilles de cartes en `div role=list`**, jamais `ul/li` ni `<p>` : le texte riche de
  Riot contient des `<li>` nus qui cassent l'hydratation.
- **Squelette à la hauteur de ce qu'il remplace** (forme `portrait` pour une grille de
  portraits), sinon CLS.
- **Dialogues CDK** : le focus initial est sur `cdk-dialog-container` ; écouter
  `DialogRef.keydownEvents`, pas `host: {'(keydown)'}`. Un dialogue plus haut que l'écran
  reste défilable jusqu'à son pied.
- L'en-tête n'a que 288 px utiles à 320 px : tout ajout au groupe d'actions se vérifie à
  320 px, `ar` compris. Un `white-space: nowrap` sur un titre doit pouvoir passer à la ligne.
- Variante store : `environment.payments` n'agit que là où le code le lit ; le paiement se
  retire par le routage et les liens. `cap sync` ne tourne que dans le conteneur
  (`tools/android/build-debug.sh`).

### API, données et comptes

- `/readyz` se lit sur l'API (18081 ou `api:8080`) ; demandé à nginx, il part au SSR (404).
- En local, les URLs absolues perdent le port (nginx transmet `Host: $host`) : l'origine
  canonique se fixe par `LoDb__Seo__CanonicalOrigin` et `LoDb__Accounts__SiteOrigin`.
- `/api/pickers/*` exige `version` et `lang` (400 `invalid-version` sinon).
- **E-mails** : le modèle d'un `EmailMessage` s'écrit avec `EmailModelKeys`, jamais en
  littéral (sinon `dead` dès le premier essai). L'outbox n'envoie rien sans
  `LoDb:Mail:Host` : voulu. En local, Mailpit sur http://localhost:18025.
- Bannir, c'est `IsBanned` plus `UpdateSecurityStampAsync`. Un hash argon2id **plus fort**
  que la cible est conservé (`Argon2Passwords.IsTarget`) ; un compte hérité arrive sans
  `security_stamp`, la connexion le remplit.
- Rapport de surveillance de l'admin : un rapport dont les lectures ont échoué reste 30 s en
  cache. Une carte absente se cherche dans les logs
  (`admin.monitoring.database_unreadable`), jamais par un `waitFor` plus long.
- **Webhook Stripe en local** : aucun secret en dev (503). Pour un rejeu, un secret de test
  dans une surcouche hors dépôt et une signature datée de moins de 5 minutes.

### Tests E2E

- **Quota d'inscriptions** : 5 par heure et par adresse, en mémoire, et toute la suite
  arrive sous une seule adresse. Seule `specs/account/register.spec.ts` passe par le
  formulaire ; les autres créent leur compte par la CLI (`support/member-account.ts`,
  `support/worker-account.ts`). Entre deux passages complets :
  `docker restart lodb-dev-api-1`. Ne jamais relever le quota pour les tests.
- Comparer le chemin d'une réponse (`new URL(url).pathname`), jamais `url.endsWith(...)`.
  Limiter les comptes de titres ou de boutons à `main` ou au composant visé. Une spec qui
  compte les POST exclut `/api/analytics/`. Un parcours qui crée un compte le supprime aussi
  en cas d'échec.
- Angular laisse des espaces autour d'un texte entre balises : ancrer une regex sur le
  contenu, pas sur `^`/`$`.
- `disclosure.spec.ts` échoue parfois en suite complète, jamais seul : relancer n'est pas
  une correction.
- Les instantanés de `tests/LoDb.E2E/test-results/` sont écrasés au passage suivant.

### Infra et déploiement

- Variables Compose préfixées **`LODB_`**. En dev, les images s'appellent
  `<projet>-<service>` : un emplacement n'écrase pas les images de la stack locale.
- nginx : sous-domaine reconnu par `server_name ~^api\.` ; CORP `same-origin` sauf
  `/cdn/blobs/` en `cross-origin` (coquille Android) ; `access_log off` voulu (l'edge
  journalise) ; `sites/*.conf` passe par `envsubst`, filtré sur `^LODB_`.
  `server.d/legacy-redirects.conf` envoie à `/api/legacy` les premiers segments de l'ancien
  site : tout nouveau chemin hors locale se vérifie contre cette liste. `server.d/seo.conf`
  possède `/sitemap.xml`, `/sitemaps/`, `/robots.txt` et `/llms.txt`.
- `gzip_types` de `docker/nginx/nginx.conf` doit lister `text/javascript` et
  `application/manifest+json` (sinon ~1,7 s perdues en 4G lente). Un budget Lighthouse ne
  se relâche jamais ; Lighthouse en local est pessimiste (cache de transfert du SSR rangé
  sans le port).
- SSR : `LODB_TRUST_PROXY_HEADERS` obligatoire derrière nginx, `LODB_ALLOWED_HOSTS` liste
  les hôtes acceptés ; `public/` ne contient ni `media/` ni script racine nommé comme un
  bundle (cache `immutable`).
- Constats de logs (lignes hors JSON, erreurs multi-lignes) : **à corriger à la source,
  jamais à filtrer**.

### Transition (jusqu'à la décommission)

- L'ancienne stack (projet `lodb`) tourne depuis `legacy/` du dépôt principal
  (`docker compose --project-directory legacy …`, son `.env` est `legacy/.env`), jamais
  recréée depuis un autre dossier (label `com.docker.compose.project.working_dir`). Ses
  commandes console gardent `-u www-data`.
- **Répétition locale** : [runbook](docs/reecriture/bascule.md) § 3.1 tel qu'écrit,
  emplacement `lodb-dev-e2`, jamais pendant un build Android. Derrière `| tee`, lire
  `$pipestatus[1]` (zsh) ou `${PIPESTATUS[0]}` (bash). Après elle, `lodb_rehearsal` ne doit
  plus exister dans le Postgres de l'ancienne stack.
- **Parité** : `tools/parity/` collecte, `tests/LoDb.Parity` compare ; un écart nouveau
  reçoit une règle argumentée de `ParityRules.cs`, jamais un filtre. Parité des builds et
  agrégats analytics : copies dans le Postgres de l'ancienne stack, jamais sa base `lodb`.
- **Rapports générés** (`diff-seo.md`, `lighthouse.md`…) : écrits par leurs outils seuls ;
  un écart SEO nouveau reçoit une règle dans `tools/seo-diff/lib/rules.mjs`.
- Les outils de comparaison (`seo-diff`, `lighthouse`, `builds-parity`) appellent encore
  `next` le côté « nouvelle stack » : ils disparaissent à la décommission avec `legacy/`.
- **Documents historiques** (`docs/reecriture/{plan-*,heritage,adr,implementation,
  rapports}`) : ils gardent les noms de leur époque (`lodb-next`, `tools/next/`…) ; on ne
  les réécrit pas.

## commit

Conventional Commits en **français**, sujet à l'infinitif, sans accents, **72 caractères
au plus**, corps réservé au *pourquoi*, pied `BREAKING CHANGE:` si besoin. Un commit = un
changement cohérent ; tests et entrée de changelog dans le même commit ; `git add` par
chemins explicites ; aucune ligne d'attribution. Branches : `type/description-courte`.

| Chemin | Scope |
|---|---|
| `src/LoDb.Domain/**` | `back/domain` |
| `src/LoDb.Ingestion/**` | `back/ingestion` |
| `src/LoDb.Infrastructure/Persistence/**` | `back/db` |
| `src/LoDb.Infrastructure/**` (autres) | `back/infrastructure` |
| `src/LoDb.Api/Modules/<Module>/**` | `back/<module>` (minuscules) |
| `src/LoDb.Api/**` (autres) | `back/host` |
| `src/LoDb.Web/src/app/features/<feature>/**` | `front/<feature>` |
| `src/LoDb.Web/src/app/{core,ui}/**`, `src/styles/**` | `front/core`, `front/ui` |
| `…/features/editorial/changelog/published/**` (+ archivage `docs/changelog/**`) | `changelog` |
| `src/LoDb.Web/public/i18n/**` | `i18n` |
| `src/LoDb.Web/android/**` | `android` |
| `src/LoDb.Desktop/**` | `desktop` |
| `tests/LoDb.E2E/**` | `e2e` |
| `tests/LoDb.*.Tests/**`, `tests/LoDb.Parity/**`, `tests/fixtures/**` | scope du code testé |
| `docker/**`, `compose*.yaml`, `.env.*.example` | `infra` |
| `tools/**` | `tools` |
| `.github/**` | `ci` |
| `docs/**`, `README.md`, `CONTRIBUTING.md`, `AGENTS.md` | `docs` |
| `legacy/**` | `legacy` |
| `LoDb.slnx`, `Directory.*.props`, `global.json`, `.editorconfig`, lockfiles | sans scope (`build` ou `chore`) |

## Références

- [`docs/reecriture/README.md`](docs/reecriture/README.md) : pourquoi la réécriture, ADR.
- [`docs/guides/developpement.md`](docs/guides/developpement.md) : commandes, ports,
  emplacements, dépannage.
- [`docs/guides/configuration.md`](docs/guides/configuration.md) : secrets et `.env`.
- [`docs/guides/github-actions-secrets.md`](docs/guides/github-actions-secrets.md) : pipeline.
- [`docs/reecriture/bascule.md`](docs/reecriture/bascule.md) : bascule, retour arrière,
  *contract*, décommission.
- [`docs/guides/observabilite.md`](docs/guides/observabilite.md),
  [`release-desktop.md`](docs/guides/release-desktop.md),
  [`release-android.md`](docs/guides/release-android.md).
