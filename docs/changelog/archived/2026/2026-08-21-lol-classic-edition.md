---
date: 2026-08-21
type: feat
scope: full-stack
title: Prise en charge de League of Legends Classic (objets et sorts jumeaux)
summary: Les objets et sorts d'invocateur de LoL Classic sont reconnus comme une édition à part — bonne icône, badge, filtre et lien vers leur version actuelle.
tags: [items, summoners, lol-classic, filter, builds, api]
---

## Ce qui change

Depuis l'arrivée de League of Legends Classic, Data Dragon livre deux versions
de nombreux objets et sorts d'invocateur sous le **même nom** : « Charme
féerique » existe en version actuelle (`1004`) et en version Classic (`771004`),
« Saut éclair » en `SummonerFlash` et `SummonerFlash_Jade`. Le site les
distingue désormais partout :

- chaque objet affiche **sa propre icône** dans la liste, sur l'accueil et dans
  les pickers de build (l'icône Classic s'affichait pour les deux) ;
- les cartes et pages de l'édition Classic portent un badge **LoL Classic**, et
  le titre de la page le mentionne ;
- les listes d'objets et de sorts gagnent un sélecteur **Édition** (Toutes /
  Actuelle / LoL Classic), combinable avec la recherche et les catégories ;
- la page de détail d'un objet ou d'un sort propose un lien direct vers sa
  version jumelle (« Voir la version actuelle » / « Voir la version LoL
  Classic ») et affiche la map « Classic Rift » ;
- la navigation précédent / suivant signale quand le voisin est l'édition Classic.

## Pourquoi

Les deux « Charme féerique » de la liste affichaient la même icône (celle de
Classic), et rien ne permettait de savoir laquelle on consultait. Les objets
Classic apparaissaient aussi dans le picker de build ARAM, en double des objets
actuels.

## Détails

- Builds : les objets LoL Classic ne sont plus proposés dans les modes de jeu
  actuels (Faille, ARAM, Nexus Blitz, Arène) et sont refusés à l'import ; un
  message d'indisponibilité cite l'objet Classic avec son id pour lever l'ambiguïté.
- API de recherche (`/api/objects/search`, `/api/summoners/search`) et API
  publique `/v1/trends` : chaque résultat porte `edition` (`modern` | `classic`).
- Sur la page d'un sort, le mode `CLASSIC` de Data Dragon (la Faille standard)
  est désormais libellé « Summoner's Rift » pour ne pas être confondu avec LoL Classic.

## Technique

- Règle d'édition centralisée dans `App\Service\API\Edition` : objet Classic =
  id `^77\d{4}$` (les flags `maps` de DDragon sont incohérents sur cette plage :
  la plupart flaggés ARAM, un sur la Faille, et tous les objets actuels flaggés
  `453`) ; sort Classic = mode `JADE`. Jumeau : `771004 ↔ 1004`,
  `X_Jade ↔ X`, vérifié contre le dataset **et homonyme** (`ResolvesEditionCounterpart`) :
  Riot a réutilisé des ids, `773001` « Sceptre abyssal » n'est pas le `3001` actuel.
- `ItemManager::projectImages` est indexé par **id** (plus jamais par nom) ;
  `getImages()` parcourt la collection avec ses clés, `PickerCatalog` ne
  ré-indexe plus le map d'objets.
- `ResourceFilter` : second axe `data-edition` (ET avec les tags), sélecteur
  affiché uniquement si la grille mélange les éditions.
- Décision : pas de mode de build « Classic » — LoL Classic n'a pas de runes
  réforgées dans Data Dragon, un build Classic serait incohérent (à revoir si
  DDragon expose ses maîtrises).
