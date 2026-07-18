---
date: 2026-07-18
type: fix
scope: back
title: Génération de clé API réservée aux comptes à e-mail confirmé
summary: Créer une clé API exige désormais un e-mail vérifié, comme la création de build.
tags: [api, securite, compte, email]
---

## Ce qui change

La génération d'une clé API depuis le portail développeur (`/profile/api`) est
maintenant réservée aux comptes dont l'adresse e-mail est confirmée. Tant que
l'e-mail n'est pas vérifié, le formulaire de création est remplacé par une
consigne invitant à confirmer l'adresse (le bouton « Renvoyer l'e-mail » de la
bannière reste disponible).

## Pourquoi

Une clé API ouvre l'accès à l'API publique payante : la réserver aux comptes
confirmés applique la même barrière anti-abus que la création de builds publics,
jusque-là absente sur ce parcours.

## Technique

- `ApiKeyController::create` passe par un garde `requireVerifiedEmail()`
  (guard clause après CSRF) rebondissant vers le portail avec un flash
  `auth.verify.gate_api` si `User::isVerified()` est faux — même patron que
  `BuildController`.
- `regenerate` reste volontairement non gardé : il ne fait que tourner le secret
  d'une clé déjà émise (cas Stripe auto-provisionné), il ne crée jamais une
  première clé pour un compte non vérifié.
- Le portail masque le formulaire de création pour les comptes non vérifiés
  (defense in depth + UX honnête). Clé i18n `auth.verify.gate_api` (fr + en,
  comme `gate_build`).
