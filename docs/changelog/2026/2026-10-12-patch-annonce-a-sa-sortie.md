---
date: 2026-10-12
type: fix
scope: back
title: Les moteurs de recherche ne voient plus un patch avant sa mise en ligne
summary: Le plan du site n'annonce plus les pages d'un patch que le site n'a pas encore mis en ligne.
tags: [seo, patchs]
---

> **Remplacé en partie le 2026-10-22** par [`une-page-par-etat-dans-google`](2026-10-22-une-page-par-etat-dans-google.md) :
> plus de fenêtre dans `/sitemap.xml` ; le sitemap d'une version listée après le dernier patch
> promu, comme celui d'une version que l'index des états ne couvre pas, répond 404 au lieu
> d'être servi, et avant toute promotion l'index n'annonce que les `latest.xml`. Un patch pas
> encore mis en ligne n'est toujours pas annoncé.

## Ce qui change

Le plan du site remis aux moteurs de recherche n'annonce plus les pages d'un patch publié par
Riot mais pas encore mis en ligne ici. Ces pages changent d'adresse à la mise en ligne du
patch : les annoncer avant revenait à faire indexer des adresses vouées à rediriger.

## Technique

- `SitemapLocations.Index` : la fenêtre de `/sitemap.xml` ne prend plus que les versions
  listées avant la dernière promue, comme le dit l'amendement du 2026-10-11 de l'ADR 0005 ;
  une version plus récente, que Data Dragon liste déjà, en sort. Avant toute promotion, les
  premières versions listées la remplissent encore.
- Le sitemap d'une telle version reste servi à son URL (cache d'une heure), comme avant.
