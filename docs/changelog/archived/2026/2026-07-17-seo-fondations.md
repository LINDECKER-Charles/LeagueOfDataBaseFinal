---
date: 2026-07-17
type: feat
scope: full-stack
title: Le site devient lisible par les moteurs de recherche
summary: Vraies pages 404, sitemap complet, titres et descriptions propres en 21 langues, et données structurées pour de meilleurs aperçus Google et réseaux sociaux.
tags: [seo, i18n]
---

## Ce qui change

Chercher un champion, un objet ou une rune sur Google mène maintenant à la bonne
fiche, avec un titre clair (« Aatrox, LoL champion — League Of Data Base ») et une
description traduite dans la langue du site. Les partages sur Discord, Reddit ou
Twitter affichent une carte propre avec le bon titre, la bonne image et le nom du
site. Une adresse inexistante affiche désormais une vraie page « introuvable » aux
couleurs du site au lieu de renvoyer silencieusement à l'accueil.

## Pourquoi

Les fiches n'étaient presque pas référençables : titres bruités (numéro de patch,
pagination), descriptions dans une seule langue, aucune carte de plan du site pour
les moteurs, et les pages inconnues répondaient « tout va bien » en redirigeant
vers l'accueil — ce que Google pénalise (soft-404).

## Détails

- Vraie page 404 Hextech (et page d'erreur générique) quand une ressource
  n'existe pas dans le patch consulté.
- Plan du site `/sitemap.xml` : accueil, listes, toutes les fiches détail, pages
  légales et don (~900 URLs), régénéré en continu depuis les données du patch.
- `robots.txt` enrichi : zones privées exclues, lien vers le sitemap.
- Titres et meta descriptions uniformisés et traduits dans les 21 langues du site
  (nouveau catalogue dédié `seo`).
- Données structurées schema.org : site + organisation, fil d'Ariane et fiche
  « League of Legends » sur chaque détail, listes d'entrées sur chaque catalogue.
- URL canonique sur chaque page : les variantes `?version&lang` ne comptent plus
  comme des doublons.

## Technique

- `App\Service\Seo\JsonLdBuilder` (nœuds schema.org + encodage anti-XSS
  `JSON_HEX_TAG|JSON_HEX_AMP|JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE`),
  `App\Service\Seo\OgLocale` (locale UI → `og:locale`), `App\Twig\SeoExtension`
  (`seo_canonical`, `seo_absolute`, `seo_title`, `seo_jsonld*`) ; tests unitaires
  dédiés dans `tests/Unit/Service/Seo` et `tests/Unit/Twig`.
- Canonical = scheme + host + path sans query (politique : la sélection
  version/langue est une variante de rendu, pas un document distinct).
- `ResourceNotFoundException` (extends `RuntimeException`) levée par les
  `getByName` des 4 managers ; `AbstractResourceController::detailFailure()`
  la convertit en `NotFoundHttpException`, le reste garde le redirect flash
  historique. Lookup déplacé avant le fetch d'image (pas d'appel CDN pour un
  slug inconnu).
- `SitemapController` (`/sitemap.xml`, cache public 1 h) : slugs énumérés via
  `listIndex()` sur la dernière version, langue de référence `en_US` (ids
  invariants par langue) ; section en échec ignorée plutôt qu'un 500.
- Domaine de traduction `seo` (`translations/seo.<loc>.yaml`, 21 locales) —
  `messages.*` intouchés.
- Templates d'erreur `templates/bundles/TwigBundle/Exception/error{404,}.html.twig`
  (rendus en prod uniquement, sans view-model `client`).
