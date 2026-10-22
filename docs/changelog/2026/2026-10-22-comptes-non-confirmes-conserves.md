---
date: 2026-10-22
type: fix
scope: full-stack
title: Un compte non confirmé n'est plus supprimé au bout de 7 jours
summary: Un compte dont l'adresse e-mail n'est pas encore confirmée reste ouvert, aussi longtemps qu'il le faut pour recevoir l'e-mail et cliquer sur le lien.
tags: [comptes, e-mails]
---

## Ce qui change

Un compte dont l'adresse e-mail n'est pas confirmée n'est plus supprimé automatiquement
7 jours après sa création. Confirmer l'adresse reste nécessaire pour créer des builds,
générer une clé API et voter, mais le compte, ses favoris et ses préférences sont gardés
en attendant. Le bandeau de confirmation et la politique de confidentialité ne parlent
plus de suppression.

## Pourquoi

Des joueurs perdaient leur compte peu après l'avoir créé, faute d'avoir reçu l'e-mail de
confirmation à temps.

## Technique

- Retrait de la tâche horaire `accounts.unverified_purge`, de `UnverifiedAccountPurge` et
  de la section `LoDb:Accounts:UnverifiedPurge` ; un réglage resté dans un `.env` est
  ignoré.
- Les `account.delete` de `reason` `email-unverified` déjà au journal d'audit restent.
- ADR 0009 amendé (§ Protections), politique de confidentialité datée du 2026-10-22.
