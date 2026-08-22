---
date: 2026-08-22
type: fix
scope: full-stack
title: Fiddlesticks retrouve son art actuel (liste, accueil, skins, bannières)
summary: Depuis LoL Classic, Fiddlesticks affichait son ancien portrait dans la liste et plusieurs de ses skins ne se chargeaient pas ; tout son art est de nouveau le bon.
tags: [champions, skins, lol-classic, images]
---

## Ce qui change

- La carte de Fiddlesticks dans la liste des champions et sur l'accueil montre
  de nouveau son **portrait actuel** (post-refonte) et non l'ancien épouvantail
  de l'époque Classic.
- Dans sa fiche, la galerie de skins affiche **tous** les splashs, y compris
  Star Nemesis, Blood Moon et Flora Fatalis qui restaient vides.
- Le choix de bannière de profil propose les mêmes skins avec leurs vraies
  vignettes.

## Pourquoi

Avec l'arrivée de League of Legends Classic, le CDN de Riot sert désormais
l'art *Classic* sous l'orthographe publique du champion et réserve l'art actuel
à son orthographe interne (« FiddleSticks »). Les skins sortis après la refonte
n'existent que sous cette orthographe interne : ils répondaient en erreur.

## Technique

- Nouveau builder unique `App\Service\API\Champion\ChampionArt` (+ enum
  `ChampionArtKind` splash / loading / centered) : la table d'orthographe
  interne (`Fiddlesticks → FiddleSticks`) s'applique aux **trois** familles
  d'art, plus seulement à `centered/` (vérifié : casse interne = 200 sur
  3 chemins × 13 skins réels ; casse publique = art Classic pour 0–8, 403 pour
  27/37/46 en splash, 9/27/37/46 en loading, tout en centered).
- `ChampionSkins` délègue à `ChampionArt` ; nouvelle fonction Twig
  `champion_art(id, kind, num)` (`App\Twig\Codex\ChampionArtExtension`)
  remplace les trois URL concaténées à la main dans `champion/liste`,
  `home/home` et `champion/detail` (view-model de la galerie).
- Sweep des 173 champions × 3 chemins à 16.16.1 : Fiddlesticks est le seul
  cas de divergence.
