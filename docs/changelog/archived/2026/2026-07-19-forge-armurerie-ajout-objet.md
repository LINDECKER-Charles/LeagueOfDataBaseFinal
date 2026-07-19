---
date: 2026-07-19
type: feat
scope: front
title: Armurerie — ajout d'objets par parcours plutôt que par recherche
summary: Un bouton « + Ajouter un objet » ouvre une fenêtre où l'on parcourt tout le catalogue, filtre par catégorie et empile plusieurs objets d'affilée.
tags: [builds, forge, ux, mobile]
---

## Ce qui change

Composer l'ordre d'achat d'un build devient beaucoup plus direct. Sous chaque
étape, une tuile « + Ajouter un objet » ouvre l'**Armurerie** : la liste
complète des objets, scrollable dès l'ouverture, sans avoir à deviner un nom.

Une barre de recherche et des filtres par catégorie (Attaque, Magie, Défense,
Mobilité, Utilitaire) affinent la liste. On clique pour ajouter, et la fenêtre
**reste ouverte** : on enchaîne plusieurs objets pour une même étape en une
seule passe, avec un compteur en direct et une pastille sur les objets déjà
présents dans l'étape.

Sur mobile, l'Armurerie s'ouvre en panneau plein écran ancré en bas, avec des
cibles tactiles plus grandes.

## Pourquoi

L'ancien champ de recherche par étape n'affichait des résultats qu'après avoir
tapé : impossible de parcourir ou de découvrir les objets, et il fallait répéter
l'opération objet par objet. Le nouveau parcours est plus rapide et plus lisible.

## Détails

- Tuile « + Ajouter » par étape, un seul modal partagé qui cible l'étape ouverte.
- Filtres de catégorie dérivés des tags Data Dragon (regroupement lisible).
- Multi-ajout : compteur d'objets ajoutés + pastille de présence par objet.
- Le réordonnancement des objets déjà placés (glisser-déposer, y compris entre
  étapes, et boutons ‹ ›) est inchangé.

## Technique

- Nouveau SFC `ItemArmory.vue` (natif `<dialog>` : top layer, focus trap,
  Escape/back Android) piloté par `StepEditor` (`armoryStep`). Remplace
  `ItemSearch.vue` (supprimé).
- Module pur `itemCategories.ts` (mapping tag→catégorie, testé) — la
  connaissance métier des buckets vit à un seul endroit.
- Le glisser-déposer catalogue→étape disparaît (superseded par le clic-ajout) :
  retrait de la source de drag `catalog` et de `dropNewItem` (`StepEditor`,
  `BuildEditor`, `useBuildEditor`). `insertItem` reste (utilisé par `addItem`).
- `appendItem` annonce désormais poliment (`aria-live`) via le libellé
  `dnd.added` — l'ajout au clic gagne l'accessibilité qu'avait le drop.
- Libellés `armory.*` ajoutés (en + fr) ; `search`/`empty` réutilisent les clés
  `steps.*` existantes.
