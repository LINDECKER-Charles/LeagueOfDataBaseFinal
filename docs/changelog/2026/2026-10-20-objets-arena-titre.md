---
date: 2026-10-20
type: fix
scope: front
title: Une variante d'objet de l'Arène porte son mode dans les résultats de recherche
summary: Le titre, la description et les données structurées d'un objet réservé à l'Arène, à Nuée, à Baston ou à Nexus Blitz nomment son mode, comme celles d'un objet LoL Classic nomment son édition.
tags: [seo, i18n]
---

## Ce qui change

Dans les moteurs de recherche, la fiche de la Titanic Hydra de l'Arène s'appelle désormais
« Titanic Hydra (Arena) », et celle de la Faille reste « Titanic Hydra ». Les deux fiches ne
se présentent plus sous le même titre. L'adresse des fiches ne change pas.

## Pourquoi

Le catalogue compte 865 fiches d'objets pour 544 noms : la plupart des doublons sont des
variantes de l'Arène (identifiants 22xxxx et 44xxxx) qui portaient exactement le titre de
l'objet de la Faille.

## Technique

- `item-head-of.ts` : un objet moderne dont `profile.maps` ne contient ni la Faille (11) ni
  l'ARAM (12) prend en qualificatif le libellé de ses cartes (`map.<id>`, via `mapLabelKey`,
  joints par `Intl.ListFormat`), dans le titre, la description, le fil d'Ariane et le JSON-LD ;
  l'édition Classic garde son qualificatif. Slug et `canonicalPath` inchangés.
- Libellés : `map.21`, `map.30`, `map.33` et `map.35` sont traduits en anglais et en français ;
  les 19 autres langues reprennent le libellé anglais, comme les filtres de la liste.
- Non couvert : les variantes 32xxxx et 66xxxx (323075 Thornmail, 663060 Sword of the Divine),
  que Data Dragon place sur la Faille (maps 11) sans autre champ qui les distingue de l'objet
  homonyme, et les variantes sans carte.
- Mesure : état d'indexation de `/en/items/223748-titanic-hydra` et
  `/en/items/3748-titanic-hydra`, impressions des URL contenant `/items/22` sur 28 jours.
