---
date: 2026-07-18
type: fix
scope: infra
title: La version de test n'est plus indexable par les moteurs de recherche
summary: L'environnement de staging (test.league-of-data-base.com) renvoie un en-tête noindex sur toutes ses réponses.
tags: [seo, staging, noindex, robots]
---

## Ce qui change

L'environnement de **test** (`test.league-of-data-base.com`) n'apparaîtra plus
dans les résultats de recherche : chaque page et chaque ressource qu'il sert
demande explicitement aux moteurs de ne pas l'indexer. Seul le site de production
reste référencé.

## Pourquoi

Staging servait le même contenu que la production sur un host public : sans
consigne, les moteurs pouvaient l'indexer et créer des doublons du site réel.

## Technique

En-tête **`X-Robots-Tag: noindex, nofollow`** émis par **nginx**
(`docker/nginx/snippets/security-headers.conf`) sur **toutes** les réponses du
host de staging — documents *et* assets/CDN — via le snippet inclus partout.

L'image est **build-once** (staging et prod partagent le binaire), donc le seul
signal distinguant les deux est le **Host**. Un `map $host $robots_noindex`
(`docker/nginx/default.conf`) renvoie `noindex, nofollow` pour
`~^([a-z0-9-]+\.)*test\.league-of-data-base\.com$` et une **chaîne vide** pour tout
host de prod — nginx **omet** un `add_header` dont la valeur est vide, donc la prod
ne porte aucun `X-Robots-Tag`.

Choix délibéré : **pas** de `Disallow: /` dans `robots.txt`. Bloquer le crawl
empêcherait les moteurs de *voir* le `noindex`, laissant le host
indexable-mais-non-crawlé — un `noindex` exige que la page reste crawlable.

Note : en **dev** uniquement, Symfony ajoute déjà son propre `X-Robots-Tag:
noindex` (`DisallowRobotsIndexingListener`, lié au profiler) ; il disparaît en
`APP_ENV=prod`, donc staging ne porte que l'en-tête nginx et la prod aucun.

**Déploiement** : la conf nginx est bakée dans l'image (`docker/nginx/Dockerfile`)
→ rebuild + redéploiement de l'image nginx nécessaire pour prise en compte.
