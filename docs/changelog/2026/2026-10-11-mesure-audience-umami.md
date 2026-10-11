---
date: 2026-10-11
type: feat
scope: full-stack
title: Une mesure d'audience sans cookie, que vous pouvez refuser d'un clic
summary: Le site compte ses visites avec Umami, hébergé chez lui et sans cookie, et la politique de confidentialité vous laisse refuser d'être compté.
tags: [confidentialite, audience]
---

## Ce qui change

Le site mesure sa fréquentation avec Umami, un outil libre qu'il héberge lui-même : aucun
cookie, aucun service tiers, aucun script extérieur chargé dans les pages. Avant de partir,
l'adresse de chaque page est réduite : les pages du compte et de l'administration ne sont
jamais comptées, et ni pseudonyme, ni lien de partage, ni recherche ne quittent votre
navigateur.

La politique de confidentialité porte un interrupteur qui arrête la mesure sur votre
navigateur, d'un clic. Un navigateur qui envoie le signal Global Privacy Control n'est jamais
compté. La politique décrit aussi désormais la durée de conservation de chaque mesure et le
journal d'accès de l'hébergement.

## Pourquoi

Savoir quelles fiches sont consultées sert à améliorer le site et à tenir à jour les
tendances publiques, sans rien savoir de vous. La mesure précédente reste en place quelques
jours, le temps de vérifier que les deux comptent de la même façon.

## Technique

- Émetteur first-party (`core/analytics/audience-tracker.ts`) : `POST /u/api/send` sur
  l'origine du site, `credentials: 'omit'`, `keepalive`, jeton de cache d'Umami gardé en
  mémoire ; seulement depuis l'hôte canonique (staging, local et E2E muets), sur le web.
- Normalisation, refus par défaut : gabarit de route sauf `data: { audience: 'path' }`
  (listes et fiches), query réduite à `lang` et `version`, référent réduit à son origine,
  routes de compte et d'admin marquées `audience: 'none'`. Événements `page-error`,
  `search`, `build-share`.
- Opposition : `localStorage` (`lodb.audience.opt-out`) et Global Privacy Control, suivis
  aussi par le beacon de navigation.
- Déclaration à Umami par le calque `compose.umami.yaml`, en production seulement ; tirage
  horaire des vues par fiche vers `audience_entity_daily` pour `/v1/trends`, en sommeil tant
  que le compte de consultation n'est pas configuré.
