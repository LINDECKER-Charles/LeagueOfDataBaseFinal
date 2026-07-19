---
date: 2026-07-18
type: fix
scope: infra
title: Les adresses www redirigent vers l'adresse sans www
summary: www.league-of-data-base.com renvoie désormais vers league-of-data-base.com.
tags: [seo, redirect, canonical]
---

## Ce qui change

Ouvrir le site via une adresse commençant par `www.` t'amène maintenant
directement sur la même page en adresse courte, sans le `www.`. Le lien que tu
partages, tes favoris et ton historique pointent tous vers une seule et même
adresse.

## Pourquoi

Le site répondait sur deux adresses (avec et sans `www.`) considérées comme
deux sites différents par les moteurs de recherche. Le référencement s'en
trouvait dilué entre les deux.

## Technique

301 permanent au niveau **nginx** (`docker/nginx/default.conf`), pas dans l'app :
la redirection ne boote jamais Symfony. Un `server` dédié capture l'apex via
`server_name ~^www\.(?<apex>…league-of-data-base\.(com|fr))$` et renvoie
`https://$apex$request_uri` (path + query verbatim, https forcé).

Le regex est **scopé au domaine de marque** — un `Host: www.evil.com` forgé ne
peut pas devenir un open-redirect (il tombe sur le `default_server`, non
redirigé) — tout en restant env-agnostic : il matche l'apex prod *et* le
`www.test.…` de staging sans valeur par environnement.

Le 301 porte un `Cache-Control: public, max-age=3600` explicite : un 301 nu est
mis en cache heuristiquement (souvent à vie) par les navigateurs, ce qui
épinglerait la politique d'hôte côté client. Les moteurs consolident le SEO sur
le *statut* 301, pas sur le `max-age` — borner à 1 h ne coûte rien en référencement
et garde la redirection réversible.

L'hôte `www` reste dans `CADDY_DOMAINS` (DNS + certificat + routage TLS de l'edge)
pour que la requête atteigne nginx et reçoive le 301 — il n'est pas supprimé.
`www.league-of-data-base.fr` est d'abord réduit à son apex ici, puis la retraite
`.fr` (`RetiredDomainSubscriber`) prend le relais côté PHP.
