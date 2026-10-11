---
date: 2026-10-11
type: perf
scope: front
title: Des pages plus rapides à afficher, surtout sur mobile
summary: L'image principale passe en premier et arrive plus légère ; le reste attend qu'on en ait besoin.
tags: [performance, mobile]
---

## Ce qui change

Les pages du catalogue s'affichent plus vite, en particulier sur un téléphone ou une connexion
lente : l'image principale arrive en premier et plus légère, et ce qui se trouve plus bas ne
consomme ni données ni calcul tant qu'on n'y arrive pas. La page reste entière dès son
affichage.

## Détails

- L'illustration d'un champion (en tête de sa fiche, sur les cartes de la liste et de
  l'accueil) arrive au format WebP, à la taille de l'écran, depuis le site ; seule l'image
  principale de la page est demandée en priorité.
- La vidéo d'un sort ne se télécharge que lorsque la moitié de son cadre est à l'écran, et se
  met en pause quand on s'en éloigne. En mode économie de données ou sur une connexion lente,
  l'image fixe du sort reste affichée.

## Technique

- Art des champions : `srcset`/`sizes` construits depuis `centeredArtVariants` (héros, `sizes`
  calqué sur la boîte 62vh en 16:9) et `loadingArtVariants` (cartes de la liste et de
  l'accueil), dans une `<source type="image/webp">` ; l'URL Data Dragon reste le `src` de repli
  (anciens patchs, variantes pas encore stockées). Le héros redescend d'un cran à chaque échec :
  variantes, art centré, splash.
- `lodb-image` sépare `eager` (chargée tout de suite) et `priority` (`fetchpriority="high"`) :
  un seul candidat LCP par page (héros, première carte de la liste des champions, icône des
  fiches objet, rune et sort, portrait d'un build partagé), la première rangée restant
  `eager`.
- Quand l'art LCP vient de Data Dragon, le rendu serveur ajoute `Link: <origine>;
  rel=preconnect` (`PageResponse.preconnect`) : la connexion s'ouvre dès les en-têtes. Aucun
  préconnect vers CommunityDragon : seules les pastilles de chroma, au fond de la galerie,
  en viennent.
- Vidéo d'un sort : `preload="none"`, plus d'`autoplay` ; un `IntersectionObserver` (seuil
  0,5) la crée et la lance, la met en pause hors écran, sans jamais passer outre la pause du
  lecteur ; poster en `loading="lazy"`. `Save-Data` ou `effectiveType` 2g/3g : poster seul.
