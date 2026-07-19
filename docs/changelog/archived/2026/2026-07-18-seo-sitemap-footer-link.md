---
date: 2026-07-18
type: feat
scope: front
title: Lien « Plan du site » dans le pied de page
summary: Le plan du site (sitemap) est désormais accessible d'un clic depuis le footer, sur toutes les pages.
tags: [seo, navigation, footer]
---

## Ce qui change

Le pied de page gagne un lien **Plan du site** dans la colonne Navigation. Il
pointe vers le plan du site complet — toutes les fiches (champions, objets,
runes, sorts) et les pages principales, à jour du dernier patch. Disponible
depuis n'importe quelle page et traduit dans les 21 langues.

## Pourquoi

Le plan du site n'était référencé que dans l'en-tête technique de la page
(invisible pour le visiteur). L'exposer dans le footer aide autant les moteurs
de recherche à explorer l'intégralité du contenu que les visiteurs à trouver une
fiche.

## Technique

Le `SitemapController` (`/sitemap.xml`) était déjà dynamique. Ajout d'un `<li>`
vers `path('app_sitemap')` dans `partials/footer.html.twig` et de la clé
`footer.navigation.sitemap` dans les 21 catalogues `messages.<loc>.yaml`.
