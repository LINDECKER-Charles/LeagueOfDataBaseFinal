---
date: 2026-10-10
type: fix
scope: back
title: Une réponse « réessayez » rapide quand le site est saturé, plutôt qu'une longue attente
summary: Quand la base de données est très sollicitée, le site répond aussitôt de réessayer un peu plus tard au lieu de faire attendre puis d'échouer.
tags: [disponibilite, performance]
---

## Ce qui change

Lors d'un pic de fréquentation, une demande qui ne peut pas être servie tout de suite reçoit
en quelques secondes une réponse « service occupé » qui indique quand réessayer, au lieu de
rester bloquée. La base de données n'est plus jamais submergée par le site lui-même : le reste
du site continue de répondre pendant le pic.

## Technique

- Pool Npgsql borné dans les deux chaînes de connexion (`compose.yaml`, `compose.deploy.yaml`) :
  `Maximum Pool Size=32;Timeout=5;Application Name=lodb-api`.
- `IExceptionHandler` de l'hôte (`src/LoDb.Api/Hosting/Errors/`) : pool épuisé sous `/api` →
  `503` ProblemDetails `service-busy` avec `Retry-After` (`LoDb__Hosting__DatabaseBusyRetryAfter`,
  5 s par défaut), journalisé en `database.pool.exhausted` ; base injoignable → `500` et compteur
  `lodb_db_unavailable_total` (meter `LoDb.Database`), journalisé en
  `database.server.unavailable`. `/v1` garde son enveloppe d'erreur.
