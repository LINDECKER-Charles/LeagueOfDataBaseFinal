---
date: 2026-10-10
type: fix
scope: back
title: Le serveur refuse d'emblée les envois démesurés
summary: Les envois de plus de 128 Kio sont refusés avant d'être lus, ce qui préserve la mémoire du serveur et la disponibilité du site.
tags: [disponibilite, securite]
---

## Ce qui change

Rien ne change pour les joueurs : même un build complet, chaque texte à sa longueur maximale,
reste loin de la limite. Les envois anormalement volumineux sont refusés avant d'être lus,
ce qui préserve la mémoire du serveur et donc la disponibilité du site.

## Technique

- Kestrel : `Limits.MaxRequestBodySize` à 128 Kio (`LoDb__Hosting__MaxRequestBodySize`), `413`
  au-delà. Le webhook Stripe garde son propre `RequestSizeLimit` d'1 Mio, qui l'emporte.
- Plus gros corps légitime mesuré par l'E2E (`specs/builds/maximal-build.spec.ts`) : un build
  maximal dont chaque caractère est écrit en échappement `\u`, soit 66 667 octets (≈ 65 Kio) :
  la limite laisse une marge d'environ deux fois.
- Un corps refusé pendant sa lecture par un endpoint qui le lit lui-même répond le code choisi
  par le serveur (`413`, `400`, `408`) au lieu d'un `500`.
