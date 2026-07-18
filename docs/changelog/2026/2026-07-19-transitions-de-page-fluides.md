---
date: 2026-07-19
type: feat
scope: front
title: Navigation entre les pages plus fluide et sans temps mort
summary: Transitions animées entre les pages, préchargement au survol et retour visuel immédiat pendant le chargement.
tags: [navigation, turbo, ux, performance]
---

## Ce qui change

Passer d'une page à l'autre est désormais fluide : un fondu enchaîné anime le
changement au lieu d'un saut brut, une fine barre dorée apparaît vite pour signaler
qu'une page charge (fini l'impression que « rien ne se passe »), et les pages sont
préchargées dès que le curseur survole un lien — au clic, elles sont souvent déjà
prêtes.

## Pourquoi

Après un clic, il pouvait s'écouler un moment sans aucun retour visuel, donnant
l'impression que l'application ne réagissait pas. Le site n'est pas une SPA : la
navigation repose sur Turbo Drive (pages rendues côté serveur, échangées sans
rechargement complet). On exploite désormais pleinement ses capacités plutôt que
de tout réécrire en client.

## Détails

- Transitions de page animées (fondu), respectant « mouvement réduit ».
- Barre de progression dorée (thème Hextech) au bout de 150 ms de chargement.
- Préchargement des liens au survol (déjà actif par défaut, confirmé).

## Technique

Turbo Drive 8, sans passage en SPA (conservation du SSR par patch, des analytics
serveur au `kernel.terminate`, du stockage sans DB et des îlots Vue — une seule
couche data). Lot :
- `<meta name="view-transition" content="same-origin">` dans `base.html.twig`
  (View Transitions API, progressive enhancement — swap instantané sinon).
- `assets/styles/turbo.css` : re-skin `.turbo-progress-bar` en dégradé gold + tuning
  durée/easing des `::view-transition-*` avec garde `prefers-reduced-motion`.
- `setProgressBarDelay(150)` dans `main.ts` (défaut 500 ms → 150 ms).
- Prefetch-au-survol : déjà ON par défaut en Turbo 8 (aucun `<meta turbo-prefetch>`
  désactivant), laissé tel quel.

Écarté volontairement : `data-turbo-permanent` sur le header / la bottom-nav — ces
éléments portent un état actif par page (`text-gold-bright`, `aria-current`), qu'un
élément permanent figerait sur la première page visitée. Le fondu View Transitions
couvre le besoin de fluidité sans ce piège.
