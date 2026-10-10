# ADR 0003 — Ingestion proactive et tâches de fond

- **Statut** : acceptée — 2026-09-24
- **Remplace** : ingestion à la demande, `DeferredImageIngestor` (`kernel.terminate`),
  loader SSE, placeholders + polling `/api/images/{type}`, warm-up au déploiement
- **Suivi** : le 2026-09-28, l'interface d'attente du point 4 est reprise pour un
  changement de patch ou de langue. Le loader de l'en-tête lit
  `GET /api/catalog/{version}/{lang}/warm-up`, un flux SSE asynchrone dont chaque trame
  est l'état complet, répétée toutes les 5 s pendant l'ingestion des datasets.
- **Amendement** : le 2026-10-10, les points 4 et 5 sont précisés : le coût de la longue
  traîne est borné à chaque étage de l'API (voir § « Amendement du 2026-10-10 »).

## Contexte

Aujourd'hui, une image Data Dragon n'est récupérée **que lorsqu'un visiteur la
demande**. Tout un appareillage existe pour masquer ce coût :

- le loader SSE « gate-then-visit », soit environ 1 200 lignes côté front, avec un
  watchdog d'inactivité ;
- l'ingestion différée après la réponse (`kernel.terminate`), qui garde un worker FPM
  occupé plusieurs secondes ;
- un état `pending` à trois valeurs, un endpoint de polling (48 noms par appel, backoff
  de 1,5 s à 24 s), et un `retry=1` ;
- le transcodage WebP dans la requête, image par image.

PHP n'a **ni worker ni planificateur**, d'où plusieurs conséquences :

- les e-mails partent de façon synchrone dans la requête ;
- les rollups analytics et audit ne tournent qu'au déploiement, donc `/v1/trends` n'est
  frais qu'au dernier déploiement ;
- la purge de rétention CNIL n'est **jamais planifiée** ;
- rien ne détecte un nouveau patch : c'est le premier visiteur qui paie l'ingestion.

## Décision

### Tâches de fond

Des `BackgroundService` dans `LoDb.Api`, alimentés par des `Channel<T>` bornés. Chaque
tâche périodique repose sur un `PeriodicTimer` et prend un **verrou consultatif
Postgres** (`pg_try_advisory_lock`) : elle ne s'exécute qu'une fois, même à N instances.
Pas de Quartz.NET tant qu'aucun besoin de planification cron complexe n'apparaît.

| Tâche | Fréquence | Remplace |
|---|---|---|
| Veille de patch (`versions.json`) | 10 min | rien (le premier visiteur payait) |
| Ingestion d'une version | à la détection, file bornée | ingestion à la demande + warm-up au déploiement |
| Transcodage WebP | dans le pipeline d'ingestion, en parallèle | GD image par image, dans la requête |
| Rollups analytics et audit | quotidien, + jour courant en continu | rollup au déploiement seulement |
| Rétention (audit 6 mois, événements bruts à IP/UA) | quotidien | jamais planifiée |
| Expiration des crédits API (12 mois) | quotidien | contractuelle, jamais implémentée |
| Effacement des comptes non vérifiés après 7 jours | horaire | jamais planifié |
| Outbox e-mail, plafond horaire par modèle (l'excédent attend) | continu | envoi synchrone dans la requête |

### Ingestion proactive

1. La veille détecte une nouvelle version dans `versions.json` (en filtrant
   `lolpatch_*`).
2. Elle ingère les **datasets JSON de toutes les langues** puis les **images des quatre
   ressources**, en parallèle borné (`Parallel.ForEachAsync`). Le WebP est produit en
   même temps.
3. **Seulement ensuite**, la version devient « latest » pour le site et les apps. Un
   visiteur ne voit donc jamais une version froide.
4. La **longue traîne des versions anciennes** (397 versions × 28 langues) reste
   ingérée à la demande. Pour ces versions :
   - la page de détail résout ses images de façon synchrone, comme aujourd'hui
     (contrat « de vraies icônes sur une version froide ») ;
   - les listes et aperçus mettent l'ingestion en file et renvoient les placeholders ;
   - l'avancement est poussé au client par SSE, en async, sans thread bloqué, si une
     interface d'attente reste utile.
5. Les crawlers qui parcourent les sitemaps de versions anciennes passent par une
   **file bornée et limitée en débit** : ils ne peuvent plus saturer le serveur.

Traitement d'image : **SkiaSharp** (MIT). ImageSharp est écarté à cause de sa licence
Six Labors Split (commerciale au-delà d'un seuil).

### Règles d'ingestion portées telles quelles

- Absence définitive (403/404) : **persistée**, dans le manifeste
  ([ADR 0004](0004-stockage-etat-et-scaling.md)) ou en dataset vide (`runesReforged`
  avant 7.22.1). Erreur transitoire : **jamais persistée**, ni vide ni absente.
- Dataset manquant dans une langue : on se replie sur `en_US`, seule langue présente
  dans toutes les versions.
- Un seul log de synthèse par lot, jamais une ligne par URL.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Garder l'ingestion à la demande, sans le détour FPM | Garde tout l'appareillage de loader, de placeholders et de polling, et les crawlers continuent de déclencher des ingestions à froid |
| Tout pré-ingérer (397 versions × 28 langues) | Coût disque et réseau disproportionné pour des versions que presque personne ne consulte |
| Quartz.NET / Hangfire | Surdimensionné pour 7 tâches à fréquence fixe ; le verrou consultatif Postgres suffit |
| Worker dans un conteneur séparé | Pertinent seulement si l'ingestion concurrence le trafic web ; le code s'y prête (même solution), à décider sur métriques |

## Conséquences

- **+** Suppression du loader SSE, de `DeferredImageIngestor`, du polling, du `retry=1`
  et du warm-up au déploiement : moins de code, et des pages chaudes dès l'exposition
  d'un patch.
- **+** Rétention CNIL, fraîcheur de `/v1/trends` et expiration des crédits deviennent
  enfin automatiques.
- **+** Chaque tâche est observable : durée, erreurs et taille de file exposées en
  métriques ([ADR 0010](0010-observabilite-tests-ci.md)).
- **−** Un patch n'apparaît qu'une fois ingéré, quelques minutes après sa publication
  par Riot. C'est assumé : mieux vaut un patch complet qu'un patch immédiat mais froid.

## Amendement du 2026-10-10 — coût borné de la longue traîne

Les points 4 et 5 ne bornaient que la file des crawlers. Une lecture synchrone (page, appel
de `/api/catalog`, sélecteurs, warm-up) d'une version froide n'avait ni budget ni délai, et
un catalogue (version, langue) absent de la mémoire se rechargeait à chaque lecture. Le
coût d'une version froide est désormais borné par instance, quel que soit le nombre de
clients. Toutes les valeurs ci-dessous sont des défauts, réglables par
`LoDb__<Section>__<Clé>` (transmis par `compose.deploy.yaml`).

### Point 5 : le budget s'applique aussi aux visiteurs

- Une version prête (ingérée en entier) ne coûte rien. Sinon, l'ingestion synchrone d'un
  visiteur prend un budget par instance : `LoDb:Ingestion:ColdDatasetsPerMinute`
  (**6** couples version × langue par minute) et `ColdImagesPerMinute` (**1 500** images
  par minute). Rejoindre un travail déjà en vol est gratuit. Hors budget : le catalogue est
  « en attente » (`503` + `Retry-After`), les images restent des placeholders.
- Une ingestion synchrone attend un emplacement (`SyncConcurrency`) **3 s** au plus
  (`SlotWait`, sous le délai d'appel du SSR, sinon le travail lancé serait perdu), derrière
  **32** autres au plus (`MaxWaitingIngestions`) ; au-delà, même réponse « en attente ».
  Le travail de la file de fond a été admis à sa mise en file : ni budget ni délai.
- Les sitemaps et `llms.txt` (origine crawler) gardent la file bornée et leur budget de
  **30** requêtes par minute (`CrawlerRequestsPerMinute`). Toute autre requête HTTP reste
  celle d'un visiteur.

### Catalogues en mémoire

- **Étage A** : les catalogues du dernier patch dans les langues
  `LoDb:Catalog:PinnedLanguages` (défaut : les 21 langues de l'interface, en_US en tête),
  **jamais évincés**, dans la limite de `PinnedBudgetMegabytes` (**256** Mio, estimés
  d'après les datasets stockés). Mesure sur 16.20.1 : ≈ 7,5 Mio pour en_US, 6,7 à 7,8 Mio
  pour chaque autre langue (latines, cyrillique, thaï, arabe, CJK), dont les chaînes
  anglaises restent partagées avec en_US ; ≈ 160 Mio pour les 21 langues. Chargement d'un
  catalogue : 20 à 30 ms au p50, 30 à 60 ms au p95, ≈ 10 Mio alloués.
- **Étage B** : les autres catalogues, LRU de `MaxEntries` (**16**) places. Les deux
  étages restent sous ≈ 400 Mio des 1 152 Mio de l'API.
- **Porte froide**, sur un vrai miss seulement (un lecteur qui rejoint un chargement en
  vol ne consomme rien) : **2** chargements simultanés (`ColdLoadConcurrency`), **3** par
  seconde en rafale de **6** (`ColdLoadsPerSecond`, `ColdLoadBurst`). Le rendu serveur,
  qui appelle l'API sans passer par nginx (adresse privée, sans `X-Forwarded-For`), a les
  deux tiers du débit et peut attendre **2 s** son tour (`InternalColdLoadWait`), derrière
  **16** autres au plus (`MaxPendingLoads`). Tout autre appelant a **1** chargement par
  seconde (`PublicColdLoadsPerSecond`), jamais le dernier emplacement, et n'attend jamais.
  Un refus est une réponse « en attente » (`503` + `Retry-After`), distincte d'un dataset
  absent.

### Point 4 : warm-up, sélecteurs et ancien site

- `GET /api/catalog/{version}/{lang}/warm-up` et `/api/pickers` passent par la porte
  froide et le budget. Le warm-up est en plus limité à **20** démarrages par heure et par
  client (adresse, ou /64 en IPv6, politique `cold-work`), à **8** flux ouverts, **2** par
  client, de **120 s** chacun (`LoDb:Catalog:WarmUp`). Un refus ou une expiration clôt le
  flux par une trame `failed` de code `catalog-pending` : la visite continue. Jamais de
  limite par adresse sur `/api/pickers`, lu par des membres qui peuvent partager une
  adresse ([ADR 0009](0009-identite-authentification-sessions.md)).
- Une URL de l'ancien site **n'ingère jamais** : son nom se cherche dans le catalogue du
  dernier patch (les ids sont stables), puis dans celui de la version visée si elle est
  déjà stockée ; sinon, `302` vers la liste de cette version, pour qu'une entrée retirée
  du jeu ne devienne pas une 404.

### Exception aux tâches périodiques

`LatestCatalogWarmer` (`Workers/Catalog/`) épingle l'étage A au démarrage, puis à chaque
changement de dernière version (vérifié toutes les `VersionsLifetime`). Il ne prend **pas**
de verrou consultatif : il remplit la mémoire de l'instance qui l'exécute, que chaque
instance doit remplir pour elle-même.

### Mesures

Meter `LoDb.Catalog` : `lodb_catalog_cache_lookups_total{outcome}`,
`lodb_catalog_cache_evictions_total`, `lodb_catalog_load_duration_seconds{outcome}`,
`lodb_catalog_loads_in_flight`, `lodb_catalog_cold_admissions_total{partition,result}` ;
et `lodb_ingestion_sync_waiting` (meter `LoDb.Ingestion`). Les refus d'ingestion restent
comptés par `lodb_ingestion_on_demand_refused_total{reason}`. Aucune version, langue ni
adresse en label.
