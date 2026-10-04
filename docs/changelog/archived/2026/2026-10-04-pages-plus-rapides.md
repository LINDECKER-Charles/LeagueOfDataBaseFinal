---
date: 2026-10-04
type: perf
scope: full-stack
title: Des pages qui s'affichent plus vite, même sur mobile
summary: Les pages arrivent déjà prêtes à lire et les images sont plus légères, pour un affichage plus rapide sur toutes les connexions.
tags: [performance, mobile]
---

## Ce qui change

Les pages de l'encyclopédie (champions, objets, runes, sorts) arrivent déjà prêtes à lire :
le contenu s'affiche avant la fin du chargement, puis la page devient interactive. Les pages
les plus consultées sont gardées prêtes à servir, et les images passent dans un format plus
léger quand votre navigateur le permet.

## Pourquoi

Sur une connexion mobile ou lente, il fallait attendre que toute la page soit construite
avant d'en voir le contenu.

## Détails

- Pages de l'encyclopédie et pages éditoriales (à propos, FAQ, changelog) servies prêtes à
  lire.
- Images en WebP quand le navigateur le prend en charge.
