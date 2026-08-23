---
date: 2026-08-23
type: devops
scope: back
title: Les pannes silencieuses du site sont maintenant tracees
summary: Une section vide, une image manquante ou un e-mail non parti laissent desormais une trace exploitable, au lieu de disparaitre sans bruit.
tags: [observabilite, logs, fiabilite]
---

## Ce qui change

Rien ne change pour le joueur — c'est précisément le sujet. Jusqu'ici, plusieurs
défaillances **se rattrapaient parfaitement** côté visiteur et **disparaissaient
totalement** côté serveur : une section vide sur l'accueil, une image qui ne se
charge pas, un e-mail de confirmation qui ne part pas, une page de ressource qui
renvoie à l'accueil. Chacune laisse maintenant une trace nommée dans la
supervision.

Le rattrapage reste identique : la page redirige toujours, la section vide reste
une section vide, l'e-mail raté est toujours renvoyable depuis la bannière. Seule
la visibilité change.

## Pourquoi

Sur 269 classes, 8 seulement journalisaient quelque chose, et 5 blocs `catch` sur
76 traçaient une cause. Une panne partielle pouvait donc durer des jours sans
qu'aucun signal ne l'annonce.

## Détails

- Page de ressource indisponible, recherche en panne, aperçus de l'accueil vides.
- Lot d'images non résolu par la passerelle Data Dragon, réchauffage différé raté,
  échec du chargeur temps réel.
- Webhook Stripe non configuré ou mal signé, e-mail de compte non remis.
- Journal d'audit qui ne peut pas s'écrire.

## Technique

- Convention d'écriture : `docs/guides/logging.md`. Clé pointée
  `domaine.sujet.résultat`, contexte PSR-3, exception **en objet** sous la clé
  `exception` (classe + `fichier:ligne` + chaîne `previous`, ce que `getMessage()`
  perd), jamais d'interpolation.
- Canaux `catalog` / `ingest` / `billing` / `mail` / `audit` : ils servent au
  **routage** (handler `business` always-on dès INFO), pas à la recherche — le
  collecteur ne voit pas `channel` comme un champ indexé.
- `#[WithMonologChannel]` **n'est pas hérité** (`AttributeAutoconfigurationPass`
  lit `ReflectionClass::getAttributes()`, qui ne remonte pas la hiérarchie) :
  `AbstractResourceController` et `HomeController` passent donc par le **nom de
  l'argument** (`LoggerInterface $catalogLogger`, alias de `LoggerChannelPass`).
- **Aucune clé de contexte `error`** : le collecteur devine `level` par regex sur
  la ligne brute, donc le mot seul reclasse l'enregistrement. Les trois occurrences
  existantes (`ApiBillingController`, `DonationController`, `StripeWebhookController`)
  passent à `exception`.
- **Miroir d'audit** vers le canal `audit`, `Success` → `info`, `Failure` et
  `Denied` → `warning`. Construit à partir d'une **liste blanche** de champs, pas
  par soustraction de clés : `ip`, `meta.identifier` (l'adresse saisie sur
  `user.login_failed`) et les libellés d'affichage n'y entrent jamais. Le NDJSON
  reste la source légale (CNIL 6 mois) ; le miroir vit dans un index 90 jours.
- Contrat *best-effort* d'`AuditLogger` corrigé : la résolution de l'acteur lit le
  jeton de sécurité et pouvait lever **pendant** le traitement d'un échec
  d'authentification, hors du `try`. `NdjsonDayStore::appendRow()` lève désormais
  `NdjsonWriteException` au lieu de retourner comme si l'écriture avait réussi —
  l'analytics l'avale (il tourne après la réponse), l'audit la journalise en
  `critical`.
- Test le plus important du lot : *le miroir d'audit n'émet jamais `ip` ni
  `meta.identifier`* — une garantie RGPD qu'une relecture de code ne rattraperait
  pas.
