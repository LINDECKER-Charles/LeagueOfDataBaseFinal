---
date: 2026-10-04
type: fix
scope: front
title: Le décor des pages courtes descend jusqu'au pied de page
summary: Sur un grand écran, les Tendances, le partage d'un build et les pages Développeurs et API ne laissent plus de bande vide sous leur contenu.
tags: [tendances, builds, affichage, grand-ecran]
---

## Ce qui change

Quand une page a peu de contenu — des Tendances avec un seul build, un build partagé, la
page Développeurs ou le portail API — son décor (le dégradé et la trame d'hexagones)
occupe désormais toute la hauteur jusqu'au pied de page.

## Pourquoi

Sur un écran haut, le décor s'arrêtait sous la dernière carte : une large bande unie, d'une
autre teinte, séparait le contenu du pied de page.

## Technique

L'hôte de ces pages ne remplissait pas le `main` (`flex-1`) de la coquille : il prend
désormais la hauteur restante, comme la page d'erreur et les pages de compte, et le
conteneur du `lodb-backdrop` grandit avec lui. Test E2E : `layout.spec.ts`, « short pages
on a tall screen » (1920 × 2000).
