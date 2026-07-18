---
date: 2026-07-18
type: fix
scope: front
title: Le sommaire d'une fiche champion défile au lieu de recharger la page
summary: Cliquer une section (Sorts, Skins, Lore…) fait glisser la page vers l'ancre sans recharger.
tags: [champion, navigation, ux]
---

## Ce qui change

Sur une fiche de champion, le petit menu de sections en haut (Sorts, Skins, Lore,
Conseils, Stats) fait maintenant défiler la page en douceur jusqu'à la section
choisie. Avant, cliquer un de ces liens rechargeait toute la page.

## Pourquoi

Le clic déclenchait une navigation complète : la page se rechargeait et repartait
du haut au lieu de simplement descendre jusqu'à la bonne section — une gêne à
chaque fois qu'on voulait sauter directement aux skins ou au lore.

## Technique

Sous Turbo Drive, un lien d'ancre same-page (`#skins`) n'est reconnu comme un
simple défilement que si son URL de requête correspond à `lastRenderedLocation`.
Les fiches détail portent une query (`?version&lang`), la comparaison échouait et
Turbo lançait une *visite* Drive (re-render perçu comme rechargement).

Le défilement est désormais pris en charge explicitement : une délégation `click`
au niveau `document` (persistante à travers les visites Turbo, dédupliquée par
référence de fonction, même schéma que la fermeture des switchers) scopée à
`[data-scrollspy] a[href^="#"]` fait `preventDefault` puis `scrollIntoView`
(smooth, respect de `prefers-reduced-motion`) et `history.pushState` pour garder
l'URL profonde-linkable. `scroll-margin-top` gère déjà l'offset du nav collant.
