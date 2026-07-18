---
date: 2026-07-18
type: feat
scope: full-stack
title: Importer un build vers un autre patch
summary: On peut copier un de ses builds vers une autre version du jeu ; les composants qui n'existent plus sur ce patch sont retirés et signalés, le reste est conservé.
tags: [builds, versioning, import]
---

## Ce qui change

Depuis « Mes builds », chaque build a désormais un bouton **Importer** avec un
choix de patch cible. On copie ainsi un vieux build vers le dernier patch (ou
n'importe quel autre) : LoDB garde automatiquement le champion, les runes et les
objets qui **existent encore** sur ce patch, retire ce qui a disparu ou n'est plus
jouable dans le mode du build, et ouvre l'éditeur pré-rempli avec un **récap de ce
qui a été retiré**. Le build d'origine n'est pas modifié : l'import crée un nouveau
build qu'on relit et enregistre.

## Pourquoi

Un build restait figé sur son patch : le remettre à jour imposait de tout resaisir,
et changer sa version dans l'éditeur le refusait en bloc dès qu'un objet avait
changé. L'import récupère tout ce qui est encore valide et ne laisse à refaire que
le strict nécessaire.

## Détails

- Cible au choix (par défaut le dernier patch, ou celui qu'on navigue).
- Objets absents du patch ou indisponibles dans le mode → retirés, listés par nom.
- Runes conservées si toutes existent sur la cible, sinon remises à zéro.
- Champion absent → à re-sélectionner avant d'enregistrer (signalé).
- Import réservé à ses propres builds.

## Technique

- `BuildStructureProjector` (pur) : reporte la structure sur les catalogues cible,
  garde l'intersection, renvoie un rapport (champion manquant / runes réinitialisées
  / objets retirés). Réutilise `BuildStructureValidator::readInt`,
  `ItemOptionsProjector::isPlayable` (nouveau, existence + `maps[mode]`) et
  `BuildCatalogGate::catalogs()` (extrait pour le chargement version+lang).
- `BuildController::import()` (GET `/builds/{id}/import?to=`, owned-or-404, e-mail
  confirmé) → projection → flashs récap → `editorResponse()` en mode création
  pré-rempli (l'original intact ; le pipeline d'écriture reste le garde-fou final).
- i18n `build.import.*` en/fr (fallback en pour les autres locales).
- Couverture : `BuildStructureProjectorTest` (compatible / champion manquant /
  objets retirés + step vidée / runes réinitialisées).
