---
date: 2026-07-18
type: fix
scope: full-stack
title: En-têtes de sécurité HTTP sur tout le site
summary: Le site envoie désormais les protections navigateur standard (CSP, HSTS, anti-clickjacking) sur chaque page.
tags: [security, headers, csp, hsts]
---

## Ce qui change

Chaque page du site est maintenant servie avec les en-têtes de sécurité que les
navigateurs modernes attendent. Concrètement, ton navigateur applique des
protections supplémentaires : il n'exécute que les scripts légitimes du site,
refuse d'afficher les pages dans un cadre piégé sur un autre site, force la
connexion en HTTPS, et limite les informations transmises en quittant le site.

Rien ne change visuellement : c'est une couche de protection invisible.

## Pourquoi

Un audit de sécurité signalait l'absence des en-têtes recommandés
(Content-Security-Policy, HSTS, X-Frame-Options, X-Content-Type-Options,
Referrer-Policy, Cross-Origin-Resource-Policy). Ces en-têtes réduisent la surface
d'attaque (injection de scripts, clickjacking, downgrade HTTP, fuite de referer).

## Technique

Répartition par couche selon la sensibilité à l'environnement :

- **nginx** (`docker/nginx/snippets/security-headers.conf`, inclus au niveau
  serveur + ré-inclus dans chaque `location` ayant son propre `add_header`) :
  les en-têtes constants, sûrs en dev comme en prod, sur **toutes** les réponses
  (documents, build Vite, CDN images MinIO, fonts) — `X-Content-Type-Options`,
  `Referrer-Policy`, `Strict-Transport-Security` (2 ans, `includeSubDomains`,
  `preload`), `Cross-Origin-Resource-Policy: same-origin`.
- **PHP** (`App\EventSubscriber\SecurityHeadersSubscriber`, `kernel.response`) :
  la politique sensible à l'environnement — `Content-Security-Policy` +
  `X-Frame-Options: DENY` — sur les réponses HTML, **en prod uniquement**
  (`kernel.debug` gate) pour ne pas casser la toolbar profiler / le HMR Vite en
  dev. Le listener s'efface devant une réponse portant déjà sa propre CSP.

CSP durcie sans `'unsafe-inline'` sur `script-src` : tous les scripts exécutables
sont des modules Vite same-origin (`/build`), les blocs JSON-LD sont des données
inertes, et les anciens handlers inline (`onerror` de repli d'image, `onsubmit`
de confirmation admin) sont désormais délégués au document dans
`assets/vue/fx/enhance.ts` (`data-img-fallback` / `data-confirm`). `style-src`
conserve `'unsafe-inline'` (styles inline Vue / barre de progression Turbo).
Sources externes explicitement autorisées : splash/chromas DDragon &
CommunityDragon (`img-src`), vidéos de sorts Cloudfront (`media-src`/`img-src`),
303 Stripe Checkout (`form-action`). L'interstitiel de domaine retiré porte sa
propre CSP scoping son script de redirection.
