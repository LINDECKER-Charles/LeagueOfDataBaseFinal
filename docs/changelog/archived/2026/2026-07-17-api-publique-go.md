---
date: 2026-07-17
type: feat
scope: back
title: API publique v1 — profils, builds et tendances accessibles par clé API
summary: Un nouveau service ouvre les données communautaires (profils publics, builds partagés, tendances de consultation) aux développeurs via une API REST authentifiée par clé.
tags: [api, comptes, builds, tendances]
---

## Ce qui change

Les développeurs peuvent maintenant interroger LeagueOfDataBase depuis leurs propres outils :
profil public d'un membre (favoris, nombre de builds partagés), builds publics d'un champion
avec leur lien de partage, et classement des champions/objets/runes/invocateurs les plus
consultés sur 7 ou 30 jours. L'accès se fait avec une clé API personnelle ; chaque clé
dispose d'un quota mensuel, de crédits optionnels et d'une limite de requêtes par minute,
consultables à tout moment via un endpoint dédié.

## Pourquoi

Les données communautaires du site (builds, tendances réelles de consultation) n'étaient
visibles que sur le site lui-même. Une API publique permet aux créateurs d'outils, d'overlays
ou de bots d'en tirer parti — et ouvre une source de financement du projet via les plans
payants.

## Détails

- `GET /v1/profiles/{username}` — profil public : favoris et nombre de builds partagés.
- `GET /v1/champions/{championId}/builds` — builds publics paginés, avec lien `/b/{token}`.
- `GET /v1/trends/{type}` — top 25 des entités les plus consultées (7j ou 30j).
- `GET /v1/usage` — consommation de la clé (toujours accessible, même quota épuisé).
- Erreurs uniformes, en-têtes `X-RateLimit-*`, CORS ouvert (utilisable depuis un navigateur).
- Offres : Free 500 req/mois, packs crédits 5/10/20 €, abonnements 5–15 €/mois et 48–144 €/an
  (détail dans `docs/api-publique.md`) ; clés générées depuis `/profile`, paiement Stripe côté site.

## Technique

- Nouveau micro-service `go-api/` (Go 1.25, stdlib `net/http` + pgx + minio-go), port 8090,
  distinct de la passerelle SSRF `go-workers/` qui garde son rôle de thin proxy.
- Auth par `key_hash` SHA-256 (format `lodb_` + 40 hex), cache in-memory des clés 60 s
  (positif et négatif), rate limit token bucket par clé, quota plan-puis-crédits :
  crédits décrémentés en synchrone (`UPDATE … WHERE credits_balance > 0 RETURNING`),
  métrage `api_usage` flushé par lots asynchrones (~1 s, upsert `ON CONFLICT`). Léger
  overshoot de quota possible sous concurrence (compteur mensuel en mémoire) — assumé.
- Trends : fusion des agrégats `analytics/daily/{date}.json` (MinIO), champ `entities`
  préfixé par type interne (`runes` public → `runesReforged`), cache 5 min ; noms résolus
  best-effort depuis `data/{version}/en_US/{type}.json` (cache 30 min).
- Tables `api_keys`/`api_usage` : contrat dans `go-api/schema.sql`, migration Doctrine à
  venir côté `app/` ; avant migration, `/healthz` reste 200 et les endpoints v1 répondent
  503 proprement.
- Compose : service `go-api` (build multi-stage calqué sur go-fetcher, healthcheck
  `/healthz`, port hôte 8090). Tests : `go vet ./...` + `go test ./...` verts en conteneur
  `golang:1.25` (clé/format, token bucket, quota, fusion trends sur fixtures, pagination).
