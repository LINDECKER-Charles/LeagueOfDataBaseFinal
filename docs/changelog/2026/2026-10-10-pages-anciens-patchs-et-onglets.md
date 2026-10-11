---
date: 2026-10-10
type: perf
scope: full-stack
title: Anciens patchs plus rapides, et plus d'onglet cassé après une mise à jour
summary: Les pages des anciens patchs restent prêtes d'une mise à jour du site à l'autre, et un onglet déjà ouvert continue de fonctionner.
tags: [disponibilite, performance]
---

## Ce qui change

Les pages des anciens patchs s'affichent plus vite : une fois consultées, elles restent
prêtes pendant une semaine, même quand le site est mis à jour, au lieu d'être recalculées à
chaque mise à jour. Les pages du dernier patch sont, elles aussi, recalculées moins souvent :
un nouveau patch y apparaît au plus une demi-heure après son arrivée sur le site.

Si le site est mis à jour pendant que vous le consultez, l'onglet déjà ouvert continue de
fonctionner : les éléments de la version précédente restent disponibles pendant un mois. Et
si l'un d'eux manque malgré tout, la page suivante ne tombe plus sur une erreur : le site se
recharge une fois, de lui-même, et vous arrivez directement sur la page demandée.

## Technique

- nginx, pages d'une version épinglée dans le chemin (`^/{locale}/[0-9]`) : zone
  `lodb_archive` (6 Go, 7 j d'inactivité, clés 32 Mo), clé **sans révision de déploiement**,
  préfixée par `LODB_ARCHIVE_EPOCH`, montée à la main ; gardée à la deuxième demande
  (`proxy_cache_min_uses`, `LODB_ARCHIVE_CACHE_MIN_USES`) ; copie périmée servie pendant son
  rafraîchissement (`updating`). Même débit par adresse que les autres pages. `lodb_pages`
  (dernier patch, clé avec révision) redescend à 24 h d'inactivité. Tailles réglables par
  `LODB_ARCHIVE_*` et `LODB_PAGE_CACHE_*`.
- Navigateur : un import dynamique qui échoue (message de Chromium, Firefox ou WebKit,
  `isChunkLoadFailure`) recharge la page entière à l'adresse demandée, une seule fois
  (`ChunkReload`, adresse retenue en `sessionStorage`) ; un second échec à la même adresse
  affiche la page `500` comme avant, et libère l'adresse pour un essai ultérieur. Sans
  stockage de session (serveur, navigation privée stricte), pas de rechargement.
- Volume `front-builds` : au démarrage puis toutes les heures, `web-ssr` y publie ses
  bundles hachés en arrière-plan (`FrontBuildsPublication`, copie atomique, jamais de
  réécriture, date rafraîchie pour un fichier déjà présent) ; `/build/` sert ceux de l'image,
  puis ceux du volume (`LODB_FRONT_BUILDS_DIR`). Le job de déploiement supprime ceux qu'aucun
  build en service n'a livrés depuis `LODB_FRONT_BUILDS_RETENTION_DAYS` jours (30).
- nginx : zone `lodb_assets` pour `/build/` (clé `$uri`, 1 Go, 30 j d'inactivité, réglables
  par `LODB_ASSETS_CACHE_*`), sur un volume ; un `404` n'est jamais gardé. `/build/` répond
  sous `Content-Security-Policy: default-src 'none'; sandbox` : un fichier ouvert directement
  n'exécute rien (les fichiers du volume sont écrits par le SSR, pas tirés de son image).
- Le SSR compresse le HTML d'une page une fois, au rendu (gzip niveau 6, `zlib` asynchrone,
  après le calcul de la CSP ; `LODB_HTML_GZIP_LEVEL`) ; nginx demande gzip à l'amont, garde
  la copie compressée (plus de recompression à chaque HIT, cache plusieurs fois plus dense),
  la décompresse pour un client sans gzip et pose un seul `Vary: Accept-Encoding`.
- Pages du dernier patch : `public, max-age=0, s-maxage=1800, stale-while-revalidate=86400`
  (au lieu de 300 s et 3 600 s) : une page demandée souvent est rendue au plus deux fois par
  heure au lieu de douze. Un nouveau patch apparaît sur une page en cache au plus 30 minutes
  après sa promotion ; un déploiement change toujours la clé du cache.
- ADR 0005 amendée (clé de cache des anciens patchs, bundles conservés).
