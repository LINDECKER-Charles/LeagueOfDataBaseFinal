---
date: 2026-10-12
type: feat
scope: back
title: Les moteurs de recherche savent quand un patch a changé les pages
summary: Le plan du site date chaque page du dernier patch de sa mise en ligne, pour que les moteurs relisent vite les pages qui ont changé.
tags: [seo, patchs]
---

> **Remplacé le 2026-10-22** par [`seo-une-page-par-etat`](../../produit/seo-une-page-par-etat.md) :
> les sitemaps des anciens patchs sont datés à leur tour. Une page d'ouverture d'un état porte
> la date de clôture de l'état, la page des changements d'un patch celle de la promotion qui
> l'a fait passer (ou de la reconstruction de l'index), et chaque entrée de l'index la date la
> plus récente de son fichier. `latest.xml` gagne la page des changements du dernier patch
> quand il a changé quelque chose, datée par sa promotion. Aucune date de sortie n'est
> devinée.

## Ce qui change

Le plan du site remis aux moteurs de recherche indique désormais la date de mise en ligne du
dernier patch sur chaque page qu'il change : accueil, listes, historique des patchs et fiches.
Les moteurs repèrent ainsi plus vite les fiches à relire après un patch. Les pages
éditoriales (à propos, FAQ, mentions) ne portent pas de date : aucun patch ne les change.

## Technique

- `ddragon_version.promoted_at` de la dernière version remonte par `VersionStates.ReadyAsync`,
  `ReadyVersions` (cache) et le lecteur de catalogue (`ICatalogReader.GetLatestPromotedAtAsync`,
  `CatalogVersions.LatestPromotedAt`).
- `SitemapXml` écrit `<lastmod>` (W3C Datetime en UTC) pour une `SitemapEntry` datée :
  `latest.xml` de chaque langue dans l'index, et dans `latest.xml` les pages de
  `SitePages.PatchBound` et toutes les fiches. Les pages de `SitePages.Editorial` et les
  sitemaps des anciens patchs restent sans date : aucune n'est connue.
