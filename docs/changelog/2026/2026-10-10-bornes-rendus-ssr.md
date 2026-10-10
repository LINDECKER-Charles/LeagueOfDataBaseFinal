---
date: 2026-10-10
type: fix
scope: front
title: Le site reste disponible pour tous pendant les pics de trafic
summary: Chaque visiteur a sa part du serveur de pages, et une adresse inconnue répond aussitôt.
tags: [disponibilite, performance, robots]
---

## Ce qui change

Le site reste rapide pour tout le monde, même quand un programme demande énormément de pages
d'un coup : chaque visiteur a sa propre part du serveur, et personne ne peut plus l'occuper
seul. Une adresse qui n'existe pas sur le site affiche aussitôt une page « introuvable », et
un lien qui porte des paramètres inutiles mène directement à la page voulue.

## Technique

- Pré-routeur du SSR (`src/server/routing/`), avant l'admission et sans démarrer Angular :
  `400` pour un segment de chemin mal encodé, décodé en `/` ou NUL, `.` ou `..`, ou qui
  contient `\`, `(`, `)` ou `;` (le rendu lirait une autre page) ; page `404` statique
  (`public, max-age=0, s-maxage=3600`, CSP sans script, `noindex`) pour un premier segment
  hors de la grammaire de l'ADR 0005 (ni forme de locale, ni `b`, ni `admin`) ; `301`
  `no-store` d'une liste ou d'une fiche du catalogue vers son URL sans les paramètres
  qu'elle ne lit pas. Listes : `version`, `lang`, `q`, `page`, `size`, clés de facettes et
  `<clé>_all` ; fiches : `version`, `lang` ; 16 paramètres de 200 caractères au plus. Les
  clés de facettes sont des constantes partagées avec les schémas (`*-facet-keys.ts`).
- La locale et la version se lisent décodées (`decodeSegment`) dans `parsePublicUrl`, donc
  dans le classement des couloirs d'admission.
- Admission par client (`trust proxy` à un saut, clé = adresse IPv4 ou /64 IPv6, table LRU
  de 50 000 clients) : 2 rendus simultanés par client ; pages du dernier patch à 300 rendus
  par minute, 60 d'affilée ; versions épinglées à 6 rendus par minute et par client, en plus
  des 3 rendus simultanés et 60 par minute existants. Tout refus : `503` `no-store` et
  `Retry-After: 30`.
- Journal : plus de ligne par refus d'admission, mais une ligne `ssr.admission.summary`
  (`warn`) par minute où des rendus ont été refusés, avec les comptes par couloir et par
  raison ; les compteurs (`RenderCounts`) ont la forme de `lodb_ssr_renders_total`. Un `503`
  servi est journalisé en `warn`, plus en `error`.
- Réglables par `LODB_CURRENT_RENDERS_PER_MINUTE`, `LODB_CURRENT_RENDER_BURST`,
  `LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT`, `LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE`,
  `LODB_RENDER_TRACKED_CLIENTS`, `LODB_QUERY_MAX_PARAMS` et `LODB_QUERY_MAX_PARAM_LENGTH` ;
  la pile de dev relève les bornes d'admission pour l'E2E.
