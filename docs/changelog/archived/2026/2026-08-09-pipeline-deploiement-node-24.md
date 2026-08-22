---
date: 2026-08-09
type: devops
scope: infra
title: Chaine de deploiement remise a niveau avant la fin de support de Node 20
summary: Les mises en ligne du site continueront de fonctionner apres l'arret de Node 20 sur les runners GitHub.
tags: [ci-cd, maintenance]
---

## Ce qui change

Rien de visible sur le site. La chaine automatique qui publie les nouvelles
versions tournait encore sur une brique logicielle en fin de vie : elle est
remise a niveau, donc les mises en ligne resteront fiables quand cette brique
sera retiree.

## Pourquoi

Les serveurs d'integration de GitHub arretent progressivement Node 20. Sans
cette mise a niveau, la publication en production aurait fini par echouer du
jour au lendemain.

## Technique

Toutes les actions GitHub encore sur le runtime `node20` sont passees a leur
majeure `node24` :

- `webfactory/ssh-agent` v0.9.0 → v0.10.0 (la seule signalee par l'avertissement
  du runner, sur le job `_deploy` staging **et** prod)
- `docker/setup-buildx-action` v3 → v4
- `docker/login-action` v3 → v4
- `docker/build-push-action` v6 → v7

Les trois majeures Docker ne suppriment que des entrees/variables depreciees
non utilisees ici (`DOCKER_BUILD_NO_SUMMARY`, `DOCKER_BUILD_EXPORT_RETENTION_DAYS`,
anciens inputs de `setup-buildx`) : les etapes de `_build.yml` / `_promote.yml`
sont inchangees. Elles exigent un runner ≥ v2.327.1, garanti par
`ubuntu-latest`.
