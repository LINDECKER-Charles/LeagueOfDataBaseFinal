# Héritage — ce que la réécriture corrige, ce qu'elle garde

Inventaire établi le 2026-09-24 à partir :

- de `CLAUDE.md` et de toute la documentation (`docs/`) ;
- des 93 entrées du changelog technique, archivées comprises ;
- du code de stockage, d'ingestion et du loader ;
- de la configuration Docker, nginx et Compose ;
- des deux services Go et des workflows de CI.

Les identifiants (A1, B2…) servent de référence stable dans les ADR et le plan.

| Étiquette | Sens |
|---|---|
| **FPM** | dû au modèle PHP-FPM (un processus par requête, rien de partagé) : disparaît avec un hôte .NET longue durée |
| **ARCH** | choix d'architecture indépendant du langage : la réécriture le tranche autrement |
| **FRONT** | propre à Twig + Turbo + îlots Vue : le besoin reste, le piège disparaît |
| **UP** | particularité Data Dragon / CommunityDragon : à conserver quelle que soit la stack |

## 1. Contournements imposés par PHP-FPM

| ID | Contournement actuel | Preuve | Réponse | ADR |
|---|---|---|---|---|
| A1 | Ingestion d'images après la réponse (`kernel.terminate`) : worker occupé, échecs longtemps invisibles, trois bugs « images cassées sur version froide » | `DeferredImageIngestor.php`, `ResolvesImages.php:175` | `BackgroundService` + file bornée, jamais liée à une requête | 0003 |
| A2 | Analytics capturés après la réponse, ajout NDJSON sous `LOCK_EX` | `RecordRequestListener.php`, `NdjsonDayStore.php:149` | middleware → `Channel<T>` → écriture groupée en Postgres | 0003, 0004 |
| A3 | Flux SSE qui immobilise un worker FPM : buffering nginx désactivé, `flush()` par trame, verrou de session relâché à la main | `LoaderController.php`, `docker/nginx/default.conf:113` | ingestion proactive ; SSE async (sans thread bloqué) pour la seule longue traîne | 0003 |
| A4 | Verrou de session en fichiers : 6 requêtes admin passaient de 2,35 s à 5,18 s | `AdminPanelController.php:34` | aucune session serveur | 0009 |
| A5 | Pas de HTTP parallèle : passerelle Go, base64 +33 %, lot bloquant, 3 plafonds à synchroniser | `GoFetcherClient.php:25`, `go/fetcher/…/handlers.go` | `HttpClient` en processus, streaming, allow-list en `DelegatingHandler` | 0002 |
| A6 | État longue durée de l'API publique déporté dans `go-api` ; révocation différée de 60 s | `go/api/internal/*` | module `/v1` dans le même hôte, cache invalidé à la révocation | 0002 |
| A7 | Aucune mémoire partagée : cache fichier vidé au déploiement, mémos par requête dans chaque manager | `cache.yaml:25`, `AbstractManager.php:53` | catalogue immuable en mémoire + `HybridCache` | 0004 |
| A8 | Pool FPM dimensionné à l'aveugle (20 × 256 Mo pour une limite de 1 Gio), aucune métrique de pool | `php-fpm.conf:44` | Kestrel async, métriques runtime OpenTelemetry | 0010 |
| A9 | Transcodage WebP dans la requête, image par image | `BlobStore.php:53` | SkiaSharp dans le pipeline d'ingestion, en parallèle | 0003 |
| E1 | Runtime PHP qui avalait ou découpait les logs (`display_errors`, `log_limit`, access log FPM, faux `ERROR` du slowlog) | `php.ini`, `php-fpm.conf` | formateur console JSON natif | 0010 |
| E2 | Pièges Monolog : `fingers_crossed`, placeholders réécrits, canal non hérité, 4xx loggés en ERROR | `logging.md` | convention portée sur `ILogger` | 0010 |
| E5 | Sonde de santé : `cgi-fcgi` parce que FPM ne parle pas HTTP | `compose.yaml` | `/healthz`, `/readyz` en HTTP | 0010 |
| F4 | root vs `www-data` en dev : dossiers root sous `var/`, 500 collée en fin de page | `CLAUDE.md` Pièges | plus de FPM ; conteneurs non root | 0001 |
| F5 | Boucle de dev lente (re-stat d'opcache, profiler de 3 à 5 s) | `docker.md` | hot reload .NET + serveur de dev Angular | 0001 |
| G6 | CSRF sans état : les POST de test exigent un `Origin` | `CLAUDE.md` Pièges | antiforgery ASP.NET + contrôle d'`Origin` | 0009 |
| I3 | Traducteur lié à la requête : e-mails envoyés de façon synchrone pour garder la locale | `messenger.yaml:22` | outbox, locale passée explicitement | 0003, 0009 |
| J3 | Outillage : collision `Api`/`API` sous Windows, classe non `final` pour PHPUnit, `strict_variables` qui diffère entre dev et prod | changelog, `architecture-report.md` | disparaît avec la stack | — |

## 2. Choix d'architecture tranchés autrement

| ID | Constat | Réponse | ADR |
|---|---|---|---|
| A10 | Ni worker ni planificateur : rollups au déploiement seulement, **purge CNIL jamais planifiée**, `/v1/trends` vide après une semaine sans déploiement | tâches périodiques avec verrou consultatif Postgres | 0003 |
| B1 | Ingestion à la demande, racine du loader (~1 200 lignes), des placeholders, du polling et du `retry=1` | ingestion proactive à la détection du patch | 0003 |
| B2 | Manifeste JSON en read-merge-write, **toujours sans verrou**, invalidation limitée au conteneur | table `ddragon_asset` (upsert) | 0004 |
| B3 | Écritures atomiques reconstruites à la main après l'abandon de MinIO | `IBlobStore` : fichier temporaire + rename, même volume | 0004 |
| B4 | Seul PHP écrit dans le stockage ; montages et propriétaires piégeux | un seul écrivain (`LoDb.Api`), S3 possible à N hôtes | 0004 |
| B5 | Blobs adressés par contenu : ça marche | **conservé** | 0004 |
| C1 | Locale portée par session et cookie : pas de cache partagé, pas de hreflang, service worker bloqué | locale en préfixe d'URL | 0005 |
| C2 | Série de bugs du commutateur version/langue | résolution unique au routeur, commutateur = fonction pure | 0005 |
| C3 | Listes rendues entières (~700 objets), îlots invisibles pour les crawlers | SSR paginé + hydratation incrémentale | 0005 |
| C4 | Hygiène d'hôte SEO (`www`, `.fr`, `noindex` du staging) dans nginx | **conservé à l'edge** | 0005 |
| C5 | Les crawlers de sitemaps de versions anciennes déclenchent des ingestions à froid | file bornée et limitée en débit | 0003 |
| D1 | État perdu à chaque déploiement avant l'ajout du volume `var/state` | état durable en Postgres, sinon volume dédié | 0004 |
| D2 | Analytics et audit en NDJSON, sans index, rollup `--include-today` destructeur | tables partitionnées + agrégats | 0004 |
| D3 | Contraintes de confidentialité (IP et UA jamais hors du stockage brut, miroir d'audit filtré, rétentions) | **conservées et testées** | 0004, 0010 |
| E3 | Le collecteur devine le niveau par regex : alerter sur les clés d'événement, jamais sur le niveau | règle conservée ; parsing JSON côté infra à proposer à `infra-vps` | 0010 |
| E4 | Pas de corrélation, pas de métriques applicatives, pas de révision de déploiement | OpenTelemetry, `trace_id`, `build_info` | 0010 |
| F1 | Edge Caddy partagé (dépôt `infra-vps`), réparation TLS au déploiement | **conservé** | — |
| F2 | Staging et prod sur le même hôte, distingués par `.env` | **conservé** (noms de projet Compose, pas de port hôte fixe) | — |
| F3 | Migrations lancées après le `up -d` : du code neuf servi sur l'ancien schéma pendant un instant | migrations expand/contract **avant** la bascule du trafic | 0010 |
| G1 | Durcissement SSRF (chaque redirection re-vérifiée, plafond de 32 Mio, refus signalé hors bande) | **porté** dans le `DelegatingHandler` | 0002 |
| G2 | En-têtes de sécurité sur deux couches ; `add_header` de nginx à ré-inclure par `location` | CSP générée par l'app (nonce SSR), en-têtes constants à l'edge | 0005 |
| G3 | Admin unique, sans rôle ni MFA | rôle Identity + TOTP obligatoire | 0009 |
| G4 | Stripe livre au moins une fois : **les packs de crédits ne sont pas dédupliqués** ; l'expiration à 12 mois n'est pas implémentée | table `stripe_event` (idempotence) + tâche d'expiration | 0003, 0004 |
| G5 | Rate limiters dans un pool fichier remis à zéro au déploiement | lockout Identity en base + limiteurs natifs | 0009 |
| I1 | Seules 2 locales sur 21 sont complètes | rapport de complétude en CI | 0006 |
| I2 | Deux axes de langue (Data Dragon : 28, interface : 21) | locale d'URL + langue Data Dragon par défaut + variante optionnelle | 0005 |
| J1 | CI sans Postgres, stockage ni go-fetcher : tests sautés, ni typecheck ni build front | Testcontainers, fixtures enregistrées, typecheck + build | 0010 |
| J2 | Même contrat écrit en PHP, TS et Go (regex de version, plafonds, layout, format de clé, `schema.sql`) | un hôte + client OpenAPI généré, contrôlé en CI | 0002, 0006 |

## 3. Front Twig + Turbo + îlots Vue : besoins qui restent

| ID | Piège actuel | Besoin conservé |
|---|---|---|
| H1 | Îlots jamais démontés (une vidéo continuait de jouer), snapshot Turbo figé | arrêter les médias en quittant une page, sans fuite |
| H2 | Ancres, `data-turbo="false"`, internes d'historique Turbo | navigation par ancres de section ; état des filtres dans l'URL |
| H3 | `muted` non appliqué (vuejs/core#3057) ; overlay `<Transition>` + `v-show` peu fiable | aperçus vidéo muets par défaut ; visibilité par classe CSS déterministe |
| H4 | Timeout de 4 s du service worker, `respondWith(undefined)` qui donnait des pages blanches | PWA conservée : network-first pour les pages, cache-first pour les blobs, contournement des routes privées |
| H5 | Admin hors pipeline Vite, tokens dupliqués | module Angular chargé à la demande, même design system |
| H6 | Zoom iOS sur les champs < 16 px, sonde de débordement, **pas de RTL** | champs ≥ 16 px, sonde reprise en E2E, RTL pour `ar` |
| H7 | Ancres et puces de section : chaque saut ajoutait une entrée d'historique (`pushState`, ancres natives comme puces) ; Retour revenait en haut de la page légale, mais rembobinait sans défiler sur les puces (restauration manuelle de Turbo) | saut par `SectionJump` : défilement avec la marge de la section, focus dans la section (le Tab suivant y entre, comme après une ancre native) et entrée d'historique (`pushState` de `history.state`, qui garde les identifiants du routeur). Retour après un saut revient en haut de la page, puces comprises ; après plusieurs sauts, les entrées intermédiaires se rembobinent sans défiler (le routeur restaure la position de sa navigation, partagée par les entrées ajoutées) |
| H8 | Le commutateur imprimait côté serveur le patch de la session dans la puce de chaque page (`page_selection()`) | les pages rendues à la requête le nomment dès le HTML ; les pages éditoriales prérendues (ADR 0005 : à propos, données, FAQ, changelog, mentions) n'en nomment aucun avant l'hydratation : leur puce garde la largeur d'un numéro (« Patch · EN ») puis nomme le patch de la session une fois `/api/meta` chargé |
| H9 | Sans JavaScript, l'inscription (formulaire et ligne d'aide du mot de passe) et l'éditeur de builds (puce « nécessite JavaScript ») restaient affichés | les routes rendues dans le navigateur (compte, éditeur de builds, admin : ADR 0005) exigent JavaScript ; sans lui, `index.html` n'affiche qu'un avis générique dans `<lodb-root>`, que tout rendu remplace. `auth.register.password_help` n'est pas repris : avec JavaScript, la liste de contrôle le remplaçait déjà |

## 4. Bugs latents et dérives — à corriger dans l'existant sans attendre

La réécriture prendra des mois. Ces points concernent la **prod actuelle** :

| Priorité | Constat | Où |
|---|---|---|
| **Haute** | **Purge de rétention CNIL jamais planifiée**, et événements bruts (IP, UA) jamais purgés | `AuditRollupCommand.php`, `analytics.md` « Rétention » |
| **Haute** | Échec transitoire mis en cache comme vide : une coupure Data Dragon fige une liste de versions vide pendant 1 h (30 jours pour les langues) | `VersionManager.php:104-121,146-158` |
| Moyenne | `/v1/trends` (API payante) ne contient que les jours agrégés au dernier déploiement | `go/api/internal/trends/trends.go:189` |
| Moyenne | Packs de crédits Stripe non dédupliqués ; expiration à 12 mois non implémentée | `api-publique.md` |
| Basse | HSTS à `max-age=86400` alors que le changelog annonce 2 ans | `docker/nginx/snippets/security-headers.conf:18` |
| Basse | Docs périmées (`architecture.md` : Encore, Stimulus, Apache ; README « pas de base de données » ; commentaire S3 dans `IngestsImages.php:193`) ; `legal.site_url` encore en `www.` | voir chemins cités |
| Basse | Règle métier (sentinelles de portée de sort) dans un template Twig | `champion/detail.html.twig:57-61` |

## 5. Particularités Data Dragon à conserver (UP)

Chaque point devient un cas de test du lot 1 ([ADR 0010](adr/0010-observabilite-tests-ci.md)).

1. `runesReforged.json` renvoie **403 avant 7.22.1** (180 versions sur 397) : dataset
   vide persisté, jamais une erreur.
2. Les langues sont listées globalement mais **absentes de nombreuses versions**
   (`ar_AE` : 49 sur 397). **`en_US` est le seul repli universel**, et il n'existe pas de
   `languages.json` par version.
3. Anciennes données incomplètes : `partype` absent en 0.x, `skin.num` absent avant
   ~3.13.24 (utiliser l'index), fichiers de détail en 403 (rendre depuis le résumé).
4. Filtrer les entrées `lolpatch_*` de `versions.json`.
5. Icônes de runes 7.22–8.7 en `.dds` mortes (403) : absence persistée.
6. **Jumeaux LoL Classic homonymes**. Objets : id `^77\d{4}$`. Sorts : mode `JADE`.
   Ne jamais dériver l'édition des `maps`, qui sont incohérents. Ids réutilisés :
   confirmer un jumeau par id **et** par nom. Pas de runes Classic, donc pas de build
   Classic.
7. **Fiddlesticks** : l'art actuel n'existe que sous `FiddleSticks`.
8. Splash, skins et vidéos de sorts **non versionnés** : hotlink assumé, avec un
   avertissement.
9. **Chromas uniquement via CommunityDragon** : chemins en minuscules, repli sur
   `latest`, étiquette de couleur dérivée de la teinte. Filtrer les chromas des skins
   Data Dragon.
10. Sentinelles de portée (`25000+`, `4294967295`, `"self"`) à masquer ; débris
    d'objets (noms vides, placeholders, balises, jetons non résolus).
11. Modes internes (`WIPMODEWIP`, `RUBY_TRIAL_1`…) : liste blanche de libellés.
    `CLASSIC` = la Faille de l'invocateur. Cartes de builds : SR = 11, ARAM = 12,
    Nexus Blitz = 21, Arena = 30.
12. Le CDN d'images ne parle que HTTP/1.1 : dimensionner le pool keep-alive sur la
    concurrence.
13. Faits dérivés : `stats` limité à 12 clés classiques ; type dérivé de `en_US` par id ;
    mêlée ≤ 325 et distance ≥ 350 ; tier via `depth`/`into` ; titres jamais mis en
    minuscules ; pas de date par version (pas de `lastmod`).

## 6. Invariants métier à porter

- **Indexation** : objets, sorts et champions par **id**, jamais par nom. L'édition est
  une dimension à part entière, avec lien vers le jumeau (`counterpart`).
- **Absences** : absence définitive (403/404) persistée et jamais re-fetchée ; erreur
  transitoire jamais persistée. Pour un dataset manquant : repli `en_US`, sinon dataset
  vide. Une page se dégrade section par section.
- **Stockage** : blobs adressés par contenu, immuables un an, servis depuis une
  allow-list, écrits de façon atomique. Rien d'autre n'est exposé.
- **Synchronisme** : détail, picker, build et recherche résolvent leurs images de façon
  synchrone ; seules les listes peuvent différer.
- **URLs** : dernier patch en URL courte canonique ; patchs passés auto-canoniques ;
  `/build/` réservé au front, partage sur `/b/{token}` (capacité opaque, noindex, sans
  JSON-LD).
- **Builds** : épinglés à leur patch et à leur mode. Un élément disparu devient un
  « fantôme », jamais supprimé automatiquement. L'import rapporte ce qui a été retiré.
  Arbre secondaire limité à 2 runes de rangées différentes (FIFO). Au plus 8 objets par
  étape et 40 au total. Objets Classic refusés en mode actuel.
- **Favoris** : résolus sur la version épinglée du profil ; un favori indisponible n'est
  jamais effacé ; un échec de données refuse la sauvegarde.
- **Confidentialité** :
  - même 404 pour un profil inconnu, privé ou banni (pas d'oracle) ;
  - e-mails masqués ; votes seulement sur les builds publics, score net seul affiché ;
  - retour au `Referer` seulement sur le même hôte ;
  - un compte banni ne peut se connecter par aucune méthode.
- **API publique** :
  - hash SHA-256 seul stocké, clé en clair affichée une fois ;
  - une clé régénérée garde plan, quota, crédits, identifiants Stripe et usage du mois ;
  - une seule clé active par compte, e-mail vérifié exigé ;
  - `/v1/usage` non décompté ; quota consommé sur le plan puis sur les crédits ;
  - `share_url` absolue ; indisponibilité du stockage = 503.
- **Stripe** : webhooks signés (secret absent = 503, signature invalide = 400, échec du
  handler = 500 pour relivraison) ; aiguillage par `metadata.kind` ; aucune identité
  client dans les logs.
- **Audit** : actions en ensemble fermé, best effort, 6 mois, miroir sans `ip` ni
  `meta.identifier` (garantie testée).
- **Préférences** : couleurs de jeu indépendantes du thème ; admin dans le thème Hextech
  par défaut. Mapping de locale : réduction au code de base, `zh_CN`/`zh_MY` → `zh-hans`,
  `zh_TW` → `zh-hant`, repli `en`. Le domaine ne détermine jamais la langue.
- **SEO** : canonique sans query ; stats en `PropertyValue` (jamais `Product`/`Offer`) ;
  pas de `SearchAction` sans URL de résultats rendue côté serveur ; JSON-LD encodé contre
  la XSS.
