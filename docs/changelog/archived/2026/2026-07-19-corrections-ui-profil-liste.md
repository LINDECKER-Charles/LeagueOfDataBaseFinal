---
date: 2026-07-19
type: fix
scope: front
title: Deux corrections visuelles (interrupteur du profil, cartes de champions)
summary: L'interrupteur de visibilité du profil n'affiche plus de pastille dorée parasite, et les cartes de la liste des champions n'ont plus de double flèche.
tags: [ui, profil, champions, hextech]
---

## Ce qui change

- **Profil** : l'interrupteur « Visibilité du profil » affichait une pastille
  dorée en trop, superposée à la pastille du curseur. Il utilise désormais le
  même composant d'interrupteur que le reste du site — une seule pastille, nette.
- **Liste des champions** : le lien « Voir le détail » affichait deux flèches
  (une dans le texte, une en icône). Il n'en reste qu'une, l'icône animée au survol.

## Technique

- Le toggle du profil réutilise la primitive `hx-switch` (comme l'éditeur de
  build) au lieu d'une variante `label/track/thumb` qui entrait en collision de
  nom de classe avec la primitive et injectait un `::after` en double.
- La flèche « → » a été retirée de la chaîne `common.detail` (21 locales) ; le
  bouton conserve son icône SVG.
