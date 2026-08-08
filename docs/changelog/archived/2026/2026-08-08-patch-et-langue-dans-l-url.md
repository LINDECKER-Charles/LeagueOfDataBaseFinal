---
date: 2026-08-08
type: fix
scope: back
title: Le patch et la langue de l'URL sont respectés partout
summary: Ajouter ?version= ou ?lang= à une adresse change désormais aussi la page d'accueil et les recherches, et un patch retiré du catalogue ne remet plus votre langue à zéro.
tags: [patch, langue]
---

## Ce qui change

Le site accepte de choisir un patch et une langue directement dans l'adresse. Trois endroits
l'ignoraient encore et affichaient toujours la sélection mémorisée dans votre session : la page
d'accueil et les recherches de champions, d'objets et d'invocateurs. Ils suivent maintenant la même
règle que le reste du site.

Autre correction : quand Riot retire un patch du catalogue, votre langue n'est plus réinitialisée avec
lui. Les deux réglages retombent désormais chacun de leur côté — un patch devenu indisponible bascule
sur le plus récent, la langue reste la vôtre.

## Pourquoi

Un lien partagé avec un patch précis devait afficher ce patch, sur toutes les pages. Et perdre sa langue
d'affichage parce qu'un patch a disparu du catalogue n'a jamais été le comportement voulu.

## Détails

- La page d'accueil affiche ses aperçus sur le patch demandé dans l'URL.
- Les recherches `/api/*/search/{nom}` interrogent le patch et la langue demandés.
- En cas de panne de la couche de données, ces recherches répondent maintenant « service
  indisponible » (503) au lieu d'un faux succès contenant un message d'erreur technique.

## Technique

- `ClientManager::getSession()` : repli par axe. `versionExists() ? … : latestVersion()` et
  `languageExists() ? … : getLangue()` sont désormais indépendants ; l'ancien `||` réinitialisait les
  deux. `getVersions()[0]` (sans garde sur catalogue vide) devient `latestVersion()`.
- `HomeController::home()` et les 3 actions de recherche passent de `ClientManager::getSession()` à
  `PageContextResolver::selection()`, conformément à l'invariant de `CLAUDE.md` (query > session, sans
  redirect).
- `AbstractResourceController::searchResponse()` mutualise les 3 endpoints et remplace le corps
  `dataError()` en HTTP 200 par `{"error": …}` en 503, sans exposer le message d'exception.
- Couvert par `ClientManagerSelectionTest` et `ResourceSearchResponseTest`.
