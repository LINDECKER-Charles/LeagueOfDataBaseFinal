---
date: 2026-10-14
type: fix
scope: front
title: La recherche d'un sort d'invocateur dit à quel niveau il se débloque
summary: La description d'une fiche de sort d'invocateur dans les moteurs de recherche cite le niveau d'invocateur qui le débloque, dans les 21 langues.
tags: [seo, i18n]
---

## Ce qui change

Sous le titre d'une fiche de sort d'invocateur, les moteurs de recherche affichent désormais
le niveau d'invocateur qui débloque le sort (« unlocked at summoner level 4 » pour Barrier),
en plus du délai de récupération, des effets et du patch. Un sort sans niveau connu garde la
description d'avant.

## Pourquoi

Des joueurs cherchent le niveau de déblocage d'un sort (« lol barrier level ») : la fiche
l'affichait déjà, mais sa description dans les résultats n'en parlait pas.

## Technique

- `public/i18n/seo/*.json` : nouvelle clé `summoner.detail.description_unlocked`
  (`{{ name }}`, `{{ level }}`, `{{ version }}`) dans les 21 langues, 160 caractères au plus
  une fois remplie ; `summoner.detail.description` reste pour un sort sans niveau.
- `summoner-head-of.ts` choisit la clé selon `profile.summonerLevel`, déjà fourni par
  `GET /api/catalog/{version}/{lang}/summoners/{id}` : aucun appel de plus.
- Mesure : impressions et position de `/en/summoners/SummonerBarrier` sur les requêtes
  contenant « level » dans Search Console, à comparer à la semaine 2026-S41
  (« lol barrier level » : 5 impressions, position 37,4, sur les anciennes adresses).
