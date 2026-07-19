---
date: 2026-07-19
type: feat
scope: back
title: Protection anti-abus sur les actions sensibles
summary: Limitation par IP sur l'inscription, la réinitialisation de mot de passe, le don et le contact ; garde anti-force-brute sur la connexion admin.
tags: [securite, rate-limiting, abuse]
---

## Ce qui change

Les actions sensibles du site sont désormais protégées contre les abus (envois
en masse, spam, force brute). Un usage anormalement répété depuis une même
connexion est temporairement bloqué avec un message clair invitant à réessayer
un peu plus tard. Les usages normaux ne sont jamais impactés.

## Pourquoi

Plusieurs points d'entrée déclenchaient un envoi d'e-mail ou une écriture sans
plafond : inscription (e-mails de confirmation), mot de passe oublié, don. Ils
pouvaient être détournés pour spammer des tiers ou saturer le service.

## Technique

- Trait `Controller\Concern\ThrottlesByIp` (clé = IP client) + limiters
  `registration`, `password_request`, `donation_checkout` (sliding_window) dans
  `framework.yaml`. Réutilise le pattern des limiters `contact_form` / `email_verification`.
- `registration` (5/h) et `password_request` (5/h, en complément du throttle
  per-compte du bundle ResetPassword) : cappent le spraying multi-adresses par hôte.
- `donation_checkout` (10/h) : limite la création de sessions Stripe Checkout.
- Connexion admin : ajout de `login_throttling` (5 / 15 min, clé user+IP) sur le
  firewall `admin` — le firewall `main` en disposait déjà.
- Actions authentifiées (builds, profil, clés API) laissées volontairement hors
  rate-limit par IP : auth + CSRF + contraintes DB sont les bons contrôles, et un
  plafond IP produirait des faux positifs derrière NAT / mobile partagé.
