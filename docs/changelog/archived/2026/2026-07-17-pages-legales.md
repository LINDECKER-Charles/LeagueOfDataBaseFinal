---
date: 2026-07-17
type: feat
scope: front
title: Mentions légales, confidentialité, CGU et cookies disponibles
summary: Quatre pages légales complètes, en français et en anglais, expliquent qui édite le site et ce qu'il fait de vos données.
tags: [legal]
---

## Ce qui change

Le site dispose désormais de quatre pages légales : mentions légales, politique de
confidentialité, conditions générales d'utilisation et politique de cookies. Elles
s'affichent en français ou en anglais selon la langue choisie, dans le même style que le
reste du site, avec un sommaire cliquable et la date de dernière mise à jour.

## Pourquoi

Avec l'arrivée des comptes, des builds partageables et du bouton de don, chacun doit
pouvoir vérifier simplement qui édite le site, ce qui est fait de ses données (aucune
publicité, aucun traceur tiers) et à quelles conditions le service est fourni.

## Détails

- Mentions légales : éditeur, hébergeur, propriété intellectuelle et avertissement
  officiel Riot Games.
- Confidentialité : tableau clair des données traitées (compte, builds, mesure
  d'audience maison, dons via Stripe), vos droits et comment les exercer.
- CGU : règles du compte, des builds (privé / public / lien), des dons et de la
  modération.
- Cookies : liste exacte des cookies du site, tous essentiels — aucune bannière
  nécessaire.

## Technique

- Routes GET `app_legal_{notice,privacy,terms,cookies}` sous `/legal/*`
  (`LegalController`, variante de template FR si la locale UI commence par `fr`,
  sinon EN).
- Identité éditeur centralisée dans `config/packages/legal_info.yaml`
  (paramètres `legal.*` → DTO `App\Dto\LegalInfo` autowiré) avec placeholders
  `[[À COMPLÉTER]]` — checklist de complétion dans `docs/legal-info.md`.
- Layout commun `templates/legal/layout.html.twig` (bandeau codex, plate de date,
  sommaire ancré, typographie prose scopée sur tokens Hextech) ; corps légal en dur
  par locale, chrome via clés `legal.*` (à ajouter aux catalogues).
- Contenu aligné sur le code réel : analytics first-party sans cookie (IP/UA en
  journaux bruts locaux, agrégats MinIO anonymisés, GeoLite2 optionnel), cookies
  `PHPSESSID` / `lod_prefs` (7 j, opt-in) / `REMEMBERME` (30 j, à venir).
