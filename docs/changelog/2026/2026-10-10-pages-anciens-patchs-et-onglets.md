---
date: 2026-10-10
type: perf
scope: full-stack
title: Un onglet ouvert avant une mise à jour du site continue de fonctionner
summary: Après une mise à jour du site, un onglet déjà ouvert garde tout ce qu'il lui faut, ou se recharge de lui-même au lieu d'afficher une erreur.
tags: [disponibilite, performance]
---

## Ce qui change

Si le site est mis à jour pendant que vous le consultez, l'onglet déjà ouvert continue de
fonctionner : les éléments de la version précédente restent disponibles pendant un mois. Et
si l'un d'eux manque malgré tout, la page suivante ne tombe plus sur une erreur : le site se
recharge une fois, de lui-même, et vous arrivez directement sur la page demandée.

## Technique

- Navigateur : un import dynamique qui échoue (message de Chromium, Firefox ou WebKit,
  `isChunkLoadFailure`) recharge la page entière à l'adresse demandée, une seule fois
  (`ChunkReload`, adresse retenue en `sessionStorage`) ; un second échec à la même adresse
  affiche la page `500` comme avant, et libère l'adresse pour un essai ultérieur. Sans
  stockage de session (serveur, navigation privée stricte), pas de rechargement.
- Volume `front-builds` : au démarrage, `web-ssr` y publie ses bundles hachés en arrière-plan
  (`publishFrontBuilds`, copie atomique, jamais de réécriture, date rafraîchie pour un
  fichier déjà présent) ; `/build/` sert ceux de l'image, puis ceux du volume
  (`LODB_FRONT_BUILDS_DIR`). Le job de déploiement supprime ceux qu'aucun build n'a livrés
  depuis `LODB_FRONT_BUILDS_RETENTION_DAYS` jours (30).
- nginx : zone `lodb_assets` pour `/build/` (clé `$uri`, 1 Go, 30 j d'inactivité, réglables
  par `LODB_ASSETS_CACHE_*`), sur un volume ; un `404` n'est jamais gardé.
