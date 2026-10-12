---
date: 2026-10-12
type: feat
scope: front
title: L'illustration d'une fiche champion est décrite
summary: Le grand visuel en tête de chaque fiche champion porte un texte alternatif, lu par les lecteurs d'écran et la recherche d'images.
tags: [champions, accessibilite, seo]
---

## Ce qui change

L'illustration en tête d'une fiche champion était muette pour les lecteurs d'écran. Elle porte
maintenant un texte alternatif, « Illustration d'Aatrox » en français, dans les 21 langues du
site. La recherche d'images sait aussi désormais ce qu'elle montre.

## Technique

- `champion-hero.html` : `alt` traduit par la clé `champion.detail.splash_alt`
  (`{{ name }}`), ajoutée aux 21 catalogues racine de `public/i18n/`.
