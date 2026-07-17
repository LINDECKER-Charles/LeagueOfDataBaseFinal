---
date: 2026-07-17
type: feat
scope: full-stack
title: API publique payante — portail de clés, page développeurs et paiement Stripe
summary: Chaque invocateur peut créer sa clé API gratuite depuis son profil, suivre sa consommation, et monter en puissance via packs de crédits ou abonnements payés par Stripe.
tags: [api, stripe, developers, comptes]
---

## Ce qui change

Une page publique **/developers** présente l'API du site : authentification, endpoints
(profils publics, builds par champion, tendances, consommation), grille tarifaire et
exemples prêts à copier. Depuis son profil, chaque invocateur dispose d'un portail
**/profile/api** : création de sa clé (affichée une seule fois, bouton copier),
consommation du mois avec jauge de quota, tableau des 30 derniers jours, régénération
et révocation.

Pour aller au-delà des 500 requêtes gratuites mensuelles : packs de crédits à usage
unique (5 € = 5 000, 10 € = 10 000, 20 € = 20 000 requêtes, valables 12 mois) ou
abonnements (Mensuel 5 €, Mensuel+ 15 €, Annuel 48 €, Annuel+ 144 €), payés via Stripe
sans que le site ne touche jamais une donnée bancaire.

## Pourquoi

Les données communautaires (profils, builds partagés, tendances) sont désormais servies
par une API : il fallait un endroit où obtenir sa clé, suivre sa consommation et payer —
sans jamais exposer le secret ni les données de paiement.

## Détails

- La clé claire n'est montrée qu'à la création/régénération ; seuls une empreinte
  SHA-256 et un préfixe d'affichage sont conservés.
- Régénérer conserve tout : offre, quota, crédits, abonnement **et la consommation du
  mois** (impossible de remettre son quota à zéro en changeant de secret).
- Révoquer coupe la clé immédiatement côté site ; l'API peut l'accepter encore au plus
  une minute (cache), documenté sur le portail.
- Un seul abonnement actif ; l'annulation (webhook Stripe) ramène au plan gratuit en
  conservant les crédits. Liens « API » ajoutés au header, au footer et au profil ;
  /developers est indexable (SEO 21 langues + sitemap).

## Technique

- Schéma contractuel go-api : migration `Version20260717140649` (`api_keys`,
  `api_usage`, index unique `(api_key_id, day)` requis par l'upsert Go) + colonnes
  additives `stripe_customer_id` / `stripe_subscription_id`. Entités `ApiKey`
  (ManyToOne User unidirectionnel), `ApiUsage` (lecture seule côté PHP), enum
  `ApiPlan` (grille actée quotas/rates/prix — plans annuels portés en quota mensuel).
- `App\Service\PublicApi\*` (le namespace `App\Service\Api` du brief est impossible :
  collision de casse avec `App\Service\API` sur checkout Windows insensible à la
  casse) : `ApiKeyIssuer` (secret `lodb_`+40 hex, report d'entitlements),
  `ApiCheckout`/`ApiCheckoutParams` (Checkout Sessions `price_data` inline, metadata
  `kind=api_pack|api_plan`), `ApiEntitlementApplier` (port `ApiEntitlements`).
- Webhook Stripe refondu en dispatch par type d'événement (`StripeEventHandlerInterface`
  taggé + `AutowireIterator`) : `checkout.session.completed` (packs → UPDATE atomique
  `credits_balance`, plans → pose plan/quota/rate + ids Stripe),
  `customer.subscription.deleted` (retour Free, rate 60 si crédits > 0) ; dons
  inchangés (log-only), ajout d'un futur handler = une classe. Signature vérifiée,
  400/503 préservés, 500 → redelivery Stripe. Livraison at-least-once non dédupliquée
  (trade-off v1 documenté).
- i18n : domaine dédié `api` (21 locales), clés SEO `developers.*` (21 locales),
  portail 100 % Twig SSR (réutilise l'îlot `copy-link`), route ajoutée au sitemap.
- Vérifié bout-en-bout contre go-api :8090 (clé créée → /v1/usage 200 free/500 →
  metering incrémenté → régénération avec report d'usage → révocation → 403 après le
  TTL de cache 60 s) ; 321 tests unitaires verts.
