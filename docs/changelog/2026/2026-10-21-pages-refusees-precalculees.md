---
date: 2026-10-21
type: perf
scope: full-stack
title: Les anciens patchs les plus consultés sont prêts en premier
summary: Une page d'ancien patch que le site n'a pas pu servir à un visiteur est préparée d'elle-même dans les minutes qui suivent, les plus demandées d'abord.
tags: [performance, seo, anciens-patchs]
---

## Ce qui change

Quand une page d'ancien patch répond « service indisponible » à un visiteur ou à un moteur de
recherche aux heures chargées, le site la note, puis la calcule d'avance dans les minutes qui
suivent : à la visite suivante, elle est prête. Les pages les plus demandées passent en
premier, sans attendre que tout le catalogue des anciens patchs soit calculé.

## Pourquoi

Le calcul complet des anciens patchs prend des semaines, dans l'ordre des patchs et non dans
celui des visites : les pages réellement consultées attendaient leur tour.

## Technique

- Le SSR des visiteurs compte chaque page d'ancien patch qu'il refuse (`503` de l'admission),
  normalisée en `/{locale}/{version}/{page}`, dans une table bornée à 50 000 chemins, et l'expose
  en comptes cumulatifs sur son port de métriques (`GET /refused?after=`), jamais publié.
- L'API la relève toutes les 30 s dans `prerender_demand` (migration additive), n'ajoute que
  l'augmentation de chaque compte, ne garde que les pages du catalogue, et les rend par le
  chemin de la passe (même SSR, même rythme, même clé et mêmes métadonnées) dans l'epoch
  servie ; une page déjà écrite par une passe est ignorée.
- Partage avec une passe manuelle : la file tient 50 % des places en vol du SSR de la passe
  pendant une passe, toutes sans passe (`LODB_PRERENDER_DEMAND_SHARE`). Sans passe, la file
  pilote elle-même le régulateur : son plancher en adaptatif, la vitesse manuelle sinon.
- Interrupteur `LODB_PRERENDER_DEMAND_ENABLED` (vrai par défaut) ; bloc « File automatique »
  au pupitre `/admin/prerender` ; métriques `lodb_prerender_demand_*`.
