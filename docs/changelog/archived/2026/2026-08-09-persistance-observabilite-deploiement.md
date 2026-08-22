---
date: 2026-08-09
type: fix
scope: infra
title: Les statistiques ne repartent plus de zéro après une mise en ligne
summary: Visites, journal d'audit et sessions survivent désormais aux déploiements.
tags: [analytics, admin, securite, deploiement]
---

## Ce qui change

Les chiffres de fréquentation du panneau d'administration ne disparaissent plus à
chaque mise en ligne : l'historique est conservé d'une version du site à l'autre.
Le journal d'audit — qui doit être conservé six mois — n'est plus tronqué non plus,
et les visiteurs connectés restent connectés après une mise à jour.

## Pourquoi

Chaque déploiement remplaçait le conteneur applicatif, et tout ce qu'il avait écrit
sur son disque partait avec lui : événements de visite, journal d'audit, sessions.
Le compteur de visites de la vue d'ensemble revenait donc à zéro à chaque mise en
ligne, et l'historique des jours précédents était irrécupérable.

## Détails

- Historique des visites conservé entre les versions.
- Journal d'audit conservé sur toute sa durée de rétention légale.
- Plus de déconnexion des comptes lors d'une mise en ligne.
- La base de géolocalisation pays se dépose une seule fois, plus à chaque version.

## Technique

**Cause.** Le tier chaud de l'observabilité est un NDJSON local
(`NdjsonDayStore` → `EventStore`, `AuditLogStore`), et le service `php` de
`compose.yaml` ne déclarait **aucun volume**. En dev, `compose.override.yaml` monte
`app_var` sur `var/`, ce qui masquait le problème ; en staging/prod, `compose pull` +
`up -d` recrée le conteneur et détruit sa couche writable. Le tier durable (agrégats
MinIO `analytics/daily/*.json`) n'était alimenté que par un déclenchement **manuel**
du rollup — aucun cron, rien dans les workflows — donc rien ne subsistait.
`AnalyticsReportService::dailyFor()` retombant sur le NDJSON local absent, le panneau
affichait 0. Les sessions PHP étaient dans le même cas : `framework.session: true`
sans `save_path` laisse le handler natif sur le `save_path` de php.ini (`/tmp`).

**Correctif.** Introduction de `var/state`, seul chemin durable, adossé au volume
nommé `app_state` :

- `EventStore` → `var/state/analytics/events`, `AuditLogStore` → `var/state/audit/events`,
  `GeoLocator` → `var/state/geoip/GeoLite2-Country.mmdb` ;
- `framework.session.save_path` → `var/state/sessions`, ce qui bascule aussi le
  handler sur `session.handler.native_file` (cf. `FrameworkExtension`, le `save_path`
  par défaut n'est appliqué que si `handler_id` ou `save_path` est renseigné) ;
- `var/cache` et `var/log` restent **hors volume volontairement** : persister le cache
  prod ferait servir un conteneur DI compilé depuis une image antérieure ;
- `var/state` est créé dans l'image et `chown www-data` avant `USER www-data` — Docker
  recopie les droits du point de montage en peuplant un volume nommé vide, et un
  dossier absent serait créé `root`, rendant les écritures impossibles (échec avalé
  silencieusement par `EventStore::append()`) ;
- le job de déploiement consolide les journées **closes** vers MinIO
  (`app:analytics:rollup`, `app:audit:rollup`), en best-effort. Volontairement sans
  `--include-today` : `RollupService::persistAggregate()` écrase l'agrégat du jour, et
  le lancer sur une journée dont le NDJSON vient d'être recréé écraserait l'agrégat
  existant par du vide.

**Reprise.** Les données antérieures au correctif sont définitivement perdues (elles
l'étaient déjà à chaque déploiement). En local, l'historique dev peut être conservé :
`docker compose exec -u www-data php sh -c 'mkdir -p var/state && mv var/analytics var/audit var/state/ 2>/dev/null'`.
