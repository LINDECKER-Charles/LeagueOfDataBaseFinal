---
date: 2026-07-19
type: fix
scope: front
title: Fin des pages qui « ne se passent rien » après un clic ou un envoi de formulaire
summary: Le service worker ne casse plus les navigations lentes et sert toujours une réponse valide.
tags: [pwa, service-worker, navigation, offline]
---

## Ce qui change

Cliquer sur un lien ou envoyer un formulaire ne débouche plus, par intermittence,
sur une page vide où « rien ne se passe ». Les navigations un peu lentes (première
visite d'une page pas encore en cache) aboutissent désormais correctement au lieu
d'être interrompues.

## Pourquoi

Le service worker (mode hors-ligne de la PWA) coupait toute page mettant plus de
4 secondes à répondre, puis — faute de copie en cache pour cette première visite —
ne renvoyait aucune réponse valide au navigateur. Résultat : `net::ERR_FAILED`,
page blanche, impression que l'application « rame » ou ne réagit pas.

## Technique

Deux défauts dans `public/sw.js`, tous deux capables de faire échouer une
navigation :

1. `pageStrategy` pouvait résoudre sur `undefined` (cache page absent **et** shell
   `/offline.html` absent) → `respondWith(undefined)` lève
   `TypeError: Failed to convert value to 'Response'`.
2. Le garde-temps de 4 s (`AbortController`) annulait la requête réseau même
   quand aucune copie en cache ne pouvait prendre le relais — une première visite
   lente était donc sabotée en navigation cassée.

Correctifs :
- Le garde-temps ne s'applique **que** si une page en cache existe comme repli
  (`networkFetch(event, cached ? PAGE_TIMEOUT_MS : null)`) ; sinon on laisse le
  réseau aller au bout.
- Toutes les branches garantissent une `Response` : repli cache → `/offline.html`
  → `offlineResponse()` de secours. Plus jamais d'`undefined`.
- `caches.open()` et les `fetch` de sous-ressources (`cacheFirst`, `corsArtCache`)
  sont gardés pour ne plus rejeter `respondWith` (`Response.error()` en dernier
  recours), ce qui éliminait aussi les avertissements
  « the FetchEvent resulted in a network error response: the promise was rejected ».

Contrats préservés : réseau-d'abord pour les pages (les vues sont comptées côté
serveur au `kernel.terminate`), bypass SSE / non-GET / surfaces privées inchangés,
`VERSION` conservée (`lodb-v2`) pour ne pas invalider les caches assets/blobs.

Correctif dev connexe (hors changelog joueur) : la route `_profiler_vite` du
recipe pentatrion/vite-bundle n'avait jamais été installée
(`config/routes/pentatrion_vite.yaml` manquant), ce qui faisait planter en 500 le
web-debug-toolbar (`/_wdt/{token}`) à chaque page — d'où le flot d'erreurs 500 en
console côté dev. Route ajoutée (dev only).
