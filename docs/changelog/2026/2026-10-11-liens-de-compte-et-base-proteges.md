---
date: 2026-10-11
type: fix
scope: full-stack
title: Les données des comptes et des builds sont mieux cloisonnées
summary: Le site n'accède plus à sa base de données qu'avec les droits dont il a besoin pour lire et enregistrer vos comptes, builds et favoris.
tags: [securite, comptes, base-de-donnees]
---

## Ce qui change

Rien ne change dans l'usage : comptes, builds, votes, favoris, clés API et dons fonctionnent
comme avant. En coulisses, le site ne se connecte plus à sa base de données avec un accès
total : il peut lire et enregistrer vos données, mais plus modifier la structure de la base
ni agir sur le serveur qui l'héberge. Une requête anormalement longue est aussi interrompue
au bout de quelques secondes au lieu d'occuper le serveur.

## Pourquoi

Si une faille venait un jour à être exploitée dans le site, elle ne donnerait accès qu'aux
opérations ordinaires sur les données, et non au serveur entier : vos comptes restent mieux
protégés.

## Technique

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
