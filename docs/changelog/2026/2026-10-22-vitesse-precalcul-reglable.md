---
date: 2026-10-22
type: feat
scope: full-stack
title: Les anciens patchs pré-rendus bien plus vite, à une vitesse réglable
summary: Le précalcul des anciens patchs passe de mois à quelques jours, avec une vitesse que l'administration règle en direct.
tags: [precalcul, performance, administration]
---

## Ce qui change

Les pages des anciens patchs sont préparées à l'avance bien plus vite : deux moteurs au lieu
d'un, et une vitesse que l'administration règle en direct, en pages par minute, en voyant ce
que le serveur a de libre et ce que ça coûte. Les visiteurs passent toujours avant : un frein
d'urgence arrête le précalcul dès qu'ils manquent de place.

## Pourquoi

Au rythme prudent d'avant, préparer tout le catalogue aurait pris plus de 150 jours : les pages
des anciens patchs resteront lentes à la première visite jusqu'à la fin de ce travail.

## Technique

- Mode manuel du rythme (`prerender_pace_setting`, `GET`/`PUT /api/admin/prerender/speed`),
  frein d'urgence (refus du dernier patch, VPS ≥ 90 % pendant 3 min, mesure manquante),
  coupe-circuit `fixed` inchangé.
- `web-ssr-prerender` en répliques (`LODB_PRERENDER_RENDERERS`, 2 en prod), rendus répartis par
  l'API sur leurs adresses ; pages en vol au rythme (`LODB_PRERENDER_PAGES_IN_FLIGHT`).
- Vue des ressources et du coût au pupitre ; commandes `prerender pause` / `prerender resume`.
