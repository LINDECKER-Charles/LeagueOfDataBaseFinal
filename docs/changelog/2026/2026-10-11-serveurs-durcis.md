---
date: 2026-10-11
type: devops
scope: infra
title: Les services du site tournent avec le strict minimum de droits
summary: Chaque partie du site s'exécute sans privilège ni moyen d'en obtenir, et la base de données n'est plus joignable que par le service qui s'en sert.
tags: [securite, deploiement]
---

## Ce qui change

Rien ne change pour les joueurs : pages, comptes, builds et paiements fonctionnent comme
avant. En coulisses, chaque partie du site (pages, données, base) tourne désormais avec le
strict minimum de droits : aucune ne peut modifier ses propres fichiers ni obtenir de
privilèges supplémentaires, et la base de données n'est plus joignable que par le service qui
s'en sert.

## Pourquoi

Si une faille venait à toucher une partie du site, elle n'en tirerait presque rien : ni
droits supplémentaires, ni fichiers modifiables, ni accès à la base de données.

## Technique

- `compose.yaml`, ancre `x-hardening` sur tous les services, en local comme sur un hôte :
  `read_only: true`, `cap_drop: [ALL]`, `no-new-privileges`, tmpfs bornés (`/tmp` ;
  `/var/run/postgresql` pour Postgres, qui reprend `CHOWN`, `DAC_READ_SEARCH`, `FOWNER`,
  `SETGID` et `SETUID` pour son entrée).
- nginx sans privilège (utilisateur `nginx`, port 8080) : configuration rendue, pid et
  fichiers temporaires dans `/tmp`, `absolute_redirect off`, labels de l'edge
  `{{upstreams 8080}}`, sonde, smoke tests et port local suivent. Caches `lodb_seo` et
  `lodb_legacy` sur des volumes (`seo-cache`, `legacy-cache`), clé préfixée par la révision.
- Réseaux : Postgres sur le seul réseau `data` (interne sur un hôte), avec l'API et
  `migrate` ; nginx et le SSR ne joignent plus la base. Le réseau `default` garde le
  sous-réseau figé que l'API et le SSR croient. `docs/guides/configuration.md` § 4.3.
