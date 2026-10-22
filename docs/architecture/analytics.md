# Mesure d'audience et analytics

Comment le site compte ses pages vues : la mesure maison (miroir nginx, balise, Postgres) et
l'audience first-party d'Umami. Font foi le code cité et, pour un hôte,
[`configuration.md`](../guides/configuration.md) ; décisions : ADR
[0003](../reecriture/adr/0003-ingestion-proactive-et-taches-de-fond.md),
[0004](../reecriture/adr/0004-stockage-etat-et-scaling.md),
[0009](../reecriture/adr/0009-identite-authentification-sessions.md),
[0012](../reecriture/adr/0012-audience-umami.md) (Umami).

## Vue d'ensemble

| Mesure | Capture | Stockage | Lue par |
|---|---|---|---|
| Maison | miroir nginx (première page) + balise `POST /api/analytics/view` (navigations internes) | `analytics_event` → `analytics_daily` | panneaux `/admin`, `/v1/trends` avant la date de bascule |
| Umami (prod seule) | le front poste `/u/api/send`, routé par l'edge vers l'Umami partagé | base d'Umami (`infra-vps`), tirée dans `audience_entity_daily` | tableau `stats.league-of-data-base.com`, `/v1/trends` à partir de la date de bascule |

Les deux tournent côte à côte (`core/analytics/provide-navigation-beacon.ts`) ; le code
présente Umami comme appelé à remplacer la mesure maison (`AnalyticsRegistration.cs`,
`AnonymousEndpointsTests.cs` : les deux captures « partent avec les analytics maison »).

## Capture maison

**Miroir de nginx.** `docker/nginx/snippets/analytics-mirror.conf` copie chaque requête de
page vers `/_analytics/page`, avant la lecture du cache : une page servie du cache compte comme
une autre. Il est inclus dans `location /` (`sites/site.conf`) et `@lodb_pinned_ssr`
(`snippets/pages-store/pages.conf`). `server.d/analytics.conf` (location `internal`) filtre
avant tout appel : autre méthode que `GET`, ou chemin hors de
`/{locale}[/{ressource}[/{fiche}]]` sans point, renvoient 204. Une version épinglée
(`/fr/16.1.1/items`) a un point : **le miroir ne la compte jamais**. Le reste part en `POST`
vers `/internal/analytics/page` (hors de `/api`, aucune location publique n'y mène), chemin
dans `X-Original-URI`, sans corps, `Cookie` et `Authorization` vidés, délais d'une seconde.

**Balise du routeur.** `core/analytics/navigation-beacon.ts` poste par
`navigator.sendBeacon` `{"path": …}` (`text/plain`, sans préflight) à la fin de chaque
navigation qui change de page, sauf la première, déjà vue par le miroir. Web seulement, rien
si le visiteur a refusé la mesure (`AudienceOptOut`).

**Deux exceptions voulues à « Jamais `AllowAnonymous` »** ([`AGENTS.md`](../../AGENTS.md),
ADR 0009). `MirrorEndpoint.cs` et `BeaconEndpoint.cs` sont `.AllowAnonymous()` : toute
politique porte le garde XSRF + `Origin` (`Hosting/AuthorizationPolicies.cs`) et
`sendBeacon` ne peut pas joindre le jeton XSRF. En contrepartie :

- la balise vérifie elle-même l'`Origin` (`TrustedOrigins.Allows` : le site, l'origine
  publique configurée, l'app Android ; pas d'`Origin` = pas une requête cross-site, `null`
  refusé → 403 `origin-mismatch`) et est limitée à 120 vues/min par adresse
  (`BeaconRateLimit.cs`) ; corps de 4 Kio au plus (`BeaconBody.cs`) ;
- le miroir n'est joignable que par nginx ;
- `tests/LoDb.Api.Tests/Hosting/AnonymousEndpointsTests.cs` ferme la liste à ces deux routes.

Les deux passent par `ViewIntake.cs`, sans I/O : la vue brute (instant, origine, cible, hôte,
adresse, user-agent, referer) va dans `PageViewQueue`, `Channel` borné ; pleine, la vue est
perdue et comptée (`reason="queue_full"`).

## Ce qui est compté

`CatalogPageResolver.cs` et `PageAddressParser.cs` appliquent les règles du front
(ADR 0005) hors de la requête :

| Compté | Pas compté |
|---|---|
| accueil (son `?version=` est gardé en colonne) | version épinglée dans le chemin, fichiers, autres pages du site |
| listes et fiches de la dernière version | `?version=` listée sur une liste ou une fiche (301), fiche hors chemin canonique (301) |
| fiche absente (404), catalogue illisible ou aucune version (503) : comptées avec leur statut | adresse non résolue (erreur) : perdue, comptée `unresolved` |

Les noms de routes sont ceux de l'ancienne stack (`TrackedRoutes.cs` : `app_champion`…), pour
que les agrégats importés et les nouveaux s'additionnent. **Robots** : `UserAgentParser`
marque `is_bot` ; la ligne est écrite, mais l'agrégation ne la compte que dans `botViews`, hors
de tout autre compteur (`DailyFold.cs`). Une navigation interne est classée referer
`internal` sur l'hôte du site (`ViewEventFactory.cs`).

## Stockage (Postgres, ADR 0004)

| Table | Contenu | Durée |
|---|---|---|
| `analytics_event` | une vue par ligne : route, chemin, type, kind, entité, statut, version, lang, locale, `visitor`, navigateur, OS, appareil, `is_bot`, referer (hôte, source), pays | 13 mois (`EventRetentionMonths`) |
| `analytics_daily` | un jour UTC, en quatre `jsonb` (`totals`, `buckets`, `visitors`, `country_names`), `source` `events` ou `import` | non purgée |
| `audience_entity_daily` | (jour, `{type}:{clé}`) → `views`, `visitors` tirés d'Umami | non purgée |

`analytics_event` est partitionnée par jour UTC (`analytics_event_yyyyMMdd`), créée en SQL brut
par `20260926185409_Lot6BillingAnalyticsApps.cs`. Une vue hors partition est refusée : la
rétention crée les partitions d'hier à J+7 (`PartitionsAhead`), `PageViewBatchWriter.cs` crée
celle qui manque puis recopie une fois. L'API (`lodb_app`) ne possède pas la table : les
partitions passent par `lodb_create_analytics_partition` / `lodb_drop_analytics_partition`
(`SECURITY DEFINER`, `Persistence/Roles/schema-privileges.sql`, [`pieges.md`](pieges.md)).
`api analytics import --source <dossier> [--dry-run]` reprend les agrégats de l'ancienne stack
en lignes `import`, que le rollup ne touche jamais (`AnalyticsDailySource.cs`).

## Traitements (ADR 0003)

| Tâche | Rythme | Rôle |
|---|---|---|
| `AnalyticsWriterWorker` | continu, chaque instance | à la première vue en file, attend `BatchWindow` (2 s) puis écrit par `COPY` binaire de `BatchSize` (500) ; à l'arrêt, vide la file dans le délai d'arrêt de l'hôte |
| `analytics.rollup` (`AnalyticsRollupJob`) | 5 min, une instance | replie aujourd'hui et les jours clos dont l'agrégat date d'avant leur clôture + 10 min (`AnalyticsRollup.ClosingGrace`) |
| `analytics.retention` (`AnalyticsRetentionJob`) | 1 jour, une instance | crée les partitions à venir, supprime celles de plus de 13 mois, efface `ip`/`user_agent` résiduels |
| `audience.trends` (`AudienceTrendsWorker`) | `PullPeriod` (1 h), une instance | tire Umami (voir plus bas) ; en sommeil sans configuration |

Un lot illisible ou non écrit est perdu et compté (`lodb_analytics_views_dropped_total`,
[`metriques.md`](../guides/metriques.md)), jamais relancé ; les logs ne portent que des
comptes, jamais un chemin, une adresse ou un user-agent (`PageViewPump.cs`). Rollup et
effacement relèvent leur délai de requête à `JobStatementTimeout` (5 min). Un agrégat borne
visiteurs (`MaxVisitorsPerDay`) et hôtes référents (`MaxRefererHostsPerDay`, puis `(autres)`).

## Consommateurs

**Administration** (`features/admin/panels/analytics/`, rôle `Admin` + TOTP, ADR 0009).
`/admin` (vue d'ensemble), `/admin/traffic` et `/admin/audience` lisent
`GET /api/admin/analytics/report?range=7d|30d|90d|all` (`ReportEndpoint.cs`) : les jours clos
depuis `analytics_daily`, aujourd'hui replié à la volée depuis `analytics_event`
(`AnalyticsReports.cs`). « Consolider » appelle `POST /api/admin/analytics/rollup`, journalisé
`admin.analytics_rollup`. Réponses `no-store`. `/admin/storage` lit `/api/admin/storage`.

**`/v1/trends/{type}`** (`TrendsSource.cs`) : les 25 entités les plus vues sur 7 ou 30 jours,
mises en cache 5 min par instance (`TrendsLifetime`). Source par jour :

| `LoDb:Audience:TrendsSwitchDate` | Jours avant la date | Jours à partir de la date |
|---|---|---|
| absente (défaut) | `analytics_daily.buckets -> 'entities'` | — |
| posée (`aaaa-mm-jj`) | `analytics_daily` | `audience_entity_daily`, chaque jour d'une entité plafonné à `ClipFactor` (3) × sa médiane sur `ClipWindowDays` (28) jours |

Le plafond existe parce que la collecte d'Umami est publique, donc gonflable
(`AudienceOptions.cs`). Retirer la date revient à la mesure maison seule.

## Umami

**Branchement** (prod seule : `compose.umami.yaml`, [`configuration.md`](../guides/configuration.md)
§ 4.6, [`pieges.md`](pieges.md)). Les labels de nginx déclarent le site à l'Umami partagé
d'`infra-vps` : `caddy_0.import: umami` fait router `/u/script.js` et `/u/api/send` par
l'edge, `caddy_2` publie le tableau sur `stats.league-of-data-base.com`. L'UUID du site est un
littéral, dans le calque et `core/analytics/audience/audience-site.ts`, vérifié par
`tools/umami/test`. Le staging n'a pas ce calque.

**Ce que mesure le front** (`audience-tracker.ts`, `send/audience-sender.ts`) : une vue par
changement de page, première comprise, les événements `page-error`, `search` et
`build-share`, avec locale, version et plateforme. `fetch` brut sans cookies ni referer,
jamais le script d'Umami. Rien ne part avant un signe de présence (geste, ou dix secondes
visibles : `presence/audience-presence.ts`), ni hors de l'hôte canonique, ni hors web, ni sous
GPC, `navigator.webdriver` ou refus enregistré depuis la politique de confidentialité
(`send/audience-opt-out.ts`). Pages de compte et d'admin : rien ; autres pages hors listes et
fiches : leur gabarit, jamais leur adresse (`audience-route-data.ts`).

**Tirage** (`Infrastructure/Audience/`). Le client HTTP `umami` (`AudienceRegistration.cs`)
ne joint que l'hôte configuré, en https (`UmamiHostHandler`), sans redirection, cookie ni log
de requête. Chaque passage se connecte avec le compte en lecture seule et réécrit en entier les
`RefreshDays` (2) derniers jours UTC de `audience_entity_daily`, en ne gardant que les fiches
de la dernière version, résolues par le même `ITrackedPageResolver` que la mesure maison
(`AudiencePull.cs`) ; un catalogue illisible arrête le passage sans écraser un jour. La jauge
`lodb_audience_vues_tirees` porte les vues de la dernière heure close. Sommeil tant que `Host`,
`WebsiteId`, `Username` et `Password` ne sont pas tous posés (`audience.trends.dormant`).

## Vie privée

- **Jamais stockés : l'adresse IP et le user-agent.** Ils servent à la capture (empreinte,
  navigateur, pays) ; `AnalyticsEventWriter.cs` ne copie pas les colonnes `ip` et
  `user_agent`, gardées nullables jusqu'à l'étape *contract* (`AnalyticsEvent.cs`). Les lignes
  d'avant ce changement voient les leurs effacées à 30 jours (`ClientDataRetention`).
- `visitor` : HMAC-SHA256 de `{adresse}|{user-agent}` tronqué à 16 hexa, clé
  `LoDb:Analytics:VisitorKey` (l'`APP_SECRET` de l'ancienne stack : mêmes visiteurs des deux
  côtés) ; sans clé, une clé par processus, visiteurs recomptés au redémarrage
  (`VisitorHasher.cs`). Les agrégats gardent ces empreintes (`visitors`) sans limite de durée.
- Vues brutes : 13 mois (`EventRetentionMonths`, 25 au plus, plafond CNIL d'après
  `AnalyticsOptionsValidator.cs`). Le miroir est côté serveur : il ne connaît ni l'opt-out ni
  GPC, que la balise et Umami respectent.
- **Pays** : `MaxMindGeoLocator.cs` lit `LoDb:Analytics:GeoIpDatabase`, à défaut
  `GEOIP_DB_PATH` (`AnalyticsOptions.LegacyGeoIpKey`). Aucun hôte n'en monte aujourd'hui
  ([`configuration.md`](../guides/configuration.md) § 8) : aucune vue n'a de pays.

## Réglages d'un opérateur

Valeurs et défauts : [`configuration.md`](../guides/configuration.md) § 4.2, 4.3 et 4.6 ;
`LoDb:Analytics` et `LoDb:Audience` sont validés au démarrage. Secrets :
`LoDb__Analytics__VisitorKey`, `LoDb__Audience__Username`, `LoDb__Audience__Password`. Posés par
`compose.umami.yaml` : `LoDb__Audience__WebsiteId`, `LoDb__Audience__Host`. Dans le `.env` de la
prod : `LoDb__Audience__TrendsSwitchDate`.
