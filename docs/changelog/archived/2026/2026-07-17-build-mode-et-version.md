---
date: 2026-07-17
type: feat
scope: full-stack
title: Builds par mode de jeu et épinglés sur leur patch
summary: Un build cible désormais un mode (Faille, ARAM, Nexus Blitz, Arène) et un patch précis — objets filtrés par mode, rendu figé sur la version choisie.
tags: [builds, modes, versions]
---

## Ce qui change

Chaque build se forge désormais pour un **mode de jeu** (Faille de l'invocateur,
ARAM, Nexus Blitz, Arène) et pour un **patch précis**, choisis en tête de
l'éditeur. La recherche d'objets ne propose que les objets réellement
disponibles dans le mode sélectionné, et changer de mode signale visuellement
les objets déjà posés qui n'y existent pas — sans jamais les supprimer à votre
place. La page de partage d'un build s'affiche épinglée sur son patch d'origine
(noms et icônes de l'époque), avec une pastille du mode et du patch.

## Pourquoi

Un build ARAM n'a pas de sens avec des objets réservés à la Faille, et un build
soigneusement rédigé sur un patch ne doit plus être re-daté silencieusement au
patch du jour à chaque retouche.

## Détails

- Sélecteurs « Patch » (30 dernières versions + celle du build) et « Mode de
  jeu » dans l'éditeur ; les catalogues (champions, runes, objets) se
  rechargent selon la sélection.
- Enregistrement : le mode et la version soumis sont validés (mode connu,
  version existante, objets disponibles dans le mode) puis stockés tels quels.
- Erreur lisible en cas d'objet indisponible dans le mode, listant les objets
  fautifs par leur nom.
- `/b/{token}` : rendu épinglé sur le patch du build ; pastille de mode et
  pastille « Forgé sur le patch X — actuel : Y » purement informative.
- Les builds existants restent valides : mode « Faille de l'invocateur » par
  défaut, aucun changement de comportement sans action de votre part.

## Technique

- Colonne `builds.game_mode` VARCHAR(16) NOT NULL DEFAULT 'sr'
  (migration `Version20260717150000`), portée par l'enum PHP
  `App\Service\Picker\GameMode` (sr→11, aram→12, nexus_blitz→21, arena→30).
  Modes limités aux cartes nommées par DDragon et réellement peuplées dans
  item.json (les cartes 22/33/35 — TFT, Swarm, Brawl — sont exclues : 0 objet
  ou `MapName` vide).
- `ItemOptionsProjector::project(..., GameMode)` remplace le map id « 11 » en
  dur ; `unavailableOn()` fournit les noms pour l'erreur de mode.
  `/api/picker/items?mode=` validé avec repli déterministe `sr`, mode écho dans
  le payload, cache public inchangé (le mode fait partie de l'URL).
- `BuildSubmission` porte `game_version`/`game_mode` (repli JS-less : version de
  la page, mode sr) ; `BuildCatalogGate::validate(structure, version, lang,
  mode)` retourne des tuples (code, params) traduits par le contrôleur ;
  `persistSubmission` stocke la version soumise (suppression du re-stamp par la
  version courante).
- `BuildViewAssembler::assemble(build, version épinglée, version courante,
  lang)` + `renderVersion()` ; images anciennes résolues via le blob store
  content-addressed, repli `.forge-ghost` existant sinon.
