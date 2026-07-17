---
date: 2026-07-17
type: feat
scope: back
title: Modération des comptes et builds, suivi des dons, clients API et surveillance
summary: L'équipe peut désormais suspendre les comptes problématiques (retirés du site public), modérer les builds, suivre les dons et l'API payante, et surveiller la santé des services.
tags: [admin, moderation, ban, donations, api, monitoring]
---

## Ce qui change

La zone d'administration gagne cinq pages de gestion : modération des comptes
(recherche, bannissement avec raison, suppression), modération des builds
(dépublication, suppression), suivi des dons, gestion des clients de l'API
payante (révocation, crédits manuels) et une page de surveillance des services.
Un compte suspendu ne peut plus se connecter — il voit un message clair dans sa
langue — et disparaît des espaces publics (profil, tendances).

## Pourquoi

Jusqu'ici, aucun outil ne permettait d'agir sur un compte ou un build
problématique ni de suivre les dons et l'API sans ouvrir la base à la main.

## Détails

- Bannissement : connexion bloquée (formulaire, Google et « rester connecté »),
  profil public en 404, builds retirés du classement des tendances ; le lien de
  partage direct d'un build reste volontairement fonctionnel.
- Message de suspension traduit dans les 21 langues du site.
- Page dons : total collecté, tendance 30 jours, donateurs identifiés/anonymes.
- Page clients API : consommation par clé, répartition par plan, top
  consommateurs, révocation et crédit manuel de requêtes.
- Page surveillance : santé de Postgres/MinIO/go-fetcher/go-api, compteurs
  applicatifs et volumes, instantané rafraîchi toutes les 30 secondes.
- Vue d'ensemble : nouvelle rangée de KPIs application (comptes, builds, dons,
  API, état des services) et navigation regroupée Analytics / Gestion.

## Technique

- Migration `Version20260717200000` : `users.is_banned` (bool, défaut false),
  `banned_at` (timestamptz null), `ban_reason` (varchar 255 null).
- Enforcement en 3 points : `App\Security\UserChecker` (`user_checker` du
  firewall `main`, message traduit à la levée — le template login passe la
  phrase telle quelle via le domaine `security`), `PublicProfileController`
  (404 owner banni, même réponse qu'un profil inconnu), et
  `BuildVoteRepository::publicBuildsQb` (JOIN owner + exclusion `is_banned`,
  liste et count alignés). `/b/{token}` reste accessible par capacité (choix
  documenté).
- Nouveaux contrôleurs `Admin\{UserModeration,BuildModeration,DonationAdmin,
  ApiClient,Monitoring}Controller` sur une base commune `AbstractAdminController`
  (garde CSRF façon rollup + redirections conservant le contexte de liste).
- `ServiceHealthProbe` (HttpClient timeout 2 s, endpoints Docker internes
  uniquement — paramètres `admin.go_*_health_url`) : ok/degraded/down + latence,
  n'importe quelle panne produit un résultat structuré, jamais une exception.
  `MonitoringReportService` mémoïsé 30 s dans `ddragon.cache` (`?refresh=1`),
  sections compteurs/volumes dégradables indépendamment.
- Repositories : recherche paginée insensible à la casse users/builds (needle
  LIKE non échappé assumé, admin only), agrégats donations (série journalière
  zéro-remplie côté PHP — troncature de date SQL non portable SQLite/PG) et
  API (sommes api_usage, usage par clé batché, top consommateurs).
- Macros Twig `table`/`cell`/`pager`/`health_badge` dans `_ui.html.twig`,
  styles tables/formulaires/pager dans `admin.css` ; graphes toujours SSR
  (`chart_sparkline` dons, `chart_donut` plans) ; filtre `|euros`.
- Tests : 337 → 363 (UserChecker, sondes santé sur MockHttpClient — port fermé
  simulé par TransportException —, recherches/compteurs et exclusion bannis via
  InMemoryOrm). Vérifié en réel par flux curl complet (ban → login bloqué/404/
  tendances, unban, crédit API visible portail, delete avec cascades).
