---
date: 2026-07-27
type: feat
scope: back
title: Listes longues de la console admin repliées par défaut
summary: Les sections à forte cardinalité n'affichent plus que leurs premières lignes, avec un bouton pour dérouler le reste.
tags: [admin, ux]
---

## Ce qui change

Dans la console d'administration, les sections qui peuvent compter des dizaines
de lignes — pays, versions stockées, langues, pages les plus vues, référents,
objets les plus lourds, complétude par version — n'affichent plus que leurs
premières entrées. Un bouton « + N de plus » déroule la suite, et « Réduire » la
replie.

## Pourquoi

Les pages Stockage, Trafic et Audience s'étiraient sur plusieurs écrans dès que
le nombre de versions ingérées ou de pays visiteurs augmentait, noyant les
sections situées en dessous.

## Détails

- Les barres restent proportionnées au maximum de la série entière, replié
  compris : le classement ne change pas quand on déroule.
- Le repli fonctionne sans JavaScript.

## Technique

Le repli est un `<input type="checkbox">` masqué piloté par un `<label>` (aucun
JS), avec une option `limit` ajoutée aux macros `rank`/`table`/`matrix` de
`templates/admin/_ui.html.twig`. Ces macros passent au passage à une map
d'options, ce qui ramène leur signature sous la limite de 4 paramètres du dépôt.
