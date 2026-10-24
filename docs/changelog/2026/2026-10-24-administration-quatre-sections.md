---
date: 2026-10-24
type: feat
scope: front
title: L'administration se range en quatre sections dans une barre latérale
summary: Dashboard, Analytics, Gestion et Health remplacent la longue barre du haut, avec un fil d'Ariane et un tiroir sur mobile.
tags: [administration, navigation]
---

## Ce qui change

L'administration se parcourt depuis une barre latérale en quatre sections : le Dashboard, qui
rassemble tout ; Analytics (trafic, robots, audience) ; Gestion (utilisateurs, messages, builds,
dons, clients API, journal) ; Health (surveillance, précalcul, stockage, sauvegardes). Une barre
au-dessus de chaque page dit où l'on se trouve, et sur un téléphone la navigation s'ouvre en
tiroir.

## Technique

- `features/admin/frame/` : `admin-nav.ts` (sections, `navPlaceOf`), `nav-icon.ts`, cadre en
  barre latérale fixe au-delà de 1024 px, tiroir en dessous (Échap, fond ou navigation le
  referment).
- Les surtitres des pages suivent leur section (`admin.eyebrows.dashboard`, `health`).
