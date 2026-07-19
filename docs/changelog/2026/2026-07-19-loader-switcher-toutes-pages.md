---
date: 2026-07-19
type: fix
scope: front
title: L'écran de chargement s'affiche au changement de version/langue sur toutes les pages
summary: Changer de version ou de langue depuis n'importe quelle page (fiche champion, objet, profil…) affiche désormais l'écran de chargement, plus seulement depuis l'accueil et les listes.
tags: [loader, switcher, ux]
---

## Ce qui change

Quand tu changes de version du jeu ou de langue depuis le sélecteur en haut, l'écran
de chargement Hextech apparaît maintenant quelle que soit la page où tu te trouves —
une fiche de champion, d'objet, de rune, ton profil… Avant, il ne s'affichait que
depuis l'accueil et les pages de liste ; partout ailleurs la page se rechargeait sans
aucun retour visuel.

## Pourquoi

Un changement de version relance toujours un chargement des données côté serveur. Sur
les pages de détail, ce temps se déroulait sans le moindre indicateur : on avait
l'impression que le clic n'avait rien déclenché. L'écran de chargement couvre désormais
ce temps partout, dès l'instant où la requête part.

## Détails

- Retour visuel instantané : l'écran apparaît dès la validation du sélecteur, sans attendre l'aller-retour réseau.
- Sur les pages à grille d'images (accueil, listes), la barre de progression détaille le chargement comme avant.
- Sur les autres pages, un indicateur d'attente sobre couvre le rechargement jusqu'à l'affichage.

## Technique

- `useLoaderStream.onSubmit` : suppression de l'early-return limité aux routes home/liste
  (`resourcesFor().length`). Le switcher (présent sur toutes les pages via le header,
  `data-turbo="false"`) est désormais gaté partout.
- `startPrepare` : opt-in orthogonal `eager` — overlay levé d'emblée au lieu d'attendre
  l'événement `start`. Une destination sans batch d'images (`active` vide → détail,
  profil…) court-circuite le flux SSE et laisse `navigateWarm` couvrir le rendu cold
  jusqu'au `turbo:load`.
- Le POST `app_setup_save` reste `redirect: 'manual'` (persistance session/cookie
  uniquement) ; l'URL de destination est calculée client-side par `destinationForSwitch`,
  en équivalence assumée avec `UrlGenerator::applySelection`. Navigation par liens
  inchangée : invariant « détail = ingestion différée à `kernel.terminate` » préservé.
