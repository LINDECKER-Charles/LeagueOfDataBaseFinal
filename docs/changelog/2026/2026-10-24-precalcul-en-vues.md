---
date: 2026-10-24
type: feat
scope: front
title: La page de précalcul de l'administration se parcourt en six vues
summary: Des tuiles-onglets résument la passe, la couverture, les files, la vitesse, le stockage et l'historique, et mènent chacune à sa vue.
tags: [administration, précalcul, navigation]
---

## Ce qui change

La page « Précalcul du catalogue » de l'administration ne s'empile plus sur une vingtaine
d'écrans. Six tuiles, sous l'en-tête, disent d'un coup d'œil où en sont la passe, la couverture
des anciens patchs, les files automatiques, la vitesse, le stockage et l'historique, et chacune
ouvre sa vue. La vue choisie reste dans l'adresse de la page, les commandes d'une passe en
cours sont en haut de son suivi, et la couverture n'ouvre plus que les saisons qui tiennent à
l'écran.

## Technique

- `admin/widgets/status-tabs/` : onglets ARIA en tuiles (`StatusTabs`, `StatusTile`), dont les
  panneaux restent des `lodb-tab`.
- `prerender/overview/tiles/` (`tilesOf`, une fonction pure par tuile) et
  `prerender/overview/view/` (`?view=`, remplacé dans l'historique) ; `RevisionReportStore` et
  `SpeedReportStore` rejoignent `DemandReportStore` ; `PrerenderKpis` disparaît.
- `openSeasonsOf` borne les saisons ouvertes d'office à 60 lignes ; « Tout déplier / replier ».
- Les actions de la passe montent en tête de `LiveMonitor` ; « Stockage et coûts » reprend ce
  que disaient les tuiles retirées (epoch servie, part de la file dans le total).
- Spécification : amendement du 2026-10-24 de `docs/produit/pre-rendu-admin-ux.md`.
