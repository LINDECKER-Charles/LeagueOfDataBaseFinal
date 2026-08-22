---
date: 2026-08-22
type: feat
scope: full-stack
title: Les images annoncent leur chargement et les listes froides se complètent sans recharger
summary: Chaque image affiche un effet de chargement tant qu'elle n'est pas arrivée, et les icônes encore absentes d'une liste (nouveau patch) apparaissent d'elles-mêmes quelques secondes plus tard au lieu de rester vides.
tags: [images, listes, performance, ux]
---

## Ce qui change

- Toutes les images de l'encyclopédie (icônes des cartes, portraits, splashs
  de skins, chromas, sorts, pickers) affichent un **balayage doré** tant que
  leurs octets ne sont pas arrivés ; une image introuvable garde sa case avec
  une marque discrète au lieu d'une icône cassée.
- Sur une liste consultée pour la première fois sur un patch, les icônes que le
  serveur n'a pas encore en stock s'affichent comme des **cases en attente**
  (initiales sous balayage) et **se remplissent toutes seules** dès qu'elles
  sont disponibles — plus besoin de recharger la page. Les cases définitivement
  sans image (anciens patchs) restent des initiales, sans attente inutile.
- Seules les cartes proches de l'écran sont interrogées : les centaines de
  cartes masquées par le filtre ne coûtent rien tant qu'elles ne sont pas
  affichées.

## Pourquoi

Après un changement de patch, les listes rendaient des initiales pour tout ce
qui n'avait pas encore été rapatrié, sans rien indiquer, et l'utilisateur devait
recharger pour voir les icônes. Rien non plus ne signalait qu'une image
hotlinkée (splash, chroma) était en cours de chargement.

## Technique

- **Tri-état serveur** : `paginate()` expose désormais `pending` (noms d'images
  différées à `kernel.terminate`), distinct du `null` du manifeste (absence
  réglée). `ResolvesImages::manifestStatus()` (lecture seule) et `warmLater()`
  (ré-enfile au terminate) ; `emptyPage()` porte la forme vide du contrat.
- **Endpoint** `GET /api/images/{type}?version&names=…[&retry=1]`
  (`ImageStatusController` + `ImageStatusResolver`, ≤ 48 noms, `no-store`) :
  lecture manifeste seule ; seul le **dernier essai** du client (`retry=1`)
  ré-enfile l'ingestion — pas de double fetch pendant que le flush initial
  tourne.
- **Un seul propriétaire du sibling WebP** : `App\Service\Storage\WebpSibling`,
  exposé en Twig par `webp_sibling()` (`_webp_source` l'utilise) et consommé
  par l'endpoint (`{src, webp}` dérivés côté serveur, le client ne dérive rien).
- **Partials** : `components/ui/resource_image` (picture | slot en attente |
  initiales), `resource_image_template` (un `<template>` par grille, cloné par
  le client), `codex/entity_card_image` (classes de la carte définies une fois).
  Grilles sous `[data-img-scope]` (type, version, taille de lot).
- **Front** : `assets/vue/images/imageLifecycle.ts` (états `data-img-state`
  loading/loaded/error sur `img.hx-img`, absorbe l'ancien `fx/imageFallback`),
  `pendingImages.ts` (IntersectionObserver, lots de 48, backoff 1.5 → 24 s,
  5 essais, arrêt sur visite Turbo), `imageStatusClient.ts` ;
  CSS `foundation/images.css`.
- Vérifié live : `/16.11.1/objects` froid → 702 slots ; 8 s plus tard
  l'endpoint les renvoie résolus.
