# 📡 Observabilité — plan d'implémentation (23 août 2026)

> **Constat.** La chaîne d'observabilité (Vector → VictoriaLogs → Grafana) est déployée
> par le dépôt d'infrastructure `infra-vps` et capte **déjà** tous les conteneurs de
> l'hôte. Le problème est ailleurs : l'application n'a quasiment rien à y verser, et le
> peu qu'elle produit, sa configuration de production le détruit avant `stderr`.
> Mesuré sur `lodb-php-1` : **2 348 lignes de stdout, dont zéro applicative** —
> l'intégralité est l'access-log de PHP-FPM.
>
> Ce document est le plan de correction, en cinq lots livrables séparément.
> Le fonctionnement durable de la chaîne et les requêtes vivent dans
> [`../guides/observabilite.md`](../guides/observabilite.md) ; la convention d'écriture
> des logs dans [`../guides/logging.md`](../guides/logging.md).

## Les quatre verrous

| # | Verrou | Preuve | Lot |
|---|---|---|---|
| 1 | `fingers_crossed(action_level: error)` sans `passthru_level` : rien n'est émis tant qu'aucun ERROR ne survient, puis le buffer est **jeté**. 14 des 17 appels existants n'atteignent `stderr` que si un ERROR survient dans la même requête. | `app/config/packages/monolog.yaml:44-48` | 1a |
| 2 | `display_errors = 1`, `log_errors = 0` : une erreur PHP hors ErrorHandler Symfony part au visiteur et n'existe nulle part. | image : `Loaded Configuration File: (none)` | 1a |
| 3 | Le stdout du conteneur `php` est 100 % de l'access-log FPM — chemin toujours `/index.php`, IP de nginx, ni durée ni mémoire. Aucun `slowlog`. | `docker/php/php-fpm.conf` | 1a |
| 4 | L'application ne logge pas : 8 classes sur 269 injectent un logger, 5 blocs `catch` sur 76 tracent quelque chose. | grep exhaustif sur `app/src/` | 3 |

**L'ordre des lots n'est pas cosmétique** : tant que le lot 1a n'est pas fait, tout log
ajouté par le lot 3 serait détruit avant `stderr`.

## Lot 1a — ouvrir le robinet côté PHP

> Sans ce lot, **tout log ajouté par les lots suivants est détruit avant `stderr`**.
> C'est le préalable à tout le reste.

Aujourd'hui le conteneur `php` n'émet que l'access-log de PHP-FPM — mesuré :
~2 350 lignes, **zéro ligne applicative**. Les seules exceptions sont les 8 lignes
`NOTICE: fpm is running` / `ready to handle connections` du démarrage, qui survivront
à `access.log = /dev/null` : leur présence n'est pas un échec du lot.

### `app/config/packages/monolog.yaml`

Le bloc `when@prod` est la recette Symfony par défaut : `fingers_crossed` avec
`passthru_level` non défini, donc le buffer est **jeté** à la fin de chaque requête
sans erreur.

D'abord déclarer les canaux métier en tête de fichier — ils servent au **routage**
(contourner le buffer), pas à la recherche : Vector ne voit pas `channel` comme un
champ indexé.

```yaml
monolog:
    channels:
        - audit    # mirror of the legal audit journal (var/state/audit/events)
        - billing  # money path: checkout, webhook, entitlements, donations
        - catalog  # Data Dragon read path: resource pages, search, fetch gateway
        - ingest   # writes into object storage: loader warm, deferred images
        - mail     # outbound delivery
        - deprecation # Deprecations are logged in the dedicated "deprecation" channel when it exists
```

Puis remplacer intégralement le bloc `when@prod` :

```yaml
when@prod:
    monolog:
        handlers:
            # Curated business events, always on, down to INFO. A successful money
            # or ingest event is worth keeping even when nothing failed, and these
            # channels carry no framework chatter — their volume is bounded by the
            # number of log calls we wrote, not by traffic.
            business:
                type: stream
                path: php://stderr
                level: info
                formatter: monolog.formatter.json
                # process_psr_3_messages stays off: our event keys carry no
                # {placeholder}, so an accidental interpolation shows up as a
                # visible literal instead of silently rewriting the aggregation key.
                process_psr_3_messages: false
                channels: [audit, billing, catalog, ingest, mail]

            # Always-on operational stream for everything else. NOTICE is the cut
            # point: below it the framework channels alone would add ~7 records per
            # request (http_client accounts for most of them). From NOTICE up every
            # record is signal, and it leaves immediately instead of waiting for an
            # unrelated error to unlock a buffer that is otherwise discarded.
            # Business channels are excluded — `business` above already emits them,
            # and a shared record would be written twice.
            stderr:
                type: stream
                path: php://stderr
                level: notice
                formatter: monolog.formatter.json
                # Same reason as `business`: monolog-bundle enables this processor by
                # default on any non-nested handler, and it would silently rewrite an
                # event key that accidentally contains a {placeholder}.
                process_psr_3_messages: false
                channels: ["!deprecation", "!audit", "!billing", "!catalog", "!ingest", "!mail"]

            # Pre-error context, replayed only when a request actually fails. The
            # filter below caps the replay at INFO so the handlers partition the
            # levels (>= NOTICE above, < NOTICE here) and no record is ever written
            # twice. passthru_level stays unset for a different reason: it would emit
            # the INFO context on EVERY request, error or not, which is exactly the
            # volume the buffer exists to avoid.
            error_context:
                type: fingers_crossed
                action_level: error
                handler: error_context_filter
                excluded_http_codes: [404, 405]
                buffer_size: 100
                channels: ["!event", "!deprecation", "!audit", "!billing", "!catalog", "!ingest", "!mail"]

            # monolog-bundle removes any handler referenced through `handler:` from
            # the channel map, so error_context cannot point at `stderr` directly —
            # doing so would silently unregister the always-on stream above.
            error_context_filter:
                type: filter
                min_level: debug
                max_level: info
                handler: error_context_stream

            error_context_stream:
                type: stream
                path: php://stderr
                level: debug
                formatter: monolog.formatter.json
                process_psr_3_messages: false

            # Symfony logs deprecations at INFO on this channel. Pinned rather than
            # left at DEBUG, and excluded from every handler above so a deprecation
            # raised during a failing request is not replayed a second time.
            deprecation:
                type: stream
                path: php://stderr
                level: info
                formatter: monolog.formatter.json
                process_psr_3_messages: false
                channels: [deprecation]

            console:
                type: console
                process_psr_3_messages: false
                channels: ["!event", "!doctrine", "!deprecation"]
```

Le câblage qui en résulte, vérifié par compilation du conteneur :

```
audit / billing / catalog / ingest / mail  →  console, business
app / request / security / …               →  console, error_context, stderr
```

> **Contrepartie assumée.** Un `critical` sur `billing` n'**active pas**
> `error_context` : une panne de webhook Stripe arrive donc sans contexte pré-erreur.
> C'est le prix de l'émission inconditionnelle. Si ce contexte devient nécessaire,
> retirer le canal concerné de la liste d'exclusion d'`error_context` — au prix d'un
> doublon sur les enregistrements ≥ INFO de ce canal.
>
> En CLI, `console` et `stderr` sont tous deux poussés sur les canaux non métier : une
> commande écrit donc chaque NOTICE+ deux fois, en JSON et en texte. Comportement
> pré-existant, conservé ; la garantie « aucun doublon » ci-dessus vaut pour le web.

### `app/config/packages/framework.yaml` — obligatoire avec le patch ci-dessus

Symfony journalise **toute** `HttpException` de statut < 500 au niveau `ERROR` sur le
canal `request` (`ErrorListener::resolveLogLevel()`). Aujourd'hui ces enregistrements
sont avalés par `excluded_http_codes: [404, 405]` du `fingers_crossed`. Le nouveau
handler always-on ne passe pas par ce filtre : **sans la correction ci-dessous, chaque
sonde de bot (`/wp-login.php`, `/.env`) écrirait une ligne `ERROR` conservée 90 jours**,
et le niveau `error` de Grafana deviendrait du bruit de crawler.

Ajouter à la fin du fichier :

```yaml
when@prod:
    framework:
        # Symfony logs any HttpException below 500 at ERROR on the `request`
        # channel. With an always-on NOTICE+ handler, every crawler probe would
        # otherwise land in a 90-day log store. INFO keeps them off the live
        # stream while still replaying them as context around a real error.
        exceptions:
            Symfony\Component\HttpKernel\Exception\NotFoundHttpException:
                log_level: info
            Symfony\Component\HttpKernel\Exception\MethodNotAllowedHttpException:
                log_level: info
            # The four IP rate limiters declared in this same file throw a 429, which
            # ErrorListener also logs at ERROR. A throttled bot would otherwise write
            # an ERROR record per attempt — the very noise this block exists to stop.
            # NOTICE, not INFO: unlike a 404 this one is worth seeing on the stream.
            Symfony\Component\HttpKernel\Exception\TooManyRequestsHttpException:
                log_level: notice
```

Les **403** restent délibérément visibles : sur un site public avec un pare-feu
`/admin`, un refus d'accès est un signal, pas du bruit.

> À savoir : `excluded_http_codes: [404, 405]` fait jeter le buffer de contexte sur une
> 404 **même si un ERROR applicatif est survenu pendant la requête**. C'est le
> comportement d'origine, conservé ; le handler always-on, lui, émettra bien cet ERROR.

### `docker/php/php.ini`

Mesuré dans l'image : `Loaded Configuration File: (none)`, `display_errors = 1`,
`log_errors = 0`. Une `E_WARNING` ou une fatale hors ErrorHandler Symfony est donc
**écrite dans le corps de la réponse HTTP et nulle part ailleurs** — vérifié par une
requête FPM réelle, chemins absolus et trace servis au visiteur avec un statut 200.

Insérer après `date.timezone = UTC` :

```ini
; Engine-level error net. The image activates no main php.ini and this file (loaded
; as conf.d/zz-app.ini) set none of these directives, so PHP fell back to its compiled
; defaults: errors echoed into the HTTP response and written nowhere.
; Symfony's ErrorHandler owns runtime errors, but only once the kernel has booted;
; these directives cover everything outside it (bootstrap, OOM, fatal, shutdown).
display_errors = Off
display_startup_errors = Off
log_errors = On
error_log = /proc/self/fd/2
error_reporting = E_ALL
; Arguments are printed verbatim in an engine stack trace, land on the container
; stream and are kept 90 days: a DSN or a password would be readable there.
zend.exception_ignore_args = On
```

Et dans `docker/php/php-dev.ini`, pour ne pas perdre les arguments dans la page
d'exception et le profiler en local :

```ini
zend.exception_ignore_args = Off
```

### `docker/php/php-fpm.conf`

> ⚠️ **Fusion, pas remplacement.** Ajouter la section `[global]` **en tête** du fichier,
> au-dessus du `[www]` existant, et les trois directives suivantes **dans** ce `[www]`.
> Ne rien supprimer : un `[www]` privé de ses `pm.*` fait échouer FPM au démarrage
> (`ALERT: [pool www] the process manager is missing`), et surtout la perte de
> `catch_workers_output = yes` annulerait **tout le lot** — le `stderr` des workers ne
> remonterait plus au flux du conteneur.

```ini
; A pool file may reopen [global]; this one is parsed after the base image's
; docker.conf, so the value below replaces its log_limit = 8192. Past the limit
; php-fpm WRAPS a worker line across several physical lines with no marker,
; splitting one JSON record into unparsable fragments.
[global]
log_limit = 65536

[www]
; The base image points access.log at /proc/self/fd/2, so every request adds a
; "GET /index.php" line to the container stream. php-fpm has no "off" switch and
; refuses to start on an empty value; /dev/null is the only way to silence it.
access.log = /dev/null

; Slow-request detector. 5s is an order of magnitude above a warm render and well
; under max_execution_time = 60, so a stuck request surfaces long before it is
; killed. The "executing too slow" warning goes to the pool error log, i.e. the
; container stream.
slowlog = /proc/self/fd/2
request_slowlog_timeout = 5s
```

Les trois valeurs ont été testées dans l'image : `access.log =` (vide) fait **refuser
le démarrage** de php-fpm, la directive absente laisse le bruit, `/dev/null` est la
seule qui fonctionne.

> **À vérifier au premier déploiement.** L'avertissement `executing too slow` sort de
> façon certaine. Le **backtrace**, lui, dépend de `ptrace` : master et worker tournent
> sous le même uid et le worker est un descendant du master, donc il peut aboutir sans
> `CAP_SYS_PTRACE` — mais en cas d'échec FPM émet une ligne `ERROR: failed to ptrace`
> qui, elle, fera basculer `.level` en `error` via la regex de Vector. Regarder le flux
> après la première requête lente réelle.

---

## Lot 1b — couper le bruit et borner la rétention

### `docker/nginx/nginx.conf`

L'access-log nginx est intégralement redondant avec le journal de l'edge, et de
qualité strictement inférieure : derrière le proxy, `$remote_addr` vaut l'IP du
conteneur Caddy, jamais celle du visiteur. Il est en outre le vecteur qui permet à
**n'importe quel visiteur de piloter le champ `.level`** en demandant une URL
contenant le mot `error`.

```nginx
    # No per-request access log. Every request reaching this container has already
    # crossed the shared edge proxy, which journals it centrally with the REAL
    # client IP, geolocation, status and latency.
    #
    # Must be `off`, NOT a deleted directive: nginx's compiled-in default is
    # `access_log /var/log/nginx/access.log combined`, so dropping the line would
    # silently keep logging in another format instead of stopping it.
    access_log  off;
```

Le `log_format main` devient du code mort et disparaît avec. L'`error_log` reste tel
quel : `/var/log/nginx/error.log` est un symlink vers `/dev/stderr` dans
`nginx:1.30-alpine`, donc il est déjà collecté.

> **Perte assumée en dev.** Il n'y a pas de Caddy en local : `access_log off` supprime
> donc toute trace de requête sur le poste de développement. Pour un débogage ponctuel,
> réactiver la directive temporairement.

### `compose.yaml` — borner la rétention locale

```yaml
# Bounded local log retention for every container of the stack.
#
# json-file is not a style choice: the host's collector reads container output
# through the Docker Engine API, which can only replay logs for the json-file,
# local and journald drivers. Any forwarding driver (syslog, fluentd, gelf, none)
# makes a container unreadable there, and the loss is silent.
#
# Deliberately redundant with the host's /etc/docker/daemon.json: this repo does
# not provision the VPS, so it cannot assume the daemon default is bounded.
x-logging: &default-logging
  driver: json-file
  options:
    max-size: "10m"
    max-file: "3"
```

Puis `logging: *default-logging` juste après chaque clé `restart:` — **y compris sur
`minio-init`** : son script boucle `until mc alias set …; do echo …; sleep 2; done`
sans timeout, soit ~43 000 lignes/jour si MinIO ne répond jamais. C'est le producteur
de logs le plus susceptible de s'emballer de tout le stack.

---

## Lot 2 — honorer le contrat de l'infra

### Limites mémoire — dans `compose.deploy.yaml`, jamais `compose.yaml`

`compose.yaml` est aussi le fichier du dev, où l'on lance `composer install` et
`npm ci` en conteneur : une limite à 1 Gio y transformerait une installation de
dépendances en OOM sans rapport avec la production.

Sans limite, cAdvisor publie `container_spec_memory_limit_bytes = 0`, que la garde
`> 0` des règles écarte : **deux alertes d'infra-vps sont structurellement inertes**,
`ConteneurProcheDeSaLimiteMemoire` et `ConteneurMemoireSaturationPrevue`.
`ConteneurTueParOom`, qui compte `container_oom_events_total`, reste armée sans limite.

| Service | `limits.memory` | Raison |
|---|---|---|
| `php` | 1g | `memory_limit` 256M × `pm.max_children` 20 ≈ 5 Gio de pire cas théorique |
| `nginx` | 256m | Proxy statique + FastCGI : ne retient jamais un dataset |
| `go-fetcher` | 768m | `MAX_CONCURRENCY` 16 × `DefaultMaxBodyBytes` 32 MiB = 512 MiB borné mais réel |
| `go-api` | 256m | Lit un objet complet par requête ; le plus gros mesuré fait 1,3 MiB |
| `minio` | 1g | Buffers par objet en vol + rafales d'écriture à l'ingestion |
| `postgres` | 512m | `shared_buffers` 128 MiB + connexions |

> **Valeurs de départ.** La règle est `limite = 2 × le pic observé`. Relever le pic
> après une semaine d'historique :
> `max_over_time(container_memory_working_set_bytes{stack=~"lodb-.*"}[7d])`.

Pas de plafond `cpus` : trop bas, il bride silencieusement et déclenche
`ConteneurCpuBride` — des lenteurs qu'aucun graphe de charge d'hôte ne montre.

### Healthcheck php réel

`["CMD","php","-r","exit(0);"]` prouve seulement que le binaire CLI existe : le
conteneur resterait `healthy` avec un master FPM mort. FPM parle FastCGI sur `:9000`,
jamais HTTP — `wget` ne peut donc pas atteindre `ping.path`.

`cgi-fcgi` est **absent** de `php:8.5-fpm-alpine` (vérifié). Le paquet alpine `fcgi`
pèse 36 Kio sur une image de 38 Mio, pour une seule dépendance musl :

Dans l'étage **`php_base`** — placé dans l'étage `app`, après `USER www-data`, le
`apk add` échouerait :

```dockerfile
# cgi-fcgi speaks FastCGI, the only protocol FPM answers on :9000 — no HTTP client
# can reach ping.path, so the container healthcheck needs this binary.
RUN apk add --no-cache fcgi
```

```yaml
    healthcheck:
      # Answered by a CHILD process: a pass proves a worker accepted and ran a
      # request, not merely that the master bound the socket. The body must be
      # matched — cgi-fcgi also exits 0 on an FPM "File not found".
      test: ["CMD-SHELL", "SCRIPT_NAME=/ping SCRIPT_FILENAME=/ping REQUEST_METHOD=GET cgi-fcgi -bind -connect 127.0.0.1:9000 | grep -q pong"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 30s
```

Et dans `php-fpm.conf`, pour que la sonde ne réintroduise pas le bruit qu'on vient de
supprimer :

```ini
access.suppress_path[] = /ping
```

### `start_period` sur les cinq healthchecks

Sans lui, chaque échec de la phase de démarrage compte comme un échec réel : les
conteneurs passent transitoirement `unhealthy` à chaque déploiement — précisément au
moment où l'on regarde l'état de la stack. `php` 30s (l'entrypoint réchauffe le cache
avant d'exécuter FPM), `nginx` 10s, `go-fetcher` 5s, `go-api` 15s, `postgres` 30s.

### Mailpit hors des environnements servis

```yaml
  mailer:
    # Dev-only SMTP sink. Kept out of every served environment: it accepts and
    # swallows mail, so a staging/prod stack falling back to it would silently
    # drop account emails instead of failing loudly.
    profiles: ["dev"]
```

> ⚠️ **À faire dans le même lot, sous peine de casser l'envoi de mail.**
> `compose.yaml` pose `MAILER_DSN: ${MAILER_DSN:-smtp://mailer:1025}`. Une fois le
> service derrière un profil, ce défaut résoudrait vers un hôte injoignable. Rendre la
> variable obligatoire dans `compose.deploy.yaml` :
> `MAILER_DSN: ${MAILER_DSN:?MAILER_DSN is required on a served environment}`,
> et poser `COMPOSE_PROFILES=dev` dans le `.env` de développement **ainsi que dans
> `.env.example`** — `.env` est git-ignoré, sans quoi tout nouveau clone perd Mailpit
> sans le moindre message.

---

## Lot 3 — poser les logs applicatifs PHP

C'est le chantier de fond : 8 classes sur 269 injectent un logger, 5 blocs `catch` sur
76 tracent quelque chose. **La convention d'écriture vit dans
[`logging.md`](../guides/logging.md)** — la lire avant d'écrire la première ligne.

Points d'insertion, par valeur décroissante. Le comportement fonctionnel reste
**inchangé** partout : on ajoute un log, on ne touche pas au flux de contrôle.

| Fichier | Ce qui est muet aujourd'hui | Clé d'événement |
|---|---|---|
| `Controller/Resource/AbstractResourceController.php` | `redirectToHomeWithError()` et le `catch` de `searchResponse()` — couvre les 4 contrôleurs de ressource | `catalog.page.unavailable` |
| `Controller/HomeController.php` | `preview()` rend des sections vides en **200 OK** | `catalog.home_preview.failed` |
| `Service/Tools/GoFetcherClient.php` | `fetchBatch()` droppe les résultats inexploitables : un batch 100 % en échec est identique à un succès | `catalog.fetch_batch.unresolved` |
| `Service/Storage/DeferredImageIngestor.php` | `flush()` avale tout sur `kernel.terminate` | `ingest.deferred_task.failed` |
| `Controller/Resource/LoaderController.php` | l'échec d'ingestion ne part qu'en frame SSE vers le navigateur | `ingest.loader.failed` |
| `Controller/Billing/StripeWebhookController.php` | secret absent (`critical`) et signature invalide (`error`) | `stripe.webhook.*` |
| `Service/Mail/**` | tout échec d'envoi | `mail.send.failed` |
| `Service/Storage/NdjsonDayStore.php`, `Service/Audit/AuditLogger.php` | l'échec d'écriture du journal légal est doublement muet | `audit.journal.write_failed` |
| `Service/Client/VersionManager.php:114,153` | **déjà loggé, mal** : `error('Erreur lors de la récupération des versions Riot', …)` — phrase française libre, exactement l'anti-pattern banni. Après le lot 1a ces deux lignes partent systématiquement sur `stderr` | `catalog.versions.unavailable` |
| `Controller/Billing/StripeWebhookController.php:87` | clé de contexte `error` à renommer en même temps qu'on ajoute les deux logs manquants | — |

### Le miroir du journal d'audit

Les événements de sécurité (connexion, échec d'authentification, action admin) partent
aujourd'hui dans un fichier NDJSON que Vector ne lit jamais. Les miroiter depuis
`AuditLogger::record()` vers un canal Monolog `audit` les rend visibles sur la même
timeline que les métriques d'infra.

- Niveau : `AuditOutcome::Success` → `info`, `Failure` → `warning`, **`Denied` →
  `warning`** (l'enum a trois cas : `Denied` couvre le refus d'autorisation et le CSRF,
  c'est l'événement de sécurité le plus intéressant du lot — ne pas l'oublier).
- **Champs exclus du miroir : `ip` et `meta.identifier`.** Le second porte l'adresse
  e-mail saisie sur `user.login_failed` — c'est la seule PII directe du journal, et
  c'est justement l'événement qu'on veut miroiter. Le fichier NDJSON reste la **source
  légale** (rétention CNIL 6 mois) ; VictoriaLogs a 90 jours pilotés par l'infra.
- Le miroir ne doit jamais faire échouer l'action auditée. Au passage, corriger le
  contrat *best-effort* de la classe : le `try/catch` n'ouvre qu'après la résolution de
  l'acteur, qui peut donc lever pendant le traitement d'un échec d'authentification.

---

## Lot 4 — mettre `go-fetcher` au niveau de `go-api`

`go-api` est le modèle : `log/slog` JSON sur `os.Stdout`, access-log structuré.
`go-fetcher` en est resté à la stdlib `log`, en texte et **100 % sur stderr** — ce qui
rend tout filtre `stream:stderr` inutilisable pour distinguer une erreur du trafic
normal.

```go
// newLogger emits JSON on stdout. The collector tags every line with the stream it
// came from, so keeping application events on stdout leaves stderr as a reliable
// "something escaped the logger" channel (runtime panics, the Go runtime itself).
func newLogger() *slog.Logger {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: level}))
	// Also re-points the stdlib log package: no line of this process can escape
	// as plain text any more.
	slog.SetDefault(logger)
	return logger
}
```

**Sentinelles d'erreur** — le niveau d'un événement ne doit jamais dépendre d'un
matching de chaîne :

```go
var (
	ErrInvalidURL         = errors.New("invalid url")
	ErrSchemeNotAllowed   = errors.New("scheme not allowed")
	ErrHostNotAllowed     = errors.New("host not allowed")
	// Deliberately distinct: the three above say the caller built a bad URL, this
	// one says an allow-listed origin is trying to relay us somewhere else.
	ErrRedirectNotAllowed = errors.New("redirect target not allowed")
	ErrBodyTooLarge       = errors.New("response exceeds the body cap")
)
```

Le reste du lot :

- **Journaliser le refus d'allowlist en `Error`** dans `fetchOne()` : soit c'est un bug
  de génération d'URL côté PHP, soit une tentative réelle — les deux doivent allumer le
  dashboard. Aujourd'hui l'erreur est rendue *in-band* dans une réponse **200**.
- **Une ligne de synthèse par batch**, jamais une par URL : amplification de logs sur
  le chemin chaud.
- **`srv.ErrorLog` sur les deux services** : sans lui, les panics de handler sortent en
  texte hors du flux JSON, et une stack trace est éclatée en autant d'événements que de
  lignes.
- **Exclure `/healthz` des access-logs** des deux services : 11 520 lignes/jour et par stack (sonde
  toutes les 15 s sur deux services), conservées 90 jours — le double avec staging.
- **Durée numérique en millisecondes**, pas `time.Duration.String()` (`"1.481s"`,
  `"532ms"`), inexploitable dans un graphe.
- **Échapper le chemin** avant de le journaliser : un chemin contenant un saut de ligne
  injecte des lignes de log entières.
- `go-api` journalise ses **5xx en `INFO`** : le niveau doit suivre le statut.

Tests en conteneur (Go n'est pas installé en local) :

```bash
docker run --rm -v "$PWD/go/fetcher:/src" -w /src golang:1.25 go test ./...
docker run --rm -v "$PWD/go/api:/src"     -w /src golang:1.25 go test ./...
```

---

## Lot 5 — corrélation et métriques

Plus tard, une fois les quatre premiers lots posés.

- **Identifiant de requête** — nginx expose `$request_id` et ne le journalise pas. Le
  passer en `fastcgi_param` sur **tous** les blocs `location` qui font du
  `fastcgi_pass` (sinon la corrélation aura des trous), l'injecter dans `extra` par un
  processor Monolog, et le relayer en en-tête vers les services Go.
- **Révision déployée** — les images sont construites au SHA puis promues sur des tags
  mutables (`prod`, `staging`) : rien ne porte la révision à l'exécution, donc aucune
  corrélation possible entre une régression et le déploiement qui l'a introduite. Un
  label OCI `org.opencontainers.image.revision` + un `APP_REVISION` journalisé une fois
  au démarrage suffisent.
- **Métriques applicatives** — labels `prometheus.scrape` / `prometheus.port` +
  adhésion au réseau externe `observability`. Un target n'est retenu que via son IP sur
  ce réseau. Cibles les plus simples : les deux services Go (`promhttp.Handler` sur un
  port dédié). Ne jamais mettre l'URL ni l'`api_key_id` en label — cardinalité.

---

## Vérification après chaque lot

| Lot | Commande | Attendu |
|---|---|---|
| 1a | `docker compose exec -T -u www-data php php bin/console debug:config monolog --env=prod --no-debug` | les handlers `stderr`, `error_context*` et `deprecation` apparaissent |
| 1a | `docker compose logs --since 5m php \| grep -c '"GET /index.php"'` | `0` — le bruit FPM a disparu |
| 1a | `curl -s -o /dev/null localhost:8080/route-inexistante && docker compose logs --since 1m php \| grep -c NotFoundHttpException` | `0` — les 404 ne polluent plus |
| 1b | `docker compose exec nginx nginx -t` | `syntax is ok` / `test is successful` |
| 1b | `docker inspect lodb-prod-php-1 --format '{{.HostConfig.LogConfig.Type}}'` | `json-file` |
| 2 | `docker compose ps --format '{{.Service}} {{.Status}}'` | `php` passe `healthy` après ~30 s, pas d'`unhealthy` transitoire |
| 2 | `docker compose ps \| grep -i mailer` | aucune ligne en environnement servi |
| 3 | provoquer une panne (arrêter `minio`), charger une page de ressource | une ligne `catalog.page.unavailable` dans Grafana sous ~15 s |
| 4 | appeler `/fetch` avec une URL hors allowlist | une ligne JSON `fetch.allowlist.refused` de `service:go-fetcher`, `stream:stdout` |

**Toujours `-u www-data`** sur les `docker compose exec` : sans ça la commande laisse
des dossiers `root` sous `var/` que le pool FPM ne peut plus écrire.

> **Changelog.** Les lots 1b, 2 et 4 portent des changements observables (perte de
> l'access-log en dev, Mailpit derrière un profil, `MAILER_DSN` devenu obligatoire) :
> chacun demande son entrée `docs/changelog/2026/AAAA-MM-JJ-slug.md` dans le commit qui
> le livre, scope `infra` pour 1b et 2, `fetcher` pour 4, `back/*` pour 3.

