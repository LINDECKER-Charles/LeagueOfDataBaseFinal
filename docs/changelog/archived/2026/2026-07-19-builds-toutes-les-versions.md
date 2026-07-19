---
date: 2026-07-19
type: fix
scope: back
title: Toutes les versions du jeu sont proposées dans les builds
summary: Les sélecteurs de version de l'éditeur de build et de l'import proposent désormais l'intégralité des patches, plus seulement les 30 plus récents.
tags: [builds, versions]
---

## Ce qui change

Le menu de version dans l'éditeur de build — et celui du transfert d'un build vers
un autre patch — liste maintenant **toutes** les versions du jeu, comme le sélecteur
en haut du site et celui du profil. Auparavant seuls les 30 patches les plus récents
étaient proposés.

## Pourquoi

La liste était tronquée aux 30 derniers patches : impossible de créer ou de projeter
un build sur une version plus ancienne, alors que le reste du site expose la liste
complète. C'était une incohérence, corrigée ici.

## Technique

- `BuildController::versionChoices()` : suppression du `array_slice(..., 30)`
  (constante `VERSION_CHOICES_MAX` retirée) ; renvoie `VersionManager::getVersions()`
  entier. Le garde-fou « patch épinglé absent de la liste (délisté upstream) → ajouté »
  est conservé pour qu'un vieux build reste éditable.
- `templates/build/index.html.twig` : le `|slice(0, 30)` de la liste d'import passe à
  la liste complète (`client.versions`), le merge par ligne du patch propre au build
  est conservé.
