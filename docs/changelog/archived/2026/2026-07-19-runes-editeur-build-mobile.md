---
date: 2026-07-19
type: fix
scope: front
title: Sélecteur de runes lisible sur mobile dans l'éditeur de build
summary: Les rangées de runes ne se cassent plus en pastilles orphelines sur petit écran.
tags: [builds, runes, mobile, responsive]
---

## Ce qui change

Dans l'éditeur de build sur téléphone, chaque rangée de runes affiche désormais
son intitulé (clé de voûte, emplacement…) sur sa propre ligne, puis toutes les
runes alignées proprement en dessous. Fini les pastilles qui débordaient sous le
libellé et cassaient la lecture.

## Pourquoi

Sur mobile, le libellé de rangée occupait une largeur fixe qui écrasait les
runes : la dernière rune de chaque ligne repassait dessous, détachée du reste,
et le tableau des runes devenait illisible.

## Technique

Fix CSS pur (`app/assets/styles/builds.css`). Media query `max-width: 640px` :
`.forge-slot__name` en `flex: 0 0 100%` (libellé pleine largeur → les perks
wrappent en grille cohérente au lieu d'orphelins inline) + pastilles réduites
(`.forge-perk` 2.9rem / `--big` 3.4rem) pour la densité. Repro et validation via
harness Playwright reproduisant le DOM du `RuneBoard` aux largeurs 360/320 px
(hauteur de rangée ~135-163 px → ~90 px, aucun overflow horizontal). Desktop
inchangé.
