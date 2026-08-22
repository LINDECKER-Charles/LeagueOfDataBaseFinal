---
date: 2026-08-22
type: feat
scope: full-stack
title: Filtres avancés par catégorie, partageables par lien
summary: Chaque liste de l'encyclopédie gagne des filtres adaptés à sa catégorie (rôle, ressource, stats, carte, palier, prix, mode, voie, emplacement…), combinables finement et mémorisés dans l'URL pour être partagés.
tags: [champions, items, runes, summoners, filter, data, url]
---

## Ce qui change

- **Champions** : rôle, ressource (mana, énergie, sans ressource…), portée
  (mêlée / distance), évaluations (difficulté, attaque, défense, magie) et
  stats de base au niveau 1 (PV, armure, résistance magique, dégâts d'attaque,
  vitesse d'attaque, vitesse de déplacement) par plages.
- **Objets** : catégories avec choix « au moins une » / « toutes », édition
  (actuelle / LoL Classic), carte ou mode, palier (composant / épique /
  légendaire), achetables ou consommables seulement, prix, et une plage par
  statistique (PV, AD, AP, armure, RM, vitesse d'attaque, critique, vol de
  vie, mana, vitesse de déplacement…).
- **Runes** : la liste présente désormais **chaque rune** (62) et non plus les
  5 voies — filtrable par voie et par emplacement (clé de voûte, rangées 1 à
  3), avec recherche dans le nom et la description courte ; un bandeau des
  voies reste en tête et chaque rune mène directement à sa place dans la page
  de sa voie.
- **Sorts d'invocateur** : mode de jeu, édition, niveau requis, temps de
  recharge.
- Les filtres principaux restent sous la main ; les autres s'ouvrent dans un
  panneau « Filtres avancés » (sur mobile, dans le volet habituel, groupes
  repliés par défaut). Les filtres actifs s'affichent en puces que l'on retire
  d'un clic, avec un bouton **Copier le lien**.
- **L'URL reflète les filtres** (`/objects?tag=Armor,Health&tag_all=1&price=0-3000`) :
  un lien partagé rouvre exactement la même sélection, y compris après un
  changement de patch ou de langue, et dans n'importe quelle langue.

## Pourquoi

Le filtrage se limitait à la recherche par nom et aux étiquettes brutes de
Data Dragon, identiques pour toutes les catégories, sans possibilité de
croiser des critères chiffrés ni de partager une sélection — à contre-courant
d'un site pensé pour explorer la donnée.

## Technique

- **Schémas de facettes serveur** (`App\Service\Catalog\Facet`) : un schéma par
  ressource (`ChampionFacets`, `ItemFacets`, `RuneFacets`, `SummonerFacets`)
  décrit les facettes (`FacetDefinition` : choice / range / toggle) et dérive
  les valeurs de chaque carte ; Twig les émet en `data-f-<clé>` via
  `facet_attrs()` et passe le schéma à l'îlot via `facet_schema()`
  (`App\Twig\Codex\FacetExtension`, dépend de `PageSelectionInterface`).
- **Propriétaires uniques** : `GameMap` (ids de carte, consommé par `GameMode`
  et la facette carte — 453 exclu, c'est une édition), `GameModeLabels`
  (liste des modes, partagée par `GameModesExtension` et la facette ; la regex
  du template summoner disparaît), `GameStat::itemStatColumns()` (stats
  objets, fractions ×100 pour les pourcentages), `DdragonText::tagLabel()`.
- **Normalisations vérifiées sur 16.16.1** : ressource champion = slug
  locale-indépendant dérivé du dataset `en_US` par id (« Energy » / « Énergie »
  → `energy`), portée mêlée ≤ 325 / distance ≥ 350 (Nilah, Rakan, Lillia,
  Urgot), palier structurel (sans `depth` + `into` = composant ; depth 2 =
  épique ; ≥ 3 = légendaire ; consommables et pseudo-objets sans palier), pas
  de valeur de stat pour un bloc vide, `shortDesc` des runes nettoyé avant
  indexation.
- **Îlot** : `filter/facets.ts` (modèle + transitions pures),
  `visibleCards.ts` (requête ET facettes), `useCardGrid.ts` (lecture
  `data-f-*` + univers des valeurs présentes), `useFacetState.ts`,
  `filter/url/` (`urlState.ts` : URL canonique — ordre du schéma, valeurs
  triées, défauts omis, bornes explicites ; `useUrlSync.ts` : écriture
  throttlée 300 ms, `try/catch` pour la limite Safari ; `turboLocation.ts` :
  réécriture via `Turbo.session.history` + `lastRenderedLocation`, feature-
  détectée, pour que le Back retrouve le snapshot). Composants
  `components/catalog/facets/` (FacetChoice, FacetRange — double curseur
  natif, commit sur `change` —, FacetToggle, FacetPanel, ActiveFilters).
  Styles globaux `codex/filter.css` (l'ancien CSS scopé disparaît).
- Le switcher version/langue conserve désormais les paramètres étrangers
  (`loader/urls.ts`), ancres `id="rune-{key}"` ajoutées au détail des voies,
  `RuneManager::countRunes()` pour l'en-tête.
- i18n : nouvelles clés `filter.*`, `facet.*`, `map.*` en `en` et `fr`
  (repli `en` pour les autres locales).
