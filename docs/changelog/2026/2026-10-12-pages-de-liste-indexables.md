---
date: 2026-10-12
type: fix
scope: front
title: Les pages suivantes des listes comptent pour les moteurs de recherche
summary: La page 2 ou 3 d'une liste (objets, sorts, champions) se déclare comme elle-même aux moteurs de recherche, au lieu de renvoyer à la première.
tags: [seo]
---

## Ce qui change

La deuxième page de la liste des objets, par exemple, se présentait aux moteurs de recherche
comme une copie de la première. Elle se déclare désormais comme elle-même : les moteurs suivent
les objets qu'elle seule affiche, comme Google le recommande pour une liste en plusieurs pages.
Les filtres, eux, restent rattachés à la liste entière.

## Technique

- `catalogueListSeo` : au-delà de la première page, la canonique et les 21 alternates portent
  `?page=N` ; la première page et une liste sans pages restent nues.
- `ListHead.page` et `CatalogueListPage.page` : la page que le serveur a rendue
  (`firstPage().page`), pour les champions, les objets et les sorts ; les runes n'ont pas de
  pages.
