---
date: 2026-07-19
type: fix
scope: back
title: Les icônes de runes s'affichent dès le premier chargement après un changement de version
summary: Sur une page d'arbre de runes, changer de version ne laisse plus les icônes cassées le temps d'un second passage.
tags: [runes, images, versions]
---

## Ce qui change

Sur une page de détail d'arbre de runes, quand on change de version, les icônes
(keystone et runes mineures) s'affichent immédiatement. Avant, au premier
chargement d'une version pas encore vue, elles restaient cassées jusqu'à ce qu'on
recharge la page.

## Pourquoi

La page de détail des runes récupérait ses images via le chemin « liste »
(chargement différé après la réponse) : sur une version froide, les icônes
n'étaient pas encore prêtes au moment du rendu → images manquantes au premier
passage, correctes seulement à la visite suivante.

## Technique

`RuneManager::getImages()` résout les images via `resolveImages()` avec
`allowDefer = true` (correct pour la **liste** : placeholder puis préchauffage).
La page **détail** (`RuneController::rune`) réutilisait ce même chemin, alors que
les autres détails (champion/objet/sort) résolvent en synchrone via
`AbstractManager::resolveImage` (`allowDefer: false`).

Correctif : nouvelle méthode `RuneManager::getDetailImages(version, tree)` qui
résout la structure imbriquée en **synchrone** (`allowDefer: false`) ; le mapping
imbriqué `treeKey => {icon, slots[...]}` est extrait dans `mapTreeImages()` (DRY,
partagé avec `getImages`). Le contrôleur détail pointe désormais dessus. La liste
et `paginate()` conservent le différé.

Vérifié : version froide `14.20.1`, **premier** hit → 16 icônes résolues
(`/cdn/blobs/…`) au lieu de 0 ; `tests/Unit` verts (415).

Note : `BuildViewAssembler` résout aussi les images de builds via le chemin
différé (champion `getImages()[0]`, runes `getImages()`). Même schéma latent sur
une version froide non préchauffée — à traiter si le rendu des builds partagés le
montre.
