# Pièges connus (ne pas « corriger » par erreur)

Comportements voulus ou contraintes constatées pendant la construction de la stack .NET +
Angular. Chacun a coûté du temps une fois : le lire avant de toucher à la zone concernée.
Les règles de code et les invariants font foi dans [`AGENTS.md`](../../AGENTS.md).

## Build et outillage

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

## Front, SSR et SEO

- **`autoCsp` est incompatible avec le SSR** ; `withFetch` et `withIncrementalHydration`
  sont omis (défauts en v22).
- Issues de navigation **rendues en place** : 301, 302, 404 et 5xx s'affichent à l'URL
  demandée (`PageResponse`), jamais d'`UrlTree` de redirection côté serveur.
  `/{locale}/account`, `/{locale}/u` et `/b` ont une route 404 explicite (`bareNotFound`).
  Les routes `RenderMode.Client` (compte, `/admin`) ne posent pas de statut.
- **Pré-routeur SSR** (`src/server/routing/`), avant l'admission : premier segment ni forme
  de locale, ni `b`, ni `admin` → 404 statique sans Angular ; segment décodé en `/` ou NUL →
  400 ; liste ou fiche du catalogue → 301 `no-store` vers son URL sans les paramètres
  qu'elle ne lit pas. Un nouveau paramètre de liste se déclare dans les clés de facettes
  (`*-facet-keys.ts`) ou dans `owned-params-of.ts`, sinon il disparaît au rechargement.
  Locale et version se lisent **décodées** (`/%66r/%31%35.13.1/` est épinglée). Un segment
  `.` ou `..`, ou qui contient `\`, `(`, `)` ou `;`, donne aussi un 400 : le rendu résout
  l'URL comme un navigateur et son routeur lit outlets et paramètres matriciels
  (`;version=` compris), il rendrait donc une autre page que celle classée.
- Une API qui ne répond pas (aucune réponse, 502, 504) donne un **502** ; seule une 503 de
  l'API (délestage, avec `Retry-After`) reste 503. Chaque appel API d'un rendu est borné
  par l'échéance du rendu (`LODB_RENDER_DEADLINE_SECONDS`, 20 s).
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

## API, données et comptes

- `/readyz` se lit sur l'API (18081 ou `api:8080`) et, par nginx, sur le sous-domaine `api.`
  seulement (1 requête/s par adresse, sonde externe) ; demandé au site, il part au SSR (404).
- En local, les URLs absolues perdent le port (nginx transmet `Host: $host`) : l'origine
  canonique se fixe par `LoDb__Seo__CanonicalOrigin` et `LoDb__Accounts__SiteOrigin`.
- **Pool Postgres** : `Maximum Pool Size=32;Timeout=5` dans la chaîne de connexion. Une
  requête `/api` qui n'obtient pas de connexion à temps reçoit un **503** `service-busy`
  avec `Retry-After` : délestage voulu, non compté. Une base injoignable donne un **500**,
  compté par `lodb_db_unavailable_total`. Npgsql ne donne au pool épuisé ni type ni code :
  son message est reconnu par son texte, et `DatabaseFailureTests` le revérifie sur un vrai
  pool à chaque montée de version.
- **Corps de requête** : 128 Kio au plus (`LoDb:Hosting:MaxRequestBodySize`), 413 au-delà.
  Le plus gros corps légitime, un build maximal dont chaque caractère est échappé, dépasse
  64 Kio (`tests/LoDb.E2E/specs/builds/maximal-build.spec.ts`) : ne pas descendre à 64 Kio.
  Un endpoint qui doit lire plus pose son propre `RequestSizeLimit` (webhook Stripe, 1 Mio).
  Les tests xUnit de cette borne tournent sur un vrai Kestrel, en Production
  (`Hosting/Support/KestrelApi.cs`) : le serveur en mémoire n'applique aucune limite, et
  Development transforme un corps invalide en exception.
- `/api/pickers/*` exige `version` et `lang` (400 `invalid-version` sinon).
- **Porte froide des catalogues** : un appel direct à l'API (18081, `api:8080`) depuis une
  adresse privée et sans `X-Forwarded-For` est compté comme le SSR (débit interne, attente
  de 2 s permise). Pour reproduire un client public, passer par nginx (18080). Un `503`
  « en attente » sous charge est un délestage voulu, pas une panne.
- Une URL de l'ancien site sur une version non stockée répond `302` vers la liste de la
  version, sans rien ingérer : voulu (une entrée retirée du jeu ne devient pas une 404).
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

## Tests E2E

- **Quota d'inscriptions** : 5 par heure et par adresse, en mémoire, et toute la suite
  arrive sous une seule adresse. Seule `specs/account/register.spec.ts` passe par le
  formulaire ; les autres créent leur compte par la CLI (`support/member-account.ts`,
  `support/worker-account.ts`). Entre deux passages complets :
  `docker restart lodb-dev-api-1`. Ne jamais relever le quota pour les tests.
- **Bornes relevées pour l'E2E**, dans `compose.override.yaml` seulement (jamais
  `compose.deploy.yaml`) : la suite ouvre des patchs anciens et des langues, d'une seule
  adresse, bien plus vite qu'un hôte servi ne l'admet. La pile de dev relève l'admission
  des rendus du SSR (`LODB_*RENDER*`), la porte froide des catalogues
  (`LoDb__Catalog__ColdLoad*`, `PublicColdLoadsPerSecond`), les flux du warm-up
  (`LoDb__Catalog__WarmUp__*`) et le budget d'ingestion des visiteurs
  (`LoDb__Ingestion__ColdDatasetsPerMinute`, `ColdImagesPerMinute`,
  `MaxWaitingIngestions`, `SlotWait` à 20 s). Ces bornes se testent en xUnit avec leurs
  valeurs de production, jamais par l'E2E.
- **Limite de connexions** : 10 connexions par adresse et par 15 min
  (`LoDb:RateLimiting:SignInPerWindow`), et toute la suite se connecte par
  `POST /api/account/login` depuis une seule adresse (`support/member-account.ts`, les
  administrateurs des specs `admin`) : `compose.override.yaml` relève la borne pour la pile
  locale, jamais `compose.deploy.yaml`. Les 4 échecs par identifiant et par adresse ne se
  relèvent pas (ils restent sous le verrouillage à 5) ; les tests xUnit gardent les valeurs
  de production.
- Comparer le chemin d'une réponse (`new URL(url).pathname`), jamais `url.endsWith(...)`.
  Limiter les comptes de titres ou de boutons à `main` ou au composant visé. Une spec qui
  compte les POST exclut `/api/analytics/`. Un parcours qui crée un compte le supprime aussi
  en cas d'échec.
- Angular laisse des espaces autour d'un texte entre balises : ancrer une regex sur le
  contenu, pas sur `^`/`$`.
- `disclosure.spec.ts` échoue parfois en suite complète, jamais seul : relancer n'est pas
  une correction.
- **Bornes du SSR relevées en dev** (`compose.override.yaml`, jamais
  `compose.deploy.yaml`) : toute la suite arrive d'une adresse et ouvre les anciens patchs
  bien plus vite qu'un hôte servi ne les admet. Admission : `LODB_RENDER_MAX_IN_FLIGHT`,
  `LODB_PINNED_*`, `LODB_CURRENT_*` et la part d'un client (`LODB_RENDER_PER_CLIENT_*`,
  `LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE`). Attente de l'API par un rendu :
  `LODB_RENDER_DEADLINE_SECONDS` et `LODB_RENDER_API_TIMEOUT_SECONDS` à 30 s, car la
  première visite d'un patch attend son ingestion. Une 503 ou une 502 du SSR en E2E se
  cherche d'abord là.
- Les instantanés de `tests/LoDb.E2E/test-results/` sont écrasés au passage suivant.
- **Débits par adresse de nginx relevés** : toute la suite arrive sous une seule adresse (la
  passerelle Docker), plus vite qu'un visiteur. `compose.override.yaml` relève
  `LODB_PAGES_*`, `LODB_API_READS_*`, `LODB_LEGACY_*` et `LODB_SITEMAPS_*` (débit et rafale) ;
  jamais `compose.deploy.yaml`. Pour retrouver un défaut, retirer la clé (`!reset null`) :
  une valeur vide fait refuser la configuration de nginx.
- **Tout relèvement ajouté à `compose.override.yaml` s'annule aussi dans
  `tools/load/compose.bench.yaml`**, le calque du banc de charge, sinon le banc mesure une pile
  sans borne : `tools/load/test/compose-bench.test.mjs` échoue tant qu'une clé relevée garde sa
  valeur relevée. Compose n'applique pas le `!reset` par clé d'un troisième fichier à une clé
  que le deuxième a ajoutée : le calque redonne l'entrée de `compose.yaml` (SSR, nginx) ou
  nomme la clé sans valeur (API, comme `compose.deploy.yaml`). Le banc ne tourne jamais sur le
  staging, qui partage les vCPU de la prod ([`tools/load/`](../../tools/load/README.md)).

## Infra et déploiement

- Variables Compose préfixées **`LODB_`**. En dev, les images s'appellent
  `<projet>-<service>` : un emplacement n'écrase pas les images de la stack locale.
- Sur l'hôte, la stack occupe le projet de l'ancienne (`lodb-staging`, `lodb-prod`) : elle
  reprend son volume `pgdata` et range ses blobs dans `ddragon`. Le volume `storage` de
  l'ancienne stack (propriété de `www-data`) n'est ni réutilisé ni supprimé : le nommer
  `storage` rendrait le stockage de l'API non inscriptible.
- nginx : sous-domaine reconnu par `server_name ~^api\.` ; CORP `same-origin` sauf
  `/cdn/blobs/` en `cross-origin` (coquille Android) ; `access_log off` voulu (l'edge
  journalise) ; tous les `.conf` de `sites/`, `server.d/` et `snippets/` passent par
  `envsubst`, filtré sur `^LODB_` (défauts dans le `Dockerfile`, jamais une valeur vide).
  Une variable `LODB_*` de nginx passe par `compose.yaml` avec son défaut
  (`${LODB_X:-défaut}`), **jamais** en clé sans valeur dans un calque comme les réglages de
  l'API : Docker effacerait le défaut `ENV` de l'image et nginx refuserait de démarrer.
  `server.d/legacy-redirects.conf` envoie à `/api/legacy` les premiers segments de l'ancien
  site : tout nouveau chemin hors locale se vérifie contre cette liste. `server.d/seo.conf`
  possède `/sitemap.xml`, `/sitemaps/`, `/robots.txt` et `/llms.txt`.
- nginx, bornes (`sites/site.conf`) : `limit_req` par adresse sur les pages, les lectures
  publiques de l'API, l'ancien site, `/sitemaps/` et `/readyz` ; **jamais** sur les autres
  `/api/` (actions d'un compte, ADR 0009), `/v1/`, `/webhooks/`, `/build/`, `/cdn/`. Une
  location qui vise une route de l'API se compare **sans casse** (`~*`) : l'API route
  `/api/Catalog/…` comme `/api/catalog/…`, que `location /api/` laisserait passer. Le 429
  vient de `snippets/server-guards.conf` : une location qui déclare son propre `error_page`
  doit répéter `error_page 429`. `/api/catalog` et `/api/meta` ne sont **jamais** mis en
  cache par nginx (un HIT contournerait le `426` des apps). Les caches `lodb_seo` et
  `lodb_legacy` vivent dans le conteneur (vides à chaque image), `lodb_pages` sur un volume.
  `/cdn/blobs/` ne sert que `{sha256}.{png,jpg,jpeg,webp,gif,webm,mp4}` : une nouvelle
  extension de blob s'ajoute à la regex. La garde des extensions de scripts
  (`snippets/server-guards.conf`) épargne les chemins de profil (`/u/`, `profiles/`) : un
  nom d'utilisateur peut finir en `.bak` ou `.ini`. Vérification : `tools/nginx/`
  (`node --test`).
- `gzip_types` de `docker/nginx/nginx.conf` doit lister `text/javascript` et
  `application/manifest+json` (sinon ~1,7 s perdues en 4G lente). Un budget Lighthouse ne
  se relâche jamais ; Lighthouse en local est pessimiste (cache de transfert du SSR rangé
  sans le port).
- SSR : `LODB_TRUST_PROXY_HEADERS` obligatoire derrière nginx, `LODB_ALLOWED_HOSTS` liste
  les hôtes acceptés ; `public/` ne contient ni `media/` ni script racine nommé comme un
  bundle (cache `immutable`).
- Constats de logs (lignes hors JSON, erreurs multi-lignes) : **à corriger à la source,
  jamais à filtrer**.

## Transition (jusqu'à la décommission)

- L'ancienne stack (projet local `lodb`) tourne depuis `legacy/` du dépôt principal
  (`docker compose --project-directory legacy …`, son `.env` est `legacy/.env`), jamais
  recréée depuis un autre dossier (label `com.docker.compose.project.working_dir`). Ses
  commandes console gardent `-u www-data`.
- **Répétition locale** : [runbook](../reecriture/bascule.md) § 3.1 tel qu'écrit,
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
