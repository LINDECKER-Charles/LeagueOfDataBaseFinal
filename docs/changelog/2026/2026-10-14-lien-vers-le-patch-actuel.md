---
date: 2026-10-14
type: fix
scope: front
title: Une fiche d'ancien patch mène à la même fiche au dernier patch
summary: Les fiches des anciens patchs ont un lien vers la même fiche au patch en cours.
tags: [seo, catalogue]
---

## Ce qui change

La fiche d'un champion, d'un objet, d'une rune ou d'un sort ouverte dans un ancien patch
affiche, au-dessus de « Dans les patchs précédents », un lien « Dans le dernier patch » vers
la même fiche au patch en cours. Un objet ou un sort retiré du jeu n'a pas ce lien : il
n'existe plus au dernier patch.

## Pourquoi

Les moteurs de recherche entrent encore par les adresses des anciens patchs, sans trouver
de lien vers la fiche du patch en cours, la seule à indexer : Google connaissait la fiche
du Warden's Mail au patch 10.13.1 et ignorait tout de celle du dernier patch.

## Technique

- `PreviousPatches` reçoit le `canonical` de la fiche et lie son URL courte quand l'API
  place l'entité au dernier patch (`version: null`) ; rien sur la page du dernier patch,
  ni pour une entité retirée ou hors de l'index. Aucun catalogue chargé en plus.
- Clé `detail.history.latest` dans les 21 langues.
