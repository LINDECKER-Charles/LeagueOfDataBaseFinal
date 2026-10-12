---
date: 2026-10-12
type: feat
scope: back
title: Une adresse de champion tapée à la main mène à sa fiche
summary: « /fr/champions/wukong » ou « /fr/champions/aatrox » ouvrent la fiche du champion au lieu d'une page introuvable.
tags: [champions, seo]
---

## Ce qui change

Une adresse de champion écrite à la main, en minuscules ou avec le nom du champion plutôt que
son identifiant, mène désormais à sa fiche : `/fr/champions/aatrox`, `/fr/champions/wukong` ou
`/fr/champions/nunu-willump` redirigent vers la bonne page au lieu d'afficher « introuvable ».
Les anciens liens du site écrits de la même façon (`/champion/wukong`) en profitent aussi.

## Technique

- `CatalogSnapshot.FindChampion` : l'id exact, sinon l'id ou le nom anglais repliés
  (`ChampionAliases` : lettres et chiffres ASCII en minuscules, les ids avant les noms).
- La fiche de l'API (`/champions/{id}` et `/champions/{id}/skins`) et l'ancien format
  (`LegacyEntityLookup`) s'en servent : la fiche répond avec son `canonicalPath`, et le rendu
  serveur fait la 301 vers l'URL canonique comme pour un slug faux.
