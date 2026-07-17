---
date: 2026-07-17
type: feat
scope: full-stack
title: Badge Supporter — les dons des invocateurs connectés sont reconnus
summary: Un don effectué en étant connecté est désormais enregistré et affiche un sceau doré « Supporter » à côté de votre nom.
tags: [communauté, don, stripe, badge]
---

## Ce qui change

Faire un don en étant **connecté** marque désormais votre compte : un petit
sceau doré en forme de gemme-cœur apparaît à côté de votre nom sur votre
profil, votre profil public, vos builds partagés et vos lignes de la page
Tendances. Le don anonyme reste possible, strictement identique à avant —
aucun compte requis. Le badge ne donne aucun avantage : c'est une marque de
gratitude, définitive dès le premier don.

## Détails

- Le badge est visible uniquement aux quatre endroits cités — discret (~14 px),
  avec info-bulle et libellé accessible « Supporter » traduits.
- Les dons sont maintenant comptabilisés côté site (montant, devise) : la base
  du futur récapitulatif admin.
- Supprimer son compte ne falsifie pas la comptabilité : le don reste
  enregistré, simplement détaché du compte.

## Technique

- Entité `Donation` immuable (`stripe_session_id` UNIQUE = clé d'idempotence,
  `user_id` NULLABLE `ON DELETE SET NULL`, montant/devise/date) +
  `users.is_supporter` (défaut false) — migration `Version20260717180000`.
  `DonationRepository` : `existsBySessionId`, `sumForUser`, `countAll`,
  `sumAll` (reporting admin à venir).
- Checkout : `CheckoutSessionParams::build()` pose `metadata.kind='donation'`
  sur TOUTES les sessions (anonymes incluses — dispatch webhook
  déterministe) ; `forDonor()` ajoute `client_reference_id=<user id>` quand le
  donateur est connecté (`DonationController`). Flux anonyme inchangé.
- Webhook : la branche par défaut de `CheckoutSessionCompletedHandler`
  (kind `donation` ET sessions historiques sans kind — rétrocompat testée)
  délègue au port `DonationLedger` / `DonationRecorder` : no-op silencieux si
  session déjà enregistrée (redélivrance Stripe), sinon persiste et pose
  `is_supporter` quand `client_reference_id` résout un compte ; référence non
  numérique ou compte disparu → don enregistré non lié. Logs sans donnée
  nominative (session, montants, ids internes).
- Tests : `DonationRecorderTest` rejoue la chaîne complète sur payload
  **signé HMAC réel** via `StripeWebhookController` et stockage réel (SQLite
  in-memory) — avec/sans user, redélivrance idempotente, référence inconnue ;
  `StripeEventHandlersTest` couvre le routage par kind (donation, legacy sans
  kind, jamais les entitlements API).
- Badge : partial `components/_supporter_badge.html.twig` (SVG inline sans
  defs/ids — répétable), styles `.supporter-badge` dans `community.css`,
  clé i18n `community.supporter.badge` ×21.
