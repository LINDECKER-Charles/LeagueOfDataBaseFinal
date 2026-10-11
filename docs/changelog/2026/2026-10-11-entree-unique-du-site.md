---
date: 2026-10-11
type: devops
scope: infra
title: Le site n'accepte plus que les requêtes passées par sa porte d'entrée
summary: Les autres services hébergés sur le même serveur ne peuvent plus s'adresser au site directement en se faisant passer pour un visiteur.
tags: [securite, deploiement]
---

## Ce qui change

Rien ne change pour les joueurs. Le site n'accepte plus que les requêtes passées par sa porte
d'entrée officielle : les autres services hébergés sur le même serveur ne peuvent plus
s'adresser à lui directement en se faisant passer pour un visiteur.

## Pourquoi

Les protections qui s'appuient sur l'adresse des visiteurs (limites de connexion et
d'inscription, journal de sécurité, mesure d'audience) ne peuvent plus être faussées par un
voisin du serveur.

## Technique

- En-tête `X-LoDb-Edge` : posé par l'edge (`caddy_*.reverse_proxy.header_up`), exigé par
  nginx sur chaque requête hors `/healthz` (`403` sinon, `snippets/edge-guard.conf`), retiré
  avant l'API et le SSR.
- Valeur générée une fois par hôte par `_deploy.yml` (`.deploy/secrets/LODB_EDGE_SECRET`,
  `openssl rand -hex 32`), écrite dans le `.env`, jamais affichée ; vide en local, rien n'est
  exigé. Le déploiement vérifie le refus sans l'en-tête et le passage par l'edge.
  `docs/guides/configuration.md` § 6.7.
