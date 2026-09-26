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

À compléter par les jalons (date, lot, commande, pic par service).
