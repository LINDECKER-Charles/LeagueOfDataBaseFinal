---
date: 2026-07-19
type: fix
scope: front
title: Le curseur signale les boutons Hextech au survol
summary: Les boutons du site (dont le bouton de validation de version dans la barre de navigation) affichent désormais un curseur « main » au survol.
tags: [ui, hextech, ux]
---

## Ce qui change

Au survol des boutons dorés « Hextech » — en particulier le bouton de validation
du sélecteur de version dans la barre de navigation — le curseur prend la forme
d'une main, indiquant clairement qu'ils sont cliquables.

## Technique

- `cursor: pointer` ajouté aux primitives `hx-btn`, `hx-btn-ghost` et
  `hx-btn-gold` (le manque était systémique, aligné sur `.switcher > summary`
  qui déclarait déjà le curseur).
