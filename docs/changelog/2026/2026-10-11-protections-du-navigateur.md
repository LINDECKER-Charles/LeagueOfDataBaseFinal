---
date: 2026-10-11
type: fix
scope: full-stack
title: Le navigateur protège mieux vos pages
summary: Les pages n'acceptent plus que leurs propres scripts et le navigateur nous signale toute anomalie.
tags: [securite]
---

## Ce qui change

Chaque page dit désormais au navigateur quels scripts sont les siens, un par un : un script
qui ne vient pas du site n'est pas exécuté, et le navigateur nous le signale.

## Technique

- CSP des pages : seuls les scripts inline des gabarits du build (thème, contrat d'événements,
  chargeur des feuilles différées) et l'appel de rejeu d'événements, de forme fixe, juste après
  le contrat, sont hachés. Tout autre script inline est laissé hors de la politique, compté
  (`lodb_ssr_csp_unexpected_scripts_total`) et résumé une fois par minute
  (`ssr.csp.unexpected_scripts`).
- Violations envoyées à `POST /api/csp-reports` (`report-to` et `report-uri`,
  `Reporting-Endpoints`) : corps borné, débit par adresse et global, rien de la page ni du
  client gardé ; une ligne `web.csp.violated` par minute et `lodb_csp_reports_total`.
- Trusted Types en `Content-Security-Policy-Report-Only` (politiques `angular`,
  `angular#unsafe-bypass`, `lodb#worker` pour l'enregistrement du service worker).
