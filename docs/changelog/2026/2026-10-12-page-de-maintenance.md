---
date: 2026-10-12
type: feat
scope: infra
title: Une page de maintenance pendant les mises à jour du site
summary: Pendant une mise à jour, le site affiche une page de maintenance dans votre langue et votre thème, avec le temps d'attente estimé, puis revient tout seul.
tags: [deploiement, disponibilite, i18n, themes]
---

## Ce qui change

Quand une nouvelle version du site est mise en ligne, les pages ne tombent plus en erreur le
temps du remplacement : une page de maintenance s'affiche à la place, dans la langue et le thème
que vous avez choisis (Hextech, Zaun, Noxus ou Spirit Blossom), y compris en arabe de droite à
gauche. Elle indique le temps d'attente estimé, calculé sur la durée de la mise à jour
précédente, ou « 10 à 15 minutes » quand il n'y en a pas encore eu, et se recharge d'elle-même
sur la page que vous demandiez dès que le site est de retour.

## Pourquoi

Une mise à jour remplace les serveurs du site pendant quelques instants : un visiteur tombait
alors sur une page d'erreur, sans savoir combien de temps attendre ni quand réessayer.

## Technique

- `docker/nginx/server.d/maintenance.conf` : tant que `/srv/maintenance/active.json` existe
  (dossier `.deploy/maintenance` de l'hôte, monté en lecture seule par `compose.deploy.yaml`),
  chaque page (`/`, une locale, `/b/`, `/admin`, `$lodb_maintenance_page` de `sites/site.conf`)
  répond 503 (`Retry-After: 120`, `no-store`, CSP propre) par `docker/nginx/maintenance/` ;
  l'API, `/v1/`, les webhooks, les blobs, `/build/`, les fichiers des robots et les 301 de
  l'ancien site gardent leur réponse. `/maintenance/status.json` sert l'état, `/maintenance/` les
  fichiers de la page (polices du site copiées dans l'image).
- Page autonome : `messages.js` (21 locales), `maintenance.js` (identité `lod_theme`, locale de
  l'URL, puis `lod_prefs`, puis le navigateur ; durées formatées par `Intl` ; heure de l'hôte lue
  dans `Date` ; rechargement au 404 de l'état), `tokens.css` et `fonts.css` recopiés de
  `src/LoDb.Web/src/styles`.
- `tools/deploy/maintenance.sh` (`start`, `end`), appelé par `_deploy.yml` juste avant le premier
  arrêt de conteneur et après les sondes de `up --wait` ; la durée mesurée reste sur l'hôte
  (`last-duration`) et sert d'estimation au déploiement suivant. Un échec entre les deux laisse
  la page (`::error` « Maintenance page left on »).
- Tests : `tools/nginx/test/nginx-maintenance.test.mjs` (image réelle), `maintenance-page` et
  `maintenance-sources` (`node --test`, sans Docker), `tools/deploy/test/maintenance.test.mjs`.
