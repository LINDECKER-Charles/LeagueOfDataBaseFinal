# Contrat `/v1` capturé sur go-api (L6.1)

- **Date** : 2026-09-26.
- **Source** : go-api construit depuis `go/api`, arbre Git `0b2172d52a4e` (consigné dans
  chaque référence, `source.goApiTree`). L'image publiée de l'ancienne stack ne diffère des
  sources que par son Dockerfile (commit `5dcb6cf`, passage à Go 1.26).
- **Livrables** : références et jeu de données dans [`tests/fixtures/v1/`](../../../tests/fixtures/v1/README.md),
  outil rejouable dans [`tools/next/v1-capture/`](../../../tools/next/v1-capture/README.md).
- **Chantier suivant** : L6.3 rejoue ces scénarios contre le module `/v1`
  ([lot 6](../implementation/lot-06-api-publique-paiements.md#l63--module-v1)).

## 1. Environnement de capture

L'ancienne stack a été démarrée depuis la racine (`docker compose up -d`). Elle n'a servi
qu'à une lecture : l'instantané du schéma (`pg_dump --schema-only` de la base de dev,
migrée jusqu'à `Version20260719150000`) et l'extraction des datasets Data Dragon 16.18.1
`en_US` réduits aux entités utiles.

La capture elle-même tourne dans un environnement privé, supprimé à la fin :
Postgres 17 en mémoire sur le réseau `lodb-v1-capture` (sans port publié), go-api construit
depuis les sources et publié sur `127.0.0.1:18990`, stockage généré dans un répertoire
temporaire. Aucun fichier Compose n'est modifié. La base de dev, le volume `storage` et les
conteneurs de l'ancienne stack ne sont jamais écrits : aucun script de nettoyage n'est donc
nécessaire. Le scénario « base arrêtée » arrête la base privée de la capture.

`PUBLIC_SITE_URL` n'est pas surchargé : les `share_url` portent l'origine de production
(`https://league-of-data-base.com`), valeur par défaut de go-api.

## 2. Jeu de données

- **Profils** : `PublicPlayer` (public, 4 favoris, 57 builds publics), `PrivatePlayer`
  (privé), `BannedPlayer` (public et banni, 2 builds publics), `NoFavorites` (public, sans
  favori ni build), et un compte privé `ApiClient<n>` par clé.
- **Builds** : 55 builds publics d'Aatrox (créés par paires à la même heure, pour observer
  le départage par id), 3 privés plus récents, 2 publics du compte banni (les plus récents),
  3 builds publics d'Ahri : texte HTML et non ASCII, JSON `runes`/`steps` imbriqué ; l'un
  appartient à un profil privé.
- **Clés** (`seed/keys.json`) : une par plan, aux valeurs de `ApiPlan` : `free` 500/10,
  `credits` 500/60 avec 5 000 crédits, `monthly` 15 000/120, `monthly_plus` 45 000/120,
  `annual` 20 000/300, `annual_plus` 60 000/300. S'y ajoutent une clé révoquée
  (`is_active=false` et `revoked_at`), une inactive (`is_active=false` seul), une à 499/500
  avec 2 crédits, une épuisée sans crédit, une de rafale, une de facturation, une à révoquer,
  une à recharger, une avec 500 requêtes le mois précédent seulement, une inconnue et une
  créée en cours de scénario.
- **`api_usage`** : lignes du mois courant (`CURRENT_DATE`) et du mois précédent.
- **Stockage** : datasets `16.18.1` et `16.9.1` (le second, plus ancien mais plus grand dans
  l'ordre lexical, porte des noms `Stale …` qui ne doivent jamais sortir), et 9 jours
  d'agrégats : un fichier corrompu, un sans `entities`, un jour à J-7 (hors 7 jours) et un à
  J-30 (hors 30 jours), des entités d'un autre type, un id vide, une égalité de vues.

## 3. Scénarios

173 requêtes et 11 actions en 10 groupes indépendants (données et processus neufs à chaque
groupe).

| Groupe | Requêtes | Ce qu'il fige |
|---|---|---|
| `01-auth` | 40 | 4 routes × clé absente, mal formée, inconnue, révoquée, inactive, `Bearer`, `X-Api-Key` ; préfixe `bearer` en minuscules, espaces, `Bearer` vide, `Bearer` prioritaire sur `X-Api-Key`, `Basic` suivi de `X-Api-Key`, clé en query ignorée, hex majuscule, mauvais préfixe, longueur, rejet mis en cache |
| `02-plans` | 12 | `/v1/usage` et `X-RateLimit-Limit` de chaque plan ; mois précédent non compté ; métrage visible après vidage |
| `03-profiles` | 10 | public, casse du nom, nom encodé, privé et inconnu (404 identiques), banni, sans favori, slash final, nom vide |
| `04-builds` | 20 | pages 1, 3 et au-delà, `per_page` 50, 51 et 1000 (plafonnés à 50), dernière page partielle, zéros de tête, `+2`, paramètre répété, valeurs vides, `page`/`per_page` à 0, négatif, non numérique, décimal ; champion inconnu, casse de l'id, JSON brut, segment vide |
| `05-trends` | 14 | 4 types, 7 et 30 jours, top 25, égalités, noms depuis la dernière version, éditions, nom absent ; `range` vide, invalide, en majuscules ; `type` invalide, en majuscules, `type` et `range` invalides |
| `06-limits` | 22 | rafale de 11 à 10/min, `/v1/usage` limité, limite avant validation ; quota → crédit → crédit → `quota_exceeded`, métrage des requêtes payées en crédits, clé épuisée |
| `07-billing` | 13 | `/v1/usage` jamais décompté ; 400, 404, 200 et `HEAD` décomptés ; route inconnue et `OPTIONS` non décomptées |
| `08-routing` | 18 | pré-vol `OPTIONS`, `/healthz`, 404 et 405 texte, `/v1` sans slash, préfixe en majuscules, `HEAD`, `Origin` |
| `09-key-cache` | 12 | révocation, création de clé, recharge de crédits et changement de plan faits en SQL pendant que l'API tourne : effet différé de 60 s |
| `10-outage` | 12 | base arrêtée (clé en cache ou non, tendances), puis stockage disparu |

## 4. Contrat observé

**Ordre de traitement** : routage (ServeMux) → CORS → clé → rate limit → quota → handler.
Une route inconnue ou une méthode refusée ne passe jamais par l'authentification ; une
requête refusée par le quota a déjà consommé un jeton ; un paramètre invalide est refusé
après le quota.

**Erreurs** : enveloppe `{"error":{"code","message"}}`, `application/json; charset=utf-8`.

| Statut | `code` | `message` exact |
|---|---|---|
| 401 | `unauthorized` | `missing API key: use Authorization: Bearer <key> or X-Api-Key` · `malformed API key` · `unknown API key` |
| 403 | `forbidden` | `API key is revoked or inactive` (révoquée ou inactive, sans distinction) |
| 429 | `rate_limited` | `rate limit exceeded, retry after X-RateLimit-Reset` |
| 429 | `quota_exceeded` | `monthly quota exhausted and no credits left` |
| 400 | `invalid_request` | `page and per_page must be positive integers` · `range must be 7d or 30d` |
| 404 | `not_found` | `no public profile for this username` · `type must be one of champions, items, runes, summoners` |
| 503 | `internal` | `service temporarily unavailable` |

**En-têtes** :
- `Access-Control-Allow-Origin: *`, `-Methods: GET, OPTIONS`, `-Headers: Authorization,
  X-Api-Key` sur **toute** réponse dont le chemin commence par `/v1/`, erreurs 401, 404 et
  405 comprises ; jamais sur `/healthz`, `/` ou `/v1`. Aucun `Vary`, aucun écho d'origine.
- `X-RateLimit-Limit`, `-Remaining`, `-Reset` dès que la clé est résolue : 200, 400, 404,
  429 (les deux codes) et 503 du handler. Absents sur 401, 403, 503 de résolution de clé,
  `OPTIONS` et routes inconnues. Sur un 429 `rate_limited`, `Remaining` vaut 0.
- `OPTIONS` sur `/v1/*`, route inconnue comprise : 204 sans corps ni authentification.

**Corps** :
- Profil : `username` (graphie stockée, recherche insensible à la casse), `created_at`
  (RFC 3339 à la seconde, en `Z`), `favorites` (4 champs, `null` si absents),
  `public_builds` (compte les builds publics, pas les privés).
- Builds : `champion_id` repris tel quel, `data[]` trié par `created_at` puis id
  décroissants, `description` `null` possible, `runes`/`steps` en JSON brut (ordre des clés
  normalisé par JSONB), `share_url` absolue, `pagination.total_pages` au moins 1 ; page au
  delà de la dernière : `data: []` et 200.
- Tendances : `type`, `range` (`7d` si vide ou absent), `entries[]` avec `rank`, `id`,
  `name` (omis s'il n'est pas résolu), `edition` (`items` et `summoners` seulement), `views` ;
  top 25, égalités par id croissant ; jours J à J-(n-1) en UTC ; noms de la version de
  dataset la plus récente en ordre numérique ; runes résolues par id et par clé.
- Usage : `plan`, `monthly_quota`, `used_this_month` (lu en base, mis à jour par le métrage
  en moins d'une seconde et demie), `remaining_this_month` (plancher 0),
  `credits_balance`, `rate_limit_per_min`.

## 5. Particularités

### À garder

| Particularité | Références |
|---|---|
| Facturation **avant** le handler : un 400 ou un 404 est décompté ; un 503 du handler aussi, d'après le code (`withAuth` enregistre la requête avant d'appeler le handler), mais les références ne peuvent pas le montrer puisque la base est arrêtée | `07-billing/usage-after-billed-requests` |
| `HEAD` routé comme `GET`, donc authentifié et décompté ; corps vide | `07-billing/billed-head`, `08-routing/head-usage` |
| Quota vérifié avant les paramètres ; rate limit avant le quota | `06-limits/exhausted-invalid-params`, `burst-invalid-route-params` |
| Requêtes payées en crédits métrées : `used_this_month` dépasse le quota (502/500) | `06-limits/quota-chain-usage` |
| Préfixe `Bearer ` sensible à la casse, prioritaire sur `X-Api-Key`, valeur rognée ; clé en query ignorée | `01-auth/bearer-*`, `query-string-key-ignored` |
| Même 404 pour un profil privé ou inconnu | `03-profiles/private`, `unknown` |
| `per_page` au-delà de 50 plafonné, pas refusé ; zéros de tête et `+` acceptés ; premier paramètre répété retenu | `04-builds` |
| Id de champion non validé et sensible à la casse : page vide en 200 | `04-builds/unknown-champion`, `champion-id-case` |
| `range` vérifié avant `type` ; `type` inconnu en 404, pas en 400 | `05-trends/type-and-range-invalid` |
| Builds publics d'un profil privé listés (la visibilité est portée par le build) | `04-builds/ahri` |
| `/healthz` toujours 200, panne dans le corps | `10-outage/healthz-*` |
| Rate limit lu dans la colonne `rate_limit_per_min` : le plancher de 60/min des crédits est posé par le site à l'écriture, pas par go-api | `02-plans/usage-credits`, `09-key-cache/topped-up-served` |

### À changer volontairement

| Particularité go-api | Cible | Fondement | Références |
|---|---|---|---|
| Révocation différée de 60 s (cache de clés) | 403 immédiat | ADR 0002, `heritage.md` A6 | `09-key-cache/revoked-still-served` |
| Rejet d'une clé inconnue gardé 60 s, même après sa création | clé servie dès sa création | invalidation de L6.3 | `09-key-cache/created-still-unknown` |
| Recharge de crédits et changement de plan pris en compte après 60 s | immédiats (`IApiKeyCache.Invalidate`) | L6.3 (droits modifiés par L6.4, L6.5, L7.3) | `09-key-cache/topped-up-still-refused`, `upgraded-old-limit` |
| Profil banni servi en 200 | 404 identique à un profil inconnu | `heritage.md` (confidentialité), site : `PublicProfileController` | `03-profiles/banned` |
| Stockage absent : tendances en 200 avec `entries: []` | 503 `internal` | lot 6 (L6.3), `heritage.md` (API publique) | `10-outage/trends-storage-down` |

### À trancher par L6.3

- **Routes inconnues et méthodes refusées en texte** (`404 page not found\n`,
  `Method Not Allowed\n` avec `Allow: GET, HEAD` et `X-Content-Type-Options: nosniff`, CORS
  présent sous `/v1/`) : artefacts du ServeMux de Go. Recommandation : garder statut,
  `Allow` et CORS, et reproduire le corps texte, peu coûteux et sans risque pour les
  clients ; un passage à l'enveloppe JSON serait un écart à lister ici.
- **Builds d'un compte banni** : go-api les liste et les compte dans `public_builds`, alors
  que le site retire les propriétaires bannis de tout classement public
  (`BuildVoteRepository::publicBuildsQb`). Recommandation : s'aligner sur le site ;
  `heritage.md` ne le dit pas explicitement pour l'API.
- **Chemin avec `//`** : le ServeMux nettoie le chemin et répond 307 avec `Location`
  (`04-builds/missing-champion-segment`). Propre à Go : à ne pas considérer comme contractuel.

## 6. Ce que L6.3 doit rejouer

- **Données** : `seed/dataset.sql` vise le schéma de l'ancienne stack (`users`, `builds`,
  `api_keys`, `api_usage`). Si L6.2 renomme une colonne, adapter le chargement plutôt que
  les références. Les clés brutes sont dans `seed/keys.json` ; les étapes `sql` de
  `09-key-cache` écrivent dans `api_keys`.
- **Horloge** : les agrégats sont datés depuis le jour de la capture. Le test les régénère
  depuis `seed/daily.json` relativement à son horloge (`FakeTimeProvider` ou jour UTC
  courant). Les étapes `sleep` peuvent devenir des avances d'horloge : 1,5 s pour le
  métrage, 61 s pour le cache.
- **Comparaison** : statut, en-têtes retenus et corps décodé, `X-RateLimit-Reset` contrôlé
  comme un horodatage dans la minute. `X-RateLimit-Remaining` sort d'un seau continu
  (recharge de `limite/60` jeton par seconde, partie entière) : exact en rafale, il dépend de
  la recharge après les `sleep` (`02-plans/usage-*-after`, `06-limits/quota-chain-usage`,
  `07-billing/usage-not-counted` et `usage-after-billed-requests`). Un seau
  `System.Threading.RateLimiting` à réapprovisionnement discret peut y différer d'un jeton :
  le signaler comme écart ou régler la période pour retrouver la même valeur. Pour que ces
  valeurs soient stables d'un rejeu à l'autre, les étapes qui suivent une action Docker
  (arrêt de la base, SQL) et les longues séquences utilisent des clés à 10 ou 60/min :
  à 300/min, la durée variable d'un `docker stop` ajoutait parfois un jeton.
- **Écarts attendus** : les lignes de « À changer volontairement » ont une référence qui
  décrit l'ancien comportement ; le test de contrat y attend la cible et le dit.
- **Pannes** : `stopDatabase` rend la base injoignable ; `hideStorage` retire le répertoire
  de stockage. Une clé déjà en cache (go-api) passe l'authentification sans base : avec un
  cache hybride, L6.3 choisit ce qu'elle renvoie, mais le code reste 503 `internal`.

## 7. Vérifications

| Commande | Résultat |
|---|---|
| `docker compose up -d` (racine) | ancienne stack démarrée ; `.env` racine créé depuis `.env.example` ; une course avec un démarrage concurrent a laissé `go-fetcher` et `go-api` sur un réseau disparu, recréés par `docker compose up -d --force-recreate --no-deps go-fetcher go-api` |
| `node tools/next/v1-capture/capture.mjs --refresh-schema` | instantané écrit (10 tables) |
| `node tools/next/v1-capture/capture.mjs` | 10 groupes capturés, environnement supprimé (aucun conteneur `lodb.v1-capture`, aucun répertoire temporaire restant) |
| `node tools/next/v1-capture/capture.mjs --check`, première version des scénarios | un rejeu passé, le suivant en échec : 5 `X-RateLimit-Remaining` décalés d'un jeton dans `10-outage` (clé à 300/min) ; scénarios `01`, `05`, `09` et `10` passés sur des clés plus lentes, puis tout recapturé |
| `node tools/next/v1-capture/capture.mjs --check` (deux rejeux après recapture) | code 0, `check passed: 10 group(s) identical to the references` à chaque fois |
| `node --test 'tools/next/v1-capture/test/*.test.mjs'` | 37 tests réussis |
