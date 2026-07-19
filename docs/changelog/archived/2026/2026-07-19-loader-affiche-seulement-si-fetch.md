---
date: 2026-07-19
type: fix
scope: front
title: Le chargement ne s'affiche plus quand les données sont déjà en cache
summary: L'écran de chargement n'apparaît que lorsqu'il y a réellement des images à récupérer, plus jamais sur une page déjà prête.
tags: [loader, ux, performance]
---

## Ce qui change

En naviguant vers une liste (champions, objets, runes, sorts d'invocateur) déjà
consultée, l'écran de chargement n'apparaît plus : la page s'ouvre directement.
Le chargement ne se montre désormais que lorsqu'il y a vraiment des images à
télécharger — un nouveau patch, une ressource jamais visitée.

## Pourquoi

L'overlay pouvait « flasher » brièvement même sur une page dont tout était déjà
en cache, parce qu'il s'affichait après un court délai fixe plutôt qu'en fonction
du travail réel à faire. Résultat : un clignotement inutile sur des pages instantanées.

## Technique

L'overlay n'est plus déclenché par un timer (`SHOW_DELAY`) : il ne s'ouvre qu'à
la réception de l'événement `start` du flux SSE **si `total > 0`** (images à
warmer). Une destination chaude (`total = 0`) navigue directement sans jamais
surfacer l'overlay, quelle que soit la latence du round-trip (overhead dev,
réseau lent) — l'affichage suit le besoin réel de fetch, plus le temps écoulé.
Cas non couvert volontairement : JSON froid + images déjà chaudes (ex. 1er
changement de langue) — un seul fetch rapide, jugé trop court pour mériter un
overlay ; à traiter via un flag serveur `fetching` si besoin ultérieur.
Spec `ResourceLoader.spec.ts` mise à jour (invariant « warm ⇒ jamais d'overlay,
indépendamment de la latence »).
