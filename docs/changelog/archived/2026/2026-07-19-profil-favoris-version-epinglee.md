---
date: 2026-07-19
type: fix
scope: full-stack
title: Favoris du profil épinglés à une version, plus de disparition ni d'effacement
summary: Vos favoris de profil ne disparaissent plus (et ne sont plus effacés) quand vous consultez une version du jeu où ils n'existent pas ; un sélecteur permet désormais de choisir la version d'affichage de vos favoris.
tags: [profil, favoris, versions]
---

## Ce qui change

Le profil dispose d'un sélecteur **Version** au-dessus de vos favoris. Il fixe le
patch sur lequel vos favoris (champion, objet, rune, sort, bannière de skin)
sont affichés et enregistrés. L'option « Version courante » suit simplement la
version que vous consultez, comme avant.

## Pourquoi

Avant, les favoris étaient résolus sur la dernière version consultée. En
naviguant vers un patch plus ancien où votre champion favori n'existait pas
encore, il **disparaissait** du profil — et pire, une simple action (basculer la
visibilité public/privé, ou enregistrer) pouvait l'**effacer définitivement**.
Désormais un favori absent de la version affichée est signalé « indisponible »
mais jamais supprimé, et vous choisissez la version qui vous convient.

## Détails

- Nouveau sélecteur de version des favoris sur la page profil.
- Un favori absent de la version affichée est conservé (jamais écrasé au save).
- Votre page publique montre vos favoris sur la version que vous avez épinglée.

## Technique

- `User.preferredVersion` (migration `preferred_version`), résolu par
  `ProfileVersionResolver` (pin valide, sinon version de navigation) — utilisé
  par `ProfileController` (index/preview/save) et `PublicProfileController`.
- `FavoriteSelectionSanitizer` préserve un id inchangé absent du patch au lieu
  de le remettre à `null` ; le save valide à la version épinglée.
- Action `POST /profile/version` ciblée par le `<select>` via l'attribut HTML
  `form=` (pas de form imbriqué dans l'autosave).
