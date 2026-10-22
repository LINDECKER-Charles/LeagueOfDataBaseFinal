# Journalisation applicative — la convention

Ce guide fixe ce que l'API et le SSR écrivent dans leurs logs, et sous quelle forme. Le code
fait foi : `src/LoDb.Api/Hosting/Logging/JsonLineConsoleFormatter.cs` pour l'API,
`src/LoDb.Web/src/server/create-logger.ts` pour le SSR ; en cas d'écart, la page se corrige.
Le transport et les requêtes : [`observabilite.md`](observabilite.md). Les métriques :
[`metriques.md`](metriques.md). Décision : [ADR 0010](../reecriture/adr/0010-observabilite-tests-ci.md).

## Où vont les logs

- **stdout, une ligne JSON par événement**, et rien d'autre. Le driver `json-file`
  (`x-logging` de `compose.yaml`) les garde, Vector les collecte ; un fichier n'est jamais lu.
- `api`, `migrate` et `states` (même image) écrivent par `AddLoDbJsonConsole()`
  (`Hosting/Logging/LoggingRegistration.cs`), posé par `AddLoDbCommon` pour l'hôte web comme
  pour les commandes. `web-ssr` et `web-ssr-prerender` écrivent par `createLogger()` (pino).
- `nginx` et `postgres` écrivent du texte, hors de cette convention : nginx ses seules
  erreurs (`error_log … warn`, `access_log off` dans `docker/nginx/nginx.conf`, l'edge
  journalise les requêtes) ; Postgres les requêtes de plus de 500 ms, sans leurs paramètres
  (`log_min_duration_statement`, `log_parameter_max_length=0` dans `compose.yaml`).
- **Vector ne parse pas le JSON applicatif** et devine le niveau par regex sur le texte brut
  ([`observabilite.md`](observabilite.md#le-piège-du-niveau-deviné)). La clé d'événement,
  sous-chaîne exacte de la ligne, est donc le seul point d'agrégation fiable.

## La ligne

| Rôle | API (`api`, `migrate`, `states`) | SSR (`web-ssr`, `web-ssr-prerender`) |
|---|---|---|
| Horodatage UTC | `Timestamp` (`2026-09-26T08:30:15.250Z`) | `time` (ISO 8601) |
| Niveau | `LogLevel` : `Information`, `Warning`, `Error`, `Critical` | `level` : `info`, `warn`, `error` |
| Clé d'événement | `EventName` (`EventId.Name`) | `msg` |
| Corrélation | `trace_id`, `span_id` | `trace_id` |
| Contexte | `State` : les propriétés du gabarit ; `Message` : le gabarit rendu | champs à la racine |
| Exception | `Exception`, seulement s'il y en a une | `exception`, seulement s'il y en a une |

L'API écrit aussi `EventId` et `Category`. Le formateur intégré de .NET perd `EventId.Name`
et range le `trace_id` dans un tableau de scopes : d'où un formateur propre, qui laisse de
côté les autres scopes (celui de la requête porte le chemin). Une ligne du SSR :

```json
{"level":"info","time":"2026-10-22T08:30:15.250Z","method":"GET","status":200,"duration_ms":42,"trace_id":"4bf92f3577b34da6a3ce929d0e0e4736","msg":"http.request.served"}
```

**`trace_id`** : nginx pose un `traceparent` neuf sur chaque requête relayée
(`docker/nginx/snippets/proxy-headers.conf`), le SSR le relaie à l'API. Côté API, il est
présent quand une activité est en cours — une requête HTTP — et absent sinon (tâches de fond,
commandes) ; le test `RecordWithoutExceptionOrActivityCarriesNeitherField` le garde. Côté SSR,
seule `http.request.served` le porte (`request-logging.ts`).

## La clé d'événement

Une clé pointée **`domaine.sujet.resultat`**, en minuscules, segments en `snake_case`,
jamais une phrase. Elle se lit comme un filtre de plus en plus fin : `"prerender."` donne
tout le domaine, `"prerender.pace."` tout le sujet.

```
database.pool.exhausted      catalog.read.failed       jobs.schedule.failed
prerender.ssr.unreachable    seo.indexnow.submitted    http.request.served
```

- La clé est un **littéral du code**, jamais construite à partir d'une donnée. Seule
  exception : le miroir d'audit écrit `audit.<action>` (`audit.user.login_failed`), l'action
  venant de l'énumération fermée `AuditAction`, écrite par `AuditVocabulary`
  (`src/LoDb.Infrastructure/Audit/Journal/AuditMirror.cs`).
- Une clé citée par une alerte ou une recherche ne se renomme pas.
- Ce qui varie va dans le contexte, pas dans la clé. Côté API, le `Message` est rendu avec
  ses valeurs : on cherche par `EventName`, jamais par le texte du message.

## Déclarer un événement

**C#** : une méthode `[LoggerMessage]` d'une classe `partial` — la classe qui journalise, ou
une classe `…Log` du module (`IngestionLog.cs`, `PrerenderLog.cs`) —, avec la clé, le niveau
et un gabarit en anglais. Jamais de chaîne interpolée : elle viderait `State`.

```csharp
[LoggerMessage(
    EventName = "ingest.version.completed",
    Level = LogLevel.Information,
    Message = "Ingested version {Version} in {DurationMs} ms: {Datasets} datasets and"
        + " {Images} images written, promoted {Promoted}.")]
public static partial void VersionCompleted(
    ILogger logger, string version, long durationMs, int datasets, int images, bool promoted);
```

Un niveau qui dépend de la cause passe en paramètre `LogLevel level` : `IngestionLog.cs`
écrit `ingest.version.failed` en `Warning` pour une panne amont (`EgressException`), en
`Error` sinon. Seul `AuditMirror` appelle `logger.Log` directement, sa clé suivant l'action.

**SSR** : le logger de `createLogger()`, objet de champs d'abord, clé en message ; champs en
`snake_case`.

```ts
logger.warn({ interval_seconds: INTERVAL_SECONDS, admitted, refused }, 'ssr.admission.summary');
```

## Les exceptions

L'exception passe **en objet**, jamais son seul message :

- **C#** : un paramètre `Exception exception` de la méthode `[LoggerMessage]`. Le formateur
  écrit `Exception` = `exception.ToString()` (type, message, pile, causes), échappé sur une
  ligne. Ce message est donc écrit : quand il peut citer une donnée personnelle, on n'écrit
  que le type (`{ErrorType} {SqlState}` de `analytics.views.write_failed`). Quand la cause
  est connue et le volume fort, on l'omet : `database.pool.exhausted` ne porte pas de pile.
- **SSR** : `{ exception: error }`. Le sérialiseur de `create-logger.ts` n'en garde que
  `class` (le `name`, les constructeurs étant minifiés) et `code` (`ECONNREFUSED`…), jamais
  le message, qui peut citer l'URL demandée. Jamais `console.error` : la pile multi-lignes
  d'Angular ou d'Express deviendrait autant d'événements sans clé, d'où `SsrErrorHandler`
  et `failureHandler`.

## Aucune clé `error`

Vector classe en `error` toute ligne dont le texte brut contient le mot `error`,
`exception` ou `critical` (`\b…\b`, sans casse). Un champ nommé `error` — un `{Error}` dans
un gabarit C# devient la clé `"Error"` de `State` — ou une valeur libre qui contient ce mot
fait passer un `Warning` pour une erreur, et le tableau de bord ment. Même piège avec
`fatal` et `panic`. Nommer le champ par ce qu'il porte : `{ErrorCode}` de
`outbox.message.retried`, `{ErrorType}` (la regex exige un mot entier).

Le champ d'exception est l'écart assumé : indispensable, il tire la ligne vers `error`. Un
`Warning` qui porte une exception (`jobs.schedule.failed`, `ingest.listener.failed`) sort
donc en `error` dans Grafana. C'est pourquoi le formateur ne l'écrit que s'il existe, et
pourquoi **aucune alerte ne se construit sur le niveau** : on alerte sur la clé.

## Les niveaux

| Niveau | Quand | Dans le code |
|---|---|---|
| `Critical` | une fonction est cassée jusqu'à ce qu'un humain agisse, rien ne se répare seul | `billing.webhook.unconfigured` (503 à chaque événement Stripe), `dataprotection.certificate.missing`, `audit.journal.write_failed` |
| `Error` | la cause est chez nous ou inconnue | `database.server.unavailable` (500), `jobs.run.failed`, `fetch.allowlist.refused`, `ssr.render.failed` |
| `Warning` | dégradé mais attendu : panne amont transitoire, délestage ; le volume fait signal | `database.pool.exhausted` (503), `catalog.read.failed` (Data Dragon), `prerender.ssr.unreachable`, `fetch.batch.degraded` |
| `Information` | un changement d'état réussi, à retrouver après coup, ou la synthèse d'un lot | `seo.indexnow.submitted`, `ingest.version.completed`, `jobs.run.succeeded` |
| `Debug` | jamais émis en prod (`Default: Information`, `appsettings.json`) | `http.request.rejected`, `CommandExecuted` d'EF Core (`LoDbDataSource.cs`) |

- **Un 4xx n'est pas une erreur** : c'est la requête du client qui est en cause.
  `http.request.served` sort en `info` sous 500, en `warn` pour un 503 (délestage voulu), en
  `error` pour les autres 5xx (`request-logging.ts`). Une requête illisible
  (`BadHttpRequestException`) donne `http.request.rejected`, en `Debug`.
- Le miroir d'audit sort en `Information` pour un succès, en `Warning` sinon.

## Une ligne de synthèse par lot

Jamais une ligne par URL, par fichier ou par page : une par lot, avec ses comptes.

| Clé | Une ligne par |
|---|---|
| `prerender.unit.completed` | (version, langue) de la passe de précalcul |
| `prerender.demand.drained` | lot de la file automatique |
| `prerender.revision.drained` | lot de la file des états clos ; `prerender.revision.enqueued` par passage du producteur |
| `states.update.completed` | mise à jour de l'index des états (commande, promotion, réconciliation), avec les comptes par ressource ; `states.update.progress` toutes les 50 versions d'une construction |
| `seo.indexnow.submitted` | version soumise (`{Urls}`, `{Requests}`) ; sans clé IndexNow, `seo.indexnow.planned` (URL courtes, pages de patch, ouvreurs, rien d'envoyé) |
| `fetch.batch.degraded` | lot de téléchargements (`{Failed} of {Batch}`) |
| `web.csp.violated`, `ssr.admission.summary` | intervalle (une minute) |

Un rendu refusé par l'admission et une page réussie de la passe n'ont pas de ligne
`http.request.served` : les synthèses et `lodb_ssr_renders_total` les comptent.

## Aucune donnée personnelle

Ni adresse IP, ni e-mail, ni token, ni mot de passe, ni texte saisi, ni chemin ou query :
un chemin peut porter un nom d'utilisateur (`/u/…`) ou un jeton de partage (`/b/…`). On
écrit un identifiant interne (`{AdminId}`, `ActorId`) qui rejoint la donnée là où elle est
conservée. Le miroir d'audit part d'une liste autorisée : ni l'adresse ni l'identifiant tapé
d'une connexion échouée n'y passent (`AuditMirror.cs`).

## Cardinalité

Les champs de flux (`stack`, `service`, `container`, `stream`) sont posés par Vector : le
code n'en ajoute aucun. Le jeu des clés reste fermé, pour que `stats by` sur une clé ait un
sens ; une version, une langue ou un identifiant va dans un champ, jamais dans la clé. Pour
les labels de métriques, la règle est plus stricte : [`metriques.md`](metriques.md#règle-de-cardinalité).

## Interroger et tester

```logsql
stack:lodb-prod service:api _time:24h "catalog.read.failed" | stats count() lignes
```

Autres requêtes : [`observabilite.md`](observabilite.md#interroger). Les tests vérifient
le comportement, pas le texte : la clé et le niveau émis sur un chemin d'échec
(`log.Id.Name == "jobs.run.failed"` avec `FakeLoggerProvider`, `PeriodicJobTests.cs`), la
forme de la ligne (`JsonLineConsoleFormatterTests.cs`, `request-logging.spec.ts`).
