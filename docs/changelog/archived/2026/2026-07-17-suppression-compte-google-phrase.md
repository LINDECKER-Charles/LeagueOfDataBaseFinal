---
date: 2026-07-17
type: fix
scope: full-stack
title: Suppression de compte Google via phrase de confirmation
summary: Les comptes Google confirment la suppression en tapant une phrase, plus un mot de passe qu'ils n'ont pas forcément.
tags: [compte, google, rgpd, securite]
---

## Ce qui change

Supprimer un compte connecté avec Google ne demande plus de mot de passe.
À la place, la zone dangereuse affiche une phrase à recopier (« SUPPRIMER MON
COMPTE ») : il suffit de la saisir pour confirmer l'effacement définitif.

## Pourquoi

Un compte Google n'a pas toujours de mot de passe — et même quand il en a
défini un pour la connexion e-mail, ce n'est pas son identifiant de référence.
Exiger ce mot de passe pouvait bloquer l'accès au droit à l'effacement. La
confirmation par phrase reste explicite tout en restant toujours atteignable.

## Technique

- `User::isGoogleAccount()` : intention explicite (présence du `googleId`),
  distincte de `hasPassword()`.
- `ProfileController::delete()` route la confirmation via `deletionConfirmed()` :
  phrase locale-aware (comparaison trim + casse insensible) pour les comptes
  Google, mot de passe pour les comptes classiques, CSRF seul si aucun mot de
  passe (invariant RGPD préservé).
- Template : champ texte `confirmation` (placeholder = phrase) pour les comptes
  Google, champ mot de passe sinon.
- i18n : clés `profile.danger.confirm_phrase` / `confirm_label` et
  `profile.flash.wrong_phrase` en fr + en ; les 19 autres locales dégradent via
  le fallback `en` (affichage et validation résolus par le même `trans()`, donc
  cohérents).
