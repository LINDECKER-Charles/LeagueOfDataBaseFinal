---
date: 2026-10-21
type: fix
scope: front
title: Le lien « Dans le dernier patch » d'un ancien patch ne nomme plus un patch dépassé
summary: Sur la fiche d'un ancien patch, le lien vers la même fiche au dernier patch ne porte plus de numéro de version, qui pouvait rester celui d'un patch précédent.
tags: [catalogue]
---

## Ce qui change

Sur la fiche d'un champion, d'un objet, d'une rune ou d'un sort à un ancien patch, le lien
vers la même fiche au dernier patch s'intitule désormais « Dans le dernier patch → ». Il ne
montre plus de numéro de version, et mène toujours à la fiche du patch en cours.

## Pourquoi

Les fiches des anciens patchs sont servies toutes prêtes pour s'afficher plus vite. Le numéro
du dernier patch y restait celui du jour où la fiche avait été préparée : après une mise à
jour du jeu, la fiche annonçait par exemple 16.20.1 alors que le dernier patch était 16.21.1.

## Technique

- `previous-patches.ts` : le lien `detail.history.latest` (clé existante, 21 langues) porte
  l'adresse courte (`injectCatalogueLink`, version non épinglée) et une flèche décorative
  (`aria-hidden`, retournée en RTL) ; `meta.latest` ne décide plus que de sa présence.
- Constat sur le staging : la page Zed 16.19.1 stockée et son rendu SSR ne différaient que
  par ce numéro (16.20.1 stocké, 16.21.1 en ligne).
