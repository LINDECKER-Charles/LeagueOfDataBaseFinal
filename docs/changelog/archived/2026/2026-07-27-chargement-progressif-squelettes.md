---
date: 2026-07-27
type: perf
scope: full-stack
title: Chargement progressif : la page s'affiche avant ses données
summary: Les tableaux de bord admin et les modules interactifs du site montrent un squelette et se remplissent au fil de l'eau au lieu de retenir la page.
tags: [performance, admin, ux]
---

## Ce qui change

La console d'administration ne fait plus attendre l'écran entier pour la section
la plus lente. La page, sa navigation et ses titres s'affichent immédiatement ;
chaque bloc de données montre un squelette animé puis se remplit dès que son
rapport est prêt, indépendamment des autres. Si un service ne répond pas, seul
son bloc affiche une erreur — avec un bouton « Réessayer » — au lieu de bloquer
la page.

Côté site, les modules interactifs (barre de filtres des listes, galerie de
skins, sélecteur de niveau des statistiques) ne laissent plus de trou pendant
leur chargement : ils réservent leur place avec un squelette, ce qui supprime le
saut de mise en page. Les modules situés plus bas dans la page n'attendent plus
le haut de page pour se charger : ils se préparent à l'approche du regard.

## Pourquoi

La vue d'ensemble de l'admin agrège trois rapports coûteux (analyse du bucket
d'images, journal de trafic, sondes de services). Le plus lent imposait son
temps à toute la page. Sur le site, les modules interactifs apparaissaient d'un
coup en décalant le contenu déjà lu.

## Détails

- Vue d'ensemble, Trafic, Audience, Stockage et Surveillance chargent leurs
  données en parallèle après l'affichage de la page.
- Sans JavaScript, la console bascule automatiquement sur un rendu complet
  classique : aucune fonctionnalité perdue.
- Les animations de squelette respectent le réglage « animations réduites » du
  système.

## Technique

`AdminPanelCatalog` est la source unique panneau → template + données ; le même
catalogue sert la route de fragment `/admin/panel/{panel}` (chargée par
`public/admin/js/panels.js`) et le rendu synchrone `?sync=1` vers lequel un
`<noscript>` redirige, de sorte que les deux chemins ne peuvent pas diverger.
Côté front, les shells `data-vue` contiennent un squelette rendu par Twig (Vue
vide le conteneur au montage, aucun nettoyage à câbler) et `data-vue-lazy` monte
l'îlot via un `IntersectionObserver`.
