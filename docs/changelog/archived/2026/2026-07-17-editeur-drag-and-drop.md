---
date: 2026-07-17
type: feat
scope: full-stack
title: Glisser-déposer dans l'éditeur de builds
summary: Réordonnez étapes et objets à la souris — y compris d'une étape à l'autre et depuis la recherche — avec indicateur or et annonces d'accessibilité.
tags: [builds, editeur, drag-and-drop, a11y]
---

## Ce qui change

L'ordre d'achat de l'éditeur de builds se manipule désormais au
glisser-déposer : réordonner les étapes (poignée de saisie), réordonner les
objets d'une étape, déplacer un objet vers une autre étape, et glisser un objet
directement depuis les résultats de recherche vers l'étape voulue. Un
emplacement en pointillé or indique le point de dépôt, l'élément saisi s'élève
légèrement, et Échap annule le déplacement en cours.

## Pourquoi

Réordonner un long build aux seuls boutons ↑↓ était laborieux ; le
glisser-déposer rend la composition immédiate sans sacrifier personne : les
boutons restent le chemin clavier et tactile.

## Détails

- Curseur main ouverte/fermée, indicateur de dépôt `--color-gold-rich` en
  pointillé, transitions respectant `prefers-reduced-motion`.
- Les boutons ↑↓/‹›/× existants sont conservés (clavier, lecteurs d'écran,
  tactile) ; les poignées de drag sont masquées sur pointeur grossier.
- Chaque déplacement (drag comme boutons) est annoncé poliment aux lecteurs
  d'écran (« Étape déplacée en position N », traduit dans les 21 langues).
- Les limites du build restent garanties pendant le drag (8 objets par étape,
  40 au total) : une étape pleine refuse le dépôt.

## Technique

- Aucune lib externe : événements HTML5 drag natifs. Composable réutilisable
  `useDragReorder<S, T>` (machine à états source/cible, annulation Échap via
  dragend natif + écouteur document, commit unique) testé vitest.
- Logique pure étendue dans `stepList.ts` : `moveStepToIndex`,
  `moveItemToIndex`, `insertItem`, `transferItem` — sémantique d'index
  d'insertion avec fix-up après retrait, immutables, testées.
- `StepEditor.vue` calcule les cibles par géométrie (milieu X/Y), rend les
  placeholders `pointer-events: none` ; `ItemSearch.vue` relaie dragstart/end ;
  annonces `aria-live="polite"` dans l'îlot (`useBuildEditor`).
- `aria-grabbed`/`aria-dropeffect` (dépréciés) volontairement non utilisés.
