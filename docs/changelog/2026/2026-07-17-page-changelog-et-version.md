---
date: 2026-07-17
type: feat
scope: full-stack
title: Journal des versions et numéro de version dans la barre
summary: Un numéro de version s'affiche à côté du nom du site et mène à un journal des versions retraçant toute l'histoire du projet.
tags: [changelog, versioning, navigation]
---

## Ce qui change

Le nom du site, dans la barre du haut, est désormais accompagné d'une pastille
de version (ex. `v2.0.0`). Un clic ouvre une nouvelle page **Journal des
versions** (`/changelog`) qui retrace toute l'histoire du projet, de
l'encyclopédie d'origine à aujourd'hui : chaque version y présente son nom de
code, sa date, ses nouveautés et un mot de l'équipe, les plus récentes en tête.

## Pourquoi

Le site avait accumulé énormément de nouveautés sans qu'un joueur puisse voir,
d'un coup d'œil, ce qui avait changé et quand. La pastille de version rend le
rythme du projet visible, et le journal donne enfin un endroit lisible — sans
jargon technique — pour suivre son évolution.

## Détails

- Pastille de version cliquable à côté du nom (distincte du sélecteur de patch
  Data Dragon, à droite, qui concerne les données du jeu).
- Journal en frise verticale : une entrée par version, dépliable, la dernière
  ouverte par défaut ; ancres par version pour partager un lien direct.
- Contenu joueur : nouveautés, correctifs (à venir) et note d'équipe par
  version ; chrome de la page traduit, contenu éditorial en français.
- Page référencée (titre, description, entrée au plan du site).

## Technique

- Source de version unique : paramètre `app.version` (services.yaml) exposé en
  global Twig `app_version` (twig.yaml) — consommé par la navbar et la page.
- Artefacts publiés `public/changelog/manifest.json` + `<id>.json` par version
  (schéma joueur : features/bugfixes/balances, note d'équipe) ; lus côté serveur
  par `App\Service\Changelog\ChangelogReader` (dégradation en historique vide,
  jamais de 500).
- `ChangelogController` (`/changelog`, `app_changelog`) rend `changelog/index`
  + partial `_release` en SSR complet (no-JS, `<details>` natif) — cohérent avec
  `LegalController`, choix SEO/TTFB plutôt qu'un îlot Vue.
- Styles scopés `assets/styles/changelog.css` (`.cl-*`) sur les tokens Hextech ;
  i18n `changelog.*` + `nav.changelog` en fr/en, les 19 autres locales retombant
  sur l'anglais via `framework.translator.fallbacks: ['en']` (ajouté).
- Entrée `/changelog` ajoutée au `SitemapController`.
