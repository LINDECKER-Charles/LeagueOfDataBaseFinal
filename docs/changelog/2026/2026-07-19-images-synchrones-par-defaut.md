---
date: 2026-07-19
type: fix
scope: back
title: Les images se chargent du premier coup partout après un changement de version
summary: Résolution d'images synchrone par défaut ; seules les listes diffèrent le chargement. Corrige aussi les icônes d'objets liés.
tags: [images, versions, runes, objets, architecture]
---

## Ce qui change

Sur une version fraîchement sélectionnée, les images s'affichent dès le premier
chargement sur toutes les pages de détail (champion, objet, rune) — y compris les
icônes des objets liés / de l'arbre de craft d'un objet, qui pouvaient rester
cassées auparavant. Les pages de liste continuent d'apparaître instantanément
(vignettes qui se remplissent juste après).

## Pourquoi

Trois bugs récents (icônes de runes, images de détail, objets liés) avaient la même
cause : le chargement différé des images — pensé pour que les grandes listes
s'affichent vite — s'appliquait aussi à des pages qui ont besoin de leurs images
tout de suite. Sur une version froide, elles se rendaient donc avec des images
manquantes jusqu'au rechargement suivant.

## Technique

Refacto de durcissement (demande : « clean & scalable »). Le différé
(`kernel.terminate`) était le **défaut** de `resolveImages` (`allowDefer = true`),
donc chaque appelant devait penser à demander le mode synchrone — piège que les
runes, les objets liés et `BuildViewAssembler` avaient tous raté.

Inversion : **synchrone par défaut, différé en opt-in explicite**.
- `DeferredImageIngestor` porte désormais l'état : `withDeferral(callable)` ouvre
  un scope (restauré en `finally`, ré-entrant) où `shouldDefer()` devient vrai ;
  hors de ce scope, résolution inline.
- `resolveImages` perd son paramètre `allowDefer` et consulte simplement
  `ingestion->shouldDefer()`.
- Seul `PaginatesResources::paginate()` (le rendu liste/preview, tolérant aux
  placeholders + réchauffé par le loader SSE) ouvre le scope `withDeferral`.
- Conséquences : détail / pickers / recherche / `BuildViewAssembler` deviennent
  synchrones **sans changement de code** (défaut sûr) ; la méthode ad hoc
  `RuneManager::getDetailImages` (ajoutée plus tôt aujourd'hui) devient inutile et
  est supprimée ; le CLI warmup reste inline (pas de requête).

Filet de tests : `DeferredImageIngestorTest` (opt-in + scoping ré-entrant) et
`RuneManagerDeferralTest` (détail cold = inline, liste = différé, CLI = inline).

Vérifié : `tests/Unit` **420 verts** ; en conteneur sur versions froides — détail
rune/champion/objet résolus au 1er hit (13/6/8 blobs), liste champions différée
(0 → 163 au 2ᵉ hit, rendu rapide préservé).
