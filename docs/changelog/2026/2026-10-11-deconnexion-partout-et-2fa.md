---
date: 2026-10-11
type: feat
scope: full-stack
title: Se déconnecter de tous ses appareils, et une double authentification renforcée
summary: Un bouton de votre profil ferme toutes vos sessions, sur le site comme dans les applications, et la double authentification des comptes est mieux protégée.
tags: [securite, comptes]
---

## Ce qui change

- Votre profil gagne une rubrique « Sessions » : un bouton vous déconnecte de tous les
  navigateurs et de toutes les applications où votre compte est ouvert, celui-ci compris.
  Utile après la perte d'un téléphone ou une connexion sur un ordinateur partagé ; les
  autres appareils sont déconnectés en quelques minutes.
- Dans les applications (bureau et Android), se déconnecter ferme désormais la session
  aussi côté serveur ; et si une connexion d'application était recopiée puis utilisée sur un
  autre appareil, elle serait aussitôt fermée sur les deux. Vous restez connecté
  normalement sur chacun de vos appareils.
- Un code de votre application d'authentification ne sert qu'une fois, et les codes de
  secours ne sont jamais gardés en clair.

## Pourquoi

Une session oubliée sur un appareil perdu devait jusqu'ici expirer d'elle-même, sans moyen
de la fermer à distance.

## Technique

- `POST /api/account/logout-everywhere` (`signOutEverywhere`) : nouveau security stamp et
  révocation de toutes les familles de jetons de rafraîchissement ; la session de la requête
  se ferme aussitôt, les autres cookies à leur revalidation (5 min au plus). Bouton dans le
  profil (`SessionsPanel`), via `AuthStrategy.signOutEverywhere()` sur les trois
  plateformes ; clés `account.sessions.*` dans les 21 langues.
- Jetons de rafraîchissement à usage unique par familles (`refresh_token_families`,
  migration additive) ; un jeton déjà échangé révoque sa famille, sauf dans les 30 s qui
  suivent l'échange (`LoDb:Accounts:RefreshTokens:ReuseGrace`). `/logout` par jeton révoque
  la famille de l'app ; Android et le bureau l'appellent avant d'oublier leurs jetons.
  Purge quotidienne des familles expirées. Métrique `lodb_sessions_revoked_total{reason}`.
- Clé TOTP scellée par Data Protection, codes de secours hachés en argon2id, codes TOTP à
  usage unique (dérive d'un pas) ; tâche quotidienne qui protège les secrets laissés en
  clair par la version précédente.
- Journal : l'identifiant saisi lors d'une connexion refusée devient une nature et un HMAC
  (`LoDb__Audit__IdentifierKey`, secret de l'hôte), les anciennes entrées perdent ce texte.
- Garde anti-CSRF selon le schéma qui a réellement authentifié la requête ; `AllowedHosts`
  restreint aux hôtes de l'environnement ; plus aucun `AllowAnonymous` hors des deux
  captures d'analytics vouées au retrait. ADR 0009 amendée.
