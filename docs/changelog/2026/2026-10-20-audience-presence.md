---
date: 2026-10-20
type: fix
scope: front
title: La mesure d'audience attend un signe de présence avant de compter une visite
summary: Une visite n'est comptée qu'au premier geste du visiteur ou après dix secondes de page visible, ce qui écarte les robots qui chargent une page et repartent.
tags: [audience, robots]
---

## Ce qui change

La mesure d'audience ne compte plus une visite dès le chargement de la page : elle attend
que le visiteur bouge la souris, touche l'écran, fasse défiler la page ou appuie sur une
touche, ou qu'il garde la page visible dix secondes. Un joueur qui lit une fiche sans rien
toucher est donc compté, avec le site d'où il vient et le titre de la page. Un onglet ouvert
en arrière-plan ne compte qu'une fois affiché.

## Pourquoi

Trois vagues de faux visiteurs (12, 14-15 et 17-18 octobre 2026) chargeaient une seule page
et repartaient : 461 visiteurs sous Windows 10 pour 463 vues les 17 et 18 octobre, un rebond
de 96,9 % et une durée moyenne de 7 s sur la semaine. Ils venaient de fournisseurs d'accès
grand public, une adresse par vue, avec des agents de Chrome ordinaires : ni plage d'adresses
ni agent à exclure.

## Technique

- `AudiencePresence` (`core/analytics/audience/presence/`) écoute `wheel`, `touchstart`,
  `keydown`, `pointerdown` et `pointermove` dès le démarrage, et un délai de 10 s de page
  visible, relancé à chaque `visibilitychange`. Pas l'événement `scroll`, qu'une position
  restaurée ou une ancre déclenche sans personne.
- `AudienceTracker` lit le titre de la vue une fois l'application stable, comme avant, puis
  attend la présence pour envoyer : la première vue garde son titre et son référent. Les
  événements (`search`, `page-error`, `build-share`) attendent de même ; une fois la présence
  établie, tout part aussitôt.
- Coût : un vrai visiteur qui part en moins de dix secondes sans aucun geste n'est plus
  compté.
- Mesure : visiteurs Umami sous Windows 10 sur 7 jours, 647 du 12 au 18 octobre 2026 ; sens
  attendu : baisse, les autres pays et systèmes restant du même ordre.
