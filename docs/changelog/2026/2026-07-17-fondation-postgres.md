---
date: 2026-07-17
type: devops
scope: infra
title: Le site s'équipe d'une base de données pour les comptes
summary: Une base PostgreSQL rejoint la plateforme pour héberger les futurs comptes et leurs données.
tags: [postgres, doctrine, comptes]
---

## Ce qui change

Rien de visible aujourd'hui, mais tout se prépare : le site dispose désormais d'une
base de données dédiée aux données personnelles (comptes, favoris, builds).
L'encyclopédie elle-même ne change pas de moteur — elle reste servie comme avant,
avec la même rapidité.

## Pourquoi

Jusqu'ici le site ne retenait rien d'un visiteur. Pour offrir des comptes
d'invocateur, des favoris et des builds partageables, il fallait un endroit fiable
et durable où les stocker.

## Technique

- Service `postgres:17-alpine` ajouté à la stack compose (volume nommé `pgdata`,
  healthcheck `pg_isready`, port 5432 publié en dev uniquement).
- Extension `pdo_pgsql` ajoutée à l'image PHP ; `DATABASE_URL` injectée via compose
  (défauts dev `lodb/lodb/lodb`, secrets réels via `.env.*`).
- doctrine/orm + doctrine-bundle + doctrine-migrations-bundle + symfony/rate-limiter
  installés ; mapping attributs sur `App\Entity`, caches query/result en prod.
- Première migration : tables `users` et `builds` (JSONB pour runes/étapes,
  index uniques fonctionnels `LOWER(email)` / `LOWER(username)`), plus la table
  du transport messenger doctrine.
- Variables Stripe (`STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`) centralisées dans
  les fichiers d'env en placeholder — l'intégration arrive séparément.
- Déploiement : `doctrine:migrations:migrate --no-interaction --allow-no-migration`
  exécuté de façon synchrone et bloquante dans `_deploy.yml` juste après `up -d`
  (un déploiement ne sert jamais un code dont le schéma n'a pas suivi).
- Le stockage Data Dragon reste sans base de données (MinIO, inchangé).
