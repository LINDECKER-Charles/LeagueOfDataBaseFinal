---
date: 2026-10-21
type: fix
scope: front
title: Le sélecteur de version propose toujours le dernier patch, même depuis un ancien patch
summary: Ouverte sur une page d'un ancien patch, l'application se met à jour dès qu'elle est prête et propose le patch sorti depuis.
tags: [versions, anciens-patchs]
---

## Ce qui change

Sur la fiche d'un ancien patch (par exemple Zed en 16.19.1), le sélecteur de version liste
désormais le dernier patch, même s'il est sorti après la mise en ligne de la page. Les liens
« dernier patch » et les adresses sans version mènent bien au patch le plus récent pendant
toute la visite. La page s'affiche aussi vite qu'avant : la mise à jour arrive juste après.

## Pourquoi

Les pages des anciens patchs sont gardées longtemps pour s'ouvrir vite. Elles gardaient aussi
la liste des versions du jour où elles avaient été préparées : le patch sorti depuis n'était
pas proposé, et un lien sans version menait au patch d'avant.

## Technique

- `ApiMeta` : l'hydratation lit le `/api/meta` que la page embarque (cache de transfert HTTP),
  puis, une fois l'application stable dans le navigateur, le relit une fois sur le réseau et
  publie le document s'il diffère (`injectEmbeddedRevalidation`). Un échec garde le document
  embarqué, sans nouvelle tentative. Seule une réponse donnée par la page (le cache de
  transfert répond dans l'abonnement, le réseau jamais) est relue : une page pré-rendue sans
  meta embarquée, le serveur et les coquilles desktop/Android ne demandent `/api/meta` qu'une
  fois.
- Le client généré ne prend pas l'option `transferCache` d'HttpClient : le jeton
  `BYPASS_TRANSFER_CACHE` (contexte HTTP) et `bypassTransferCacheInterceptor` la posent.
- Les copies déjà gardées (stockage, cache `lodb_archive`) portent les bundles du build qui les
  a rendues : elles ne profitent du correctif qu'après une passe sur une epoch neuve.
