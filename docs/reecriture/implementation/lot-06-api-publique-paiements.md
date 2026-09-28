# Lot 6 — API publique et paiements

Objectif du lot : l'API payante `/v1` absorbée dans l'hôte unique sans casser aucun client,
le portail des clés, et les paiements Stripe idempotents.

Critère de sortie local : tests de contrat `/v1` verts face aux références capturées sur
go-api ; un webhook rejoué ne crédite pas deux fois.

## L6.1 — Références de contrat `/v1` capturées sur go-api

- **Objectif** : figer le comportement observable de go-api avant de le réécrire.
  Aucune réponse de référence n'existe aujourd'hui : les tests Go vérifient des statuts et
  des fragments.
- **À lire** : `go/api/**` (routes, middleware, erreurs, quotas, rate limit, métrage,
  tendances) et ses tests, [`api-publique.md`](../../architecture/api-publique.md),
  `app/src/Entity/{ApiKey,ApiUsage}.php`, `app/src/Entity/Enum/ApiPlan.php`,
  `app/src/Service/PublicApi/ApiKeyIssuer.php`.
- **Périmètre** : `tests/fixtures/v1/**`, `tools/next/v1-capture/**`,
  `docs/reecriture/rapports/contrat-v1.md`.
- **Conception** :
  - Ancienne stack démarrée (go-api sur 8090), jeu de données inséré en SQL : profils
    public, privé et banni ; plus de 50 builds publics et quelques privés d'un champion ;
    une clé par plan (`free` 500/10, `credits` 500/60, `monthly` 15 000/120,
    `monthly_plus` 45 000/120, `annual` 20 000/300, `annual_plus` 60 000/300) ; une clé
    révoquée ; une clé au quota épuisé avec crédits, une sans ; des lignes `api_usage` ;
    agrégats quotidiens et datasets `en_US` dans le volume de stockage pour les tendances.
  - Scénarios : chaque route × authentification (absente, mal formée, inconnue, révoquée,
    `Bearer` et `X-Api-Key`) ; rafale au-delà du rate limit ; quota → crédits →
    `quota_exceeded` ; pagination invalide et plafond de `per_page` à 50 ; `type` et `range`
    invalides ; profil privé et inconnu (404 identiques) ; pré-vol `OPTIONS` (204, CORS) ;
    routes inconnues (404/405 en texte) ; `/v1/usage` non décompté ; base arrêtée → 503.
  - Enregistrement : statut, en-têtes utiles (`Content-Type`, `X-RateLimit-*`, CORS), corps
    JSON, avec les champs volatils (dates, `X-RateLimit-Reset`) remplacés par des marqueurs.
  - Le rapport liste les scénarios et les particularités à garder ou à changer
    volontairement : facturation **avant** le handler (un 400/404/503 est décompté), routes
    inconnues en texte, révocation différée de 60 s (devient immédiate).
- **Dépend de** : lot 0 (seulement l'ancienne stack). **Taille** : M.

## L6.2 — Schéma des lots 6, 7, 9 et 10

- **Objectif** : la migration unique des tables des lots 6, 7, 9 et 10, après celle du
  lot 4.
- **À lire** : ADR [0004](../adr/0004-stockage-etat-et-scaling.md) (tables),
  ADR [0008](../adr/0008-mises-a-jour-integrees.md) (politique client),
  `app/src/Service/Analytics/Storage/**` et `Analytics/Model/**` (format des événements et
  des agrégats), `app/src/Service/PublicApi/**` (packs de crédits), `heritage.md` G4.
- **Périmètre** : migration, entités et configurations du lot dans
  `src/LoDb.Infrastructure/Persistence/**`, compléments de l'anonymisation
  (`tools/next/db/`), tests associés.
- **Conception** :
  - `stripe_event` (id d'événement en clé primaire, type, dates, statut) : idempotence.
  - `api_credit_grants` (clé, requêtes achetées, date, échéance à 12 mois, session Stripe
    unique) ; `api_keys.credits_balance` reste le compteur de référence, lu aussi par
    l'ancienne stack. Attribution FIFO : le solde appartient aux achats les plus récents ;
    à l'échéance d'un achat, sa part restante ainsi calculée est retirée du solde.
  - Reprise : la migration crée pour chaque clé dont le solde est positif un achat
    synthétique égal à ce solde, daté de la migration. Pendant la période de retour
    arrière, un solde que les achats connus ne couvrent pas (crédits vendus par l'ancienne
    stack) reçoit de même un achat synthétique, daté du jour où la tâche d'expiration le
    constate.
  - `analytics_event` partitionnée par jour (`PARTITION BY RANGE`, créée en SQL brut) avec
    les champs actuels (route, chemin, type, sorte, entité, statut, version, langue,
    locale, IP, visiteur, UA, navigateur, OS, appareil, bot, référent, source, pays) et
    l'origine de la capture (page servie ou navigation interne, L7.1).
  - `analytics_daily` (jour, totaux JSONB, buckets JSONB) au format des agrégats quotidiens
    actuels : leur reprise est directe et `/v1/trends` somme le bucket `entities`.
  - `client_policy` (plateforme, version minimale, dernière version ; pour Android, URL,
    signature et version native minimale du bundle ; date) : la politique client vit en
    base, comme tout état mutable (ADR 0004), utilisée par L9.0.
- **Tests** : migration appliquée sur `Baseline` + lot 4 ; création et suppression de
  partition ; reprise des soldes en achats synthétiques ; aller-retour de chaque entité.
- **Dépend de** : lot 4. **Taille** : M.

## L6.3 — Module `/v1`

- **Objectif** : le même contrat que go-api, dans l'hôte unique, révocation immédiate.
- **À lire** : les références de L6.1 et leur rapport, `go/api/internal/**`.
- **Périmètre** : `src/LoDb.Api/Modules/PublicApi/**` (hors `Keys/`), tests associés
  (`tests/LoDb.Api.Tests/PublicApi/**`).
- **Conception** :
  - Schéma d'authentification séparé : `Authorization: Bearer ` (préfixe sensible à la
    casse), sinon `X-Api-Key` ; format `lodb_` + 40 hex minuscules, rejeté sans requête
    en base s'il est mal formé ; recherche par SHA-256.
  - Ordre : clé → rate limit → quota → handler. `X-RateLimit-Limit`, `-Remaining`, `-Reset`
    (horodatage Unix) sur toute réponse après résolution de la clé, 429 et erreurs du
    handler compris ; absents sur 401, 403 et 503 de résolution.
  - Rate limit : seau à jetons par clé (capacité = `rate_limit_per_min`, recharge par
    seconde), `System.Threading.RateLimiting` partitionné par id de clé.
  - Quota : consommation du mois en cache par clé (`HybridCache`) ; plan d'abord, puis
    décrément atomique d'un crédit en SQL, sinon 429 `quota_exceeded`. Le module expose
    `IApiKeyCache.Invalidate(clé)`, appelé par toute révocation, régénération ou
    modification de droits (portail L6.4, Stripe L6.5, admin L7.3) : la révocation est
    immédiate.
  - Métrage : `Channel` de 4096 événements qui ne bloque jamais (événement perdu +
    avertissement), vidé chaque seconde par upsert groupé dans `api_usage`, dernier vidage
    à l'arrêt. `/v1/usage` n'est ni décompté ni métré.
  - Routes : `profiles/{username}`, `champions/{championId}/builds` (publics, `page` et
    `per_page` à 1 et 20 par défaut, plafond 50, tri par date puis id décroissants,
    `share_url` absolue), `trends/{type}` (`7d` ou `30d`, top 25, égalités par id, noms
    depuis le catalogue `en_US` de la dernière version, édition des objets et des sorts,
    lecture d'`analytics_daily`), `usage`.
  - CORS `*` en GET et OPTIONS, en-têtes `Authorization` et `X-Api-Key` ; `OPTIONS` en 204
    sans authentification. Enveloppe, codes et messages d'erreur identiques à go-api ;
    panne de base ou de stockage → 503 `internal`.
  - Document OpenAPI `public-v1`. Tout écart volontaire est listé dans `contrat-v1.md`.
- **Tests** : tests de contrat qui rejouent chaque scénario de L6.1 sur le même jeu de
  données et comparent statut, en-têtes et corps normalisés ; révocation immédiate ;
  seau à jetons ; quota puis crédits sous concurrence.
- **Dépend de** : L6.1, L6.2. **Taille** : L.

## L6.4 — Portail des clés et page `/developers`

- **Objectif** : gérer sa clé et lire la documentation de l'API.
- **À lire** : `app/src/Controller/Billing/ApiKeyController.php`,
  `app/templates/api/**`, `app/templates/developers/**`,
  `app/src/Controller/Editorial/DevelopersController.php`, scope `api` des traductions.
- **Périmètre** : `src/LoDb.Api/Modules/PublicApi/Keys/**`,
  `src/LoDb.Web/src/app/features/{api-portal,developers}/**`,
  `tests/LoDb.E2E/specs/{api-portal,developers}/**`, tests associés.
- **Conception** :
  - API : aperçu (plan, quota, consommé, restant, crédits, rate, usage des 30 derniers
    jours) ; création (e-mail vérifié, une seule clé active, clé en clair montrée une fois)
    ; régénération (garde plan, quota, crédits, identifiants Stripe et usage du mois) ;
    révocation. Chaque changement invalide le cache de la clé (`IApiKeyCache`, L6.3) et
    s'audite.
  - Portail `/{locale}/account/api` (client, `noindex`) : aperçu, révélation unique avec
    copie, tableau d'usage, achats de packs et d'abonnements (via L6.5).
  - `/{locale}/developers` : endpoints, authentification, quotas, erreurs, tarifs ; URL de
    base de l'API tirée de la configuration (aujourd'hui codée en dur à
    `http://localhost:8090`).
- **Tests** : règles de clé (une active, régénération qui conserve, révélation unique,
  révocation effective immédiatement sur `/v1`) ; E2E du portail et de `/developers`.
- **Dépend de** : L6.3, fondations du lot 3. **Taille** : M.

## L6.5 — Stripe : checkout, webhooks idempotents, expiration des crédits, dons

- **Objectif** : les paiements actuels, sans double crédit, avec l'expiration à 12 mois
  enfin appliquée.
- **À lire** : `app/src/Controller/Billing/**`, `app/src/Service/{Stripe,Donation,
  PublicApi}/**`, `app/src/Repository/ApiKeyRepository.php` (`addCredits`, rétrogradation),
  [`api-publique.md`](../../architecture/api-publique.md) (événements),
  `app/templates/donate/**`, `app/templates/api/_billing.html.twig`, `heritage.md` G4.
- **Périmètre** : `src/LoDb.Api/Modules/Billing/**`, `src/LoDb.Api/Workers/Billing/**`,
  `src/LoDb.Web/src/app/features/donate/**`, `tests/LoDb.E2E/specs/donate/**`, tests
  associés.
- **Conception** :
  - Checkout Sessions en EUR avec `price_data` en ligne, derrière une abstraction
    testable : dons (3, 5, 10, 25 € ou montant libre de 1 à 500 €, `submit_type=donate`,
    métadonnées `{source: lodb-donate, kind: donation}`, `client_reference_id` si connecté) ;
    packs (5 € = 5 000, 10 € = 10 000, 20 € = 20 000 requêtes, `kind: api_pack`) ;
    abonnements (`monthly`, `monthly_plus`, `annual`, `annual_plus`, `kind: api_plan`).
  - `/webhooks/stripe` : secret absent → 503, signature invalide → 400. L'insertion de
    `event.id` dans `stripe_event` et les effets métier se font **dans une seule
    transaction** : un doublon (conflit sur l'id) répond 200 sans effet ; un échec du
    handler annule tout, événement compris, et répond 500 pour que Stripe relivre.
  - Aiguillage par `metadata.kind` : `api_pack` → crédits + rate porté à 60 au moins +
    ligne `api_credit_grants` ; `api_plan` → plan, quota, rate et identifiants Stripe ;
    `donation` ou absent → don idempotent sur la session et badge de soutien ; clé gratuite
    créée si l'acheteur n'en a pas. `customer.subscription.deleted` → retour au plan
    gratuit (quota 500, rate 10, ou 60 s'il reste des crédits), abonnement effacé, client
    conservé. Types non gérés → 200. Chaque changement de droits invalide le cache de la
    clé (L6.3).
  - Tâche quotidienne d'expiration des crédits (FIFO et achats synthétiques, L6.2).
  - Les builds store des apps n'affichent aucun paiement par un **drapeau de build du
    front** (L10.1) ; l'API, elle, reste active pour le web.
  - Aucune identité client dans les logs ; appels d'audit des paiements et dons.
  - Front : page de dons, pages de retour (`noindex, follow`).
- **Tests** : cas de signature ; même événement rejoué deux fois → un seul crédit ; handler
  en échec → rien d'écrit, 500, puis relivraison réussie ; chaque `kind` ; résiliation
  d'abonnement ; calcul d'expiration FIFO ; paramètres des sessions ; E2E de la page de
  dons jusqu'à la redirection vers Stripe (doublure).
- **Dépend de** : L6.2, fondations du lot 3. **Taille** : L.
