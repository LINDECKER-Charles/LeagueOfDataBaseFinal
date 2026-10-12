---
date: 2026-10-12
type: feat
scope: full-stack
title: Les noms des skins apparaissent dès l'ouverture d'une fiche champion
summary: La section Skins nomme chaque skin avant même que la galerie ne se charge, et les moteurs de recherche les lisent enfin.
tags: [champions, seo]
---

## Ce qui change

La section Skins d'une fiche champion affiche désormais le nom de chaque skin dès l'ouverture de
la page, à la place de cases vides, en attendant que la galerie se charge au défilement. Ces
noms font partie de la page envoyée par le serveur : les moteurs de recherche et les assistants
qui ne lancent pas la galerie les lisent aussi, là où ils ne voyaient qu'un nombre de skins.

## Technique

- API : `ChampionDetails.alternateSkinNames`, les noms affichés (`SkinName.Display`) des skins
  hors skin de base, dans l'ordre de la galerie ; contrat OpenAPI et client régénérés
  (`npm run api:generate`). Les skins eux-mêmes restent lus à part par la galerie.
- Front : le `@placeholder` du `@defer (on viewport)` de la section, rendu par le serveur,
  liste une tuile par nom (squelette d'image, nom en légende, comme la galerie).
