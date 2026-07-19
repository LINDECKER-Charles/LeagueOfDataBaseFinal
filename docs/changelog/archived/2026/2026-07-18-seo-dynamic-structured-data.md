---
date: 2026-07-18
type: feat
scope: full-stack
title: Données structurées enrichies (profils, builds, tendances)
summary: Les pages communautaires exposent désormais des données structurées dynamiques pour de meilleurs aperçus dans les moteurs de recherche.
tags: [seo, structured-data, json-ld, profile, builds, trends]
---

## Ce qui change

Les pages alimentées par les données de la communauté décrivent maintenant leur
contenu aux moteurs de recherche via des données structurées (schema.org) :

- **Profils publics** (`/u/{pseudo}`) — carte d'invocateur décrite comme une
  page de profil, avec la liste des builds publics du joueur.
- **Builds partagés** (`/b/{token}`) — un build public est décrit comme un
  article : titre, auteur, champion concerné, dates de publication et de mise à
  jour, langue.
- **Tendances** (`/trends`) — le classement des builds publics par votes est
  exposé comme une liste ordonnée, régénérée à chaque page.

Les pages Développeurs, Don et Journal des modifications gagnent un fil
d'Ariane structuré. Les titres des pages profil et build suivent désormais le
même format « {page} — League Of Data Base » que le reste du site.

## Pourquoi

Ces pages changent en fonction des données joueurs (favoris, builds, votes) :
un balisage statique ne les représentait pas. Le balisage dynamique permet aux
moteurs d'afficher des aperçus riches (fil d'Ariane, auteur, listes) et de mieux
comprendre la nature de chaque page.

## Détails

- Un build **privé** partagé par lien (non listé, `noindex`) n'émet aucune donnée
  structurée — aucune fuite d'auteur ou de contenu vers l'index.
- L'aperçu du profil (côté propriétaire, `noindex`) n'émet pas non plus de
  balisage : seul le rendu public indexable le fait.

## Technique

- `JsonLdBuilder` : nouveaux nœuds `person()`, `profilePage()`, `article()` +
  helper `prune()` (les champs optionnels absents ne sont jamais émis vides).
- `SeoExtension` : fonctions Twig `seo_profile_page` / `seo_article` ;
  `seo_item_list` et `seo_breadcrumbs` réutilisés (cap `ITEM_LIST_MAX = 20`).
- Blocs `structured_data` ajoutés sur `profile/public`, `build/show`,
  `trends/index`, `developers/index`, `donate/index`, `changelog/index`.
  Titres alignés sur `seo_title()`.
- Tests unitaires `JsonLdBuilderTest` couvrant les nouveaux nœuds et la
  rétention/omission des champs optionnels.
