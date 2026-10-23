---
date: 2026-10-23
type: fix
scope: back
title: Les anciens patchs s'affichent de nouveau en grec, en thaï et en russe
summary: Certaines pages des patchs les plus anciens restaient en erreur dans quelques langues ; elles s'affichent de nouveau.
tags: [catalogue, anciens-patchs, langues]
---

## Ce qui change

Les pages des patchs les plus anciens (jusqu'en 2015) s'affichent de nouveau dans les langues
où certaines restaient en erreur, comme le grec, le thaï ou le russe.

## Pourquoi

Le fichier des sorts d'invocateur de ces versions est plus long dans ces langues, et une
valeur inhabituelle (une portée écrite « self » au lieu d'un nombre) le rendait illisible en
entier.

## Technique

- `LenientNumberListConverter` et `LenientIntConverter` passaient la valeur ignorée par
  `Utf8JsonReader.Skip()`, qui lève sur tout bloc sauf le dernier : lu en flux, un
  `summoner.json` de plus de 16 Ko échouait (`UpstreamDocumentException`, classée transitoire,
  donc 503). Ils passent maintenant par `TrySkip()` (`ValueSkipping`). Les fixtures, réduites
  sous 16 Ko, ne le voyaient pas : `LenientConvertersTests` lit un fichier de 40 Ko.
- Vu en prod le 2026-10-23 : la passe 5 du précalcul est restée bloquée plus de 2 h sur une page
  de 16.9.1 en grec. Le correctif de `PageRunner` part dans un commit séparé (20 refus au plus
  par page, pouls battu pendant l'attente).
