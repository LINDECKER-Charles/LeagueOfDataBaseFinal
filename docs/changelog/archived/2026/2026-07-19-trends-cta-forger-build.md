---
date: 2026-07-19
type: feat
scope: front
title: Bouton « Forger mon build » toujours visible sur la page Tendances
summary: La page Tendances propose désormais en permanence un appel à l'action pour créer son build — ou créer un compte si tu n'es pas connecté — même quand des builds sont déjà classés.
tags: [trends, builds, cta, ux]
---

## Ce qui change

La page Tendances affiche maintenant un bouton d'action en haut, à côté du compteur
de résultats, quel que soit le nombre de builds classés. Si tu es connecté, il t'invite
à forger ton propre build ; sinon, il te propose de créer un compte pour te lancer.

Avant, cet appel à l'action n'apparaissait que lorsqu'aucun build ne correspondait au
filtre : dès qu'une tendance existait, plus aucune porte d'entrée pour publier la sienne.

## Pourquoi

Les tendances se nourrissent des builds de la communauté : il faut inviter à contribuer
au moment où l'on regarde le classement, pas seulement quand il est vide. Le bouton
s'adapte à ton état — connecté ou non — pour t'amener au bon endroit sans détour.

## Détails

- Connecté : « Forger mon build » ouvre directement l'éditeur de build.
- Non connecté : « Créer un compte » mène à l'inscription (l'éditeur exige un compte vérifié).
- L'état « aucun résultat » réutilise le même bouton et devient lui aussi adaptatif.

## Technique

- Nouveau partiel `templates/trends/_forge_cta.html.twig` : source unique de la règle
  « connecté → `app_build_new`, sinon → `app_register` », réutilisé par l'en-tête et
  l'état vide (fin de la divergence possible entre les deux).
- Aucune nouvelle clé i18n : réutilisation des libellés déjà localisés dans les 21 locales
  (`community.trends.empty_cta` et `nav.register`).
- CTA primaire `hx-btn-gold` (design system), aligné avec le bouton de filtre.
