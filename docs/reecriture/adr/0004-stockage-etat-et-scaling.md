# ADR 0004 — Stockage, état et scaling

- **Statut** : acceptée — 2026-09-24
- **Révise** l'invariant « Postgres = données utilisateur uniquement » : le **manifeste
  d'images**, les **analytics** et l'**audit** y entrent. Les **contenus** Data Dragon
  (JSON, images) restent hors base.

## Contexte

Ce qui empêche aujourd'hui de lancer une deuxième instance **ne tient pas au
langage** :

| État | Où | Problème |
|---|---|---|
| Images (blobs) | volume Docker local | un seul hôte ; MinIO abandonné (redémarrait en boucle sur sa limite mémoire) |
| Manifeste d'images | fichier JSON par version, read-merge-write | **toujours sans verrou** ; invalidation limitée au conteneur local (périmée jusqu'à 7 jours) |
| Sessions | fichiers `var/state/sessions` | verrou qui sérialise les requêtes d'un utilisateur ; perdues avant l'ajout du volume |
| Analytics, audit | NDJSON + agrégats journaliers | aucun index ; rollup `--include-today` destructeur ; lus par `go-api` sur disque |
| Cache de données | pool fichier `var/cache` | vidé à chaque déploiement ; mémos recalculés à chaque requête |
| Rate limiters, anti-bruteforce | pool de cache fichier | par conteneur, remis à zéro au déploiement |

## Décision

**Viser une instance, sans rien s'interdire pour N.** Tout état partagé passe par une
abstraction dont l'implémentation v1 est locale ; passer à N instances devient une
affaire de configuration.

### Contenus Data Dragon : fichiers, écritures atomiques

- **Blobs** : `IBlobStore`, adressés par contenu (`blobs/{sha256}.{ext}` + voisin
  `.webp`). L'implémentation v1 écrit sur le volume `storage`, avec des **écritures
  atomiques** : fichier temporaire sur le même volume, puis `File.Move` (rename). Une
  implémentation S3 est possible le jour où il y a N hôtes.
- **Datasets** : fichiers JSON normalisés et immuables par (version, langue, type),
  écrits une seule fois.
- nginx sert **uniquement** `blobs/` (`/cdn/blobs/`, `immutable`, un an). Les datasets
  et le reste ne sont jamais exposés.
- **Catalogue en mémoire** : une instance immuable par (version, langue), chargée une
  fois, en cache LRU avec plafond mémoire. Les mémos par requête disparaissent.

### Index et état mutable : Postgres

| Table | Contenu | Pourquoi Postgres |
|---|---|---|
| `ddragon_asset` | (version, type, clé) → sha, statut `present`/`absent` | upsert concurrent sûr, multi-instance ; plus de read-merge-write |
| `analytics_event` | événements bruts, **partitionnés par jour** | rétention = `DROP PARTITION` ; IP/UA purgés à échéance |
| `analytics_daily` | agrégats | lus par `/v1/trends` sans passer par le disque |
| `audit_log` | journal d'audit, 6 mois | « actions de l'utilisateur X » devient une requête indexée |
| `data_protection_keys` | clés ASP.NET Data Protection | cookies d'auth valides après un déploiement, et entre instances |
| `email_outbox`, `stripe_event` | outbox mail, idempotence des webhooks | envois fiables ; plus de double crédit |

Le manifeste garde sa sémantique : **statut `absent` = absence définitive persistée**,
résolue en placeholder sans re-fetch. Une clé absente de la table signifie que la
ressource n'a jamais été tentée. Une erreur transitoire n'y est jamais écrite.

### Caches, limiteurs, sessions

- **`HybridCache`** (.NET) : un cache mémoire (L1) avec protection anti-stampede. Un
  Redis (L2) ne s'ajoute que si l'on passe à N instances.
- **Pas de session serveur.** Les préférences passent par l'URL (version, locale) et
  des cookies (thème, variante de langue), cf.
  [ADR 0009](0009-identite-authentification-sessions.md).
- **Rate limiting natif** (en mémoire). Il faudra un limiteur distribué (Redis) le jour
  des N instances. L'anti-bruteforce du login passe par le **lockout d'Identity**,
  stocké en base : il survit aux déploiements.

### Chemin vers N instances (documenté, pas construit)

1. `IBlobStore` en S3 (ou bucket compatible) ;
2. Redis pour `HybridCache` L2 et le rate limiting ;
3. Rien d'autre : pas de session serveur, clés Data Protection et verrous consultatifs
   déjà en Postgres, serveur SSR sans état.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Manifeste en fichier + verrou en processus | Règle la course à une instance, pas à N ; l'upsert Postgres coûte moins cher à écrire qu'un verrou correct |
| Datasets Data Dragon en JSONB | Contenu immuable et volumineux : un fichier est plus simple à produire, servir et mettre en cache |
| Analytics dans ClickHouse / Timescale | Surdimensionné pour le trafic actuel ; les partitions natives de Postgres couvrent la rétention |
| Revenir à MinIO | Déjà écarté (instabilité mémoire, aucune réplication réelle sur un seul hôte) |

## Conséquences

- **+** Plus de course sur le manifeste, plus de sérialisation par la session, plus de
  déconnexion au déploiement.
- **+** Analytics et audit interrogeables en SQL ; rétention automatique ([ADR 0003](0003-ingestion-proactive-et-taches-de-fond.md)).
- **−** Postgres devient critique pour le rendu du catalogue (via le manifeste) :
  sauvegardes et supervision de la base passent au niveau « service essentiel ».
- **−** Migration des analytics existants : les agrégats journaliers sont repris, les
  événements bruts ne le sont pas (ils contiennent IP et UA et arrivent à expiration).
