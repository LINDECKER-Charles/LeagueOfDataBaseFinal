---
date: 2026-10-11
type: fix
scope: front
title: Un seul visiteur ne peut plus occuper le serveur pour les pages récentes
summary: Chaque visiteur a sa part des pages du dernier patch que le serveur prépare ; les autres gardent la leur.
tags: [disponibilite, performance, robots]
---

## Ce qui change

Un programme qui demande énormément de pages du dernier patch encore jamais affichées ne peut
plus prendre à lui seul toute la capacité du serveur de pages : il reçoit sa part, et les autres
visiteurs continuent d'obtenir leurs pages normalement. Une lecture ordinaire du site ne s'en
aperçoit pas : ouvrir le site ne prépare qu'une page, et les clics suivants n'en demandent
aucune au serveur.

## Technique

- Part par client du couloir courant de l'admission du SSR (`src/server/admission/`), sur le
  modèle de la part épinglée : 60 rendus par minute et 20 d'affilée par client (adresse IPv4,
  /64 IPv6, même table LRU). Elle est vérifiée avant le budget global du couloir (300 par
  minute, 60 d'affilée), et rien n'est dépensé tant qu'une vérification échoue : un rendu
  refusé par sa part laisse le budget du couloir aux autres.
- Refus : `503` `no-store` et `Retry-After: 30`, sans ligne par requête ; compté en
  `current`/`client_rate` dans `ssr.admission.summary`, à part de `current`/`lane_rate`.
- Réglable par `LODB_CURRENT_RENDERS_PER_CLIENT_PER_MINUTE` et
  `LODB_CURRENT_RENDER_PER_CLIENT_BURST` : absente, une part se ramène à son tout
  (`LODB_CURRENT_RENDERS_PER_MINUTE`, `LODB_CURRENT_RENDER_BURST`) ; posée au-dessus, le serveur
  refuse de démarrer. La pile de dev les relève pour l'E2E, le calque du banc de charge les rend.
- Un robot sur une seule adresse reçoit toujours les pages que nginx a en cache ; seules ses
  pages absentes du cache au-delà d'une par seconde reçoivent un `503`.
