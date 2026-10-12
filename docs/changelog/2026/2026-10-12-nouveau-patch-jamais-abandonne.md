---
date: 2026-10-12
type: fix
scope: back
title: Un nouveau patch arrive sur le site même après une panne passagère
summary: Le site ne reste plus bloqué sur l'ancien patch quand une source de données tombe au moment où le nouveau sort.
tags: [patchs, disponibilite]
---

## Ce qui change

Quand un nouveau patch sort, le site le récupère et le met en ligne de lui-même. S'il n'y arrive
pas tout de suite, il réessaie désormais aussi longtemps qu'il le faut, au moins toutes les six
heures, au lieu d'abandonner après quelques essais.

## Pourquoi

À la sortie du patch 16.20, une des sources de données ne répondait plus pendant quelques heures.
Le site a épuisé ses essais pendant cette panne et a abandonné le patch : il affichait encore
16.19 six jours plus tard, alors que la source répondait de nouveau. Le patch 16.20 sera repris
automatiquement dès la mise en ligne de ce correctif.

## Technique

- Constat (logs de prod et de staging) : 16.20.1 découverte le 2026-10-06 à 21:31 UTC, cinq
  échecs `ingest.version.failed` en trois heures, tous sur `DdragonDatasets.ReadChromasAsync`
  (`skins.json` de CommunityDragon, `TimeoutRejectedException` après 15 s), puis
  `ingest.version.abandoned` le 2026-10-07 à 00:42 ; l'état `failed` n'était plus jamais repris.
- `VersionStates` : seule une version en retard sur la dernière promue est abandonnée une fois
  ses tentatives épuisées (la longue traîne). Une version en avance, ou toute version tant
  qu'aucune n'est promue, est reportée avec le délai doublé, plafonné à six heures, sans fin.
- `DueAsync` reprend aussitôt une version abandonnée en avance de la dernière promue : c'est
  l'état actuel de 16.20.1 en prod et en staging, repris à la première veille après le
  déploiement.
- Débloquer à la main avant le déploiement : `exec -T api dotnet LoDb.Api.dll ingest --version
  16.20.1` dans le projet Compose de l'environnement ingère toutes les langues et promeut la
  version.
