# Lot 1 — Données Data Dragon

Objectif du lot : récupérer, normaliser, stocker et servir en mémoire les données et images
Data Dragon / CommunityDragon, avec toutes les particularités connues en tests, et la
persistance de base de toute la réécriture.

Critère de sortie local : parité des datasets et manifestes normalisés, PHP vs .NET, sur les
10 dernières versions × 5 langues + les versions pièges (rapport) ; un patch complet
(toutes les langues, images des quatre ressources) ingéré sans intervention.

Référence de comportement commune à tout le lot : [`heritage.md`](../heritage.md) § 5 et
§ 6, [`version-compatibility-audit.md`](../../audits/version-compatibility-audit.md) (matrice
des échecs version × langue), et les tests PHPUnit de `app/tests/Unit/Service/{API,Catalog,
Client,I18n,Picker,Storage}/`.

Chaque chantier remplit le fichier d'enregistrement de sa zone, pré-créé par L0.1 (§5.1 du
plan) : `Egress` (L1.2), `Storage` (L1.3), `Persistence` et `Jobs` (L1.4), `Ddragon` (L1.5),
`Ingestion` (L1.6), `Catalog` (L1.7). Aucun ne touche `Program.cs`.

## L1.1 — Domaine Data Dragon pur

- **Objectif** : toutes les règles Data Dragon en code pur, sans I/O ni parsing JSON,
  entièrement testé.
- **À lire** : `app/src/Service/API/Edition/` (règles d'édition), `API/RecipeTreeBuilder.php`,
  `API/Champion/ChampionArt*.php`, `Client/VersionManager.php` (motif de version, filtre
  `lol*`), `I18n/UiLocaleResolver.php`, `Catalog/` (`GameMap`, `GameModeLabels`,
  schémas de facettes), `Tools/DdragonText.php`, `app/src/Stat/GameStat.php`,
  `app/src/Twig/{EditionExtension,GameModesExtension,DdragonExtension}.php`,
  `app/templates/champion/detail.html.twig` (sentinelles de portée, l. 57-61),
  `Picker/ItemOptionsProjector.php` (`isAvailableOn`).
- **Périmètre** : `src/LoDb.Domain/{Versions,Languages,Editions,Catalog,Derived,Text,
  Paths}/**`, `tests/LoDb.Domain.Tests/` (mêmes dossiers).
- **Conception** :
  - `PatchVersion` : parsing selon le motif `\d+(?:\.\d+)+`, comparaison numérique, filtre
    des entrées `lolpatch_*` (UP 4).
  - `UiLocale` (21 valeurs) et correspondance vers la langue Data Dragon par défaut
    (`fr` → `fr_FR`, `pt` → `pt_BR`, `zh-hans` → `zh_CN`, `zh-hant` → `zh_TW`…) ; sens
    inverse : réduction au code de base, `zh_CN`/`zh_MY` → `zh-hans`, `zh_TW` → `zh-hant`,
    repli `en`. `DdragonLanguage` reste une donnée (liste lue dans `languages.json`),
    `en_US` étant le seul repli universel (UP 2).
  - `Edition` (`Modern`, `Classic`) : objet Classic = id `^77\d{4}$` ; sort Classic = mode
    `JADE` ou suffixe `_Jade`. Jamais dérivé des `maps`. Jumeau confirmé par id **et** par
    nom (UP 6).
  - Modèle normalisé immuable (`record`) : `ChampionSummary`, `ChampionDetail` (sorts,
    passif, skins avec repli sur l'index quand `num` manque, stats, `partype` facultatif),
    `Item` (from/into, profondeur, or, stats, maps, tags, achetable, `hideFromAll`,
    `requiredChampion`, édition, jumeau), `RuneTree`/`RuneSlot`/`Rune`, `SummonerSpell`
    (modes, édition, jumeau), `Skin`, `Chroma`.
  - Faits dérivés (UP 10, 11, 13) : 12 clés de stats classiques ; sentinelles de portée
    (`25000+`, `4294967295`, `"self"`) masquées ; mêlée ≤ 325, distance ≥ 350 ; tier par
    `depth`/`into` ; filtre des débris d'objets (noms vides, placeholders, balises, jetons
    non résolus) ; liste blanche des modes et libellés (`CLASSIC` = Faille de
    l'invocateur) ; cartes `GameMap` 11, 12, 21, 30, 33, 35 ; clé d'art `FiddleSticks`
    (UP 7) ; étiquette de couleur d'un chroma dérivée de la teinte (UP 9) ; nettoyage du
    texte riche (`DdragonText`).
  - Les titres ne sont jamais mis en minuscules.
  - `CanonicalPath` : chemin canonique d'une entité, `champions/{id}`, `items/{id}-{slug}`,
    `runes/{id}-{slug}`, `summoners/{id}` ; slug ASCII dérivé du nom `en_US`, identique dans
    toutes les locales (stable, lisible, sans écriture non latine). Seule source de cette
    grammaire : l'API le renvoie, sitemaps, 301 héritées, analytics et front s'en servent.
- **Tests** : une classe par règle ; chaque point UP 1 à 13 porte un test dont le nom cite
  son numéro ; les cas des tests PHPUnit correspondants sont repris ; slugs (accents,
  apostrophes, jumeaux Classic homonymes).
- **Acceptation** : tests verts ; le compte rendu contient la table UP → tests.
- **Dépend de** : lot 0. **Taille** : L.

## L1.2 — Egress filtré

- **Objectif** : un client HTTP nommé `ddragon` qui porte toutes les garanties de
  go-fetcher, en processus, en streaming.
- **À lire** : `go/fetcher/internal/{api,fetcher,config}/*.go` et leurs tests,
  `app/src/Service/Tools/GoFetcherClient.php`, ADR [0002](../adr/0002-hote-serveur-unique.md)
  (egress), `heritage.md` A5, G1, UP 12.
- **Périmètre** : `src/LoDb.Ingestion/Egress/**`, `tests/LoDb.Ingestion.Tests/Egress/**`.
- **Conception** :
  - `SocketsHttpHandler` : `AllowAutoRedirect = false`, `MaxConnectionsPerServer` = la
    concurrence de fetch (16 par défaut), requêtes en **HTTP/1.1**.
  - `AllowListHandler` : https uniquement, hôtes de `LoDb:Egress:AllowedHosts`
    (`ddragon.leagueoflegends.com`, `raw.communitydragon.org`) ; démarrage refusé si la
    liste est vide.
  - `RedirectHandler` : suit jusqu'à 10 redirections à la main, chaque saut repasse par
    l'allow-list.
  - Résilience (`Microsoft.Extensions.Http.Resilience`) : timeout 15 s par tentative,
    retry exponentiel sur 5xx, 408 et timeout, circuit breaker.
  - Lecture plafonnée (32 Mio) : dépassement = erreur explicite, jamais de troncature.
  - `FetchOutcome` : `Present(contenu, type)` | `Absent(403 ou 404)` ; transitoire =
    exception typée. Corps lus avec `ResponseHeadersRead`.
  - Refus d'allow-list et de redirection : une ligne de log par lot (`fetch.allowlist.refused`,
    `fetch.redirect.refused`), jamais une par URL.
- **Tests** (gestionnaire primaire factice) : hôte permis ou refusé, http refusé,
  redirection vers un hôte interdit refusée, chaîne de redirections permise, 403/404 →
  `Absent`, 5xx → nouvelles tentatives puis transitoire, timeout, dépassement de taille,
  version HTTP 1.1.
- **Dépend de** : lot 0. **Taille** : M.

## L1.3 — Stockage des contenus

- **Objectif** : blobs adressés par contenu et datasets immuables, écrits de façon atomique.
- **À lire** : `app/src/Service/Storage/{BlobStore,AtomicWriteAdapter,WebpSibling}.php`,
  [`architecture.md`](../../architecture/architecture.md) (disposition du stockage), ADR
  [0004](../adr/0004-stockage-etat-et-scaling.md), `heritage.md` B3 à B5.
- **Périmètre** : `src/LoDb.Infrastructure/Storage/**`, tests associés.
- **Conception** :
  - `IBlobStore` : dépôt par contenu (SHA-256) sous `blobs/{sha}.{ext}`, voisin
    `blobs/{sha}.webp` ; déjà présent = rien à écrire ; sinon fichier temporaire dans
    `.staging/` (même volume) puis `File.Move` sans écrasement ; une course perdue contre un
    autre écrivain du même contenu est un succès. Chemin public `/cdn/blobs/{sha}.{ext}`.
  - `IDatasetStore` : `data/{version}/{lang}/{type}.json`, écrit une seule fois, même
    mécanique atomique ; chemin relatif validé (pas de `..`, pas d'absolu). Les types de
    dataset sont définis par L1.5.
  - Racine `LoDb:Storage:Root` (`/srv/storage` en conteneur). Seuls `blobs/` sont servis
    par nginx.
- **Tests** : déduplication, aucune écriture partielle visible, écritures concurrentes du
  même contenu, validation des chemins, voisin WebP.
- **Dépend de** : lot 0. **Taille** : M.

## L1.4 — Persistance de base

- **Objectif** : le `LoDbDbContext` de toute la réécriture, la migration `Baseline` qui
  reproduit exactement le schéma Doctrine, les tables du lot 1, les verrous consultatifs,
  la base des tâches périodiques, et de quoi migrer une base réelle dès maintenant.
- **À lire** : `app/migrations/*.php` (11 migrations), `app/src/Entity/**`,
  `app/src/Repository/**` (requêtes et index), `go/api/schema.sql`,
  [`architecture.md`](../../architecture/architecture.md), ADR 0004 et
  [0009](../adr/0009-identite-authentification-sessions.md), `plan-migration.md`
  (expand/contract, horodatages).
- **Périmètre** : `src/LoDb.Infrastructure/Persistence/**` (contexte, entités de persistance
  par domaine, configurations, migrations), `src/LoDb.Infrastructure/{Locks,Jobs}/**`,
  `src/LoDb.Api/Cli/{Migrate,Baseline}*`, service `migrate` de `compose.next.yaml`,
  `tests/LoDb.Infrastructure.Tests/Persistence/**`, extensions de `tests/LoDb.Testing/`,
  `tools/next/{schema,db}/**`, `tests/fixtures/schema/doctrine-schema.sql`,
  `docs/reecriture/rapports/schema-baseline.md`.
- **Conception** :
  - Entités de persistance pour les tables existantes : `users`, `builds`, `build_votes`,
    `donations`, `api_keys`, `api_usage`, `contact_messages`. Les tables que la nouvelle
    stack n'utilise pas (`reset_password_request`, `messenger_messages`) et les index
    fonctionnels (`LOWER(email)`, `LOWER(username)`) sont créés dans `Baseline` par SQL
    brut. `Baseline` crée aussi `doctrine_migration_versions` et y inscrit les 11 versions
    Doctrine : une base neuve reste utilisable par l'ancienne stack, qui ne rejoue rien.
  - Nommage snake_case, noms Doctrine repris explicitement. Le convertisseur UTC ne
    s'applique qu'à une **liste explicite** de colonnes `timestamp(0) without time zone` ;
    celles qui sont déjà en `timestamptz` (`users.banned_at`, `api_keys.created_at`,
    `api_keys.revoked_at`…) n'en ont pas besoin. Tout nouveau champ est en `timestamptz`.
  - Tables du lot 1 : `ddragon_asset` (version, type, clé → sha, extension, statut
    `present`/`absent`, date ; clé primaire version + type + clé) ; `ddragon_version`
    (version, statut `discovered`/`ingesting`/`ready`/`failed`, nombre de tentatives,
    prochaine tentative, horodatages, promotion). Upsert concurrent sûr
    (`INSERT … ON CONFLICT`) ; une absence n'est jamais remplacée que par une ingestion
    forcée.
  - `IDistributedLock` : verrou consultatif Postgres (`pg_try_advisory_lock`) sur une
    connexion dédiée tenue pendant le verrou, clé stable dérivée du nom de la tâche.
  - `Jobs/` : base des tâches périodiques (`PeriodicTimer` + verrou + métriques de durée,
    d'erreurs et de dernier succès + ligne de synthèse), réutilisée par les lots 1, 4, 6
    et 7.
  - Sous-commandes `baseline mark-applied` (inscrit `Baseline` dans l'historique EF si la
    base porte exactement le schéma Doctrine attendu ; idempotente ; refuse un schéma
    inattendu) et `migrate` (marquage si besoin, puis migrations en attente). Service
    Compose éphémère `migrate` (image `lodb-api`), dont `api` attend la réussite.
  - Vérification du schéma : `tools/next/schema/` applique les migrations Doctrine sur une
    base vide via le conteneur `php` de l'ancienne stack (`-u www-data`), compare le
    `pg_dump --schema-only` normalisé à celui de `Baseline`, et fige le résultat Doctrine
    dans `tests/fixtures/schema/doctrine-schema.sql` pour que la CI compare sans l'ancienne
    stack.
  - Anonymisation (`tools/next/db/`) d'un dump pour `next` et pour les essais locaux :
    e-mails, `google_id`, identifiants Stripe, hash de clés d'API, IP et contenus de contact
    remplacés, hash de mot de passe remplacés par un hash connu, structure et volumes
    conservés. Le lot 4 et le lot 6 le complètent si leurs tables l'exigent.
- **Tests** (Testcontainers) : `Baseline` appliquée sur une base vide = schéma Doctrine
  (écarts listés et justifiés : historique EF, tables nouvelles) ; `migrate` sur une base
  créée par Doctrine (marquage puis migrations), deuxième passage sans effet, schéma
  inattendu refusé ; aller-retour de chaque entité ; conversion UTC limitée à sa liste ;
  upserts concurrents sur la même clé ; exclusivité du verrou entre deux connexions ; une
  tâche périodique ne s'exécute qu'une fois à deux instances ; anonymisation sans donnée
  personnelle résiduelle.
- **Acceptation** : rapport `schema-baseline.md` sans écart inexpliqué ; tests verts ;
  `docker compose … up` applique les migrations avant de démarrer l'API.
- **Dépend de** : lot 0 ; ancienne stack démarrée pour la comparaison. **Taille** : L.

## L1.5 — Clients Data Dragon et CommunityDragon, normalisation, fixtures

- **Objectif** : lire toutes les sources, appliquer replis et absences, produire les
  datasets normalisés ; enregistrer les fixtures qui rendent les tests indépendants du
  réseau.
- **À lire** : `app/src/Service/API/{AbstractManager,ChampionManager,ItemManager,RuneManager,
  SummonerManager}.php` et `API/Concern/*`, `Client/VersionManager.php`, `heritage.md` UP 1
  à 5, 7 et 9.
- **Périmètre** : `src/LoDb.Ingestion/{Ddragon,Normalization}/**`, tests associés,
  `tests/fixtures/ddragon/**`, `tests/LoDb.Testing/Fixtures/**` (rejeu),
  `tools/next/fixtures/**` (enregistreur).
- **Conception** :
  - `versions.json` (sans `lolpatch_*`), `languages.json`, datasets par (version, langue) :
    champions (préférer `championFull.json`, une requête par langue ; repli sur les fichiers
    de détail, puis sur le résumé si le détail est en 403 — UP 3), objets, runes
    (`runesReforged.json` en 403 avant 7.22.1 → dataset vide persisté — UP 1), sorts
    d'invocateur.
  - Chromas CommunityDragon : chemins en minuscules, repli sur `latest`, chromas des skins
    Data Dragon filtrés (UP 9).
  - Langue absente d'une version → repli `en_US` ; `en_US` absent → dataset vide persisté.
    Une erreur transitoire ne persiste **rien**, ni vide ni absence.
  - Sortie : JSON normalisé stable (clés ordonnées, contexte `System.Text.Json` généré),
    écrit par le pipeline (L1.6) dans l'`IDatasetStore`.
  - Fixtures : un enregistreur rejoue une liste d'URL choisies (dernière version et la
    précédente, 0.151.2, ~3.13.24, 7.21.1, 7.22.1, 8.7.1 ; `en_US`, `fr_FR`, `ko_KR`,
    `ar_AE`, `zh_CN` quand elles existent). **10 Mo au plus** au total : fichiers réduits
    quand le fichier complet n'apporte rien. Le rejeu échoue sur toute URL non enregistrée.
- **Tests** : chaque type de dataset sur chaque version piège (`partype` absent en 0.x,
  `skin.num` absent, runes en 403, icônes `.dds` en 403), repli de langue, filtre
  `lolpatch_*`, art de Fiddlesticks, chromas, débris d'objets filtrés, jumeaux Classic et
  sorts `JADE`.
- **Dépend de** : L1.1, L1.2 ; accès réseau pour l'enregistrement. **Taille** : L.

## L1.6 — Pipeline d'ingestion et tâches de fond

- **Objectif** : l'ingestion proactive d'un patch à sa sortie et la longue traîne à la
  demande sans jamais bloquer ni saturer, sur la base des tâches périodiques de L1.4.
- **À lire** : `app/src/Command/{WarmupDdragonCommand,GenerateWebpVariantsCommand}.php`,
  `app/src/Service/API/Concern/{ResolvesImages,IngestsImages}.php`,
  `Storage/{DeferredImageIngestor,ImageTranscoder}.php`, `API/Image/ImageStatusResolver.php`,
  `app/src/Controller/Resource/LoaderController.php` (ce que le loader réchauffe), ADR
  [0003](../adr/0003-ingestion-proactive-et-taches-de-fond.md), `heritage.md` A1, A3, A9,
  A10, B1, C5.
- **Périmètre** : `src/LoDb.Ingestion/{Pipeline,Queue,Images}/**`,
  `src/LoDb.Api/Workers/Ingestion/**`, `src/LoDb.Api/Cli/Ingest*`, tests associés.
- **Conception** :
  - **Veille de patch** (10 min, verrou) : nouvelle version de `versions.json` →
    `ddragon_version` en `discovered` + ingestion en file.
  - **Ingestion d'une version** : datasets de toutes les langues disponibles, puis images des
    quatre ressources (portraits, passifs, sorts, objets, runes, sorts d'invocateur), en
    parallèle borné (`Parallel.ForEachAsync`), blob + voisin **WebP produit par SkiaSharp**
    (qualité 82, comme aujourd'hui ; paquet `SkiaSharp.NativeAssets.Linux.NoDependencies`
    pour l'image *chiseled*, sans fontconfig), upsert `ddragon_asset` (`present` ou
    `absent`). Les
    icônes de runes ne sont pas versionnées. Splash, skins et vidéos ne sont **pas**
    ingérés (hotlink assumé, UP 8).
  - **Promotion** : la version passe `ready`, puis devient la dernière pour le site
    seulement si elle est plus récente et entièrement ingérée.
  - Idempotente et reprenable : une relance saute ce qui existe. Une erreur transitoire
    laisse la version à reprendre au tick suivant, avec un nombre de tentatives borné.
  - **Longue traîne** : `IOnDemandIngestion` offre un mode synchrone (détail, picker, build,
    recherche) et un mode en file (listes, aperçus). File `Channel<T>` bornée, requêtes en
    double fusionnées, débit limité pour le travail déclenché par les crawlers (C5).
  - Pas de flux SSE en v1 : une liste de version froide renvoie ses placeholders et un
    en-tête `Retry-After` ; le front relance **une** fois, sans boucle de polling.
  - Métriques : profondeur de file, durées d'ingestion par type, blobs écrits, absences
    persistées, versions prêtes. Une ligne de synthèse par lot.
  - Sous-commande `ingest` : `--version X` ou `--latest N`, `--languages all|liste`,
    `--force`.
- **Tests** (fixtures rejouées, Testcontainers, stockage temporaire) : ingestion complète
  d'une version réduite, idempotence, promotion seulement après la fin, 403 → absence
  persistée, transitoire → rien de persisté puis reprise, plafond de tentatives, une seule
  exécution à deux instances, file bornée et dédoublonnée, WebP produit. Le transcodage est
  aussi vérifié **dans l'image `lodb-api`** (sous-commande `ingest` sur une version réduite).
- **Dépend de** : L1.3, L1.4, L1.5. **Taille** : L.

## L1.7 — Catalogue en mémoire et résolution d'images

- **Objectif** : servir un catalogue immuable par (version, langue) et résoudre chaque image
  en blob, placeholder ou ingestion, sans jamais mettre en cache un échec transitoire.
- **À lire** : `app/src/Service/API/Concern/{ResolvesImages,ResolvesEntries,
  ResolvesEditionCounterpart,PaginatesResources}.php`, `API/Champion/ChampionArt*.php`,
  `heritage.md` § 4 (liste de versions vide mise en cache) et A7.
- **Périmètre** : `src/LoDb.Ingestion/Catalog/**`, `src/LoDb.Api/Cli/Catalog*`, tests
  associés.
- **Conception** :
  - `ICatalogReader` : catalogue immuable (index par id, recherche normalisée sans accents,
    jumeaux reliés) chargé depuis l'`IDatasetStore`, datasets d'une version froide obtenus
    par ingestion synchrone ; LRU plafonné (`LoDb:Catalog:MaxEntries`) ; `HybridCache` pour
    la dernière version et la liste des versions.
  - `IImageResolver` : `present` → `/cdn/blobs/{sha}.{ext}` (+ WebP) ; `absent` →
    placeholder sans nouvelle tentative ; inconnu → ingestion synchrone ou en file selon le
    contexte d'appel.
  - URLs hotlink (splash, skins, vidéos de sorts sur le CDN CloudFront actuel) construites
    ici, avec la règle `FiddleSticks`.
  - Sous-commande `catalog export --version --lang` : projection canonique utilisée par la
    parité (L1.8).
- **Tests** : LRU, succès de cache, placeholder / synchrone / en file, échec transitoire
  jamais mis en cache, URLs hotlink.
- **Dépend de** : L1.6. **Taille** : M.

## L1.8 — Parité PHP ↔ .NET

- **Objectif** : prouver que la nouvelle ingestion produit les mêmes données et le même
  manifeste que l'ancienne, ou justifier chaque écart.
- **À lire** : `app/src/Service/API/**` (managers et règles de l'ancienne stack),
  `app/src/Service/Picker/**`, `app/src/Command/WarmupDdragonCommand.php`.
- **Périmètre** : `tests/LoDb.Parity/**`, `tools/next/parity/**`,
  `docs/reecriture/rapports/parite-lot-1.md`.
- **Conception** :
  - Échantillon : les 10 dernières versions × `en_US`, `fr_FR`, `ko_KR`, `ar_AE`, `zh_CN`
    (une langue absente d'une version est notée N/A), plus 0.151.2, ~3.13.24, 7.21.1,
    7.22.1 et 8.7.1.
  - Ancienne stack (ports 8080…) réchauffée sur l'échantillon (`app:ddragon:warmup
    --only=… --langs=…`), nouvelle ingestion par `ingest`.
  - Comparaison 1, **manifestes** : `manifest/{v}/{type}.json` du volume `lodb_storage`
    (lu en lecture seule par un conteneur jetable) contre `ddragon_asset` (même SHA-256
    pour un même contenu, même statut d'absence).
  - Comparaison 2, **projection canonique** : un script PHP en lecture seule
    (`tools/next/parity/php/`, copié dans le conteneur `php` de l'ancienne stack par
    `docker compose cp` et lancé avec `-u www-data`) exporte, par les managers de
    l'ancienne stack, la même projection que `catalog export` : ids, noms, édition et
    jumeau (objets Classic compris, que `/api/picker/*` exclut), image présente ou
    placeholder, faits dérivés. Aucun fichier de `app/` n'est modifié.
  - Chaque écart est classé : défaut de la nouvelle stack (corrigé), défaut de l'ancienne
    corrigé volontairement (justifié), ou attendu (justifié).
- **Tests** : l'outil de comparaison a ses propres tests sur des paires construites à la main.
- **Acceptation** : rapport sans écart non classé.
- **Dépend de** : L1.6, L1.7 ; ancienne stack démarrée ; accès réseau. **Taille** : L.
