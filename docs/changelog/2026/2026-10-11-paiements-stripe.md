---
date: 2026-10-11
type: fix
scope: back
title: Achats et dons accordés une fois payés, remboursements pris en compte
summary: Les crédits de l'API, les abonnements et le badge de soutien ne sont accordés qu'une fois le paiement par carte encaissé, et un remboursement reprend ce qu'il avait donné.
tags: [paiements, securite]
---

## Ce qui change

- Les packs de crédits, les abonnements à l'API et les dons se règlent par carte : ils sont
  accordés dès que le paiement est accepté, comme avant.
- Un paiement qui n'est pas encore encaissé n'accorde rien tant qu'il n'est pas confirmé.
- Si le paiement d'un renouvellement d'abonnement échoue, la clé repasse à l'offre gratuite
  le temps de régulariser, puis retrouve son offre dès que le paiement passe. Les crédits
  achetés restent acquis.
- Un remboursement total reprend ce que le paiement avait donné : les crédits du pack pas
  encore utilisés, l'offre d'un abonnement, ou le badge de soutien d'un don. Les requêtes
  déjà consommées le restent, et un remboursement partiel ne reprend rien.

## Pourquoi

Ce qui est accordé doit correspondre à ce qui est effectivement payé : c'est plus juste pour
tous, et plus simple à suivre en cas de remboursement.

## Technique

- `checkout.session.completed` n'accorde un pack, un plan ou un don que si `payment_status`
  vaut `paid` ou `no_payment_required`, et que le montant et la devise sont ceux du
  catalogue ; sinon la session est enregistrée `pending` ou `refused` (`stripe_checkout`).
- Nouveaux gestionnaires : `checkout.session.async_payment_succeeded` (même octroi, une
  seule fois par session), `checkout.session.async_payment_failed`,
  `customer.subscription.updated`, `invoice.payment_failed`, `charge.refunded`,
  `charge.dispute.created`. Les événements d'un abonnement plus anciens que le dernier
  appliqué sont ignorés (`stripe_subscription`).
- Sessions Checkout en carte seule (`payment_method_types = ["card"]`).
- Migration additive `StripePaymentStates` (deux tables). Événements à activer sur
  l'endpoint Stripe : `docs/guides/configuration.md`, § 6.5.
