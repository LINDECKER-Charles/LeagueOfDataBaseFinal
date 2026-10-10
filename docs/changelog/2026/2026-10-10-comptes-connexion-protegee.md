---
date: 2026-10-10
type: fix
scope: back
title: Comptes mieux protégés, et un site qui tient sous un afflux de connexions
summary: Les tentatives de connexion sont limitées, un compte ne se verrouille plus sous la rafale d'une seule autre connexion, et vérifier un mot de passe ne ralentit plus le reste du site.
tags: [securite, comptes, disponibilite]
---

## Ce qui change

- Trop de tentatives de connexion en peu de temps depuis la même connexion internet : le
  site demande de patienter quelques minutes. Se connecter normalement n'est jamais concerné.
- Après quatre mots de passe faux pour un même identifiant depuis la même connexion, les
  essais suivants attendent un moment, avant que le compte ne se verrouille : votre compte
  ne se bloque plus sous une rafale d'essais d'une seule autre personne.
- Supprimer son compte demande toujours son mot de passe ; un mot de passe faux y compte
  comme à la connexion, et les essais sont limités pour chaque compte.
- Aux heures de grande affluence, une connexion peut demander de réessayer quelques
  secondes plus tard : le serveur garde ainsi le reste du site rapide.
- Les administrateurs associent leur application d'authentification avec une invitation
  à durée limitée, et non plus avec leur seul mot de passe.

## Pourquoi

Vérifier un mot de passe est volontairement coûteux, pour le protéger. Sans limite, beaucoup
de vérifications à la fois pouvaient occuper le serveur et ralentir toutes les pages.

## Technique

- Admission asynchrone au hachage dans chaque point d'entrée qui hache (connexion, jeton,
  inscription, réinitialisation, premier mot de passe, suppression) : 1 permis, file de 8,
  attente de 500 ms au plus, puis `503` + `Retry-After: 5` (`server-busy`). Bornes sous
  `LoDb:Accounts:Hashing`. La porte synchrone du hacheur reste un filet ; la vérification
  bcrypt héritée y passe aussi et refuse un coût supérieur à 13.
- Politique `sign-in` (connexion, jeton, échange Google des apps) : 10 par adresse (ou /64)
  et par 15 min. Plafond de 4 échecs par 15 min pour un couple (identifiant normalisé,
  adresse), consulté avant la recherche du compte, même `429 rate-limited` que le compte
  existe ou non ; le compte trouvé, aussi par couple (compte, adresse), que l'e-mail ou le
  nom le désigne. Bornes sous `LoDb:RateLimiting` ; `compose.override.yaml` relève
  `SignInPerWindow` pour l'E2E seulement.
- Suppression de compte : `CheckPasswordSignInAsync` avec verrouillage, et politique
  `password-confirm` de 5 par 15 min, partitionnée par compte (jamais par adresse).
- Champs refusés `400 malformed` avant tout traitement : identifiant de plus de 254
  caractères, codes de second facteur hors chiffres ou lettres, code Google ou vérificateur
  PKCE de forme impossible (aucun appel sortant).
- Admin : l'enrôlement du TOTP exige une invitation Data Protection de 15 min, affichée par
  `admin create` ou `admin mfa-invite` ; `admin mfa-reset` retire un TOTP perdu et ferme les
  sessions.
- Meter `LoDb.Security` : `lodb_hashing_in_flight`, `lodb_hashing_wait_seconds`,
  `lodb_hashing_refused_total{reason}`, `lodb_sign_in_attempts_total{result,account,channel}`,
  `lodb_lockouts_total{is_admin}`, `lodb_admin_security_events_total{event}`.
