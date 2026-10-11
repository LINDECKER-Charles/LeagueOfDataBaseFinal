# Métriques de LoDb — le contrat

Ce que l'API et le serveur SSR exposent à la collecte de l'hôte, sous quels noms et avec
quels labels. Les alertes d'`infra-vps` (`observability/vmalert/rules/`) et les tableaux de
Grafana lisent ces noms : **un nom ou un label qui change casse une alerte sans bruit**.
Toute métrique ajoutée, renommée ou retirée met cette page à jour dans le même commit.

Logs et requêtes LogsQL : [`observabilite.md`](observabilite.md) et [`logging.md`](logging.md).

## Où elles sont lues

| Processus | Point de collecte | Réglage |
|---|---|---|
| API (`api`) | `GET /metrics` sur le port **9464**, seule route de ce port | `LoDb:Hosting:MetricsPort` |
| SSR (`web-ssr`) | `GET /metrics` sur le port **9464**, serveur HTTP séparé du site (404 pour tout le reste) | `LODB_METRICS_PORT` |

- Le port 9464 n'est **jamais publié** (`compose*.yaml`) ni **routé par nginx**. Sur le
  port du site, `/metrics` est la 404 statique du pré-routeur ; sur le port 8080 de l'API,
  une 404.
- Sur un hôte, `api` et `web-ssr` rejoignent le réseau partagé `observability` et portent
  les labels `prometheus.scrape: "true"` et `prometheus.port: "9464"`
  (`compose.deploy.yaml`, contrat d'`infra-vps` `docs/APPLICATIONS.md` § 4). La collecte
  (job `applications`) ajoute `stack` (`lodb-prod`, `lodb-staging`), `service` et
  `container`. Toute requête filtre sur `stack` : staging et prod partagent l'hôte.
- Sur ce réseau partagé, tout conteneur de l'hôte joint l'API et le SSR : le port 8080 de
  l'API et le port 4000 du SSR ne répondent qu'au réseau de la stack et à la loopback
  (`NetworkGate`, `networkGate`, 404 sinon), et les conteneurs de la stack ne s'appellent
  que par leurs alias préfixés (`lodb-api`, `lodb-postgres`). Ce que les ports 9464
  exposent est lisible de tout l'hôte : jamais de donnée personnelle ni de secret.
- En local, lire depuis le réseau de la stack :
  `docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml exec -T nginx wget -qO- http://lodb-api:9464/metrics`
  et `… exec -T web-ssr node -e "fetch('http://127.0.0.1:9464/metrics').then(r => r.text()).then(console.log)"`.

## Noms

- **API** : exportateur Prometheus d'OpenTelemetry. Le point devient `_`, l'unité `s`
  donne `_seconds`, une unité `{…}` disparaît, un compteur finit en `_total`, un
  histogramme donne `_bucket`, `_sum` et `_count`. Chaque série porte `otel_scope_name`
  (le meter). Exemple : `lodb.catalog.load.duration` (s) → `lodb_catalog_load_duration_seconds`.
- **SSR** : `prom-client`, noms tels que déclarés (`src/server/metrics/ssr-metrics.ts`).
- Un compteur de l'API n'a de série qu'à sa **première incrémentation** : une série absente
  veut dire « rien n'est arrivé », pas « requête cassée ». Une alerte `absent()` ne cite que
  les familles marquées « dès le démarrage » ci-dessous. Une jauge n'existe qu'une fois créé
  le service qui la porte : « tâches de fond actives » veut dire créée au démarrage par une
  tâche de fond (`LoDb:Workers:Enabled`, le cas des hôtes), sinon à la première requête qui
  s'en sert.
- Le label `job` est réservé à la cible (`job="applications"`) : une série qui en porte un
  le verrait renommé `exported_job`. Les tâches périodiques sont donc étiquetées `task`.

## Règle de cardinalité

- **Aucun label** ne porte une version, une langue, une adresse IP, un chemin ou une
  route brute, un identifiant (compte, clé, build), un nom ou un user agent. Les valeurs
  sont des **énumérations fermées** du code (constantes nommées) ; `http_route` de l'API
  est le **gabarit** de la route, jamais le chemin reçu.
- Cible : **moins de 5 000 séries** par stack, API et SSR réunis. À mesurer à chaque
  ajout et après un déploiement :
  `count by (service) ({stack="lodb-prod", job="applications"})`.
- Au-delà : une View OpenTelemetry côté API (bornes d'histogramme, label retiré), ou un
  label en moins côté SSR, plutôt qu'un échantillonnage de la collecte.
- `http.server.request.duration` garde **8 bornes** (0,01 · 0,05 · 0,1 · 0,25 · 0,5 · 1 ·
  2,5 · 10 s) au lieu des 14 du framework : chaque borne est une série de plus par route,
  méthode et statut (`Hosting/Telemetry/TelemetryRegistration.cs`).

## API — métriques LoDb

| Nom | Type | Labels (valeurs) | Dès le démarrage | Meter, source |
|---|---|---|---|---|
| `lodb_build_info` | jauge, toujours 1 | `revision`, `version` | oui | `LoDb.Api` |
| `lodb_network_gate_refusals_total` | compteur | — | non | `LoDb.Api`, requêtes du port 8080 venues hors du réseau de la stack |
| `lodb_db_unavailable_total` | compteur | — | non | `LoDb.Database` : serveur injoignable, délai de commande dépassé, session refusée (500 compté ; le pool plein donne un 503 non compté) |
| `lodb_hashing_in_flight` | jauge | — | non : à la première requête qui sollicite les comptes | `LoDb.Security` |
| `lodb_hashing_wait_seconds` | histogramme | — | non | `LoDb.Security`, attente d'un permis de hachage |
| `lodb_hashing_refused_total` | compteur | `reason` (`queue_full`, `timeout`) | non | `LoDb.Security` |
| `lodb_sign_in_attempts_total` | compteur | `result` (`success`, `failure`, `two_factor_required`, `locked_out`, `throttled`, `banned`), `account` (`member`, `admin`, `none`, `unchecked`), `channel` | non | `LoDb.Security` |
| `lodb_lockouts_total` | compteur | `is_admin` (`true`, `false`) | non | `LoDb.Security` |
| `lodb_admin_security_events_total` | compteur | `event` (`mfa_enrolled`, `mfa_invitation_refused`) | non | `LoDb.Security` |
| `lodb_sessions_revoked_total` | compteur | `reason` (`sign_out`, `sign_out_everywhere`, `refresh_replayed`) | non | `LoDb.Security` ; `refresh_replayed` : un jeton de rafraîchissement déjà échangé est revenu, sa famille est révoquée |
| `lodb_catalog_cache_lookups_total` | compteur | `outcome` (`hit`, `joined`, `miss`) | non | `LoDb.Catalog` ; `hit` compte aussi la lecture `en_US` que fait toute lecture froide d'une autre langue |
| `lodb_catalog_cache_evictions_total` | compteur | — | non | `LoDb.Catalog` |
| `lodb_catalog_load_duration_seconds` | histogramme | `outcome` (`built`, `missing`, `failed`) | non | `LoDb.Catalog` |
| `lodb_catalog_loads_in_flight` | jauge | — | non | `LoDb.Catalog` |
| `lodb_catalog_cold_admissions_total` | compteur | `partition` (`internal`, `public`), `result` (`admitted`, `refused`) | non | `LoDb.Catalog`, porte froide |
| `lodb_ingestion_sync_waiting` | jauge | — | oui (tâches de fond actives) | `LoDb.Ingestion`, ingestions à la demande en attente d'un créneau |
| `lodb_ingestion_queue_depth` | jauge | `queue` (`versions`, `on_demand`) | oui, file par file (tâches de fond actives) | `LoDb.Ingestion` |
| `lodb_ingestion_on_demand_refused_total` | compteur | `reason` (`full`, `crawler_budget`, `dataset_budget`, `image_budget`, `backlog`, `slot_wait`) | non | `LoDb.Ingestion` |
| `lodb_ingestion_duration_seconds` | histogramme | `stage` (`version`, `datasets`, `images`) | non | `LoDb.Ingestion` |
| `lodb_ingestion_versions_ready_total`, `lodb_ingestion_fetch_failures_total`, `lodb_ingestion_absences_total{type}`, `lodb_ingestion_blobs_written_total{format}` | compteurs | `type` (type d'image du manifeste), `format` (`source`, `webp`) | non | `LoDb.Ingestion` |
| `lodb_job_period_seconds` | jauge | `task` | oui, dès qu'une tâche démarre (tâches de fond actives) | `LoDb.Jobs` |
| `lodb_job_last_success_seconds` | jauge (horodatage Unix) | `task` | après le premier succès de l'instance | `LoDb.Jobs` |
| `lodb_job_duration_seconds` | histogramme | `task`, `outcome` (`succeeded`, `failed`) | non | `LoDb.Jobs` |
| `lodb_job_failures_total` | compteur | `task` | non | `LoDb.Jobs` |
| `lodb_outbox_messages_total` | compteur | `outcome` (`sent`, `retried`, `dead`) | non | `LoDb.Outbox` |
| `lodb_outbox_pending` | jauge | — | oui (tâches de fond actives) | `LoDb.Outbox` |
| `lodb_outbox_deferred` | jauge | — | oui (tâches de fond actives) | `LoDb.Outbox`, messages dus que le plafond horaire de leur modèle retient (`LoDb:Outbox:HourlyCaps`) |
| `lodb_outbox_batch_duration_seconds`, `lodb_outbox_batch_failures_total` | histogramme, compteur | — | non | `LoDb.Outbox` |
| `lodb_analytics_views_queued_total`, `…_written_total`, `…_dropped_total` | compteurs | `origin` ; `reason` (`queue_full`, `unresolved`, `write_failed`) | non | `LoDb.Analytics` |
| `lodb_audience_vues_tirees` | jauge | — | non : au premier tirage réussi (jamais tant que le tirage dort) | `LoDb.Audience`, pages vues qu'Umami a comptées sur le site pendant la dernière heure close, relevées par le tirage horaire (`audience.trends`) : la sentinelle de toute la chaîne (émetteur, edge, Umami, tirage). Préfixe `lodb_`, jamais `umami_` (réservé au reconciler d'`infra-vps`) |
| `lodb_publicapi_refusals_total` | compteur | `code` (codes d'erreur de `/v1`) | non | `LoDb.PublicApi` |
| `lodb_publicapi_usage_pending` | jauge | — | oui | `LoDb.PublicApi` |
| `lodb_publicapi_usage_dropped_total`, `…_flush_duration_seconds`, `…_flush_failures_total` | compteurs, histogramme | — | non | `LoDb.PublicApi` |
| `lodb_client_policy_upgrade_required_total` | compteur | `platform` (`desktop`, `android`) | non | `LoDb.ClientPolicy`, réponses `426` |

`lodb_job_period_seconds` et `lodb_job_last_success_seconds` se lisent ensemble : une tâche
est en retard quand `time() - lodb_job_last_success_seconds > 3 * lodb_job_period_seconds`
(`tools/cutover/monitoring/metrics.promql`, requête `jobs-late`).

## API — métriques du framework

Noms à confirmer au premier scrape d'un hôte (`.NET 10`, OpenTelemetry 1.x) :

- ASP.NET Core : `http_server_request_duration_seconds` (`http_route`,
  `http_request_method`, `http_response_status_code`…), `http_server_active_requests`,
  `aspnetcore_rate_limiting_requests_total` (résultat et politique du limiteur),
  `kestrel_queued_connections`, `kestrel_active_connections`.
- Sécurité du framework, ajoutés explicitement (`TelemetryRegistration.cs`) : meters
  `Microsoft.AspNetCore.Identity`, `Microsoft.AspNetCore.Authentication` et
  `Microsoft.AspNetCore.Authorization` (connexions, défis, refus, résultats des politiques).
- Runtime : `dotnet_thread_pool_queue_length`, `dotnet_gc_*`, `dotnet_process_*`.
- Données : meters `Npgsql` (pool de connexions) et `Microsoft.EntityFrameworkCore`.
- Client HTTP sortant (Data Dragon) : `http_client_request_duration_seconds`.

Traces : sans `OTEL_EXPORTER_OTLP_ENDPOINT`, aucune n'est enregistrée
(`ParentBased(TraceIdRatioBased(0))`, y compris sous un `traceparent` marqué échantillonné) ;
l'identifiant de trace reste dans chaque ligne de log (`trace_id`).

## SSR

| Nom | Type | Labels (valeurs) | Dès le démarrage | Source |
|---|---|---|---|---|
| `lodb_ssr_renders_total` | compteur | `lane` (`pinned`, `current`), `outcome` (`admitted`, `refused`), `reason` sur un refus (`capacity`, `lane_capacity`, `client_capacity`, `lane_rate`, `client_rate`) | oui pour `admitted` (0) | comptes de l'admission (`RenderCounts`), lus à chaque scrape |
| `lodb_ssr_renders_in_flight` | jauge | `lane` | oui | rendus en cours de l'admission |
| `lodb_ssr_render_duration_seconds` | histogramme, bornes 0,1 · 0,25 · 0,5 · 1 · 1,5 · 3 · 5 · 10 · 20 s | `lane`, `status_class` (`2xx`…`5xx`, `aborted` quand le client est parti) | non | de l'admission à la fin de la réponse |
| `lodb_ssr_normalized_redirects_total` | compteur | `reason` : `query` (301 du pré-routeur, paramètres que la page ne lit pas, sans rendu), `canonical` (301/308 d'une page vers son URL canonique : slug, dernier patch dans le chemin, `?version=`) | oui (0) | pré-routeur, réponse du rendu |
| `lodb_ssr_network_gate_refusals_total` | compteur | — | oui (0) | requêtes du port 4000 venues hors du réseau de la stack (`networkGate`) |
| `nodejs_*`, `process_*` | — | `nodejs_eventloop_lag_seconds`, `nodejs_eventloop_lag_p99_seconds`, `nodejs_heap_size_used_bytes`, `nodejs_gc_duration_seconds{kind}`, `process_cpu_seconds_total`… | oui | `collectDefaultMetrics` de `prom-client` |

Un refus d'admission n'écrit pas de ligne de log par requête : la ligne `ssr.admission.summary`
(une par minute de refus) et `lodb_ssr_renders_total` en rendent compte.

## Exemples

```promql
# Part des lectures de catalogue qui ont manqué le cache (10 min)
sum(rate(lodb_catalog_cache_lookups_total{stack="lodb-prod", outcome="miss"}[10m]))
  / sum(rate(lodb_catalog_cache_lookups_total{stack="lodb-prod"}[10m]))

# Refus du SSR par couloir et raison
sum by (lane, reason) (rate(lodb_ssr_renders_total{stack="lodb-prod", outcome="refused"}[5m]))

# p95 d'un rendu du couloir courant
histogram_quantile(0.95, sum by (le) (
  rate(lodb_ssr_render_duration_seconds_bucket{stack="lodb-prod", lane="current"}[10m])))

# Tâches en retard de plus de trois périodes
(time() - max by (task) (lodb_job_last_success_seconds{stack="lodb-prod"}))
  > 3 * max by (task) (lodb_job_period_seconds{stack="lodb-prod"})
```
