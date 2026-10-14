---
date: 2026-10-14
type: fix
scope: front
title: Les robots ne comptent plus dans la mesure d'audience
summary: Un navigateur piloté par un programme n'envoie plus de visite à la mesure d'audience, qui ne compte plus que les vrais joueurs.
tags: [audience, robots]
---

## Ce qui change

Les navigateurs pilotés par un programme (robots d'exploration, outils de test automatisés)
n'envoient plus de visite à la mesure d'audience du site. Les chiffres de fréquentation
reflètent les joueurs, pas les robots qui parcourent les anciens patchs. Rien ne change pour
un visiteur : son choix d'être compté ou non reste celui qu'il a fait.

## Pourquoi

Le 12 octobre 2026, la mesure a compté 145 visiteurs « en accès direct », une seule page vue
chacun, dispersés sur des fiches d'anciens patchs : la forme d'un robot qui exécute les
scripts de la page, pas celle de joueurs.

## Technique

- `AudienceOptOut.isMeasured` est faux quand `navigator.webdriver` vaut `true` (Chrome sans
  tête piloté par Puppeteer, Playwright ou Selenium). Ni Umami (`/u/api/send`) ni la balise
  des navigations internes (`/api/analytics/view`) ne partent alors.
- `isRefused` ne change pas : la case de la politique de confidentialité ne reflète que le
  choix du visiteur et Global Privacy Control.
- Limite : un robot qui masque `navigator.webdriver` reste compté.
- Mesure : visiteurs Umami du canal direct entrés sur une page de patch épinglé, à comparer
  au pic du 12 octobre 2026 (145 visiteurs).
