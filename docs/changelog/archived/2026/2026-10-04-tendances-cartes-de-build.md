---
date: 2026-10-04
type: fix
scope: front
title: Les cartes de build des Tendances restent ordonnées sur tous les écrans
summary: Le score, le build, son auteur et ses objets gardent leur place, du téléphone au grand écran.
tags: [tendances, builds, mobile, responsive]
---

## Ce qui change

Sur la page Tendances, chaque carte de build garde la même organisation quelle que soit la
largeur de l'écran : le score de votes à gauche sur toute la hauteur de la carte, puis le
build, son auteur et ses étiquettes, et ses premiers objets.

## Pourquoi

Sur un petit téléphone, le score se retrouvait seul sur une ligne au-dessus du portrait du
champion ; sur une tablette, les objets partaient seuls sur une ligne et l'auteur flottait au
milieu de la carte.

## Détails

- Grand écran : tout tient sur une ligne.
- Tablette : le build et ses objets, puis l'auteur et les étiquettes dessous.
- Téléphone : le build, l'auteur et les objets empilés à côté du score.

## Technique

`trend-row.css` passe d'une ligne flex qui passe à la ligne à une grille à zones nommées
(`vote`, `id`, `meta`, `items`), en propriétés logiques (RTL). Seuils 40rem et 64rem, la
liste faisant 64rem au plus.
