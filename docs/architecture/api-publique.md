# API publique LeagueOfDataBase

API REST JSON v1, payante, servie par le micro-service Go `go-api` (port 8090). Elle expose
les données communautaires du site : profils publics, builds partagés et tendances de
consultation. La facturation est gérée côté site (Stripe) ; les clés API se génèrent depuis
la page `/profile/api` de votre compte (présentation publique : `/developers`).

## Authentification

Chaque requête `/v1/*` exige une clé API au format `lodb_` + 40 caractères hexadécimaux,
transmise au choix :

```
Authorization: Bearer lodb_...
X-Api-Key: lodb_...
```

Seul le hash SHA-256 de la clé est stocké côté serveur : une clé perdue ne peut pas être
récupérée, seulement régénérée. Une clé révoquée ou désactivée répond `403 forbidden`.

`GET /healthz` est le seul endpoint sans authentification.

## Endpoints

### `GET /healthz`

État du service et de ses dépendances (toujours `200`, une dépendance en panne est signalée
dans le corps) :

```bash
curl https://api.leagueofdatabase.example/healthz
```

```json
{"status":"ok","dependencies":{"postgres":"ok","minio":"ok"}}
```

### `GET /v1/profiles/{username}`

Profil public d'un membre (uniquement si le membre a activé « profil public » ; sinon `404`,
sans révéler l'existence du compte).

```bash
curl -H "Authorization: Bearer lodb_votrecle..." \
  https://api.leagueofdatabase.example/v1/profiles/Faker
```

```json
{
  "username": "Faker",
  "created_at": "2026-07-01T09:12:00Z",
  "favorites": {
    "champion_id": "Ahri",
    "item_id": "3006",
    "rune_id": "Domination",
    "summoner_id": "SummonerFlash"
  },
  "public_builds": 3
}
```

### `GET /v1/champions/{championId}/builds`

Builds publics pour un champion (id Data Dragon, ex. `Aatrox`), du plus récent au plus
ancien. Paginé : `?page=` (défaut 1) et `?per_page=` (défaut 20, plafond 50).

```bash
curl -H "X-Api-Key: lodb_votrecle..." \
  "https://api.leagueofdatabase.example/v1/champions/Aatrox/builds?page=1&per_page=20"
```

```json
{
  "champion_id": "Aatrox",
  "data": [
    {
      "name": "Aatrox top lethality",
      "description": "Snowball early.",
      "game_version": "15.1.1",
      "runes": { "primary": 8000 },
      "steps": [ { "items": ["3074"] } ],
      "share_url": "/b/Kf3xQ9pLmT2c",
      "created_at": "2026-07-15T18:40:00Z"
    }
  ],
  "pagination": { "page": 1, "per_page": 20, "total": 42, "total_pages": 3 }
}
```

`runes` et `steps` sont restitués tels que sauvegardés par leur auteur (JSON brut).
`share_url` est relatif au site public (`https://<site>/b/{token}`).

### `GET /v1/trends/{type}`

Entités les plus consultées sur le site, `type` ∈ `champions` | `items` | `runes` |
`summoners`. Fenêtre via `?range=7d` (défaut) ou `?range=30d`. Top 25, classement recalculé
au plus toutes les 5 minutes.

```bash
curl -H "Authorization: Bearer lodb_votrecle..." \
  "https://api.leagueofdatabase.example/v1/trends/champions?range=30d"
```

```json
{
  "type": "champions",
  "range": "30d",
  "entries": [
    { "rank": 1, "id": "Ahri", "name": "Ahri", "views": 1204 },
    { "rank": 2, "id": "Aatrox", "name": "Aatrox", "views": 987 }
  ]
}
```

`name` est résolu depuis les données Data Dragon les plus récentes et peut être omis si la
résolution échoue (le champ `id` reste toujours présent).

### `GET /v1/usage`

Consommation de la clé appelante. Authentifié et soumis au rate limit, mais **jamais
décompté du quota** : une clé épuisée peut toujours consulter son état.

```bash
curl -H "Authorization: Bearer lodb_votrecle..." \
  https://api.leagueofdatabase.example/v1/usage
```

```json
{
  "plan": "free",
  "monthly_quota": 500,
  "used_this_month": 137,
  "remaining_this_month": 363,
  "credits_balance": 0,
  "rate_limit_per_min": 10
}
```

## Erreurs

Enveloppe uniforme sur toutes les erreurs :

```json
{ "error": { "code": "rate_limited", "message": "rate limit exceeded, retry after X-RateLimit-Reset" } }
```

| Code HTTP | `error.code` | Signification |
|---|---|---|
| 401 | `unauthorized` | Clé absente, malformée ou inconnue |
| 403 | `forbidden` | Clé révoquée ou désactivée |
| 404 | `not_found` | Ressource inexistante (ou profil privé) |
| 429 | `rate_limited` | Rythme par minute dépassé |
| 429 | `quota_exceeded` | Quota mensuel épuisé et aucun crédit restant |
| 400 | `invalid_request` | Paramètre invalide (`page`, `per_page`, `range`…) |
| 500 / 503 | `internal` | Erreur interne ou dépendance indisponible |

## Rate limiting et quotas

- **Rate limit** : token bucket par clé, capacité = `rate_limit_per_min` du plan. Chaque
  réponse `/v1` porte `X-RateLimit-Limit`, `X-RateLimit-Remaining` et `X-RateLimit-Reset`
  (timestamp Unix). Au-delà : `429 rate_limited`.
- **Quota** : chaque requête est d'abord imputée au quota mensuel du plan ; quota épuisé,
  les crédits prépayés sont décomptés un par un ; sans crédit : `429 quota_exceeded`.
- CORS ouvert (`Access-Control-Allow-Origin: *`) : l'API est appelable directement depuis
  un navigateur.

## Tarifs

| Offre | Prix | Volume | Rate limit |
|---|---|---|---|
| Free | 0 € | 500 req/mois | 10 req/min |
| Pack crédits S | 5 € | 5 000 req (validité 12 mois) | 60 req/min |
| Pack crédits M | 10 € | 10 000 req (validité 12 mois) | 60 req/min |
| Pack crédits L | 20 € | 20 000 req (validité 12 mois) | 60 req/min |
| Mensuel | 5 €/mois | 15 000 req/mois (0,33 €/1000) | 120 req/min |
| Mensuel+ | 15 €/mois | 45 000 req/mois | 120 req/min |
| Annuel | 48 €/an | 240 000 req/an, portés en quota **mensuel** de 20 000 (0,20 €/1000) | 300 req/min |
| Annuel+ | 144 €/an | 720 000 req/an, portés en quota **mensuel** de 60 000 | 300 req/min |

Packs crédits : 1 €/1000 requêtes, consommés après le quota mensuel du plan actif ;
dès qu'un pack est acheté (crédits > 0), le rate limit est porté à 60 req/min minimum.
La validité de 12 mois est une règle **contractuelle** : la v1 n'implémente pas
d'expiration automatique des crédits (à faire respecter côté opérateur si besoin).
Les plans annuels sont vérifiés par go-api au **mois calendaire** : leur volume annuel
est stocké comme quota mensuel (20 000 / 60 000).
Achat et gestion depuis `/profile/api` sur le site (paiement Stripe) — l'API elle-même ne
manipule jamais de moyen de paiement. Présentation publique : page `/developers`.

## Gestion des clés (v1)

- **Une seule clé active par compte.** Le plan, le quota mensuel et les crédits vivent
  sur la clé (contrat go-api) — pas sur le compte.
- **Régénérer** = révoquer l'ancienne clé (`is_active=false`, `revoked_at`) et en émettre
  une nouvelle qui **reporte** plan, quota, crédits, rate limit, ids Stripe **et la
  consommation du mois** (les lignes `api_usage` suivent la clé remplaçante — la rotation
  du secret ne remet jamais le quota à zéro).
- **Révocation** : effective immédiatement en base ; le cache de clés de go-api (60 s)
  peut encore accepter la clé pendant au plus une minute, puis `403 forbidden`.
- **Changement d'abonnement** : un seul abonnement Stripe actif par clé. En v1, changer
  d'offre = annuler l'abonnement en cours (retour au plan Free dès réception du webhook
  `customer.subscription.deleted`, crédits conservés, 60 req/min conservés si crédits > 0)
  puis souscrire la nouvelle offre.
- **Clé auto-provisionnée** : si un paiement aboutit alors que le compte n'a plus de clé
  active, le webhook crée une clé Free porteuse de l'achat ; son secret n'est connu de
  personne — l'utilisateur la régénère depuis le portail pour obtenir un secret utilisable
  (les droits sont reportés).

## Opérateur — configuration Stripe

Facturation gérée côté Symfony (`/profile/api`) via Stripe Checkout, `price_data` inline
(aucun catalogue produit à maintenir dans le dashboard). Webhook : `POST /webhooks/stripe`.

1. **Dashboard → Developers → Webhooks → Add endpoint** : URL
   `https://<domaine>/webhooks/stripe`, événements à activer :
   - `checkout.session.completed` (packs `metadata.kind=api_pack`, abonnements
     `metadata.kind=api_plan`, dons — comportement log-only conservé) ;
   - `customer.subscription.deleted` (retour au plan Free, crédits conservés).
2. Reporter le signing secret de l'endpoint dans **`STRIPE_WEBHOOK_SECRET`**
   (et la clé API dans `STRIPE_SECRET_KEY`) — variables d'environnement du conteneur PHP.
   Sans `STRIPE_WEBHOOK_SECRET`, l'endpoint répond `503` (webhook désactivé volontairement) ;
   signature absente/invalide → `400` ; échec d'un handler → `500` (Stripe rejoue l'événement).
3. **Mode test** : utiliser les clés `sk_test_…`/`whsec_…` de test ; en local,
   `stripe listen --forward-to localhost:8080/webhooks/stripe` fournit le secret de test.
   Cartes de test : `4242 4242 4242 4242`.
4. Les webhooks ne journalisent jamais de donnée nominative (ids internes, montants,
   ids de session uniquement). La livraison Stripe étant *at-least-once*, un événement
   rejoué après un 2xx perdu peut créditer deux fois un pack — trade-off v1 assumé
   (pas de table de déduplication), le dashboard Stripe fait foi pour arbitrer.

## Notes d'implémentation (résumé)

- Service Go autonome (`go-api/`, port 8090), lecture seule sur Postgres (`users`,
  `builds`) et MinIO (agrégats `analytics/daily/*.json`, datasets Data Dragon pour les
  noms). Schéma des tables `api_keys` / `api_usage` : `go-api/schema.sql` (migration
  Doctrine côté `app/`).
- Clés validées mises en cache 60 s ; le compteur mensuel est décompté en mémoire entre
  deux rafraîchissements et le métrage est flushé en base par lots (~1 s) : un léger
  dépassement de quota est possible sous forte concurrence (assumé). Les crédits, eux,
  sont décrémentés de façon synchrone et atomique.
