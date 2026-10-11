---
date: 2026-10-11
type: fix
scope: full-stack
title: Le navigateur protège mieux vos pages, et les liens sortants sont tous chiffrés
summary: Les pages n'acceptent plus que leurs propres scripts, se coupent des accès dont le site n'a pas besoin, et l'application desktop reçoit les mêmes protections.
tags: [securite]
---

## Ce qui change

Chaque page dit désormais au navigateur quels scripts sont les siens, un par un : un script
qui ne vient pas du site n'est pas exécuté, et le navigateur nous le signale. Le site se
coupe aussi l'accès à ce qu'il n'utilise jamais (caméra, micro, position, paiement par le
navigateur…) et ne garde aucun lien avec les fenêtres qui l'ouvrent. La connexion avec Google
et les paiements fonctionnent comme avant.

L'application desktop applique les mêmes règles à ses pages, et un lien vers un autre site ne
s'ouvre plus que s'il est chiffré (https), sur le web, sur Android comme sur le desktop.

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
- Coquille desktop : son `index.html` part avec sa propre CSP (scripts inline de la coquille et
  marqueur du lancement hachés, API par le proxy local, médias de l'API et des CDN de Riot).
- Liens externes : `https` seulement (`isHttpsUrl` des trois plateformes, `ExternalUrl` du pont
  desktop).
- nginx : `Cross-Origin-Opener-Policy: same-origin` et `Permissions-Policy` (fonctions
  d'appareil et de paiement refusées) sur toute réponse.
