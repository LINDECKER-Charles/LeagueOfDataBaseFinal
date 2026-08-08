---
date: 2026-07-21
type: fix
scope: full-stack
title: Changer de version depuis une fiche ne fige plus la navigation
summary: Après un changement de version depuis une fiche champion ou objet, la navigation ne bloque plus, et un objet absent d'un ancien patch mène à la liste plutôt qu'à une page d'erreur.
tags: [versions, navigation, performance, objets]
---

## Ce qui change

- Naviguer vers la liste des objets juste après avoir changé de version depuis une
  fiche ne fige plus la page pendant de longues secondes : elle s'affiche tout de
  suite, comme sur la version la plus récente.
- Passer une fiche d'objet à un ancien patch où cet objet n'existe pas encore
  renvoie désormais vers la **liste des objets de ce patch**, au lieu d'une page
  « introuvable » sans issue.

## Pourquoi

Sur un patch pas encore chargé, l'ouverture de la liste des objets attendait le
téléchargement de **toutes** les icônes d'évolution avant de s'afficher — d'où le
long gel ressenti après un changement de version. Et ouvrir une fiche d'objet sur
une version où l'objet n'existe pas tombait sur une erreur 404 : un cul-de-sac au
lieu d'un retour naturel vers le catalogue.

## Détails

- Les icônes d'évolution de la liste des objets s'affichent maintenant comme le
  reste de la liste : la page arrive immédiatement, les vignettes se remplissent
  ensuite (et sont déjà là au passage suivant).
- Le retour vers la liste conserve le patch et la langue sélectionnés.

## Technique

**Gel de la navigation (perf).** `ItemManager::relatedIndex` (les icônes d'évolution
de `/objects`) résolvait son lot d'images de façon **synchrone**, hors du scope de
déferration ouvert par `paginate()`. Sur une version froide, la réponse de la liste
bloquait donc sur l'union des icônes d'évolution de tous les objets — le lot le plus
lourd de la page — après que le loader SSE avait déjà rendu la main. Ce batch est
désormais différé à `kernel.terminate` comme les icônes primaires : placeholders au
rendu, chauffés après la réponse. Nouveau seam protégé `AbstractManager::withImageDeferral()`
(le `DeferredImageIngestor` est privé, inaccessible aux managers concrets). Les rendus
**détail / éditeur de build / tendances** restent inline (icônes réelles sur version
froide) — contrat préservé, cf. `RuneManagerDeferralTest`.

**404 sur objet absent (UX).** `AbstractResourceController::detailFailure()` : un slug
absent alors qu'un **patch réel est épinglé dans l'URL** redirige en 302 vers la liste
de ce patch (`/{version}/objects`) plutôt que 404. « Épinglé dans l'URL » couvre les
deux formes que peut prendre la version (mêmes priorités que `PageContextResolver` :
chemin > query) — le segment `/{version}/…` (switcher JS, lien canonique historique)
**et** un `?version=` (repli sans-JS du switcher, liens partagés) — canonicalisé vers
la forme chemin. Un patch simplement déduit de la session n'est **pas** épinglé :
l'URL propre canonique (`/object/{name}`) continue de renvoyer un **vrai 404**, réponse
honnête pour les crawlers sur un slug invalide. Appliqué aux quatre ressources
(champion / objet / rune / invocateur).
