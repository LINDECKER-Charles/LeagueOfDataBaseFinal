---
date: 2026-10-11
type: fix
scope: back
title: Comptes non confirmés supprimés après 7 jours, votes et e-mails mieux protégés
summary: Un compte dont l'adresse n'est pas confirmée sous 7 jours est supprimé, voter demande un compte confirmé d'au moins un jour, et le site n'envoie plus d'e-mails en rafale.
tags: [securite, comptes, e-mails, votes]
---

## Ce qui change

- Un compte dont l'adresse e-mail n'est pas confirmée dans les 7 jours qui suivent sa
  création est supprimé automatiquement, comme si son titulaire l'avait supprimé lui-même.
  Le bandeau de confirmation et la politique de confidentialité l'annoncent. Un compte qui a
  déjà un build, une clé API ou un don n'est jamais concerné.
- Renvoyer l'e-mail de confirmation reste possible cinq fois, puis une fois par jour.
- Voter pour un build demande une adresse confirmée et un compte d'au moins un jour : le
  classement des tendances reflète des joueurs, pas des comptes créés à la chaîne. Un vote
  refusé dit désormais pourquoi.
- Chaque compte a un plafond horaire raisonnable pour les achats de crédits API, les clés
  API, les builds enregistrés et les votes : un joueur ne l'atteint pas en jouant
  normalement, et il ne dépend que de son compte, jamais de la connexion internet partagée.
- Le site limite le nombre d'e-mails qu'il envoie chaque heure : un afflux de demandes ne
  peut plus faire classer ses e-mails comme indésirables. Au-delà, l'envoi est différé, rien
  n'est perdu.

## Pourquoi

Une adresse jamais confirmée peut appartenir à quelqu'un d'autre : elle n'a pas à recevoir
nos e-mails ni à garder un compte ouvert. Et les e-mails de confirmation ou de mot de passe
oublié n'arrivent bien que si le site garde la confiance des messageries.

## Technique

- Tâche horaire `accounts.unverified_purge` (verrou consultatif, une ligne de synthèse) :
  efface par `UserManager.DeleteAsync` les comptes non vérifiés de plus de 7 jours, sans
  build, clé API, don, rôle, bannissement, statut de soutien ni connexion Google ; un
  `account.delete` par compte, `reason` `email-unverified`, acteur anonyme. Bornes
  `LoDb:Accounts:UnverifiedPurge` (`After` 7 j, `BatchSize` 500).
- Renvoi de vérification : `MailThrottleRule.ConfirmEmail` passe de 3 par 15 min à 5 en tout
  puis 1 par 24 h, par compte (`LoDb:Accounts:VerificationResend`) ; la ligne de comptage
  garde le total sur la vie du compte. Le renvoi et le mot de passe oublié lisent et
  inscrivent ce compte sous le verrou de la ligne du compte (`MailThrottle.HoldAsync`) :
  des demandes simultanées sont comptées l'une après l'autre.
- Vote : politique `VerifiedEmail` et compte de 24 h au moins (`LoDb:Builds:Votes`), sinon
  `403 email-not-verified` ou `account-too-recent`. `compose.override.yaml` ramène l'âge à
  zéro pour l'E2E seulement.
- Limites par compte (`AccountActionLimits`, partition par `NameIdentifier`, jamais par
  adresse) sous `LoDb:RateLimiting` : `CheckoutsPerHour` 10, `ApiKeyIssuesPerHour` 5,
  `BuildEditsPerHour` 60, `VotesPerHour` 120 ; `429 rate-limited` au-delà.
- Outbox : plafond horaire glissant par modèle pour tout le site (`LoDb:Outbox:HourlyCaps`,
  100 confirmations, 50 réinitialisations, 20 messages de contact) ; l'excédent reste en
  attente sans tentative consommée. Jauge `lodb_outbox_deferred`, journaux
  `outbox.hourly_cap.reached` et `outbox.hourly_cap.released`.
