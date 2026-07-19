---
date: 2026-07-19
type: fix
scope: front
title: Le son des sorts s'arrête vraiment quand on quitte la page
summary: L'aperçu sonore d'un sort ne continue plus de tourner en arrière-plan après un changement d'onglet ou de page.
tags: [champion, sorts, audio, turbo]
---

## Ce qui change

Quand vous activez le son sur l'aperçu d'un sort puis changez d'onglet de
navigateur ou naviguez vers une autre page, la bande-son s'arrête maintenant
immédiatement. Fini le son qui continuait à tourner en fond « fantôme ». En
revenant sur l'onglet, l'aperçu reprend là où il en était s'il jouait.

## Pourquoi

L'aperçu vidéo restait actif après avoir quitté la page : le son continuait de
se jouer alors que le sort n'était plus visible, parfois pour plusieurs sorts
en même temps.

## Technique

- `main.ts` montait les îlots Vue à chaque `turbo:load` mais ne les démontait
  jamais : le `<video>` détaché gardait son audio vivant (comportement Chrome
  jusqu'au GC). Ajout d'un registre des îlots montés + `teardownIslands` sur
  `turbo:before-cache` / `turbo:before-render` (démonte l'app et efface
  `data-vue-mounted` pour un re-montage propre au retour cache). Montage ignoré
  si le shell a été détaché pendant le chargement du chunk (`el.isConnected`).
- `useVideoPlayback` gère la Page Visibility : pause quand l'onglet passe en
  arrière-plan, reprise au retour uniquement si la lecture était active (jamais
  par-dessus une pause manuelle), et met en pause l'élément courant dans
  `onBeforeUnmount`.
