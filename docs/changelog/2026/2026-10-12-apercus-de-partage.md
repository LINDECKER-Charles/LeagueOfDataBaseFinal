---
date: 2026-10-12
type: feat
scope: front
title: Un lien vers une fiche champion s'affiche avec son illustration
summary: Partagé sur Discord, Reddit ou X, le lien d'une fiche champion montre l'illustration du champion au lieu de l'image générique du site.
tags: [champions, partage, seo]
---

## Ce qui change

Le lien d'une fiche champion partagé sur Discord, Reddit, X ou une messagerie s'affiche
maintenant avec l'illustration du champion, au lieu de la même carte « Champions » pour tous.
Chaque aperçu de lien du site annonce aussi sa taille et décrit son image : les aperçus
s'affichent plus vite et sont lisibles par un lecteur d'écran.

## Technique

- `SeoImage` (`core/seo/images/`) : URL, taille et texte alternatif d'une image de partage ;
  `SeoPage.image` accepte une URL seule ou une `SeoImage`.
- `build-head-tags` écrit `og:image:width`, `og:image:height` quand la taille est connue,
  `og:image:alt` et `twitter:image:alt` toujours (le titre de la page par défaut).
- `splashImage` : le splash Data Dragon du champion (1215 × 717), décrit par
  `champion.detail.splash_alt` ; `previewImage` : les cartes de `public/preview/` (1200 × 630),
  pour les listes, les objets, les runes sans icône, les sorts et `about/data`.
