# 📡 Observabilité — envoyer les logs de LODB vers Grafana

> **Ce que ce guide couvre.** La chaîne d'observabilité (Vector → VictoriaLogs →
> Grafana) est déployée par le dépôt d'infrastructure `infra-vps`, hors de ce dépôt.
> Elle capte **déjà** tous les conteneurs. Ce guide décrit comment elle fonctionne, ce
> que ce projet doit — et ne doit surtout pas — déclarer, et comment interroger le
> résultat.
>
> - Mettre l'application en état d'y verser quelque chose d'exploitable :
>   [`../audits/observabilite-2026-08-23.md`](../audits/observabilite-2026-08-23.md).
> - Ce qu'on écrit dans un log applicatif : [`logging.md`](logging.md).
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

1. **Vector ne lit que `stdout` / `stderr`.** Un log écrit dans un fichier — y compris
   sous `var/state/` — n'est **jamais** collecté.
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
applicatif PHP, Vector ne produit rien de tel : `channel`, `level_name` et tout le
contexte n'existent que comme texte dans `_msg`.

---

## ⛔ Ce qu'il ne faut **pas** faire

**Ne posez jamais `caddy.import: journal-acces`, ni aucune directive `log` par site.**

L'edge journalise désormais **tous** les sites qu'il sert, via l'option globale
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

Trois conséquences mesurables :

- un `WARNING` dont le contexte porte une clé `error` est classé **error** — le cas
  existe aujourd'hui dans le dépôt ;
- un `EMERGENCY` ou un `ALERT` est classé **info** : aucun des deux mots n'est dans la
  regex ;
- tant que l'access-log nginx en texte brut est collecté, **n'importe quel visiteur
  pilote le niveau** via une URL contenant `error`. Le lot 1b ferme ce vecteur.

> **Aucune alerte, aucun taux d'erreur ne se construit sur le champ `level`.**
> On alerte sur la **clé d'événement**, qui est une sous-chaîne exacte de `_msg`.

---

## Interroger

Le filtre de phrase est la construction la plus sûre : la clé d'événement est une
sous-chaîne exacte de `_msg`.

```logsql
# Toutes les pannes de catalogue des dernières 24 h
stack:lodb-prod service:php _time:24h "catalog.page.unavailable"

# Refus d'allowlist SSRF — événement de sécurité
stack:lodb-prod service:go-fetcher _time:24h "fetch.allowlist.refused"

# Échecs d'authentification (miroir d'audit, lot 3)
stack:lodb-prod service:php _time:1h "audit.user.login_failed"

# Requêtes PHP lentes (slowlog, lot 1a)
stack:lodb-prod service:php _time:6h "executing too slow"

# Ce qu'un conteneur a dit pendant un pic — cadrer la fenêtre dans Grafana
container:lodb-prod-php-1

# Quel service est devenu bavard ?
(stack:lodb-prod OR stack:lodb-staging) _time:1d
  | stats by (stack, service) count() lignes | sort by (lignes desc)

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
> `| unpack_json from _msg` permettrait de filtrer sur le `level_name` réellement écrit
> par l'application plutôt que sur le `level` deviné — mais il n'apparaît nulle part
> dans `infra-vps`. Le banc d'essai gratuit est `go-api`, seul service qui émet déjà du
> JSON structuré :
> `stack:lodb-prod service:go-api _time:15m | unpack_json | fields path, status`.
> S'il échoue, tout ce guide reste utilisable avec les filtres de phrase ci-dessus.

Côté métriques, disponibles sans rien implémenter :

```promql
# Mémoire des conteneurs LODB
container_memory_working_set_bytes{stack=~"lodb-.*"}

# Proximité de la limite (après le lot 2)
container_memory_working_set_bytes{stack=~"lodb-.*"}
  / container_spec_memory_limit_bytes{stack=~"lodb-.*"}

# Bande passante sortante par site, vue par l'edge.
# `hote`, pas `host` : ce dernier est un external_label valant le nom du VPS, et
# `sum by (host)` agrégerait silencieusement tous les sites sous ce nom.
# `handler="subroute"` : le scrape ne conserve que ce handler-là.
sum by (hote) (rate(caddy_http_response_size_bytes_sum{handler="subroute", hote!=""}[5m]))
```

---

## 🔙 Dépannage

| Symptôme | Cause probable | Action |
|---|---|---|
| Rien n'arrive dans Grafana | le conteneur n'écrit pas sur stdout/stderr | `docker compose logs --tail=20 <service>` — si c'est vide, le problème est dans l'app, pas dans la collecte |
| `docker logs` montre les lignes, Grafana non | Vector ou VictoriaLogs à l'arrêt | côté hôte : `docker compose -f /opt/observability/compose.yaml ps` |
| Les lignes arrivent sous le mauvais `stack` | `COMPOSE_PROJECT_NAME` absent du `.env` de l'hôte | l'ajouter au secret `ENV_*`, redéployer |
| Staging et prod se mélangent dans un panneau | requête sans filtre `stack` | toujours préfixer `stack:lodb-prod` — les deux stacks sont co-localisés |
| Le niveau `error` explose sans incident | `.level` deviné, ou clé de contexte `error` | filtrer sur la clé d'événement, jamais sur `level` |
| Une ligne JSON est coupée en plein milieu | `log_limit` FPM | vérifier que `[global] log_limit = 65536` est bien dans `php-fpm.conf` ; alléger le contexte |
| Le site disparaît du dashboard *Edge — trafic* | une directive `log` ou `caddy.import: journal-acces` a été réintroduite quelque part | `docker inspect <conteneur> --format '{{json .Config.Labels}}'` ; retirer le label. L'alerte `SiteAbsentDuJournalAcces` le détecte site par site |
