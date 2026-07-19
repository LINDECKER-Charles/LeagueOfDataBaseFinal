---
date: 2026-07-19
type: feat
scope: front
title: Champions — icône fidèle à la version et avertissement sur les vidéos
summary: La liste et la fiche d'un champion affichent désormais son icône du patch sélectionné (qui change selon la version), et les vidéos de compétences sont accompagnées d'un avertissement précisant qu'elles montrent la dernière version des sorts.
tags: [champions, versions, video]
---

## Ce qui change

- **Icône fidèle à la version** : sur l'**accueil** et la **liste des champions**, chaque
  carte porte, en incrustation sur le splash, l'icône carrée du champion ; sur la **fiche**,
  elle apparaît dans la carte de statistiques (en haut à droite). Partout elle est résolue
  pour le **patch sélectionné**. Contrairement aux splash arts (servis par un CDN non
  versionné, donc toujours les plus récents), cette icône change bien selon la version
  consultée.
- **Avertissement vidéos** : sous chaque vidéo de compétence, une note rappelle que
  les vidéos montrent la **dernière version** des sorts et peuvent différer sur les
  anciens patchs.

## Pourquoi

Les splash arts et les vidéos de compétences proviennent de sources non versionnées
côté Riot : elles reflètent toujours l'état le plus récent, même quand on consulte un
vieux patch. L'icône du champion, elle, est versionnée — l'afficher donne un repère
visuel juste, et l'avertissement lève l'ambiguïté sur les vidéos.

## Technique

- Icône (fiche) : `ChampionController` fournit `image` via `getImage($version, …)`
  (ingérée MinIO, versionnée, sibling WebP) ; rendue dans l'aside stats.
- Icône (accueil + liste) : le tableau `images` (déjà fourni par `paginate`, aligné
  positionnellement avec `champions` via `loop.index0`) est incrusté en badge sur chaque
  carte ; masqué tant que l'image est froide (différée).
- Avertissement : nouveau label `champion.detail.video_notice` passé à l'îlot
  `AbilityShowcase`, affiché quand une vidéo est disponible pour la compétence.
