---
date: 2026-07-17
type: feat
scope: full-stack
title: Confirmation d'e-mail et réinitialisation du mot de passe
summary: Confirmez votre adresse à l'inscription et réinitialisez un mot de passe oublié, avec des e-mails aux couleurs Hextech.
tags: [auth, email, security]
---

## Ce qui change

À l'inscription, vous recevez désormais un e-mail pour confirmer votre adresse.
Un bandeau discret vous rappelle de la confirmer et permet de renvoyer l'e-mail en un clic.
Tant que l'adresse n'est pas confirmée, la création de builds reste bloquée.

Vous avez aussi un vrai parcours « mot de passe oublié » : depuis la page de connexion,
demandez un lien de réinitialisation, choisissez un nouveau mot de passe, et reconnectez-vous.

Les deux e-mails adoptent l'identité visuelle Hextech (fond sombre, or, losange) — soignés
sur mobile comme sur ordinateur.

## Pourquoi

Prouver la propriété de l'adresse limite les comptes jetables et le spam de contenu public,
et donne un moyen fiable de récupérer son compte sans passer par le support.

## Détails

- Lien de confirmation signé, valable 1 heure ; renvoi limité pour éviter les abus.
- Les comptes Google sont considérés vérifiés d'office (adresse déjà validée côté Google).
- Lien de réinitialisation à usage unique, valable 1 heure, une demande par heure.
- Les comptes déjà existants restent vérifiés — aucune action requise de leur part.

## Technique

- Bundles `symfonycasts/verify-email-bundle` (URL signée sans état) et
  `symfonycasts/reset-password-bundle` (entité `ResetPasswordRequest` en Postgres,
  cohérent avec l'invariant « Postgres = données utilisateur »).
- `User.isVerified` + migration additive avec grandfathering des comptes existants.
- Envoi **synchrone** des e-mails d'auth (`SendEmailMessage` retiré du routage async) :
  aucun worker déployé, et le rendu se fait dans la locale de la requête.
- Service `AuthMailer` (SRP) : expéditeur (`MAILER_FROM`), sujet traduit, paire de
  templates HTML + texte partagée (`templates/email/`).
- `MAILER_DSN`/`MAILER_FROM` câblés sur le service `php` (dev → Mailpit `smtp://mailer:1025`).
  Recréer le conteneur `php` pour prise en compte.
