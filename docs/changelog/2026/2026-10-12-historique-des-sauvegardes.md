---
date: 2026-10-12
type: feat
scope: full-stack
title: Les données du site sauvegardées chaque nuit hors du serveur, sous surveillance
summary: Comptes, builds et dons peuvent partir chaque nuit, chiffrés, hors du serveur, et l'administration montre si chaque sauvegarde a réussi et ce qu'elle coûte.
tags: [sauvegardes, securite, administration]
---

## Ce qui change

Rien ne change à l'écran pour les joueurs. En coulisses, la base du site (comptes, builds,
dons, clés d'API) peut désormais être sauvegardée chaque nuit, chiffrée, chez un hébergeur
distinct du serveur, et une sauvegarde est réellement restaurée chaque mois pour prouver
qu'elle fonctionne. L'administration gagne une page « Sauvegardes » : l'état de chaque base,
une case par nuit (réussie, réussie après nouvelle tentative, en échec ou sans passe), le
détail de chaque étape et le coût mensuel estimé du stockage.

## Pourquoi

Une panne ou une perte du serveur ne doit pas emporter les comptes et les builds des joueurs.
Une sauvegarde qu'on ne surveille pas n'en est pas une : la page dit, nuit après nuit, si
elle a vraiment eu lieu.

## Technique

- Table `backup_run` (migration additive `BackupRuns`), écrite par le conteneur de
  sauvegarde d'infra-vps : une ligne par étape (`backup`, `retention`, `restore`, `check`),
  contraintes de cohérence côté base.
- Rôle `lodb_sauvegarde` posé par `migrate` : membre de `pg_read_all_data`, deux connexions,
  et le seul droit d'insérer dans `backup_run` (colonnes listées) ; mot de passe
  `LoDb__Database__BackupRole__Password` (secret, monté dans `migrate` seul).
- Prod : `compose.sauvegarde.yaml` relie Postgres au réseau `sauvegardes` d'infra-vps
  (alias `lodb-prod-postgres`) ; le déploiement vérifie le réseau et le format du mot de passe.
- `GET /api/admin/backups?days=` : état par base (échéances alignées sur `SauvegardeEnRetard`,
  26 h, et `RestaurationNonVerifiee`, 35 j), taille du dépôt restic et coût estimé
  (`LoDb:Backups:PricePerGbMonth`, par défaut Scaleway Standard Multi-AZ, 0,01606 €/Go/mois),
  étapes de la fenêtre. Page `/admin/backups` (7, 30 ou 90 jours).
- Guide : `docs/guides/sauvegardes.md`.
