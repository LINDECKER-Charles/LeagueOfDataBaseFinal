---
date: 2026-10-12
type: feat
scope: full-stack
title: Les fiches d'un nouveau patch arrivent plus vite dans Bing et les autres moteurs
summary: À chaque nouveau patch, le site prévient directement Bing, Yandex, Naver et Seznam que ses fiches ont changé.
tags: [seo, patchs]
---

## Ce qui change

Quand un nouveau patch est mis en ligne, le site prévient aussitôt les moteurs de recherche du
protocole IndexNow (Bing, Yandex, Naver, Seznam…) que ses fiches ont changé, dans les 21
langues. Ils n'attendent plus leur prochain passage pour relire les valeurs du patch : les
résultats de recherche, et les réponses des assistants qui s'appuient sur Bing, suivent plus
vite.

## Technique

- Ingestion : `IPromotionListener`, appelé par le run qui promeut une version, une fois la
  promotion enregistrée. Un écouteur qui lève est journalisé (`ingest.listener.failed`) sans
  défaire la promotion.
- `IndexNowSubmitter` (module SEO) : les URLs de `SitemapLocations.PatchBound` (accueil, listes,
  historique des patchs, fiches) des 21 langues, par requêtes de 10 000 au plus, vers
  `https://api.indexnow.org/indexnow` (client `indexnow` : pas de redirection ni de cookie,
  30 s). Une ligne de synthèse par promotion (`seo.indexnow.submitted`, `.refused`,
  `.skipped`), aucune relance : les sitemaps datés disent la même chose.
- Clé : `LoDb__Seo__IndexNowKey` (8 à 128 lettres, chiffres ou tirets, validée au démarrage),
  servie à `/{clé}.txt` ; nginx envoie ces noms à l'API (`server.d/seo.conf`). Sans clé ou sans
  `LoDb__Seo__CanonicalOrigin`, rien n'est soumis : la clé ne se pose qu'en prod
  (`configuration.md`).
