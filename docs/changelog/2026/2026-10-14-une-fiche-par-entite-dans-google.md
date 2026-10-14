---
date: 2026-10-14
type: fix
scope: full-stack
title: Les fiches du dernier patch reprennent leur place dans Google
summary: Les pages des anciens patchs renvoient les moteurs de recherche vers la fiche du dernier patch, au lieu de lui faire concurrence.
tags: [seo]
---

## Ce qui change

Chaque champion, objet, rune ou sort existait aux yeux de Google en autant de pages presque
identiques que de patchs, dans chacune des 21 langues : Google n'en gardait qu'une poignée, et
les fiches du patch en cours disparaissaient des résultats. Une page d'ancien patch désigne
désormais la fiche du dernier patch comme la page à indexer. Les anciens patchs restent
consultables comme avant, depuis l'historique des patchs et le bloc « Dans les patchs
précédents » de chaque fiche.

Un objet ou un sort retiré du jeu garde une page indexée : celle du dernier patch où il
existait.

## Pourquoi

Depuis mi-août, le site avait perdu presque toutes ses apparitions dans Google : environ
2 500 pages indexées contre 20 000 fin juillet, et plus de 31 000 pages explorées puis
écartées comme doublons.

## Technique

- ADR 0005, amendement du 2026-10-14 (décision, options pesées, mesure dans la Search
  Console) ; diagnostic : `docs/produit/seo-anciens-patchs.md`.
- API : chaque fiche porte `canonical` (`{ version, path }`, `null` hors de l'index), décidé
  par `CanonicalReferences` (`LoDb.Ingestion/Catalog/Canonicals`) à partir des catalogues en_US
  du dernier patch et de la version suivante. Contrat OpenAPI et client régénérés.
- Front : `buildHeadTags` écrit la canonique, `og:url` et `robots` d'une page d'ancien patch ;
  ni hreflang ni JSON-LD sur une page canonisée ailleurs ou `noindex, follow`.
- Sitemaps : `LoDb__Seo__SitemapVersionWindow` passe à `0` ; le sitemap d'une version passée
  ne liste plus que les fiches des entités dont c'est la dernière version, avec
  `max-age=3600` au lieu d'`immutable`.
- `LODB_ARCHIVE_EPOCH` passe à `3` : les pages d'anciens patchs gardées par nginx portaient
  l'ancienne canonique.
