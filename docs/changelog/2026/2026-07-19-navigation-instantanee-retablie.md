---
date: 2026-07-19
type: fix
scope: front
title: La navigation entre les pages redevient instantanée
summary: Cliquer d'une page à l'autre affiche de nouveau la page immédiatement, sans le court fondu qui donnait une impression de latence.
tags: [navigation, ux, performance]
---

## Ce qui change

Passer d'une page à une autre est de nouveau **instantané** : la page demandée
s'affiche tout de suite, sans le petit fondu enchaîné qui s'intercalait à chaque
clic. Fini l'impression que « rien ne se passe » l'espace d'un instant.

## Pourquoi

Une amélioration récente animait chaque changement de page par un fondu de ~220 ms
et raccourcissait le délai d'apparition de la barre de progression. Sur des pages
déjà rapides, cet enrobage se ressentait comme une latence plutôt que comme de la
fluidité : la sensation d'immédiateté d'avant avait disparu. On revient au
comportement instantané.

## Technique

Revert de `feat(front/nav): navigation fluide (c4a5079)` : suppression du
`<meta name="view-transition">` (donc plus d'appel à `document.startViewTransition`
sur les visites Turbo same-origin — retour au swap DOM direct), du fichier
`turbo.css` et de son import, et du `Turbo.setProgressBarDelay(150)` (retour au
délai par défaut de 500 ms).

Périmètre volontairement limité à la navigation : la résolution d'images
synchrone (pages détail) est **conservée** — l'analyse montre que sur `main` les
détails champion/objet/invocateur étaient déjà synchrones, et le passage synchrone
des runes corrige un vrai bug d'icônes froides. La régression de fluidité perçue
venait uniquement des view transitions. Switch de version, URLs versionnées SEO,
mutualisation des managers : inchangés.

Le WIP « indicateur de navigation lente » (pansement sur la lenteur perçue) est
retiré du même coup — devenu sans objet une fois la navigation instantanée rétablie.
