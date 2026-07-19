---
date: 2026-07-19
type: fix
scope: front
title: Runes — la ligne secondaire inutilisée se grise quand les 2 runes sont prises
summary: Une fois les 2 runes secondaires choisies, la rangée restée vide s'estompe pour ne plus laisser croire qu'une 3e rune est possible.
tags: [builds, runes, ux]
---

## Ce qui change

Dans l'éditeur de build, la voie secondaire n'accepte que 2 runes. Dès que les
deux sont sélectionnées, la rangée sur laquelle tu n'as rien pris s'estompe
au lieu de rester colorée comme si elle attendait encore un choix.

## Pourquoi

La rangée vide restait en pleine couleur : elle donnait l'impression qu'une
3e rune secondaire pouvait être ajoutée, alors que la limite est de 2.

## Technique

Purement présentation, la règle métier (`runeRules.ts`) est inchangée : la
FIFO qui remplace la plus ancienne sélection quand on clique une nouvelle
rangée reste testée et active. Ajout d'un état `.forge-slot--locked`
(`RuneBoard.vue` + `builds.css`) sur les rangées secondaires non utilisées
quand `secondaryPicks.length === SECONDARY_PICKS` — grisage cohérent avec le
dim existant `.forge-slot--picked`, la rangée reste cliquable (swap).
