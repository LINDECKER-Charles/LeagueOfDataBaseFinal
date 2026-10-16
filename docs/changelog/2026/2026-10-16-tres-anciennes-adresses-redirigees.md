---
date: 2026-10-16
type: fix
scope: back
title: Les plus anciens liens du site retrouvent leur page
summary: Les liens des toutes premières versions du site, comme /champion_redirect/Lucian, ouvrent la fiche ou la liste correspondante au lieu d'une page introuvable.
tags: [seo]
---

## Ce qui change

Les toutes premières versions du site avaient leurs propres adresses
(`/champion_redirect/Lucian`, `/object_redirect/3112`, `/objects_redirect/9/16`…), que
Google et d'anciens favoris suivent encore. Elles affichaient une page introuvable : elles
mènent désormais, en une seule redirection, à la fiche du champion, de l'objet, de la rune ou
du sort dans le patch en cours, ou à la liste correspondante. Un nom inconnu reste une page
introuvable, comme pour les autres anciens liens.

## Pourquoi

Google en explorait encore plusieurs centaines en deux mois, et chacune lui répondait une
erreur au lieu de lui faire retrouver la bonne page.

## Technique

- nginx (`server.d/legacy-redirects.conf`) : les segments de ressource acceptent le suffixe
  `_redirect`, toujours en un seul saut vers l'API (`/api/legacy`), mêmes cache et débit.
- `LegacyPathParser` et `LegacyResources` : `{ressource}_redirect/{name}` se lit comme
  `/{ressource}/{name}` sans version ; `{ressources}_redirect` suivi de zéro à deux segments
  numériques (anciennes pagination et filtres, abandonnés) comme la liste. Toute autre forme,
  versionnée comprise, reste une 404.
- Table des 301 héritées (`plan-migration.md`) ; tests : `LegacyPathParserTests`,
  `LegacyCatalogRedirectTests`, `LegacyRedirectContractTests`, `tools/nginx`
  (`nginx-local.test.mjs`).
