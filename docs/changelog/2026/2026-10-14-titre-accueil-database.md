---
date: 2026-10-14
type: fix
scope: front
title: L'accueil anglais se présente comme une base de données League of Legends
summary: Dans les résultats de recherche en anglais, l'accueil s'intitule « League of Legends database », les mots que tapent les joueurs qui cherchent le site.
tags: [seo]
---

## Ce qui change

En anglais, le titre et la description de l'accueil affichés par les moteurs de recherche
parlent de « League of Legends database » (et de « LoL ») plutôt que d'« encyclopedia ».
Un joueur qui cherche une base de données du jeu reconnaît plus vite le site dans les
résultats.

## Pourquoi

Les recherches anglaises qui font apparaître l'accueil sont « league of legends database »,
« lol db », « lol database » ou « league of legends db », alors que le titre ne contenait
aucun de ces mots.

## Technique

- `public/i18n/seo/en.json` : `home.title` passe à « League Of Data Base — League of Legends
  database » (48 caractères) et `home.description` commence par « The League of Legends
  (LoL) database ».
- Les autres langues gardent leur titre : les requêtes relevées sont toutes en anglais.
- Mesure : impressions et position de l'accueil sur les requêtes contenant « database » ou
  « db » dans Search Console, sur 28 jours, à comparer à la semaine 2026-S41
  (« league of legends database » : 8 impressions, position 30,5).
