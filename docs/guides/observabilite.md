# Observabilité — les logs de LoDb dans Grafana

> **Ce que ce guide couvre.** La chaîne d'observabilité (Vector → VictoriaLogs →
> Grafana) est déployée par le dépôt d'infrastructure `infra-vps`, hors de ce dépôt.
> Elle capte tous les conteneurs. Ce guide décrit comment elle fonctionne, ce
> que ce projet doit — et ne doit surtout pas — déclarer, et comment interroger le
> résultat.
>
> - Ce qu'on écrit dans un log applicatif : [`logging.md`](logging.md).
> - Les métriques de l'API et du SSR (noms, labels, cardinalité) :
>   [`metriques.md`](metriques.md).
> - Ce que l'hôte porte : [`migration-edge-proxy.md`](migration-edge-proxy.md).

---

## Comment la chaîne fonctionne

```
conteneur ──stdout/stderr──> démon Docker ──API──> Vector ──bulk──> VictoriaLogs ──> Grafana
                                                     │
                                            normalise stack / service /
                                            container / stream, devine .level
```

Trois propriétés à intégrer avant toute chose :

1. **Vector ne lit que `stdout` / `stderr`.** Un log écrit dans un fichier (un volume ou un
   tmpfs du conteneur) n'est **jamais** collecté.
2. **Aucun conteneur n'a à se déclarer.** Vector s'abonne au flux du démon Docker :
   tout ce qui démarre est capté.
3. **Vector ne parse aucun JSON applicatif.** Il devine le champ `level` par
   expression régulière sur le **texte brut** de la ligne. Voir
   [le piège du niveau](#le-piège-du-niveau-deviné).

`stack` (= `COMPOSE_PROJECT_NAME`, donc `lodb-prod` ou `lodb-staging`), `service`,
`container` et `stream` sont les champs de **flux** : ils définissent l'axe
d'indexation, ce qui rend une recherche « logs de ce conteneur » instantanée. Les
autres champs restent interrogeables (`journal:`, `classe:`, `level:`…) **quand Vector
les produit** — c'est le cas du journal d'accès de l'edge, qui est parsé. Pour un log
applicatif de l'API ou du SSR, Vector ne produit rien de tel : `EventName`, `LogLevel`,
`State` (API), `level`, `msg` et les champs du SSR n'existent que comme texte dans `_msg`.

Les services de la stack (`compose.yaml`) : `api`, `migrate` (conteneur éphémère des
migrations), `states` (conteneur éphémère de l'index des états d'entité), `web-ssr`,
`web-ssr-prerender` (le SSR de la passe de précalcul), `nginx` et `postgres`. Les cinq
premiers écrivent la ligne JSON de [`logging.md`](logging.md) ;
`nginx` n'écrit que ses erreurs (`access_log off`, `docker/nginx/nginx.conf`) et `postgres`
ses requêtes lentes, en texte. Les services de l'ancienne stack (`php`, `go-fetcher`,
`go-api`, `mailer`) sont retirés au déploiement (`--remove-orphans`,
`.github/workflows/_deploy.yml`) : une requête qui les vise ne renvoie rien, ce qui ne
prouve pas l'absence d'erreur.

---

## Ce qu'il ne faut **pas** faire

**Ne posez jamais `caddy.import: journal-acces`, ni aucune directive `log` par site.**

L'edge journalise **tous** les sites qu'il sert, via l'option globale
`servers :443 { trace }` de son Caddyfile. LODB apparaît donc dans le tableau de bord
*Edge — trafic* **sans rien déclarer**. Poser une directive `log` sur un site fait
basculer Caddy en mode opt-in : il place tous les **autres** sites de l'hôte dans
`skip_hosts` — toujours servis, mais silencieusement absents du journal.

> Le snippet `(journal-acces)` est conservé **vide** en amont uniquement pour qu'un
> label oublié ne fasse pas refuser la configuration entière de l'edge. Il est destiné
> à disparaître : l'`import` deviendrait alors un snippet inconnu, ce qui couperait
> l'edge du VPS entier. Contrat détaillé : `infra-vps/docs/APPLICATIONS.md`.

Ce que le journal de l'edge conserve par requête, sans effort de notre part : hôte,
domaine, `schema` (`https` / `http`), méthode, chemin, query string, statut, durée,
octets, IP cliente réelle, User-Agent, Referer, et continent / pays / région / ville /
coordonnées quand l'adresse est publique. Aucun autre en-tête n'est conservé : le
transform de Vector ne recopie que `User-Agent` et `Referer`, et remplace le JSON brut
de Caddy par une ligne courte.

> **Compter les requêtes d'un site se fait sur `schema:https`.** En clair sur le
> port 80, l'appelant choisit librement son `Host` : sans ce filtre, n'importe qui
> peut gonfler les compteurs d'un domaine.

---

## Le piège du niveau deviné

Vector devine `level` par regex sur le **texte brut** :
`(?i)\bwarn(ing)?\b` → `warn`, `(?i)\b(error|exception|critical)\b` → `error`,
`(?i)\b(fatal|panic|emerg)\b` → `fatal`.

Conséquences sur les lignes de l'API et du SSR :

- un `Warning` qui porte une exception est classé **error** : le champ `Exception` (API)
  ou `exception` (SSR) contient le mot — `jobs.schedule.failed`, `ingest.listener.failed` ;
- un champ ou une valeur libre contenant `error` fait de même, d'où la règle « aucune clé
  `error` » de [`logging.md`](logging.md#aucune-clé-error) ;
- un `Critical` de l'API est classé **error** comme un `Error` : `critical` est dans la
  regex ;
- `Information` et `info` ne sont pas dans la regex : sans autre mot de la liste, la ligne
  reste **info**.

> **Aucune alerte, aucun taux d'erreur ne se construit sur le champ `level`.**
> On alerte sur la **clé d'événement**, qui est une sous-chaîne exacte de `_msg`.

---

## Interroger

Le filtre de phrase est la construction la plus sûre : la clé d'événement est une
sous-chaîne exacte de `_msg`.

```logsql
# Les lectures de catalogue en échec (Data Dragon) des dernières 24 h
stack:lodb-prod service:api _time:24h "catalog.read.failed"

# Délestage faute de connexion Postgres libre (503), et base injoignable (500)
stack:lodb-prod service:api _time:1h ("database.pool.exhausted" OR "database.server.unavailable")

# Refus d'allowlist de l'ingestion — événement de sécurité
stack:lodb-prod service:api _time:24h "fetch.allowlist.refused"

# Échecs de connexion (miroir du journal d'audit)
stack:lodb-prod service:api _time:1h "audit.user.login_failed"

# Tâches périodiques en échec, ou qui n'ont pu prendre leur verrou ni lire leur calendrier
stack:lodb-prod service:api _time:24h ("jobs.run.failed" OR "jobs.schedule.failed")

# Rendus en échec et requêtes tombées dans le gestionnaire d'erreurs du SSR
stack:lodb-prod service:web-ssr _time:1h ("ssr.render.failed" OR "http.request.failed")

# La passe de précalcul : une ligne par (version, langue), et son SSR injoignable
stack:lodb-prod service:api _time:1d ("prerender.unit.completed" OR "prerender.ssr.unreachable")

# Le dernier passage des migrations (conteneur éphémère `migrate`)
stack:lodb-prod service:migrate _time:7d ("db.migrate.completed" OR "db.baseline.refused" OR "db.roles.applied")

# L'index des états d'entité : le passage du déploiement (conteneur éphémère `states`), puis
# ses mises à jour par la promotion et la réconciliation, et les copies illisibles de l'API
stack:lodb-prod service:states _time:7d ("states.update.completed" OR "states.build.")
stack:lodb-prod service:api _time:1d ("states.update." OR "states.copy.failed")

# Requêtes Postgres de plus de 500 ms (texte de Postgres, jamais leurs paramètres)
stack:lodb-prod service:postgres _time:6h "duration:"

# Ce qu'un conteneur a dit pendant un pic — cadrer la fenêtre dans Grafana
container:lodb-prod-api-1

# Quel service est devenu bavard ?
(stack:lodb-prod OR stack:lodb-staging) _time:1d
  | stats by (stack, service) count() lignes | sort by (lignes desc)

# Une requête de page de bout en bout : nginx démarre une trace W3C par requête qu'il relaie,
# le SSR la passe à ses appels à l'API ; le trace_id de la ligne http.request.served du SSR
# retrouve les lignes de l'API pour cette page
stack:lodb-prod (service:web-ssr OR service:api) _time:1h "4bf92f3577b34da6a3ce929d0e0e4736"

# La facture Scaleway des panneaux d'administration : lectures réussies et échouées
# (une ligne par lecture, au plus une par heure ; un échec réessaie 5 minutes après)
stack:lodb-prod service:api _time:1d ("admin.scaleway_bill.read" OR "admin.scaleway_bill.unavailable")

# Violations de la CSP signalées par les navigateurs (une ligne par minute), et scripts
# inline que le SSR a laissés hors de la politique d'une page
stack:lodb-prod _time:1d ("web.csp.violated" OR "ssr.csp.unexpected_scripts")

# Les URL en erreur 5xx de la journée, par domaine (journal de l'edge)
stack:edge service:caddy journal:acces classe:5xx schema:https _time:1d
  | stats by (domaine, chemin) count() requetes | sort by (requetes desc) | limit 20

# Qui scanne : adresses avec beaucoup de chemins distincts et de 4xx.
# Volontairement SANS schema:https — le port 80 en clair est justement là où frappent
# les scanners, et on veut les voir.
stack:edge service:caddy journal:acces classe:4xx _time:1h
  | stats by (ip_client, pays) count() requetes, count_uniq(chemin) chemins
  | sort by (chemins desc) | limit 10
```

> **`unpack_json` n'est calqué sur aucun exemple qui tourne.** Le pipe
> `| unpack_json from _msg` permettrait de filtrer sur le niveau réellement écrit
> (`LogLevel` de l'API, `level` du SSR) plutôt que sur le `level` deviné — mais il
> n'apparaît nulle part dans `infra-vps`. Banc d'essai, sur une ligne au contenu connu :
> `stack:lodb-prod service:web-ssr _time:15m "http.request.served" | unpack_json | fields status, duration_ms`.
> S'il échoue, tout ce guide reste utilisable avec les filtres de phrase ci-dessus.

Côté métriques, disponibles sans rien implémenter :

```promql
# Mémoire des conteneurs LODB
container_memory_working_set_bytes{stack=~"lodb-.*"}

# Proximité de la limite mémoire (posée par `compose.deploy.yaml`)
container_memory_working_set_bytes{stack=~"lodb-.*"}
  / container_spec_memory_limit_bytes{stack=~"lodb-.*"}

# Bande passante sortante par site, vue par l'edge.
# `hote`, pas `host` : ce dernier est un external_label valant le nom du VPS, et
# `sum by (host)` agrégerait silencieusement tous les sites sous ce nom.
# `handler="subroute"` : le scrape ne conserve que ce handler-là.
sum by (hote) (rate(caddy_http_response_size_bytes_sum{handler="subroute", hote!=""}[5m]))
```

Les métriques **applicatives** de l'API et du SSR (`lodb_*`, ASP.NET Core, Node) sont
collectées sur leur port 9464 par le réseau `observability` : le contrat des noms et des
labels, la règle de cardinalité et des requêtes types sont dans
[`metriques.md`](metriques.md). Toujours filtrer sur `stack` (`lodb-prod`, `lodb-staging`).

---

## Pages des anciens patchs : ce que voit chaque source

Avec le [catalogue pré-rendu](../produit/pre-rendu-catalogue-cdn.md), nginx sert
une page d'ancien patch (`/{locale}/{version}/…`) de trois façons : l'objet du stockage
Scaleway, ou, quand le stockage n'a pas la page ou ne répond pas, un rendu du SSR ou sa copie
gardée dans `lodb_archive`. nginx ne garde jamais la réponse du stockage (ADR 0011, amendement
du 2026-10-27). Chaque réponse le dit dans l'en-tête **`X-LoDb-Source`** :

| `X-LoDb-Source` | Qui a répondu | `X-Cache-Status` |
|---|---|---|
| `cache` | la copie d'un rendu du SSR gardée par nginx | `HIT`, `STALE`, `UPDATING` ou `REVALIDATED` |
| `store` | le stockage objet, lu à l'instant | absent : aucun cache n'est consulté |
| `ssr` | un rendu du SSR, le repli | `MISS`, ou la réponse de délestage (`503`) |

Le stockage ne renvoie jamais d'erreur au visiteur : un objet absent (`404`), une `403`
(politique du bucket, adresse du VPS changée), une `429` de Scaleway ou un délai dépassé
repassent par le SSR.
**Seule la part de `ssr` le montre** : une fois une passe complète, elle doit rester proche de
zéro, hors des pages avec query (`?page=N`) et des versions pas encore écrites.

Ce que chaque source voit de ces pages :

| Source | Ce qu'elle voit |
|---|---|
| Journal de l'edge (`journal:acces`) | Chaque requête, avec `cache` (tiré de `X-Cache-Status`). Il ne garde aucun autre en-tête : `source` n'existe qu'une fois `infra-vps` lit `X-Lodb-Source` (sa forme canonique en Go) dans le champ `.source` de Vector. Tant que ce n'est pas le cas, un `MISS` ne dit pas « rendu » : il peut venir du stockage. |
| Métriques du SSR (`lodb_ssr_renders_total`) | `lane="pinned"` compte tout rendu d'une URL à version épinglée, dans le chemin ou en `?version=` : les replis du stockage, mais aussi les 301 du dernier patch épinglé et les `?version=` ; ses refus (`outcome="refused"`) doivent rester proches de zéro. Les rendus de la passe sont à part, sous `lane="prerender"` ([`metriques.md`](metriques.md)). |
| Métriques de l'API (`lodb_prerender_pages_total{outcome}`) | Les pages de la passe : `written`, `skipped`, `refused`, `failed`, et une ligne `prerender.unit.completed` par (version, langue). Rien de ce que nginx sert. |
| File automatique (`lodb_prerender_demand_*`) | Les pages refusées aux visiteurs relevées sur le SSR (`captured_total{result}`), celles qui attendent (`pending`) et celles que la file a rendues (`pages_total{outcome}`), une ligne `prerender.demand.drained` par lot. Un `pending` qui ne baisse pas pendant que `captured_total{result="queued"}` monte : la file ne suit pas la demande. |
| File des états clos (`lodb_prerender_revision_*`) | Les pages d'anciens patchs que chaque promotion fait recalculer, parce qu'elle a clos leur état (`enqueued_total`, `pending`, `pages_total{outcome}`), une ligne `prerender.revision.drained` par lot. Un `pending` qui ne redescend pas dans l'heure qui suit une promotion, ou des `failed` : des pages révisables gardent l'URL courte pour canonique. |
| Journaux du SSR (`http.request.served`) | Les rendus servis aux visiteurs, replis compris ; aucune ligne par page de la passe, sauf ses `5xx`. Une page servie par le cache ou le stockage n'y paraît pas. |
| Audience interne (miroir de nginx, balise) | Le miroir ne compte jamais une version épinglée, et la passe ne passe pas par nginx. |
| Umami | Il mesure dans le navigateur, quelle que soit la source de la page. |

Requêtes types (le champ `source` suppose la lecture de l'en-tête par Vector) :

```logsql
# Part de chaque source sur les pages des anciens patchs, sur 24 h
stack:edge service:caddy journal:acces schema:https hote:league-of-data-base.com
  chemin:~"^/[a-z]{2}(-han[st])?/[0-9]" _time:1d
  | stats by (source) count() requetes

# Les versions encore servies par le SSR : celles que la passe n'a pas écrites
stack:edge service:caddy journal:acces schema:https hote:league-of-data-base.com
  source:ssr _time:1d
  | extract_regexp "^/[a-z-]+/(?P<version>[0-9.]+)/" from chemin
  | stats by (version) count() requetes | sort by (requetes desc) | limit 20
```

```promql
# Replis vers le SSR et leurs refus, par motif (24 h)
sum by (outcome, reason) (
  increase(lodb_ssr_renders_total{stack="lodb-prod", lane="pinned"}[24h])
)
```

**Cloudflare est prévu, pas construit.** Au lot 4 de la
[spécification](../produit/pre-rendu-catalogue-cdn.md), Cloudflare répondrait seul aux pages
qu'il garde : l'edge, nginx et ces sources ne verraient plus que ses ratés, et ses propres
chiffres arriveraient par l'exportateur d'`infra-vps`. Rien de Cloudflare n'existe dans
`docker/`, `compose*.yaml` ni `.github/`.

---

## Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| Rien n'arrive dans Grafana | le conteneur n'écrit pas sur stdout/stderr | `docker compose logs --tail=20 <service>` — si c'est vide, le problème est dans l'app, pas dans la collecte |
| `docker logs` montre les lignes, Grafana non | Vector ou VictoriaLogs à l'arrêt | côté hôte : `docker compose -f /opt/observability/compose.yaml ps` |
| Le déploiement échoue sur `COMPOSE_PROJECT_NAME is '…' in the .env` | le `.env` poussé ne nomme pas `lodb-<cible>` : `_deploy.yml` refuse le déploiement avant tout changement, plutôt que de laisser les lignes partir sous un autre `stack` | corriger `COMPOSE_PROJECT_NAME` dans le secret `ENV_STAGING` ou `ENV_PROD`, relancer |
| Staging et prod se mélangent dans un panneau | requête sans filtre `stack` | toujours préfixer `stack:lodb-prod` — les deux stacks sont co-localisés |
| Le niveau `error` explose sans incident | `.level` deviné, ou clé de contexte `error` | filtrer sur la clé d'événement, jamais sur `level` |
| Des lignes sans clé ni JSON dans `api` ou `web-ssr` | une sortie hors du logger : `console.error`, `Console.Write`, pile multi-lignes d'un handler par défaut | corriger à la source, jamais filtrer ([`pieges.md`](../architecture/pieges.md)) ; voir [`logging.md`](logging.md#les-exceptions) |
| Aucune série `lodb_*` pour un `stack` | `api`, `web-ssr` ou `web-ssr-prerender` hors du réseau `observability`, ou labels `prometheus.*` absents | `docker inspect <conteneur> --format '{{json .NetworkSettings.Networks}}'` ; redéployer (`compose.deploy.yaml` les pose) |
| Le site disparaît du dashboard *Edge — trafic* | une directive `log` ou `caddy.import: journal-acces` a été réintroduite quelque part | `docker inspect <conteneur> --format '{{json .Config.Labels}}'` ; retirer le label. L'alerte `SiteAbsentDuJournalAcces` le détecte site par site |
