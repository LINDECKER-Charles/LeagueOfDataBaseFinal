---
date: 2026-10-11
type: perf
scope: full-stack
title: Les fiches champion s'ouvrent plus légères
summary: Les skins et leurs chromas ne se chargent plus qu'au moment où l'on arrive à la galerie.
tags: [performance, champions]
---

## Ce qui change

Une fiche champion n'emporte plus la liste complète de ses skins et de leurs chromas : la page
arrive plus légère et s'affiche plus vite, surtout sur mobile. La galerie des skins se remplit
quand on la fait défiler jusqu'à l'écran, comme ses images le faisaient déjà ; le nombre de
skins reste affiché dans l'en-tête de la section dès l'ouverture de la page.

## Technique

- Nouvel endpoint `GET /api/catalog/{version}/{lang}/champions/{id}/skins` (`ChampionSkins`) :
  mêmes porte froide, budget et en-têtes de cache que le reste du catalogue, aucune limite par
  adresse. Le détail ne porte plus `skins` mais `alternateSkinCount`, sauf pour une app
  installée (en-tête `X-LoDb-Client`, réponse `Vary`) : son front peut précéder `/skins`, et
  l'API des apps ne fait que grandir (ADR 0008). Le champ partira quand la version minimale
  de la politique client de chaque app lira `/skins`.
- Front : `lodb-skin-shelf` lit les skins (`injectChampionSkins`, une seconde tentative après
  5 s) dans le bloc `@defer (on viewport)` de la section, donc jamais côté serveur ; la page
  rendue et son `ng-state` ne contiennent plus ni art ni chroma de skin.
- Référencement : aucun texte visible n'est retiré. L'ancienne page ne passait les skins qu'en
  JSON, dans l'attribut d'un îlot paresseux rendu par un squelette ; le rendu serveur actuel ne
  les affichait pas davantage (`@defer`) : seul le JSON de transfert les portait.
