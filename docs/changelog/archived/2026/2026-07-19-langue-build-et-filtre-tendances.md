---
date: 2026-07-19
type: feat
scope: full-stack
title: Langue de rédaction des builds et filtre par langue dans les Tendances
summary: Choisissez la langue de votre build à la forge et filtrez les Tendances par langue.
tags: [builds, tendances, forge, i18n, filtre]
---

## Ce qui change

Quand vous forgez un build, vous choisissez désormais la **langue dans laquelle
vous l'avez rédigé** (le nom et la description). Par défaut, c'est la langue que
vous consultez, mais vous pouvez en choisir une autre — pratique pour écrire un
build en anglais afin de toucher plus de monde.

Sur la page **Tendances**, un nouveau filtre **Langue** apparaît à côté des
filtres Champion et Mode. Il est réglé sur **Toutes les langues** par défaut ;
sélectionnez-en une pour ne voir que les builds rédigés dans cette langue. La
langue de chaque build est aussi affichée sous forme de pastille, dans la liste
des Tendances comme sur la page partagée du build.

## Pourquoi

Les noms et descriptions de builds sont du texte libre : un joueur hispanophone
n'a pas forcément envie de parcourir des builds rédigés en coréen. Le filtre
permet de rester dans les langues qu'on comprend, tout en gardant l'accès à tout
par défaut.

## Détails

- Sélecteur de langue à la création et à la modification d'un build.
- Filtre Langue sur les Tendances, « Toutes les langues » par défaut.
- Le filtre ne propose que les langues effectivement présentes parmi les builds
  publics.
- Pastille de langue dans la liste des Tendances et sur la page publique du build.

## Technique

- Nouvelle colonne `builds.language` (locale Data Dragon, ex. `fr_FR`),
  `NOT NULL DEFAULT 'en_US'` — les builds antérieurs sont rétro-remplis en `en_US`
  par le défaut de colonne Postgres (migration `Version20260719150000`).
- La langue est une **métadonnée de rédaction** : le build ne stocke que des IDs
  Data Dragon (langue-agnostiques), donc elle n'affecte jamais le rendu, seulement
  le filtre. Les catalogues de l'éditeur restent résolus sur la langue de
  navigation.
- Le nom de champ/paramètre est `language` (et non `lang`, déjà réservé par
  `PageContextResolver` pour la langue d'affichage).
- Les critères de filtrage des Tendances sont regroupés dans un value object
  `TrendsFilter` (champion, mode, langue) consommé par `BuildVoteRepository`.
- i18n fr + en ; les autres locales retombent sur le fallback anglais.
