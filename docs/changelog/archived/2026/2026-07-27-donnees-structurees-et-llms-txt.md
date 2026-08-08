---
date: 2026-07-27
type: feat
scope: back
title: Les fiches décrivent enfin leurs vraies données aux moteurs
summary: Chaque fiche expose ses valeurs réelles (stats, prix en or, délais) aux moteurs de recherche et aux IA, et un fichier /llms.txt résume le projet.
tags: [seo, donnees-structurees, ia]
---

## Ce qui change

Les moteurs de recherche et les assistants IA voient désormais le contenu réel
des fiches, et plus seulement leur nom :

- une fiche **champion** annonce ses rôles, son type de ressource, ses notes de
  profil Riot et ses statistiques de base ;
- une fiche **objet** annonce son coût total, son coût de combinaison, sa valeur
  de revente et ses catégories ;
- une fiche **sort d'invocateur** annonce son délai de récupération, sa portée et
  son niveau de déblocage ;
- une **branche de runes** annonce son nombre d'emplacements.

Un fichier `/llms.txt` décrit par ailleurs le projet en clair pour les IA : ce
qu'il publie, le patch courant, les pages principales et ce qu'il ne fait pas.

## Pourquoi

Une IA à qui l'on décrivait le site — « un site qui liste tous les objets de LoL
avec leur prix en or » — ne le trouvait pas : les pages n'exposaient qu'un nom,
une image et une phrase de description. Rien ne disait qu'on y trouve des prix,
des statistiques ou des délais, ni même ce qu'est le projet dans son ensemble.

## Détails

- Le robots.txt n'impose plus d'attente de 10 secondes entre deux pages : un
  parcours complet du site prenait plusieurs heures par patch, plus lent que le
  rythme des patchs eux-mêmes.
- Les pages À propos, Données et FAQ sont ajoutées au plan du site.
- Les robots des assistants IA restent explicitement autorisés.

## Technique

Le vocabulaire schema.org est éclaté par responsabilité : `JsonLdEncoder`
(encodage + élagage, la frontière de sécurité `JSON_HEX_TAG` inchangée),
`SiteGraphJsonLd` (graphe de site), `GameEntityJsonLd` (entités Data Dragon),
`ContentJsonLd` (FAQPage / AboutPage / Dataset), `JsonLdBuilder` gardant les nœuds
génériques (fil d'Ariane, listes, Person / Article).

**Entités de jeu.** `GameEntityJsonLd` projette les tableaux Data Dragon bruts en
`PropertyValue`, avec `unitText` explicite pour l'or. Choix assumé : schema.org n'a
pas de vocabulaire pour des statistiques de jeu, et un objet payé en or n'est pas un
`Product` avec une `Offer` (pas de devise réelle, rien d'achetable) — `PropertyValue`
énonce les mêmes faits sans revendiquer une sémantique intenable. Lecture défensive
de bout en bout : un patch qui retire une clé dégrade le nœud au lieu de casser la
page. Les courbes `*perlevel` sont volontairement exclues (elles ne se lisent pas
comme un fait isolé).

**Graphe de site.** `WebSite` / `Organization` / `WebPage` sont désormais émis en un
seul `@graph` dont les nœuds se référencent par `@id` (`publisher`, `isPartOf`) —
c'est ce qui permet à un consommateur de les résoudre en **une** entité au lieu de
trois fragments homonymes. `Organization` porte `sameAs` (dépôt GitHub + profil),
paramétré dans `config/packages/seo.yaml`.

Pas de `SearchAction` : le site n'a pas d'URL de résultats de recherche rendue côté
serveur (le filtrage des listes est client), et un `SearchAction` pointant vers une
page qui ne répond pas à la requête est invalide.

**`/llms.txt`** (convention llmstxt.org) : `LlmsTxtBuilder` est une fonction pure de
`(origine, InventorySnapshot)`, servie en `text/markdown` avec un cache d'une heure,
comme le sitemap. Rédigé en anglais quelle que soit la locale : il est servi sur une
URL fixe à des crawlers sans session, donc sans locale à honorer.
