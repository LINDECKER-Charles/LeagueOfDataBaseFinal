---
date: 2026-10-20
type: perf
scope: full-stack
title: Les pages des anciens patchs s'ouvrent sans attente ni erreur
summary: Les fiches et les listes des anciens patchs viennent d'une copie déjà calculée, au lieu d'être recalculées à chaque visite, et ne répondent plus « service indisponible » aux heures chargées.
tags: [performance, seo, anciens-patchs]
---

## Ce qui change

Les pages d'un ancien patch (une fiche de champion, d'objet, de rune ou de sort, et les quatre
listes) arrivent tout de suite : le site les calcule une fois, les range, puis les sert telles
quelles. Une page encore absente de cette réserve reste calculée à la demande, comme avant.

## Pourquoi

Chaque page d'ancien patch était recalculée à sa première visite de la semaine, et le site n'en
calcule qu'un nombre limité par minute. Aux heures chargées, une visite sur deux tombait sur un
« service indisponible », surtout pour les moteurs de recherche, qui parcourent ces pages en
masse.

## Technique

- nginx cherche une page d'ancien patch sans query dans le stockage objet
  (`pages/{LODB_ARCHIVE_SERVE_EPOCH}/{chemin}`, client anonyme, TLS vérifié), derrière le cache
  chaud `lodb_archive` ; une 403, une 404, une 429, une 5xx ou un délai dépassé (lecture 3 s)
  repassent par la voie épinglée du SSR, comme une query ou un chemin hors de la grammaire des
  pages (ADR 0011, `snippets/pages-store/`).
- Les en-têtes propres à la page (CSP, CSP en rapport seul, `Reporting-Endpoints`, `Link`)
  reviennent des métadonnées de l'objet ; aucun `x-amz-*` ne sort ; une page du stockage porte
  la `Cache-Control` de la classe `archived`, gardée un an (`s-maxage=31536000`).
- La clé de `lodb_archive` porte les deux epochs (`LODB_ARCHIVE_EPOCH.LODB_ARCHIVE_SERVE_EPOCH`) :
  le déploiement repart d'un cache froid, et changer d'epoch servie le vide aussi.
- `X-LoDb-Source: cache|store|ssr` sur ces pages, pour le journal de l'edge : une part de `ssr`
  qui reste haute après une passe signale un stockage illisible (politique, adresse du VPS).
- Un client au-delà de son débit garde sa 429 et son `Retry-After` ; la 429 du stockage
  (limitation de Scaleway) mène au SSR. `LODB_PAGES_STORE_ENABLED=0` (local, CI) ne demande
  jamais rien au stockage.
- Mesure : 503 par jour du site et rendus épinglés refusés
  (`lodb_ssr_renders_total{lane="pinned",outcome="refused"}`), avant et 7 jours après la fin de
  la première passe (lot 5).
