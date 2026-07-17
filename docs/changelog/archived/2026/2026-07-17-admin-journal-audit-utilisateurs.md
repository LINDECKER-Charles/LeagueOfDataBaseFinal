---
date: 2026-07-17
type: feat
scope: back
title: Journal d'audit — suivre toutes les actions d'un utilisateur
summary: L'admin peut consulter l'historique complet des actions d'un compte, journalisées de façon structurée et conservées 6 mois (CNIL).
tags: [admin, audit, logs, cnil, securite]
---

## Ce qui change

L'espace d'administration gagne un **Journal d'audit**. Chaque action sensible du
site — connexion, inscription, réinitialisation de mot de passe, création /
modification / suppression de build, gestion des clés API, ainsi que toutes les
actions de modération — est désormais enregistrée avec son auteur, sa cible, son
issue, son horodatage et son IP.

Deux vues :

- **Journal global** (`/admin` → Journal) : le flux récent de toutes les actions,
  filtrable par catégorie et par période.
- **Activité d'un compte** : depuis la liste des utilisateurs, un bouton « Activité »
  ouvre l'historique complet des actions effectuées par ce compte — ou le ciblant.

## Pourquoi

La modération existait, mais rien ne permettait de **retracer ce qu'un utilisateur
avait fait**, ni de garder trace des actions d'administration (redevabilité). Ce
journal comble ce manque tout en restant conforme aux durées de conservation.

## Détails

- **Rétention CNIL** : les journaux sont conservés **6 mois** (recommandation CNIL
  pour les journaux de sécurité), puis purgés automatiquement.
- **Purge manuelle** : un bouton permet de libérer de l'espace à la demande, sur la
  période choisie (au-delà de 6 mois, avant une date, ou tout) — local et archive.
- Les événements de sécurité (connexions, échecs de connexion, déconnexions) sont
  captés automatiquement, sur le firewall public comme sur l'admin.

## Technique

- Pipeline **calqué sur l'analytics** : NDJSON append-only local
  (`var/audit/events/{jour}.ndjson`, `file_put_contents` atomique) → archivage
  **verbatim** dans MinIO (`audit/{jour}.ndjson`). Divergence assumée vs les
  agrégats analytics : un journal d'audit doit préserver **chaque ligne** pour
  reconstituer « les actions de X », donc pas d'agrégation.
- Écriture centralisée par `AuditLogger` (voie unique, best-effort — ne casse jamais
  la requête qu'elle décrit) ; ensemble d'actions fermé (`AuditAction`), acteur
  polymorphe (`user` / `admin` env / `anonymous`). Capture des événements auth via
  `AuditSecurityListener` ; mutations journalisées explicitement au point de succès.
- Lecture : `AuditQueryService` fusionne les tiers local + MinIO, filtre en mémoire
  (scan borné newest-first) — pas d'index DB, contrainte « stockage fichiers ».
- Rétention : `AuditRollupService::RETENTION_PERIOD = P6M`, appliquée par
  `app:audit:rollup --prune --enforce-retention` (à planifier avec l'analytics
  rollup) et par le bouton de purge admin (CSRF, lui-même audité).
- Sécurité : préfixe `audit/` **interdit au web** via nginx
  (`location ^~ /cdn/audit/ { return 404; }`), lu uniquement en interne par PHP.
