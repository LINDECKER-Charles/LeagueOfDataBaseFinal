---
date: 2026-07-17
type: feat
scope: full-stack
title: Changez votre pseudo et affichez votre tag Riot
summary: Le profil permet de renommer son invocateur et d'ajouter le tag Riot (#EUW) affiché partout où votre nom apparaît.
tags: [comptes, profil, riot-id]
---

## Ce qui change

Une nouvelle section « Identité d'invocateur » sur votre profil permet de
changer votre nom d'invocateur et d'ajouter, si vous le souhaitez, votre tag
Riot — la partie après le `#` de votre Riot ID (ex. `GG#EUW`). Une fois
renseigné, votre nom s'affiche `pseudo#TAG` sur votre profil, votre carte
publique et vos builds partagés.

## Pourquoi

Votre Riot ID fait partie de votre identité de joueur ; le site l'affiche
désormais fidèlement, sans imposer de recréer un compte pour changer de nom.

## Détails

- Champ pseudo pré-rempli (mêmes règles qu'à l'inscription, unicité vérifiée
  sans distinction de casse) et champ tag avec le `#` en préfixe visuel
  (3 à 5 lettres ou chiffres, facultatif).
- Avertissement affiché : changer de pseudo change aussi l'adresse de votre
  page publique `/u/…`.
- Le tag est purement décoratif : deux joueurs peuvent partager le même tag.

## Technique

- Décision produit : le `username` reste le handle URL-safe du site (route
  `/u/{username}` — un `#` ne peut pas vivre dans un path) et devient
  modifiable ; le Riot ID est porté par une colonne séparée `riot_tagline`
  VARCHAR(5) nullable (`/^[A-Za-z0-9]{3,5}$/`), non unique. L'index fonctionnel
  `LOWER(username)` existant reste la source d'unicité ; renommer est sûr car
  `getUserIdentifier()` = email.
- `User::displayName()` (`username` ou `username#TAG`) consommé par
  `profile/index`, `profile/public` et `build/show` (le lien `/u/…` garde le
  username brut). Le header compact conserve le pseudo seul (troncature à 14).
- Route `app_profile_identity` (POST, CSRF stateless id `submit`) : mutation
  puis `validator->validate($user)` — violations → `refresh()` de l'entité et
  flash ciblé (pris/invalide/tag) ; succès → flush.
- Migration `Version20260717131738` (colonne + prérequis OAuth du même lot).
