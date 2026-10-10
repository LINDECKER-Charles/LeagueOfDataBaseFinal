---
date: 2026-10-11
type: perf
scope: full-stack
title: Le site tient mieux la charge et partage mieux son serveur
summary: Chaque partie du site dispose d'une part réservée du serveur, et la version de test n'en prend qu'une petite fraction.
tags: [disponibilite, performance]
---

## Ce qui change

Le site garde sa vitesse quand il est très sollicité. Chacune de ses parties (pages, données,
base) dispose désormais d'une part réservée du serveur, et la version de test du site n'en
prend plus qu'une petite fraction. Sous une charge exceptionnelle, le site demande de
patienter quelques secondes plutôt que de ralentir tout le serveur qu'il partage avec
d'autres services.

## Pourquoi

Le 10 octobre 2026, une vague de requêtes automatiques a occupé tout le serveur, au point que
l'hébergeur l'a bridé pour tous ses services. Le site ne peut plus en prendre davantage que
sa part.

## Technique

- `compose.deploy.yaml` : quota CPU, poids et plafond de processus par service (web-ssr 1,0,
  api 1,25, postgres 0,5, nginx 0,25 cœur ; poids 1024, nginx 2048 ; `pids` 512 pour l'API,
  256 ailleurs), à recalibrer après 7 jours de bridage mesuré (`ConteneurCpuBride`).
- Sonde de santé de l'API toutes les 60 s (2 s au démarrage, `start_interval`, Docker Engine
  25 vérifié par le déploiement) : elle démarre un runtime .NET à chaque passage.
- Postgres : `max_connections=60`, `pg_stat_statements` préchargé et installé par une
  migration additive, requêtes de plus de 500 ms journalisées sans leurs paramètres, session
  inactive en transaction fermée après 60 s, `/dev/shm` de 256 Mio ; réglables par
  `LODB_PG_*`.
- `compose.staging.yaml`, ajouté par `_deploy.yml` au seul staging : environ 0,75 cœur en
  tout, poids 128, admission du SSR à 3 rendus (1 épinglé, 10 par minute), ingestion,
  langues et téléchargements un à la fois. `docs/guides/configuration.md` § 4.3 et 4.5.
