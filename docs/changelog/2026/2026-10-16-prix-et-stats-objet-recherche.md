---
date: 2026-10-16
type: feat
scope: front
title: Les résultats de recherche d'un objet donnent son prix et ses statistiques
summary: La description d'une fiche d'objet dans les moteurs de recherche cite son prix total en or et ses principales statistiques, dans les 21 langues.
tags: [seo, i18n]
---

## Ce qui change

Sous le titre d'une fiche d'objet, les moteurs de recherche affichent désormais son prix total
et ses deux ou trois premières statistiques, telles que la fiche les montre (« +36 Attack
damage, +30 % Attack speed, and +333 Health for 3333 gold » pour Trinity Force), avec le
patch. Un objet qui ne s'achète pas ne cite pas de prix, un objet sans statistiques (une
potion, un élixir) cite son seul prix, et un objet qui n'a ni l'un ni l'autre garde la
description d'avant.

## Pourquoi

Des joueurs cherchent un objet par son prix et ses statistiques (« elixir of sorcery 50
ability power 500 gold ») : la fiche les affichait déjà, mais sa description dans les
résultats était la même pour les 865 objets.

## Technique

- `public/i18n/seo/*.json` : nouvelles clés `item.detail.description_full` (prix et
  statistiques), `description_priced` (prix seul), `description_stats` (statistiques seules)
  et `item.detail.stat` (ordre valeur et libellé propre à chaque langue : « 攻撃力+36 » en
  japonais) dans les 21 langues ; `item.detail.description` reste pour un objet sans prix ni
  statistiques.
- `item-description-of.ts` choisit la clé selon `profile.gold` (achetable et prix non nul) et
  `profile.stats`, déjà fournis par `GET /api/catalog/{version}/{lang}/items/{id}` : aucun
  appel de plus. Statistiques formatées comme dans l'encart (`formatItemStat`), libellés du
  catalogue racine (`stat.*`), liste jointe par `Intl.ListFormat` dans la langue de la page ;
  au-delà de 160 caractères, la description cite une statistique de moins, jamais moins
  d'une.
- Le JSON-LD de la fiche porte déjà l'icône de l'objet en `image` (URL absolue du blob
  affiché) ; l'image Open Graph reste la carte `previewImage('items')`, l'icône de 64 px étant
  sous le minimum de 200 px des cartes.
- Mesure : impressions et CTR des fiches `/*/items/*` dans Search Console, à comparer à la
  semaine 2026-S41, en particulier sur les requêtes qui citent un prix ou une statistique.
