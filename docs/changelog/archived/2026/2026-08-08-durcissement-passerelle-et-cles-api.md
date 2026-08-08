---
date: 2026-08-08
type: fix
scope: fetcher
title: Passerelle de données et cache de clés API durcis
summary: La passerelle qui récupère les données du jeu ne peut plus être détournée par une redirection, et le service d'API publique ne peut plus voir sa mémoire gonfler indéfiniment.
tags: [securite, robustesse]
---

## Ce qui change

Rien de visible au quotidien : ce sont trois verrous posés sur les coulisses du site.

La passerelle qui va chercher les données et les images du jeu n'accepte de parler qu'à une liste
d'adresses connues. Elle ne vérifiait cette liste qu'au premier contact : si l'un de ces serveurs
renvoyait ailleurs, la passerelle suivait sans redemander. Chaque étape est désormais revérifiée.

Elle refuse aussi maintenant une réponse anormalement volumineuse au lieu de la charger entièrement en
mémoire, et le service de l'API publique borne le nombre de clés qu'il garde en mémoire — un flot de
clés inventées ne peut plus faire enfler le service.

## Pourquoi

Trois défauts trouvés lors d'un audit interne. Aucun n'a été exploité ; ils ont été corrigés avant
qu'ils puissent l'être.

## Technique

- `go-workers/internal/fetcher` : `http.Client.CheckRedirect` repasse chaque saut par `Allowed()`
  (schéma https + hôte de l'allowlist). Sans ce hook, Go suit jusqu'à 10 redirections sans contrôle.
- Même paquet : lecture via `io.LimitReader(body, max+1)` avec erreur si le plafond est franchi
  (`DefaultMaxBodyBytes` = 32 MiB, surchargeable par `MAX_RESPONSE_BYTES`) — refus explicite plutôt
  que troncature silencieuse en asset corrompu.
- `go-api/internal/keys/cache.go` : plafond de 50 000 entrées. Une entrée négative n'étant par
  définition jamais relue, elle n'était jamais évincée par le nettoyage paresseux de `Get()`. Au
  plafond, le cache balaie les expirées puis, s'il reste plein d'entrées vivantes, cesse de mettre en
  cache — la requête coûte un aller-retour base, jamais de la mémoire non bornée.
- Couvert par 8 nouveaux tests Go (redirection hors allowlist, plafond de corps atteint et à la limite,
  saturation du cache, récupération après expiration).
