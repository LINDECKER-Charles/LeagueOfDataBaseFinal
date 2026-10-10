---
date: 2026-10-10
type: perf
scope: full-stack
title: Un onglet ouvert avant une mise à jour du site continue de fonctionner
summary: Après une mise à jour du site, un onglet déjà ouvert se recharge de lui-même au lieu d'afficher une erreur.
tags: [disponibilite, performance]
---

## Ce qui change

Si le site est mis à jour pendant que vous le consultez, la page suivante que vous ouvrez
dans le même onglet ne tombe plus sur une page d'erreur : le site se recharge une fois, de
lui-même, et vous arrivez directement sur la page demandée, dans sa nouvelle version.

## Technique

- Navigateur : un import dynamique qui échoue (message de Chromium, Firefox ou WebKit,
  `isChunkLoadFailure`) recharge la page entière à l'adresse demandée, une seule fois
  (`ChunkReload`, adresse retenue en `sessionStorage`) ; un second échec à la même adresse
  affiche la page `500` comme avant, et libère l'adresse pour un essai ultérieur. Sans
  stockage de session (serveur, navigation privée stricte), pas de rechargement.
