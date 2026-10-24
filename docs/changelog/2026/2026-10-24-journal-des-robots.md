---
date: 2026-10-24
type: feat
scope: full-stack
title: Les robots qui explorent le site, enfin identifiés et vérifiés
summary: Chaque requête de robot est désormais journalisée, son auteur nommé et vérifié, ce qui permet de suivre comment Google et les autres moteurs parcourent le site.
tags: [administration, robots, seo, trafic]
---

## Ce qui change

L'administration voit désormais chaque robot qui visite le site, anciens patchs, sitemaps et
`robots.txt` compris : qui il est (Googlebot, Bingbot, GPTBot, outils SEO, scripts…), s'il est
authentique ou s'il usurpe le nom d'un moteur, ce qu'il demande et comment le site lui répond.
Les robots qui se font passer pour un navigateur sont repérés à leur rythme. La page Trafic
compare chaque période à la précédente, place les robots à côté des visiteurs et permet de
chercher n'importe quelle page, entité ou site référent.

## Pourquoi

Le référencement dépend de la façon dont les moteurs explorent les pages de chaque patch : on
peut maintenant voir si Googlebot les obtient bien, et repérer un robot qui charge trop le site.

## Technique

- ADR 0013. nginx (`njs/robot-journal.js`) poste les requêtes à `/internal/robots/journal` ;
  zone `LoDb.Infrastructure/Robots` (catalogue d'UA, cadence, DNS inverse confirmé) ; tables
  `robot_agent`, `robot_hit` (30 j), `robot_daily` (migration additive `RobotJournal`).
- `GET /api/admin/analytics/robots`, `/robots/{key}`, `/robot-hits`, `/breakdown` ;
  `AnalyticsReport.previous`.
- Pages `/admin/robots`, `/admin/robots/{key}`, `/admin/robots/journal`, refonte de
  `/admin/traffic`.
