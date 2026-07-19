---
date: 2026-07-19
type: feat
scope: front
title: Choix du champion en portrait, avec liste repliable dans la forge
summary: Le champion choisi s'affiche en vignette et la liste de sélection se replie/déplie.
tags: [builds, forge, ui]
---

## Ce qui change

Dans l'éditeur de build, quand tu choisis un champion, il s'affiche désormais
par sa vignette (liseré doré) plutôt que par son nom écrit. La liste de
sélection se replie en un bandeau compact qui montre le champion choisi : tu
l'ouvres pour en changer, elle se referme automatiquement dès ta sélection.
Les vignettes de la grille sont aussi resserrées — plus de champions visibles
d'un coup d'œil.

## Détails

- Bandeau repliable : portrait + nom du champion choisi, bouton ouvrir/fermer
  avec chevron ; ouvert par défaut à la création, replié à l'édition.
- Repli automatique de la grille dès qu'un champion est sélectionné.
- Repli sur le nom si le portrait est indisponible (ex. champion absent du
  patch sélectionné).

## Technique

`ChampionPicker.vue` : header `.forge-champ-toggle` (`aria-expanded` +
`aria-controls`) sur `v-show` de la grille ; `choose()` referme au `select`.
`<img>` conditionnelle sur `selected.image` (fallback nom). CSS `.forge-selected`
(médaillon 2.75rem) ; `.forge-champs` resserré (`minmax 4.25→3.25rem`, gap
0.6→0.45rem, max-height 21→18rem). Libellés `champion.open`/`champion.close`
(fr + en, fallback en pour les autres locales).
