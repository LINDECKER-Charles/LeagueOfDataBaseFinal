---
date: 2026-07-17
type: feat
scope: full-stack
title: Forgez vos builds de champion et partagez-les par lien
summary: Éditeur de runes et d'ordre d'achat, builds privés ou publics, page de partage accessible à tous.
tags: [builds, runes, objets, partage, comptes]
---

## Ce qui change

Votre compte d'invocateur sait désormais forger des builds : choisissez un
champion, composez votre page de runes comme dans le client (voie principale,
clé de voûte, voie secondaire à deux runes de rangées différentes) et décrivez
votre ordre d'achat étape par étape — Départ, Premier retour, Cœur… — avec le
coût en or calculé à chaque étape.

Chaque build possède un lien de partage : envoyez-le à vos amis, il s'ouvre
sans compte, même si le build est privé (seul le lien y donne accès). Les
builds publics ont vocation à apparaître sur votre profil public.

## Détails

- « Mes builds » : la liste de vos créations, avec portrait du champion, clé de
  voûte, visibilité publique/privée, actions modifier et supprimer (avec
  confirmation).
- Éditeur en trois sections : recherche de champion en grille de portraits,
  plateau de runes teinté aux couleurs de l'arbre choisi, étapes d'achat avec
  recherche d'objet, notes optionnelles et réordonnancement par boutons
  (accessible au clavier, pas de glisser-déposer).
- La rangée secondaire applique la règle du client LoL : deux runes de deux
  rangées différentes, re-choisir une rangée remplace sa rune, une troisième
  rangée évince la plus ancienne.
- Page de partage « parchemin de stratégie » : sceau du champion, page de
  runes, chronologie d'achat à nœuds dorés, coût total, bouton copier le lien.
- Un build rédigé sur un ancien patch reste lisible : les éléments disparus
  s'affichent en fantôme avec l'étiquette « indisponible sur ce patch », et un
  badge rappelle le patch d'origine.

## Technique

- `BuildController` (CRUD sous `^/builds`, ROLE_USER, ownership → 404 sans
  oracle, erreurs → re-render 422 avec structure re-proposée) +
  `BuildShareController` (`/b/{token}` public — `/build/{token}` est occupé par
  l'output statique Vite via le `location /build/` nginx, le nom de route
  `app_build_show` reste le contrat).
- Cœur pur `BuildStructureValidator` (codes d'erreur stables `build.error.*`,
  couverts par 24 tests) + `BuildStructureNormalizer`, `BuildSubmission`,
  `BuildCatalogGate` (I/O managers), `BuildViewAssembler` (projection
  version-courante, dégradée jamais cassée).
- Îlots Vue `build-editor` (modules purs `runeRules`/`stepList`/`structure` +
  composables `useBuildEditor`/`usePickerCatalog`, specs vitest — dont la règle
  d'éviction FIFO secondaire) et `copy-link` (clipboard + fallback).
- Consomme l'API picker `/api/picker/{champions,items,runes}` ; styles
  `builds.css` (`.forge-*`, `.bsteps-*`, `.bshare-*`) sur les primitives
  Hextech ; CSRF stateless `submit` + intentions par-build pour la suppression.
