---
date: 2026-07-17
type: devops
scope: infra
title: Le HTTPS du site est vérifié et réparé automatiquement à chaque mise en ligne
summary: Chaque déploiement contrôle que tous les domaines servent un certificat valide et le réémet si besoin, évitant les erreurs de connexion sécurisée.
tags: [tls, https, deploy, caddy]
---

## Ce qui change

À chaque mise en ligne, le site vérifie désormais que **chacun de ses domaines**
(`.fr` comme `.com`) présente bien un cadenas HTTPS valide. Si un certificat manque,
il est réémis automatiquement avant la fin du déploiement. Plus de page « la connexion
n'est pas sécurisée » qui passe inaperçue.

## Pourquoi

Le domaine `.fr` affichait une erreur de connexion sécurisée : son certificat n'avait
jamais pu être émis (au premier essai, le domaine ne pointait pas encore vers le bon
serveur), et les remises en ligne suivantes ne relançaient pas la démarche. Le problème
restait invisible jusqu'à ce qu'un visiteur tombe dessus.

## Technique

- Cause racine : le certificat `.fr` était bloqué en back-off côté Caddy après un échec
  de validation ACME (challenge servi par l'ancienne IP `213.210.20.90` → 404). Un
  `docker compose up -d` sur l'edge à config inchangée est un no-op → aucun retry.
- Ajout d'un **smoke test TLS post-déploiement** dans `_deploy.yml` (partagé
  staging/prod, donc env-agnostique) : après que l'app est live sur le réseau `edge`,
  chaque domaine de `CADDY_DOMAINS` est testé en HTTPS (handshake + vérification de
  chaîne, sans `-k`).
- **Self-heal conditionnel** : en cas d'échec, un unique `docker compose restart caddy`
  sur l'edge partagé force certmagic à relancer l'émission, puis re-test en boucle
  (~90 s de marge). En régime normal (certs déjà émis) → no-op, aucun impact sur les
  autres tenants du VPS.
- Le déploiement **échoue explicitement** si le TLS reste cassé après reload, en
  dumpant les lignes ACME pertinentes (`challenge failed`, `rateLimited`, `unauthorized`) :
  une prod certless ne peut plus passer silencieusement.
