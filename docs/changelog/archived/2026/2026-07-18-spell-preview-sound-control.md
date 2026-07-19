---
date: 2026-07-18
type: fix
scope: front
title: Contrôle du son dans l'aperçu des sorts d'un champion
summary: Les aperçus de sorts démarrent en silence, avec un bouton pour activer le son ; changer de sort coupe le son du précédent.
tags: [champion, sorts, audio, accessibilité]
---

## Ce qui change

Dans l'aperçu vidéo des sorts d'un champion (rail P/Q/W/E/R), les animations
démarrent désormais **sans son**. Un bouton haut-parleur permet d'activer ou de
couper le son quand vous le souhaitez, et votre choix est conservé d'un sort à
l'autre. En changeant de sort, le son du sort précédent s'arrête immédiatement —
plus de boucle audio qui continue en arrière-plan.

## Pourquoi

L'aperçu jouait la bande-son du sort en boucle sans aucun moyen de la couper, et
le son du sort précédent continuait après un changement de sort.

## Technique

- La boucle audio venait de l'attribut `muted` du `<video>`, non appliqué en
  propriété IDL par Vue (vuejs/core#3057) : la vidéo jouait donc à voix haute.
- `useVideoPlayback` possède maintenant l'état `isMuted` (mute par défaut,
  compatible autoplay) appliqué sur la propriété `video.muted`, un `toggleMute`,
  et met en pause l'élément précédent lors du swap keyed (coupe la boucle audio).
- `AbilityShowcase` expose un bouton son (label i18n `video_mute`/`video_unmute`,
  ajoutés aux 21 catalogues), `aria-pressed` reflétant l'état.
