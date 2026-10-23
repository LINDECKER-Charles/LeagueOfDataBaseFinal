---
date: 2026-10-23
type: feat
scope: front
title: Le nombre de cartes par page se choisit aussi en bas des listes
summary: Les listes des champions, objets, runes et sorts proposent le choix 12, 24, 48 ou Tout sous la grille, plus seulement au-dessus.
tags: [catalogue]
---

## Ce qui change

Arrivé au bas d'une liste de champions, d'objets, de runes ou de sorts d'invocateur, on peut
changer le nombre de cartes par page (12, 24, 48 ou Tout) sans remonter en haut : le même
choix est repris sous la grille. Il n'y apparaît pas quand la liste tient de toute façon sur
la plus petite page.

## Technique

- `PageSizePicker` (`results/page-size-picker.ts`) : les tailles de page, sorties de
  `FilterToolbar`, qui l'affiche toujours en tête des résultats.
- `CatalogueList` le répète sous la grille quand les cartes retenues dépassent la plus petite
  taille (`PAGE_SIZES[0]`) ; jamais sous un squelette, une liste vide ou « aucun résultat ».
- Changer la taille ramène en page 1 et en haut de la page, comme tout changement de la liste
  (`scrollPositionRestoration: 'enabled'` du routeur à l'écriture de l'URL).
