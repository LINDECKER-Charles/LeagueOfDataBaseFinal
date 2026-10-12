---
date: 2026-10-12
type: fix
scope: front
title: Chaque page n'a plus qu'une seule adresse
summary: Une adresse terminée par une barre oblique, ou l'accueil sans la sienne, redirige vers l'adresse officielle de la page.
tags: [seo]
---

## Ce qui change

`/fr/champions/Aatrox/` et `/fr/champions/Aatrox` affichaient la même page sous deux adresses,
tout comme `/fr` et `/fr/` pour l'accueil. La variante redirige désormais vers l'adresse
officielle : l'accueil d'une langue garde sa barre oblique finale (`/fr/`), aucune autre page
n'en a. Les moteurs de recherche ne voient plus qu'une adresse par page.

## Technique

- `preRouteOf` (`src/server/routing`) : avant tout rendu, 301 `no-store` vers le chemin
  canonique, la query d'une liste ou d'une fiche nettoyée dans le même saut.
- `lodb_ssr_normalized_redirects_total` gagne le motif `slash` (`metriques.md`).
- `tools/routing/check-urls.sh` : `/en` répond 301 vers `/en/`, plus trois lignes de slash
  final.
