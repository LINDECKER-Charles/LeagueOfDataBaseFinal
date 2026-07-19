---
date: 2026-07-19
type: fix
scope: back
title: Les pages de détail (champion, objet, sort) se rechargent à nouveau
summary: Corrige une régression qui renvoyait chaque page de détail vers l'accueil avec un message d'erreur.
tags: [regression, champion, item, summoner]
---

## Ce qui change

Les pages de détail d'un champion, d'un objet ou d'un sort d'invocateur s'ouvrent
de nouveau normalement. Elles ne renvoient plus vers la page d'accueil avec un
message « Données absentes ».

## Pourquoi

Depuis le dernier refactoring backend, ouvrir une page de détail déclenchait une
erreur interne : le joueur était systématiquement redirigé vers l'accueil, ce qui
donnait l'impression que le site « ramait » et rendait les champions inaccessibles.

## Technique

Le refactoring de mutualisation des managers (`c7ce1df`) avait supprimé la méthode
publique `getImage()` d'`AbstractManager` **et** de `CategoriesInterface` en la
croyant morte, alors que les 3 contrôleurs de détail (`ChampionController`,
`ItemController`, `SummonerController`) l'appellent toujours pour l'image
principale. L'appel levait `Call to undefined method`, capturé par le
`catch (\Throwable)` des actions détail → `detailFailure()` → redirection accueil
+ flash « Données absentes » (d'où la lenteur perçue et l'impossibilité de charger
une page).

Correctif : réintroduction d'un point d'entrée public unique `getImage(version, name)`
dans `AbstractManager` (délègue au `resolveImage()` protégé toujours présent),
déclaré au contrat `CategoriesInterface`, avec suppression des paramètres morts
(`$dir`, `$lang`) de l'ancienne signature. Les 3 appels contrôleurs sont alignés
sur la signature propre `getImage($version, $name . '.png')`.

Vérifié : `tests/Unit` verts (415), pages détail champion/objet/sort en HTTP 200
avec image résolue (plus d'erreur ni de flash).
