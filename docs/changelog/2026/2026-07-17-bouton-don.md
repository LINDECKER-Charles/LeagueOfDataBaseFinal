---
date: 2026-07-17
type: feat
scope: full-stack
title: Soutenir le site par un don
summary: Une page « Offrande à la forge » permet de soutenir l'hébergement du site via Stripe, sans compte requis.
tags: [don, soutien, stripe]
---

## Ce qui change

Une nouvelle page de don s'ouvre sur `/donate` : choisissez une offrande parmi
quatre paliers (3 €, 5 €, 10 €, 25 €) ou saisissez un montant libre (1 € à
500 €), puis réglez en toute sécurité sur la page de paiement Stripe. Aucun
compte n'est nécessaire, et le don n'apporte aucun avantage — il couvre
simplement l'hébergement et garde l'encyclopédie gratuite, rapide et sans
publicité.

## Détails

- Cartes de paliers dans l'écrin Hextech : la gemme hexagonale de l'offrande
  choisie s'illumine ; le montant libre prend le dessus dès qu'il est saisi.
- Page de remerciement après le paiement (Stripe envoie le reçu par e-mail) et
  page neutre en cas d'annulation, avec bouton pour réessayer.
- Si la passerelle de paiement n'est pas activée, la page l'annonce clairement
  au lieu d'afficher une erreur.
- Aucune donnée bancaire ne transite par le site : la saisie de carte se fait
  entièrement chez Stripe.

## Technique

- Stripe Checkout en mode `payment` (`submit_type: donate`, metadata
  `source: lodb-donate`) : le contrôleur crée la session côté serveur puis
  redirige en 303 vers la page hébergée Stripe ; aucune persistance locale, le
  dashboard Stripe est la source de vérité.
- `Service/Donation` : `DonationTiers` (politique de montants pure — paliers,
  bornes 100-50000 cents, normalisation euros→cents avec virgule française),
  `CheckoutSessionParams` (builder pur du payload, placeholder
  `{CHECKOUT_SESSION_ID}` non urlencodé), `StripeCheckout` (passerelle fine,
  clé via `STRIPE_SECRET_KEY`, dégradation propre si vide).
- Webhook `POST /webhooks/stripe` signé (`STRIPE_WEBHOOK_SECRET`) : signature
  vérifiée via `\Stripe\Webhook::constructEvent`, `checkout.session.completed`
  loggé (session, montant, devise — jamais l'identité du donateur), 503 si
  secret absent, 400 si signature invalide.
- CSRF stateless (token id `submit`), formulaire `data-turbo="false"`, styles
  isolés dans `donate.css` (préfixe `.donate-*`), 27 tests unitaires
  (`tests/Unit/Service/Donation`).
