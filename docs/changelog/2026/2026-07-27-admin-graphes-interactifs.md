---
date: 2026-07-27
type: feat
scope: back
title: Graphes de la console admin zoomables et lisibles au point près
summary: Les courbes de fréquentation et d'ingestion se zooment, se déplacent et affichent la valeur exacte de chaque série au point survolé.
tags: [admin, analytics, dataviz]
---

## Ce qui change

Les courbes temporelles de la console d'administration (fréquentation, ingestion
du stockage) ne sont plus des images figées. On peut zoomer sur une période à la
molette, déplacer la vue en la faisant glisser, et revenir à la vue complète d'un
double-clic. Au survol, un repère vertical suit le curseur et une infobulle donne
la date et la valeur exacte de chaque série à ce point.

## Pourquoi

Sur 90 jours ou sur tout l'historique, les points se tassaient au point de rendre
un pic impossible à dater ou à chiffrer.

## Détails

- Zoom à la molette centré sur le curseur, boutons −/+/réinitialiser, et pilotage
  au clavier (flèches pour parcourir les points, +/− pour zoomer, Home pour
  réinitialiser).
- Les graduations de dates se recalculent sur la fenêtre visible.
- Sans JavaScript, le graphe reste celui rendu par le serveur, avec sa valeur au
  survol point par point.

## Technique

Le SVG reste rendu côté serveur (`TimeSeriesChart`) et sert de substrat : les
marques vivent dans un groupe `.c-plot` clippé que le module statique
`public/admin/js/chart.js` transforme (zoom X uniquement, l'échelle Y reste
valide), et les valeurs brutes voyagent dans un attribut `data-chart` pour que
l'infobulle soit exacte plutôt que redérivée de la géométrie. L'algèbre de la
vue est isolée et testée (`public/admin/js/chart-scale.js`,
`tests/js/chart-scale.spec.js`). `SvgChartRenderer` a été découpé au passage
(`SvgPrimitives`, `NumberFormat`, `TimeSeriesChart`).
