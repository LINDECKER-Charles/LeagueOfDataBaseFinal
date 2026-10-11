---
date: 2026-10-11
type: perf
scope: front
title: Des pages plus légères, préparées plus vite, et une sécurité renforcée
summary: Une fiche en français pèse un quart de moins, le serveur prépare chaque page environ un tiers plus vite, et le navigateur n'exécute plus aucun code fabriqué à la volée.
tags: [performance, disponibilite, securite]
---

## Ce qui change

Chaque page n'embarque plus que les textes de sa langue : jusqu'ici, une page en français
transportait aussi tout l'anglais, en réserve des quelques phrases pas encore traduites, et
les textes complets de la FAQ et de l'API pour quatre liens du pied de page. Une fiche en
français pèse ainsi environ 70 Ko de moins. Le serveur prépare chaque page environ un tiers
plus vite et en sert davantage à la fois, ce qui aide le site à tenir les pics de visites.
Les textes encore absents d'une langue restent affichés en anglais, comme avant.

Le site s'interdit désormais d'exécuter du code fabriqué à la volée : une protection de plus
contre l'injection de script.

## Technique

- SSR : catalogues et changelog lus une fois au démarrage, servis sans requête vers lui-même
  (`SsrHttpBackend`) ; `/api/meta` gardée 60 s par processus avec copie de secours
  (`LODB_API_META_TTL_SECONDS`) ; seule la version de la release passe dans la page.
- Build des catalogues (`npm run i18n:build`, avant chaque build) : `public/i18n` complété par
  `en` clé par clé, `useFallbackTranslation: false` ; les libellés du chrome pris aux scopes
  `about` et `api` sont prêtés au catalogue racine (`src/LoDb.Web/i18n/chrome-keys.json`).
- Messages ICU compilés au build et rendus par `PrecompiledTranspiler` : plus de
  `@messageformat/core` dans le bundle (initial 699 → 625 Ko) ni de `'unsafe-eval'` dans la CSP.
  Un test compare le rendu de chaque message de chaque locale à celui de MessageFormat.
- Mesures hors prod (même API, un processus SSR, 8 connexions) : p50 d'une fiche fr
  273 → 176 ms, d'une fiche en 247 → 174 ms, de la liste fr 604 → 467 ms, d'une 404
  202 → 117 ms ; CPU par rendu −23 à −42 % ; état transféré d'une fiche fr 159 → 86 Ko ; une
  ligne de journal par page au lieu de 7 à 16.
