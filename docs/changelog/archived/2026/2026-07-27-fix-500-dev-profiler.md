---
date: 2026-07-27
type: fix
scope: infra
title: Fin des erreurs 500 sur la stack de developpement
summary: Les pages servies en local ne se terminent plus par une page d'erreur 500 parasite.
tags: [docker, dev]
---

## Ce qui change

En environnement de développement, chaque page pouvait se terminer par un bloc
d'erreur 500 collé après le contenu, et la barre de debug Symfony restait
introuvable. Les pages se chargent désormais proprement.

## Pourquoi

Le conteneur PHP tourne en `root` alors que le serveur d'application écrit en
`www-data`. Toute commande console lancée à la main laissait derrière elle des
dossiers que le serveur ne pouvait plus écrire ; il échouait alors juste après
avoir envoyé la page, ce qui ajoutait une erreur 500 en fin de document.

## Technique

- Cause racine : `var/cache/dev/profiler` créé en `root:root` par un
  `docker compose exec php php bin/console …` (le service dev déclare `user: root`).
  `FileProfilerStorage::write()` lève `RuntimeException: Unable to create the storage
  directory` au `kernel.terminate`, **après** `Response::send()` : les headers sont déjà
  partis, Symfony rend malgré tout sa page d'erreur (d'où le
  `Response::sendHeaders() after headers have already been sent`), le HTML se termine par
  l'écran 500 et le profil n'est jamais stocké → `_wdt` en 404. L'abonné analytics
  enregistrait bien `status: 500` (22 occurrences sur `/`, `/champions`, `/objects`,
  `/champion/*`, `/rune/*`).
- Correctif : `compose.override.yaml` chowne `var` **récursivement** au démarrage du
  service (`chown -R www-data:www-data var`) au lieu des seuls dossiers de premier niveau,
  qui ne rattrapaient jamais les sous-arbres root.
- Convention documentée (CLAUDE.md « Pièges connus », `docs/guides/docker.md`) : lancer les
  commandes conteneur en `docker compose exec -u www-data php …` — les garde-fous
  (`phpunit`, migrations) sont mis à jour en conséquence.
- Vérification Playwright après correctif : 20 routes anonymes + parcours connecté
  (login → profil, builds, portail API), 11 pages admin, 7 fragments de panneaux et leur
  repli `?sync=1`, tous en 200, zéro erreur JS ; îlots Vue (dont les îlots différés) montés
  et squelettes résorbés, aller-retour Turbo compris ; zoom/pan/molette/clavier des graphes
  admin fonctionnels. Backend `tests/Unit` 477 verts, vitest 169 verts, `vue-tsc` propre.
