---
date: 2026-10-10
type: fix
scope: infra
title: Le site reste rapide pour tous quand une seule adresse le sollicite en rafale
summary: Une adresse qui enchaîne les requêtes bien au-delà d'une navigation normale est freinée, sans gêner les autres visiteurs.
tags: [disponibilite, performance, securite]
---

## Ce qui change

Le site garde sa vitesse pour tout le monde, même quand un programme enchaîne les requêtes
depuis une seule adresse. Au-delà d'un rythme largement supérieur à celui d'une navigation
normale, cette adresse reçoit une réponse « trop de requêtes » et doit patienter quelques
secondes. Les actions de votre compte (connexion, builds, favoris, paiements) ne sont
jamais freinées.

Les plans du site pour les moteurs de recherche et les redirections des adresses de l'ancien
site sont aussi gardés en mémoire : ils répondent plus vite et ne refont plus le même travail
à chaque visite. Davantage de pages restent prêtes à être servies d'une visite à l'autre.

## Pourquoi

Le 10 octobre 2026, une vague de requêtes automatiques a rendu le site inaccessible pendant
plusieurs heures. Chaque étage du site borne désormais le travail qu'un même client peut lui
demander.

## Technique

- nginx (`docker/nginx/`) : `limit_req` par adresse réelle sur les pages, les lectures
  publiques de l'API (`/api/catalog`, `pickers`, `meta`, `legacy`, `trends`, `share`), les
  URL de l'ancien site et `/sitemaps/` ; `429` avec `Retry-After` et `no-store`
  (`snippets/server-guards.conf`). Jamais sur les actions d'un compte, `/v1/`, `/webhooks/`,
  `/build/` ni `/cdn/`. Débits et rafales réglables par `LODB_*` (défauts dans le
  `Dockerfile`), relevés pour l'E2E dans `compose.override.yaml` seulement.
- Tous les fichiers de `sites/`, `server.d/` et `snippets/` sont désormais des gabarits
  `envsubst` : une borne posée dans une location est réglable comme une borne globale.
- Caches : `lodb_seo` (sitemaps, `robots.txt`, `llms.txt`, clé sans query, durée de vie de
  l'API) et `lodb_legacy` (301 de l'ancien site, clé avec query) ; `lodb_pages` agrandie
  (2 Go, clés 32 Mo, inactivité 7 j, réglables) ; verrou de cache à 30 s ; copie périmée
  servie aussi sur un `429` de l'amont. `/api/catalog` et `/api/meta` restent hors cache
  (contrôle de version des apps). Limite mémoire de nginx à 256 Mo sur les hôtes.
- Corps de requête : 128 Kio, gardés en mémoire ; 1 Mio pour `/webhooks/`.
- Garde-fous communs aux deux hôtes (`snippets/server-guards.conf`, inclus aussi par le
  sous-domaine `api.`) : agents refusés, délais clients, `404` de nginx pour les fichiers
  cachés et les extensions de scripts serveur ou de sauvegardes.
- Vérification : `tools/nginx/` démarre l'image derrière des amonts factices
  (`node --test`).
