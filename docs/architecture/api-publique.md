# API publique LeagueOfDataBase

> **Fait foi** : pour le contrat `/v1`, le document OpenAPI
> `src/LoDb.Api/openapi/LoDb.Api_public-v1.json` (généré, jamais édité à la main) et
> [`contrat-v1.md`](../reecriture/rapports/contrat-v1.md) (contrat capturé sur l'ancien
> `go-api`, et écarts retenus par LoDb) ; pour le comportement, le code de
> `src/LoDb.Api/Modules/PublicApi/`. Cette page en est le résumé.

API REST JSON v1, payante, servie par le module `PublicApi` de l'hôte unique `LoDb.Api`
(`src/LoDb.Api/Modules/PublicApi/`, [ADR 0002](../reecriture/adr/0002-hote-serveur-unique.md)).
nginx l'expose sur le sous-domaine `api.` (`docker/nginx/sites/api.conf` : `/v1/`, `/healthz`
et `/readyz`, tout autre chemin en `404 page not found` texte), et aussi sous `/v1/` de l'hôte
du site (`docker/nginx/sites/site.conf`). Elle expose les données communautaires du site :
profils publics, builds partagés et tendances de consultation. La facturation passe par le
module `Billing` (Stripe) ; les clés se gèrent depuis la page `/{locale}/account/api` du compte
(présentation publique : `/{locale}/developers`).

En production, l'origine documentée de l'API est `https://api.league-of-data-base.com`
(`LoDb:PublicApi:Reference:BaseUrl`, posée par `LODB_PUBLIC_API_ORIGIN` dans
`compose.deploy.yaml`).

## Authentification

Chaque requête `/v1/*` exige une clé API au format `lodb_` + 40 caractères hexadécimaux
**minuscules**, transmise au choix :

```
Authorization: Bearer lodb_...
X-Api-Key: lodb_...
```

Le préfixe `Bearer ` est sensible à la casse et l'emporte sur `X-Api-Key` ; une clé passée en
query est ignorée. Une valeur hors format répond `401` sans lecture de la base
(`Access/ApiKeyCredential.cs`). Seul le hash SHA-256 de la clé est stocké (`api_keys`) : une clé
perdue ne peut pas être récupérée, seulement régénérée. Une clé révoquée ou désactivée répond
`403 forbidden`.

Sans clé : les sondes `/healthz` et `/readyz` (hors `/v1`), et le pré-vol `OPTIONS /v1/*`
(`204`, `src/LoDb.Api/Hosting/CorsPolicies.cs`).

## Sondes de santé (hors contrat `/v1`)

Code : `src/LoDb.Api/Hosting/Health/`. Elles n'appartiennent pas au contrat `/v1` (écart 17 de
[`contrat-v1.md`](../reecriture/rapports/contrat-v1.md)) : ni en-têtes CORS ni
`X-RateLimit-*`, et le rate limiter de l'hôte ne s'y applique pas (`DisableRateLimiting`).

| Sonde | Contrôles | Statut |
|---|---|---|
| `GET /healthz` | aucun (vivacité) : une panne de base ne fait jamais redémarrer le conteneur | `200` |
| `GET /readyz` | `postgres` (`SELECT 1` sur `ConnectionStrings:LoDb`) et `storage` (fichier créé, écrit et vidé dans `LoDb:Storage:Root`), 5 s chacun | `200`, ou `503` si un contrôle échoue |

Corps `{status, checks}` : l'état global et celui de chaque contrôle, sans description ni
exception (les sondes sont anonymes).

```bash
curl https://api.league-of-data-base.com/readyz
```

```json
{"status":"Unhealthy","checks":{"postgres":"Unhealthy","storage":"Healthy"}}
```

`/healthz` répond `{"status":"Healthy","checks":{}}`. Sur le sous-domaine `api.`, nginx relaie
les deux sondes à l'API, `/readyz` avec un débit limité par adresse (`lodb_rate_readyz`). Sur
l'hôte du site, `/healthz` est répondu par nginx lui-même (`ok`) et `/readyz` n'atteint pas
l'API ([`pieges.md`](pieges.md), « API, données et comptes »).

## Endpoints

Chaque route est en `GET` (`HEAD` routé comme `GET`, donc authentifié et décompté).

### `GET /v1/profiles/{username}`

Profil public d'un membre, nom cherché sans tenir compte de la casse. Un profil privé, banni
ou inconnu répond le même `404`, sans révéler l'existence du compte
(`Profiles/ProfileEndpoint.cs`).

```bash
curl -H "Authorization: Bearer lodb_votrecle..." \
  https://api.league-of-data-base.com/v1/profiles/Faker
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
ancien. Paginé : `?page=` (défaut 1) et `?per_page=` (défaut 20, plafonné à 50 sans refus).
L'id n'est pas validé et est sensible à la casse : un id inconnu donne une page vide en `200`.
Les builds des comptes bannis sont retirés, comme sur le site
(`Builds/ChampionBuildsEndpoint.cs`, `Builds/Pagination.cs`).

```bash
curl -H "X-Api-Key: lodb_votrecle..." \
  "https://api.league-of-data-base.com/v1/champions/Aatrox/builds?page=1&per_page=20"
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
      "share_url": "https://league-of-data-base.com/b/Kf3xQ9pLmT2c",
      "created_at": "2026-07-15T18:40:00Z"
    }
  ],
  "pagination": { "page": 1, "per_page": 20, "total": 42, "total_pages": 3 }
}
```

`runes` et `steps` sont restitués tels que sauvegardés par leur auteur (JSON brut).
`share_url` est une URL **absolue** vers la page de partage du build : les clients appellent
l'API depuis une autre origine que le site, un chemin relatif leur serait inexploitable.
L'origine vient de `LoDb:PublicApi:SiteOrigin` (défaut `https://league-of-data-base.com`,
`PublicApiOptions.cs` ; sur un hôte servi, `https://` + `LODB_CANONICAL_HOST`,
`compose.deploy.yaml`).

### `GET /v1/trends/{type}`

Entités les plus vues, `type` ∈ `champions` | `items` | `runes` | `summoners`. Fenêtre via
`?range=7d` (défaut, aussi si vide) ou `?range=30d` : jours J à J-6 ou J-29 en UTC. Vues
sommées sur la fenêtre, top 25, égalités par id croissant (`Trends/TrendsService.cs`).

Source des vues (`Trends/Reading/TrendsSource.cs`) :

- **avant** `LoDb:Audience:TrendsSwitchDate` (`LoDb__Audience__TrendsSwitchDate`) : les
  agrégats journaliers de la mesure maison (`analytics_daily`, calculés par
  `Workers/Analytics/AnalyticsRollupJob.cs`) ;
- **à partir de** ce jour : les chiffres d'Umami (`audience_entity_daily`, tirés chaque heure
  par `Workers/Analytics/AudienceTrendsWorker.cs`). La collecte d'Umami étant publique, le jour
  d'une entité compte au plus `ClipFactor` (3) fois la médiane de ses `ClipWindowDays` (28)
  derniers jours (`src/LoDb.Infrastructure/Audience/AudienceOptions.cs`) ;
- sans date de bascule : la mesure maison seule.

Un classement est gardé `LoDb:PublicApi:TrendsLifetime` (5 min) par type, fenêtre et jour UTC :
la fenêtre avance à minuit.

```bash
curl -H "Authorization: Bearer lodb_votrecle..." \
  "https://api.league-of-data-base.com/v1/trends/champions?range=30d"
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

`name` vient du dernier catalogue promu en `en_US` (`Trends/Reading/CatalogTrendNames.cs`) et
est omis s'il n'est pas résolu (le champ `id` reste toujours présent). Un catalogue absent du
stockage répond `503 internal`.

Pour `items` et `summoners`, chaque entrée porte en plus `edition` ∈ `modern` | `classic` :
depuis League of Legends Classic, un objet ou un sort d'invocateur peut exister dans les deux
jeux sous le **même nom** (`1004` / `771004` « Faerie Charm », `SummonerFlash` /
`SummonerFlash_Jade`). Les deux jumeaux sont classés séparément ; seul `edition` permet de les
distinguer sans interpréter l'id.

```json
{ "rank": 3, "id": "771004", "name": "Faerie Charm", "edition": "classic", "views": 42 }
```

`range` est vérifié avant `type` : un `range` invalide répond `400`, un `type` inconnu `404`.

### `GET /v1/usage`

Consommation de la clé appelante, relue en base. Authentifié et soumis au rate limit, mais
**jamais décompté du quota** ni métré : une clé épuisée peut toujours consulter son état
(`Usage/UsageEndpoint.cs`). Les requêtes de la dernière seconde peuvent ne pas y figurer encore
(métrage par lots, voir plus bas).

```bash
curl -H "Authorization: Bearer lodb_votrecle..." \
  https://api.league-of-data-base.com/v1/usage
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

Enveloppe uniforme `{error:{code,message}}`, noms en snake_case,
`application/json; charset=utf-8` (`Http/V1Errors.cs`, `Http/V1Json.cs`) :

```json
{ "error": { "code": "rate_limited", "message": "rate limit exceeded, retry after X-RateLimit-Reset" } }
```

| Code HTTP | `error.code` | Signification |
|---|---|---|
| 401 | `unauthorized` | Clé absente, malformée ou inconnue |
| 403 | `forbidden` | Clé révoquée ou désactivée |
| 404 | `not_found` | Profil inexistant, privé ou banni ; `type` de tendance inconnu |
| 429 | `rate_limited` | Rythme par minute dépassé |
| 429 | `quota_exceeded` | Quota mensuel épuisé et aucun crédit restant |
| 400 | `invalid_request` | Paramètre invalide (`page`, `per_page`, `range`) |
| 503 | `internal` | Base ou stockage indisponible (`service temporarily unavailable`) |
| 500 | `internal` | Toute autre erreur (`internal error`) ; la cause est journalisée, jamais montrée |

Les messages exacts sont listés dans [`contrat-v1.md`](../reecriture/rapports/contrat-v1.md),
§ 4. Sous `/v1/`, un chemin qu'aucune route n'écrit exactement répond `404 page not found` en
texte, et une méthode non servie `405 Method Not Allowed` avec `Allow: GET, HEAD`
(`Http/GoMuxText.cs`) : ni authentification ni décompte.

## Rate limiting et quotas

Ordre de traitement d'une requête (`Gate/PublicApiGate.cs`, `Gate/ApiAccess.cs`) : chemin
exact → clé → rate limit → quota puis crédit (routes facturées) → handler. Une requête admise
est décomptée **avant** son handler : un `400` ou un `404` du handler est facturé.

- **Rate limit** : un seau à jetons par clé, rechargé en continu à `rate_limit_per_min` jetons
  par minute (`Limits/ApiRateLimiter.cs`, `Limits/ContinuousTokenBucket.cs`). Dès que la clé
  est connue, la réponse porte `X-RateLimit-Limit`, `X-RateLimit-Remaining` et
  `X-RateLimit-Reset` (timestamp Unix), y compris sur les `429` et sur les `400`, `404` et `503`
  du handler ; jamais sur `401` ni `403`. Au-delà : `429 rate_limited`.
- **Quota** : chaque requête est d'abord imputée au quota mensuel du plan (mois UTC,
  `Metering/UsageCalendar.cs`), décompté sous verrou par l'instance (`Limits/KeyLedger.cs`) ;
  quota épuisé, un crédit prépayé est retiré en base de façon atomique
  (`Access/ApiKeyStore.cs`, `SpendCreditAsync`) ; sans crédit : `429 quota_exceeded`.
- Seaux, compteurs de quota et cache de clés vivent dans la mémoire de chaque instance : avec
  N instances, la limite par minute est multipliée par N (écart 15 de `contrat-v1.md`).
- CORS ouvert (`Access-Control-Allow-Origin: *`, méthodes `GET, OPTIONS`, en-têtes
  `Authorization, X-Api-Key`) sur toute réponse dont le chemin commence par `/v1/` : l'API est
  appelable directement depuis un navigateur.

## Tarifs

Catalogue : `src/LoDb.Api/Modules/Billing/Catalog/ApiPlans.cs` et `ApiPacks.cs`.

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

Packs crédits : 1 €/1000 requêtes, consommés après le quota mensuel du plan actif ; tant que
la clé détient des crédits, son rate limit vaut au moins 60 req/min (`ApiPlans.RateOf`).
Les crédits d'un pack expirent 12 mois après l'achat : `Workers/Billing/CreditExpiryJob.cs`
passe chaque jour et retire du solde ce qui reste des lots échus (`api_credit_grants`, règle
FIFO, `src/LoDb.Api/Modules/Billing/Expiry/CreditExpiry.cs`). Un plan annuel est vérifié au
**mois calendaire** : son volume annuel est stocké comme quota mensuel (20 000 / 60 000).
Achat et gestion depuis `/{locale}/account/api` (paiement Stripe,
`POST /api/billing/checkout/pack` et `/plan`) ; l'API ne manipule jamais de moyen de paiement.

## Gestion des clés (v1)

Le portail appelle `/api/account/api-key` (`Keys/ApiKeyEndpoints.cs`) : `GET` (clé active et
consommation, jamais le secret), `POST` (création, e-mail vérifié requis), `POST /regenerate`,
`DELETE` (révocation). Créations et régénérations : 5 par heure et par compte
(`Keys/ApiKeyLimits.cs`).

- **Une seule clé active par compte.** Le plan, le quota mensuel et les crédits vivent sur la
  clé (`api_keys`), pas sur le compte.
- **Régénérer** = révoquer l'ancienne clé (`is_active=false`, `revoked_at`) et en émettre
  une nouvelle qui **reporte** nom, plan, quota, crédits, rate limit et ids Stripe ; les lignes
  `api_usage` et `api_credit_grants` suivent la clé remplaçante : la rotation du secret ne
  remet jamais le quota à zéro et les crédits gardent leur échéance (`Keys/OwnedKeys.cs`).
- **Révocation** : immédiate. Qui modifie une clé (portail, webhooks Stripe, admin) appelle
  `IApiKeyCache.Invalidate` une fois la transaction validée, et la requête suivante relit la
  clé sur l'instance : `403 forbidden` dès la requête suivante. Sur une autre instance, ou pour
  un changement fait hors de l'API (SQL), l'effet arrive au plus après
  `LoDb:PublicApi:KeyCacheLifetime` (60 s) (`IApiKeyCache.cs`, `PublicApiOptions.cs`).
- **Changement d'abonnement** : un seul abonnement Stripe actif par clé
  (`Billing/Purchases/PlanCheckoutEndpoint.cs`). Changer d'offre = annuler l'abonnement en
  cours (retour au plan Free dès réception du webhook `customer.subscription.deleted`, crédits
  conservés, 60 req/min conservés si crédits > 0) puis souscrire la nouvelle offre.
- **Clé auto-provisionnée** : si un paiement aboutit alors que le compte n'a plus de clé
  active, le webhook crée une clé Free porteuse de l'achat ; son secret n'est connu de
  personne : l'utilisateur la régénère depuis le portail pour obtenir un secret utilisable
  (les droits sont reportés) (`Billing/Fulfilment/BuyerKeys.cs`).

## Opérateur — configuration Stripe

Paiement par Stripe Checkout, `price_data` inline (aucun catalogue produit à maintenir dans
le dashboard), **carte uniquement** (`payment_method_types = ["card"]`). Webhook :
`POST /webhooks/stripe`.

1. **Dashboard → Developers → Webhooks → Add endpoint** : URL
   `https://<domaine>/webhooks/stripe`, événements à activer (tous, sinon l'état local
   diverge de Stripe) :
   - `checkout.session.completed` (packs `metadata.kind=api_pack`, abonnements
     `metadata.kind=api_plan`, dons) ;
   - `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`
     (paiement différé : accordé à l'encaissement, rien en cas d'échec) ;
   - `customer.subscription.updated`, `customer.subscription.deleted`,
     `invoice.payment_failed` (le plan suit le statut de l'abonnement) ;
   - `charge.refunded`, `charge.dispute.created` (reprise de ce que le paiement a donné).
2. Reporter le signing secret de l'endpoint dans `LoDb__Billing__StripeWebhookSecret` et la
   clé API dans `LoDb__Billing__StripeSecretKey`
   ([`configuration.md`](../guides/configuration.md), § 6.5). Sans secret, l'endpoint
   répond `503` (Stripe garde ses événements) ; signature absente/invalide → `400` ; échec
   d'un handler → `500` (Stripe rejoue l'événement).
3. **Mode test** : clés `sk_test_…`/`whsec_…` ; en local, la stack de dev (`lodb-dev`) n'a
   aucun secret Stripe (`503`) : poser le secret de test dans une surcouche hors dépôt
   ([`pieges.md`](pieges.md)), puis
   `stripe listen --forward-to localhost:18080/webhooks/stripe` (nginx de la stack,
   `LODB_HTTP_PORT`, `compose.override.yaml`) fournit ce secret.
   Cartes de test : `4242 4242 4242 4242`.
4. Livraison *at-least-once* et sans ordre garanti : chaque événement est appliqué une fois
   (`stripe_event`, clé = id de l'événement), chaque session une fois
   (`stripe_checkout`), et un événement d'abonnement plus ancien que le dernier appliqué ne
   change rien (`stripe_subscription.changed_at`). Les journaux ne nomment personne (ids
   internes, montants, ids Stripe).

### Cycle de vie d'un paiement

| Événement | Effet |
|---|---|
| Session terminée et payée (`paid`, `no_payment_required`) au prix du catalogue | Pack crédité, plan appliqué ou don enregistré ; session `paid` |
| Session terminée `unpaid` (moyen différé) | Rien d'accordé ; session `pending` jusqu'à `async_payment_succeeded` (accordé) ou `async_payment_failed` (`failed`) ; un remboursement total ou un litige reçu avant le succès la passe `revoked`, et le succès arrivé ensuite n'accorde rien |
| Montant ou devise hors catalogue (prix du pack ou du plan, dons de 1 à 500 €, en euros) | Rien d'accordé, session `refused`, avertissement `billing.checkout.refused` : à rembourser ou régulariser à la main |
| Abonnement `past_due`, `unpaid`, `paused`, `canceled`, `incomplete_expired`, ou facture impayée | Clé au plan Free, abonnement conservé sur la clé ; crédits et plancher de 60 req/min conservés |
| Abonnement de nouveau `active` (ou `trialing`) | Plan de l'abonnement rétabli |
| Abonnement supprimé | Plan Free définitif, abonnement retiré de la clé |
| Remboursement **total** ou litige d'un pack | Ce qui reste du pack (règle FIFO des crédits) est retiré du solde, jamais en dessous de 0 ; les requêtes déjà consommées le restent |
| Remboursement total ou litige d'un don | Don retiré ; badge supporter retiré s'il ne reste aucun autre don |
| Remboursement total d'un paiement d'abonnement (trouvé par son client Stripe) | Clé au plan Free jusqu'à ce que Stripe signale l'abonnement de nouveau actif |
| Remboursement **partiel** | Rien n'est repris (geste commercial) |

À reprendre à la main, signalés par `billing.payment.unmatched` : le remboursement ou le
litige d'un paiement antérieur à `stripe_checkout`, celui reçu avant tout événement de sa
session (qui sera accordée à son arrivée), et le litige d'un paiement d'abonnement (mettre
fin à l'abonnement dans le dashboard). Un litige gagné ne rend rien de lui-même.

## Notes d'implémentation

**Module** `src/LoDb.Api/Modules/PublicApi/`, câblé par `PublicApiModule.cs` (`AddPublicApi`,
`MapPublicApi`) ; réglages `LoDb:PublicApi` (`PublicApiOptions.cs`), vérifiés au démarrage.

| Dossier | Rôle |
|---|---|
| `Gate/` | filtre posé sur chaque route (`PublicApiGate`), admission clé → seau → quota (`ApiAccess`), exemption de `/v1/usage` (`QuotaExemption`) |
| `Access/` | lecture des clés par hash (`ApiKeyStore`), cache (`ApiKeyDirectory`, `UnknownKeys`) |
| `Limits/` | seau à jetons (`ApiRateLimiter`), quota puis crédits (`ApiQuota`, `KeyLedger`) |
| `Metering/` | métrage vers `api_usage` (`UsageMeter`, `UsageFlusher`, `UsageWriter`) |
| `Profiles/`, `Builds/`, `Trends/`, `Usage/` | les quatre routes `/v1` |
| `Http/` | enveloppe d'erreur, codes, JSON, réponses texte du routeur |
| `Keys/` | portail `/api/account/api-key` et référence `/api/public-api/reference` de `/developers` |
| `OpenApi/` | compléments du document `public-v1` |

- **Cache** : `HybridCache` en mémoire seule (`DisableDistributedCache`). Une clé lue en base
  est gardée `KeyCacheLifetime` (60 s) ; les clés inconnues aussi (`UnknownKeys`, 50 000 au
  plus) ; `Invalidate` ouvre une nouvelle génération du cache. Les classements de tendances
  sont gardés `TrendsLifetime` (5 min).
- **Métrage** : une requête facturée admise dépose un événement dans un `Channel` borné à
  4 096, sans jamais attendre ; tampon plein, l'événement est perdu et compté
  (`lodb.publicapi.usage.dropped`, `PublicApiMetrics.cs`). Toutes les `MeteringInterval` (1 s),
  `UsageFlusher` ajoute les comptes à `api_usage` en un seul
  `INSERT … SELECT unnest(…) ON CONFLICT DO UPDATE` ; un échec garde les comptes pour le vidage
  suivant, un dernier vidage suit l'arrêt du serveur. `UsageFlusher` est enregistré par le
  module, pas sous `Workers/` : il tourne partout où `/v1` est servi, **même avec
  `LoDb:Workers:Enabled=false`**, sinon les requêtes facturées n'atteindraient jamais
  `api_usage` ; jamais pendant la génération du document OpenAPI au build.
- **Pannes** : une exception de base ou de stockage répond `503 internal`, toute autre
  `500 internal` (`Http/DependencyFailures.cs`), journalisées `publicapi.request.unavailable`
  et `publicapi.request.failed`.
- **Schéma** : `api_keys`, `api_usage`, `api_credit_grants` (entités
  `src/LoDb.Infrastructure/Persistence/PublicApi/`, migrations EF
  `src/LoDb.Infrastructure/Persistence/Migrations/`).
- **OpenAPI** : document `public-v1` (routes `v1/`,
  `src/LoDb.Api/Hosting/OpenApi/OpenApiDocuments.cs`), complété par `OpenApi/PublicApiDocumentTransformer.cs` (schémas `bearer` et `apiKey`,
  en-têtes `X-RateLimit-*`). Écrit dans `src/LoDb.Api/openapi/LoDb.Api_public-v1.json` par
  `npm run api:generate` (`tools/api/generate.mjs`, `dotnet build -p:LoDbGenerateOpenApi=true`),
  dans le commit qui change le contrat ; servi par l'API en `Development` seulement
  (`MapOpenApi`, `src/LoDb.Api/Hosting/HostingRegistration.cs`).
- **Tests de contrat** : `tests/LoDb.Api.Tests/PublicApi/Contract/ContractTests.cs` rejoue les
  références capturées sur go-api ; chaque écart admis est écrit dans `ContractDeviations.cs`.
