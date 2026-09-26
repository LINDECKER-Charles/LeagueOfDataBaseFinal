# Mémoire de la stack `lodb-next`

Relevés `docker stats --no-stream` (colonne `MEM USAGE`) de la stack `lodb-next`. Ils sont
la base des limites mémoire de `compose.next.deploy.yaml` (règle du plan, §5.4 : deux
fois le pic observé), que L8.1 fixe définitivement. Les jalons ajoutent ici les pics
relevés pendant leurs E2E.

## L0.3 — socle (2026-09-26)

Contexte : poste de dev (macOS arm64, Docker Desktop, 11,67 Gio pour les conteneurs),
stack d'intégration construite depuis `59d3a15` + L0.3, `ASPNETCORE_ENVIRONMENT=Development`,
base vide, volume `storage` vide. Rien de fonctionnel encore : une page SSR « hello »
localisée et les sondes.

Charge : 400 rendus SSR de `/en/` et 400 appels `/api/inconnu` (ProblemDetails) via
nginx, 8 requêtes en parallèle pour chacun, en même temps.

| Service | Au repos | Pic sous charge | Après la charge | Limite provisoire |
|---|---:|---:|---:|---:|
| `api` | 170 Mio | 174 Mio | 174 Mio | 384m |
| `web-ssr` | 144 Mio | 255 Mio | 255 Mio | 512m |
| `nginx` | 16 Mio | 17 Mio | 17 Mio | 64m |
| `postgres` | 79 Mio | 78 Mio | 78 Mio | 512m |
| `mailpit` (dev seulement) | 9 Mio | 9 Mio | 9 Mio | — |

Lecture :

- `web-ssr` monte de 110 Mio sous la première charge et ne redescend pas : le tas de V8
  grandit puis se stabilise. C'est le service à surveiller quand les vraies pages (listes,
  détails, 21 locales) arriveront.
- `api` est stable ; les modules sont encore vides.
- `postgres` : la limite reprend celle de l'ancienne stack (`shared_buffers` de 128 Mio
  plus les connexions) plutôt que deux fois un relevé sur une base vide, qui ne dit rien de
  la charge réelle.
- `nginx` : le cache `proxy_cache` (L3.11) est sur disque, mais le cache de pages du noyau
  est compté dans la mémoire du conteneur ; à revoir au lot 3.

Tailles d'image (arm64) : `lodb-api` 245 Mo, `lodb-web-ssr` 352 Mo, `lodb-nginx` 93 Mo.

## Pics des E2E

Chaque jalon ajoute une ligne par service : date, lot, commande, pic.

### Lot 0 — jalon (2026-09-26)

Contexte : stack d'intégration reconstruite sans cache depuis `dd0450c`, conteneurs
recréés juste avant la mesure, base et volume `storage` vides. Relevés
`docker stats --no-stream` en boucle pendant la suite. La suite ne dure que 1,1 s (2 tests,
2 échantillons) : le pic retenu vient donc d'un second passage, plus long, de la même
suite.

Commandes :

- `npm --prefix tests/LoDb.E2E test` : 2 tests réussis ;
- `npm --prefix tests/LoDb.E2E test -- --repeat-each=30` : 60 tests, 8,4 s, 25
  échantillons.

| Service | Avant la suite | Pic (2 tests) | Pic (60 tests) | Limite provisoire |
|---|---:|---:|---:|---:|
| `api` | 37 Mio | 38 Mio | 45 Mio | 384m |
| `web-ssr` | 26 Mio | 50 Mio | 69 Mio | 512m |
| `nginx` | 13 Mio | 13 Mio | 16 Mio | 64m |
| `postgres` | 28 Mio | 28 Mio | 29 Mio | 512m |
| `mailpit` (dev seulement) | 9 Mio | 9 Mio | 9 Mio | — |

Lecture : tous les pics restent sous les relevés de L0.3, qui mesuraient 800 requêtes
concurrentes sur des conteneurs démarrés depuis plus longtemps. Les limites provisoires ne
changent pas. La suite E2E ne sollicite presque pas la stack tant qu'elle ne compte que le
test de fumée : ce relevé ne vaut pas mesure de charge.

### Lot 1 — jalon (2026-09-26)

Contexte : stack d'intégration reconstruite depuis `2a712e6`, conteneurs démarrés à
07:03 UTC. Avant la mesure, la veille de patchs avait ingéré 16.19.1 en 28 langues
(2 009 images, 1 750 WebP), puis 14 processus `ingest` et 75 `catalog export` avaient
tourné dans le conteneur `api` pour la parité. Base et volume `storage` chargés (15
versions). Relevés `docker stats --no-stream` en boucle, 6 échantillons sur les deux
passages.

Commandes :

- `npm --prefix tests/LoDb.E2E test` : 2 tests réussis ;
- `npm --prefix tests/LoDb.E2E test -- --repeat-each=30` : 60 tests, 8,8 s.

| Service | Avant la suite | Pic (2 + 60 tests) | Limite provisoire |
|---|---:|---:|---:|
| `api` | 369 Mio | 370 Mio | 384m |
| `web-ssr` | 70 Mio | 205 Mio | 512m |
| `nginx` | 14 Mio | 15 Mio | 64m |
| `postgres` | 36 Mio | 37 Mio | 512m |
| `mailpit` (dev seulement) | 20 Mio | 21 Mio | — |

Lecture :

- `api` n'est pas sollicité par les E2E, mais il reste à 369 Mio après l'ingestion
  complète, **à 15 Mio de la limite de déploiement**. Côté métriques du processus (port
  9464) : 309 Mio de working set, dont 151 Mio engagés par le GC ; le tas des gros
  objets retient 66 Mio après la dernière collecte. Le reste est natif (SkiaSharp,
  runtime). En dev, aucune limite ne s'applique : le GC n'a aucune raison de rendre la
  mémoire. Le comportement sous 384m reste à mesurer (G2 du
  [jalon 1](jalons/lot-01.md)) avant L8.1.
- `web-ssr` monte à 205 Mio sur 60 rendus, contre 69 Mio au jalon 0. Le front a grandi
  depuis : le bundle initial fait 549 ko, au-delà du budget d'avertissement de 500 ko. La
  limite de 512m tient.
