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

### Lot 2 et fondations du lot 3 — jalon (2026-09-26)

Contexte : stack d'intégration reconstruite depuis `300e218` (L3.1, L3.4, L3.12, L4.2 et
L4.3 intégrées), conteneurs `api`, `web-ssr` et `nginx` recréés juste avant la mesure ;
base chargée (16.19.1 ingérée). Relevés `docker stats --no-stream` en boucle pendant les
deux passages, 10 échantillons.

Commandes :

- `npm --prefix tests/LoDb.E2E test` : 26 tests réussis ;
- `npm --prefix tests/LoDb.E2E test -- --repeat-each=10` : 260 tests, 18 s.

| Service | Avant la suite | Pic (26 + 260 tests) | Limite provisoire |
|---|---:|---:|---:|
| `api` | 282 Mio | 299 Mio | 384m |
| `web-ssr` | 213 Mio | 338 Mio | 512m |
| `nginx` | 14 Mio | 17 Mio | 64m |
| `postgres` | 46 Mio | 47 Mio | 512m |
| `mailpit` (dev seulement) | 33 Mio | 35 Mio | — |

Lecture : `web-ssr` passe de 205 à 338 Mio. Les E2E rendent désormais les pages
prérendues, les 404 et les pages de compte, et la navigation charge les routes
paresseuses. La limite de 512m tient, avec 174 Mio de marge. `api` reste sous son relevé
du jalon 1 : ses conteneurs sont neufs, sans ingestion lourde depuis leur démarrage.

### Lot 4 — jalon (2026-09-26)

Contexte : stack d'intégration reconstruite depuis `292e641` (vague D intégrée : pages
champions, objets, runes, sorts, compte et profil), conteneurs `api`, `web-ssr` et
`nginx` redémarrés juste avant la mesure ; base chargée (16.19.1 ingérée), deux passages
E2E complets derrière la base. Relevés `docker stats --no-stream` en boucle pendant les
deux commandes, 43 échantillons.

Commandes :

- `npm --prefix tests/LoDb.E2E test` : 132 tests, 126 réussis, 6 échecs (voir le
  [jalon 4](jalons/lot-04.md)) ;
- `npm --prefix tests/LoDb.E2E test -- specs/champions specs/items specs/runes
  specs/summoners specs/catalogue specs/home specs/public specs/seo --repeat-each=5` :
  200 tests, 1,5 min (les specs de compte et de profil, qui inscrivent des comptes, sont
  exclues de la répétition).

| Service | Avant la suite | Pic (132 + 200 tests) | Limite provisoire |
|---|---:|---:|---:|
| `api` | 93 Mio | 292 Mio | 384m |
| `web-ssr` | 26 Mio | 214 Mio | 512m |
| `nginx` | 13 Mio | 16 Mio | 64m |
| `postgres` | 49 Mio | 62 Mio | 512m |
| `mailpit` (dev seulement) | 27 Mio | 31 Mio | — |

Lecture : `web-ssr` reste sous le pic du jalon 2 (214 contre 338 Mio), alors qu'il rend
désormais les listes et les détails du catalogue. Ses conteneurs sont neufs et nginx sert
une part des pages depuis son cache. `api` monte de 93 à 292 Mio : il sert les listes
complètes du catalogue, les connexions (argon2id, 19 Mio par hachage) et les favoris.
Il reste à 92 Mio de la limite de 384m, et le comportement sous cette limite reste à
mesurer avant L8.1 (G2 du jalon 1).

### Lot 3 — jalon (2026-09-26)

Contexte : stack d'intégration reconstruite depuis `ebe34c7` (vagues E et F intégrées :
lot 3 complet, builds, tendances, dons, `/v1`, analytics, politique client), conteneurs
`api`, `web-ssr` et `nginx` recréés juste avant la mesure, migration
`20260926185409_Lot6BillingAnalyticsApps` appliquée ; base chargée (16.19.1 et versions
antérieures ingérées). Relevés `docker stats --no-stream` en boucle pendant les E2E,
87 échantillons. `api` est redémarré (`docker restart`) après la première suite pour
vider le limiteur d'inscriptions en mémoire (G5 du [jalon 3](jalons/lot-03.md)).

Commandes :

- `npm --prefix tests/LoDb.E2E test` : 264 tests, 226 réussis, 37 échecs, 1 ignoré ;
- `npm --prefix tests/LoDb.E2E test -- specs/context-switcher specs/builds-editor
  specs/builds-share specs/trends` : 15 tests, relance ciblée ;
- `npm --prefix tests/LoDb.E2E test -- specs/builds-editor`, après redémarrage d'`api`.

| Service | Avant la suite | Pic (264 tests) | Pic (relances) | Limite provisoire |
|---|---:|---:|---:|---:|
| `api` | 123 Mio | 541 Mio | 571 Mio | 384m |
| `web-ssr` | 33 Mio | 300 Mio | 177 Mio | 512m |
| `nginx` | 14 Mio | 18 Mio | 18 Mio | 64m |
| `postgres` | 41 Mio | 46 Mio | 46 Mio | 512m |
| `mailpit` (dev seulement) | 35 Mio | 39 Mio | 39 Mio | — |

Lecture :

- **`api` dépasse sa limite provisoire** : 541 Mio pendant la suite complète, 571 Mio
  après les relances, contre 292 Mio au jalon 4. Les E2E exercent désormais les builds
  (sélecteurs, éditeur, partage, tendances), les dons, l'API publique et la balise
  d'analytics, en plus des inscriptions argon2id. Sans limite en dev, le GC n'a aucune
  raison de rendre la mémoire. Sous `384m`, le comportement n'est toujours pas mesuré
  (G2 du [jalon 1](jalons/lot-01.md)) : c'est à trancher avant L8.1, soit par une mesure
  sous limite, soit par une limite relevée.
- `web-ssr` monte à 300 Mio, sous le pic du jalon 2 (338 Mio) ; la limite de 512m tient.
- Le second passage complet, lancé après un nouveau redémarrage d'`api` et l'arrêt des
  relevés, ne figure pas dans ce tableau ; il ne change pas la liste des échecs.

### Lots 5, 6 et 7 — jalon (2026-09-27)

Contexte : stack d'intégration reconstruite depuis `01db165` (vague G intégrée : portail
des clés, admin avec MFA, mise à jour Android), `IMAGE_TAG=APP_REVISION=01db165`,
conteneurs `api`, `web-ssr` et `nginx` recréés juste avant la mesure, base chargée
(16.19.1 servie). Relevés `docker stats --no-stream` en boucle, environ toutes les 5 s :
14 échantillons pendant la suite complète (1,1 min), 21 pendant la suite admin. `api` est
redémarré entre les deux passages (limiteur d'inscriptions en mémoire).

Commandes :

- `npm --prefix tests/LoDb.E2E test` : 301 tests, 262 réussis, 28 échecs, 10 non lancés,
  1 ignoré ;
- `npm --prefix tests/LoDb.E2E test -- specs/admin` (passage de diagnostic, voir le
  [jalon](jalons/lots-05-06-07.md)) : 29 tests, 12 réussis.

| Service | Avant la suite | Pic (301 tests) | Pic (admin) | Limite provisoire |
|---|---:|---:|---:|---:|
| `api` | 135 Mio | 440 Mio | 482 Mio | 384m |
| `web-ssr` | 124 Mio | 329 Mio | 178 Mio | 512m |
| `nginx` | 14 Mio | 19 Mio | 19 Mio | 64m |
| `postgres` | 47 Mio | 56 Mio | 56 Mio | 512m |
| `mailpit` (dev seulement) | 40 Mio | 46 Mio | 42 Mio | — |

Les deux stacks ensemble (critères des lots 5 et 7, relevé ponctuel au repos) : ancienne
stack `php` 67 Mio, `postgres` 36 Mio, `go-fetcher` 21 Mio, `mailer` 14 Mio, `nginx`
13 Mio, `go-api` 5 Mio, soit environ 156 Mio ; `lodb-next` `api` 118 Mio, `web-ssr`
234 Mio. Avant le démarrage de l'ancienne stack, tous les conteneurs du poste occupaient
environ 4,2 Gio des 11,67 Gio : la marge est large.

Lecture :

- `api` reste au-dessus de sa limite provisoire : 440 Mio sur la suite complète, 482 Mio
  sur la suite admin seule, qui charge surtout les rapports (analytics, stockage,
  surveillance) et les connexions argon2id. C'est sous le pic du jalon 3 (541 Mio), mais
  toujours sans mesure sous limite : à trancher avant L8.1.
- Le panneau de surveillance de l'admin lit la mémoire du processus `api` : 290 Mio
  à un instant de la suite admin (tas managé 60 Mio). L'écart avec le pic de
  `docker stats` n'est pas expliqué (instant différent, ou cache de pages du noyau compté
  dans le conteneur) : hypothèse à vérifier par la mesure sous limite.
- `web-ssr` monte à 329 Mio, sous le pic du jalon 2 (338 Mio) ; la limite de 512m tient.

### Lot 8 — jalon (2026-09-27)

Contexte : stack d'intégration reconstruite depuis `fed0c64`
(`IMAGE_TAG=APP_REVISION=fed0c64`), conteneurs `api`, `web-ssr` et `nginx` recréés juste
avant la mesure, base chargée. Relevés `docker stats --no-stream` en boucle, environ toutes
les 10 s : 8 échantillons pendant la suite complète (1,4 min). Commande :
`npm --prefix tests/LoDb.E2E test` : 302 tests, 284 réussis, 10 échecs, 7 non lancés,
1 ignoré (voir le [jalon](jalons/lot-08.md)).

| Service | Avant la suite | Pic (302 tests) | Limite de `compose.next.deploy.yaml` |
|---|---:|---:|---:|
| `api` | 132 Mio | 594 Mio | 1152m |
| `web-ssr` | 49 Mio | 287 Mio | 704m |
| `nginx` | 14 Mio | 19 Mio | 64m |
| `postgres` | 49 Mio | 53 Mio | 512m |
| `mailpit` (dev seulement) | 37 Mio | 40 Mio | — |

Lecture : `api` monte à 594 Mio, au-dessus du pic du jalon 3 (541 Mio) et sous la limite de
L8.1 (1152m). L'échantillonnage à 10 s peut manquer le vrai sommet. La répétition locale (emplacement `lodb-next-e2`, ancienne
stack et `lodb-next` en même temps) n'a pas été échantillonnée.
