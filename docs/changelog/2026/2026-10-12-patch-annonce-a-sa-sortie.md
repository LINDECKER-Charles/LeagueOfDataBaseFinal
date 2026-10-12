---
date: 2026-10-12
type: fix
scope: back
title: Les moteurs de recherche ne voient plus un patch avant sa mise en ligne
summary: Le plan du site n'annonce plus les pages d'un patch que le site n'a pas encore mis en ligne.
tags: [seo, patchs]
---

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
