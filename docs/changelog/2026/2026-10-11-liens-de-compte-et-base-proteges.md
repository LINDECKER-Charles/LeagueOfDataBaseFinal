---
date: 2026-10-11
type: fix
scope: full-stack
title: Liens de compte plus discrets, données des comptes mieux cloisonnées
summary: Les liens de confirmation et de nouveau mot de passe ne laissent plus leur code secret dans l'adresse, et le site n'accède plus à sa base qu'avec les droits dont il a besoin.
tags: [securite, comptes, e-mails, base-de-donnees]
---

## Ce qui change

- Les liens des e-mails de confirmation d'adresse et de nouveau mot de passe gardent leur
  code secret dans une partie de l'adresse que le navigateur n'envoie jamais au serveur :
  il n'apparaît plus dans les journaux de connexion. Dès la page ouverte, le code disparaît
  de la barre d'adresse et de l'historique ; un lien copié ensuite ne permet plus de rien
  faire. Les liens déjà reçus fonctionnent toujours.
- Rien ne change dans l'usage des comptes, builds, votes, favoris, clés API et dons. En
  coulisses, le site ne se connecte plus à sa base de données avec un accès total : il peut
  lire et enregistrer vos données, mais plus modifier la structure de la base ni agir sur le
  serveur qui l'héberge. Une requête anormalement longue est aussi interrompue au bout de
  quelques secondes au lieu d'occuper le serveur.

## Pourquoi

Un code de confirmation ou de nouveau mot de passe n'a pas à traîner dans un journal ou dans
l'historique du navigateur. Et si une faille venait un jour à être exploitée dans le site,
elle ne donnerait accès qu'aux opérations ordinaires sur les données, et non au serveur
entier : vos comptes restent mieux protégés.

## Technique

- `Modules/Accounts/Links/FrontPages.cs` : `verify-email?user={id}#token={token}` et
  `reset-password?user={id}#token={token}`. Le front (`features/account/auth/link/`) lit le
  fragment, accepte encore `verify-email?token=` et `reset-password/{token}` (liens déjà
  envoyés), puis retire le jeton de l'URL par une navigation `replaceUrl`. Pages rendues côté
  navigateur seulement (`RenderMode.Client`) : le serveur ne voit jamais le fragment.
- `migrate` (superutilisateur) tient en place, de façon idempotente, `lodb_owner`
  (propriétaire du schéma, NOLOGIN), `lodb_partitions` (propriétaire d'`analytics_event`, de
  ses partitions et de deux fonctions `SECURITY DEFINER` de création et de suppression des
  partitions quotidiennes, NOLOGIN) et `lodb_app` (LOGIN, DML seul, `statement_timeout` 15 s,
  `CONNECTION LIMIT 60`, mot de passe posé en vérificateur SCRAM) ; privilèges par défaut pour
  les tables des migrations à venir ; `Persistence/Roles/`.
- L'API se connecte comme `lodb_app` ; l'agrégation quotidienne et l'effacement des données
  client relèvent leur délai pour leur transaction (`LoDb:Analytics:JobStatementTimeout`).
- `LODB_APP_DB_PASSWORD` : tiré sur l'hôte au premier déploiement (`split-env.sh`,
  `openssl rand`), jamais dans `ENV_*`, monté en fichier dans `api` et `migrate`.
- L'ancienne stack, qui se connecte comme le superutilisateur, n'est pas touchée : retour
  arrière inchangé (`bascule.md` § 8.2). Guides : `configuration.md` § 4 et 4.3, `pieges.md`.
