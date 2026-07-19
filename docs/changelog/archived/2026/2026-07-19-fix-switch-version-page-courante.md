---
date: 2026-07-19
type: fix
scope: front
title: Changer de version met à jour la page en cours immédiatement
summary: Le sélecteur de version/langue rafraîchit la page courante au lieu d'attendre la navigation suivante.
tags: [versions, navigation, turbo]
---

## Ce qui change

Quand on change de version (ou de langue) depuis le sélecteur du bandeau, la page
affichée se recharge tout de suite sur la nouvelle sélection. Avant, sur une page
de champion (ou tout détail), le changement n'était appliqué qu'à la navigation
suivante — la page en cours restait sur l'ancienne version.

## Pourquoi

Le formulaire du sélecteur redirige vers la page courante avec la nouvelle
version. Turbo interceptait l'envoi mais ne rejouait pas la redirection, laissant
la page visible inchangée.

## Technique

Le POST `/setup-submit` renvoie une 302 vers la page d'origine (Referer) avec la
query réécrite (`?version=NEW&lang=NEW`) — vérifié correct côté serveur. Mais le
formulaire du switcher était le **seul** formulaire POST-redirect sans
`data-turbo="false"` : Turbo suivait la 302 sans re-rendre la page redirigée.

Correctif : `data-turbo="false"` sur le formulaire du switcher, alignant sur la
convention déjà appliquée à tous les autres POST-redirect (login/logout/vote/
profil/donate). L'envoi natif suit la 302 et recharge la page sur la nouvelle
sélection.
