---
date: 2026-07-18
type: feat
scope: full-stack
title: Icônes de statistiques sur les fiches champion et objet
summary: Chaque statistique de jeu affiche son icône, et les objets gagnent un vrai tableau de stats lisible.
tags: [champion, item, i18n, ui]
---

## Ce qui change

Les statistiques de jeu sont désormais illustrées par leur icône du jeu — dégâts
d'attaque, puissance, vitesse d'attaque, points de vie, armure, résistance magique
et vitesse de déplacement. Les fiches d'**objet** gagnent en plus un vrai tableau
de statistiques (PV, dégâts, armure, mana, coup critique, vol de vie…), à côté de
la description. Les libellés de stats sont maintenant traduits dans les 21 langues.

## Pourquoi

Repérer d'un coup d'œil ce qu'apporte un objet ou un champion : une ligne
« ⚔ Dégâts d'attaque +75 » se lit bien plus vite qu'un bloc de texte.

## Détails

- Icônes officielles (CommunityDragon) pour 7 stats. Les stats sans icône fiable
  disponible à l'unité (mana, régénérations, portée, coup critique, vol de vie)
  restent en texte pour l'instant — l'icône s'ajoutera dès qu'une source existe.
- Nouveau tableau de statistiques sur la fiche objet.
- Tableau champion enrichi des icônes ; le curseur de niveau est inchangé.
- Libellés de stats traduits dans les 21 langues (auparavant en anglais pour la
  plupart des langues).

## Technique

- Catalogue unique `App\Stat\GameStat` (enum) : clé i18n, icône, et mapping des
  clés Data Dragon du bloc item `stats`. Exposé aux templates via
  `App\Twig\StatExtension` (`stat_icon_url`, `item_stats`) ; markup `.stat-board`
  partagé entre champion et objet.
- Icônes statiques bundlées sous `public/icons/stats/*.png` (source CommunityDragon
  `game/assets/perks/statmods`, set indépendant de la version DDragon), servies
  directement par nginx — hors pipeline MinIO, choix KISS pour un set figé de 7
  fichiers de 32×32.
- Namespace i18n `stat.*` (12 clés × 21 locales) : source unique partagée
  champion + objet ; les anciennes clés `champion.detail.stats.{attack_damage…}`
  (présentes seulement en en/fr) sont supprimées.
- Rappel : Data Dragon ne structure que 12 clés « classiques » dans item `stats` ;
  les stats modernes (ability haste, pénétration, létalité, ténacité) vivent dans
  la description et ne sont donc pas dans le tableau.
- Tests : `tests/Unit/Stat/GameStatTest`, `tests/Unit/Twig/StatExtensionTest`.
