---
date: 2026-10-10
type: fix
scope: back
title: Les pages du patch actuel restent rapides même quand les anciens patchs affluent
summary: Le patch actuel reste toujours prêt en mémoire, et les anciens patchs se préparent à un rythme qui ne ralentit plus le reste du site.
tags: [disponibilite, performance]
---

## Ce qui change

Les champions, objets, runes et sorts du patch actuel restent prêts en permanence, dans
toutes les langues du site : leurs pages s'ouvrent vite, même quand beaucoup de visiteurs
consultent des patchs anciens au même moment. Les anciens patchs se préparent à un rythme
mesuré. S'ils sont très demandés, une page d'ancien patch peut répondre « Service
indisponible » quelques secondes : il suffit de recharger un peu plus tard.

Le chargement qui s'affiche quand on change de patch ou de langue s'arrête de lui-même
après deux minutes au plus, et la page s'ouvre quand même. Les anciennes adresses du site
mènent toujours à la bonne page, ou à la liste du patch quand l'entrée n'existe plus.

## Technique

- Catalogues du dernier patch épinglés en mémoire (`ICatalogPinning`, tâche
  `LatestCatalogWarmer`) : jamais évincés, dans un budget estimé (`PinnedBudgetMegabytes`).
  Les autres (version, langue) gardent un LRU de 16 places.
- Porte froide sur un vrai miss (`ColdLoadGate`) : 2 chargements simultanés, 3/s ; le SSR
  (appel direct, sans `X-Forwarded-For`) a son propre débit et peut attendre 2 s, les autres
  appelants sont refusés sans attente. Refus : `503` + `Retry-After`.
- Ingestion à froid d'un visiteur sous budget par instance (6 couples version × langue et
  1 500 images par minute), 32 ingestions en attente et 3 s d'attente d'emplacement au plus.
- Warm-up : 20 démarrages par heure et par client, 8 flux, 2 par client, 120 s par flux ;
  un refus clôt le flux par une trame `failed` `catalog-pending`.
- Ancien site : aucune ingestion ; dernier patch d'abord, puis la version visée si elle est
  stockée, sinon `302` vers sa liste.
- Meter `LoDb.Catalog` (`lodb_catalog_*`) et `lodb_ingestion_sync_waiting`. Toutes les
  bornes sont réglables (`LoDb__Catalog__*`, `LoDb__Ingestion__*`), voir l'amendement du
  2026-10-10 de l'ADR 0003.
