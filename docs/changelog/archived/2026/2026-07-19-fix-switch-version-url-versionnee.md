---
date: 2026-07-19
type: fix
scope: back
title: Changer de version fonctionne aussi sur les URLs versionnées (/{version}/…)
summary: Le switch de version met à jour la page et le bandeau, y compris quand la version est dans le chemin de l'URL.
tags: [versions, navigation, seo]
---

## Ce qui change

Changer de version depuis une page dont l'URL contient déjà la version dans le
chemin (ex. `/10.13.1/champion/Akali`) met désormais à jour la page **et** la
version affichée dans le bandeau, immédiatement. Avant, la page restait bloquée
sur l'ancienne version, même en actualisant.

De plus, la version affichée dans la barre de navigation correspond toujours à
celle réellement affichée par la page (y compris via `?version=` dans un lien
partagé), au lieu de parfois montrer la version de session.

## Pourquoi

La priorité de résolution est : segment de chemin `/{version}/…` > `?version=` >
session. Le sélecteur ne réécrivait que le `?version=` de la query : sur une URL
versionnée par le chemin, l'ancien segment restait et l'emportait → version
inchangée. En parallèle, le bandeau recalculait la version de son côté en
ignorant la query, d'où un affichage parfois incohérent avec le contenu.

## Technique

- `UrlGenerator::rewriteQueryParams` (query-only, mal nommé) remplacé par
  `applySelection(url, version, lang)` : réécrit le **segment de chemin** quand
  l'URL est versionnée (sinon `?version=`), la langue toujours en query ; params
  et fragment préservés. Fichier passé `declare(strict_types=1)` + `final`.
- Source unique de la sélection : `PageContextResolver::selection()` mémoïsé par
  requête, exposé aux templates via la fonction Twig `page_selection()`. Le header
  et la bottom-nav s'en servent au lieu de re-dériver `path ?: session` (qui
  ignorait la query et dupliquait la logique de priorité).
- Nettoyage : `app.request.get('_route')` déprécié → `attributes.get('_route')`.

Tests : `UrlGeneratorApplySelectionTest` (chemin versionné réécrit, query, URL
propre, id numérique ≠ version, fragment/params préservés, `/working-progress`
inerte). `tests/Unit` **427 verts**. Vérifié en conteneur : switch sur
`/10.13.1/champion/Akali` → `/14.9.1/champion/Akali` rendu en 14.9.1 ; bandeau
cohérent avec la query même sans session.
