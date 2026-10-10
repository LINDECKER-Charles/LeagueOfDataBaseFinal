---
date: 2026-10-10
type: fix
scope: front
title: Le site reste rapide quand un robot parcourt toutes les anciennes versions
summary: Les pages des anciens patchs sont construites à un rythme borné, et le reste du site ne ralentit plus.
tags: [disponibilite, performance, robots]
---

## Ce qui change

Le site reste disponible et rapide même quand un robot visite en masse les pages des anciens
patchs. Les pages de la version actuelle gardent leur place réservée. Si les pages d'anciens
patchs sont très demandées, celles qui ne sont pas déjà prêtes peuvent répondre
« Service indisponible » quelques instants : il suffit de recharger un peu plus tard.

## Pourquoi

Le 10 octobre 2026, un robot venu de plus de 200 adresses a demandé chaque objet et chaque
champion de chaque patch, dans chaque langue, environ 25 fois par seconde. Chaque page devait
être construite à la demande. Le site entier est devenu inaccessible pendant plusieurs heures,
et le serveur, partagé avec d'autres services, a ralenti.

## Technique

- Le serveur SSR admet les rendus (`src/server/admission/`) : 8 rendus simultanés au plus,
  dont 3 pour une page de version épinglée (segment de version dans le chemin ou
  `?version=`). Ces pages ont aussi un budget de 60 rendus par minute, 10 d'affilée (seau à
  jetons). Au-delà, `503` immédiat avec `Cache-Control: no-store` et `Retry-After: 30`. nginx
  sert sa copie périmée par-dessus (`proxy_cache_use_stale http_503`). Les fichiers
  statiques, catalogues i18n compris, ne passent pas par l'admission.
- Un emplacement reste occupé jusqu'à la fin du rendu, pas jusqu'au départ du client : le
  robot abandonnait chaque requête au bout de 3,6 s, mais le rendu continuait à consommer du CPU.
- Cause de la charge de l'API : son cache garde 16 catalogues (version, langue) ; un parcours
  uniforme de ~150 versions × 21 langues le vidait à chaque requête, et chaque requête
  rechargeait un catalogue complet. Borner les rendus borne ces chargements.
- Réglables sans rebuild par `LODB_RENDER_MAX_IN_FLIGHT`, `LODB_PINNED_RENDER_MAX_IN_FLIGHT`,
  `LODB_PINNED_RENDERS_PER_MINUTE` et `LODB_PINNED_RENDER_BURST` dans le `.env` de l'hôte.
  La pile de dev les relève, pour que l'E2E ne bute pas sur les limites.
