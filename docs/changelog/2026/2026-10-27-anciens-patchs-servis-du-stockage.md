---
date: 2026-10-27
type: fix
scope: infra
title: Les anciens patchs servis directement depuis leur stockage, sans saturer le serveur
summary: Les pages des anciens patchs sont lues à chaque visite dans le stockage où elles sont précalculées, au lieu d'être recopiées sur le serveur, dont la mémoire saturait.
tags: [performance, anciens-patchs, disponibilite]
---

## Ce qui change

Les pages des anciens patchs, toutes précalculées, sont désormais lues directement dans leur
stockage à chaque visite, en quelques dizaines de millisecondes. Le serveur web n'en garde plus
de copie : il ne garde que les rares pages qu'il doit encore calculer lui-même.

## Pourquoi

Les robots des moteurs de recherche parcourent les anciens patchs page par page, presque jamais
deux fois la même dans la journée. Chaque page lue était recopiée sur le serveur pour rien : ces
copies avaient rempli sa mémoire, au risque de ralentir tout le site.

## Technique

- `snippets/pages-store/pages.conf` : la location du stockage n'a plus de `proxy_cache` ; ses
  en-têtes passent dans `snippets/pinned-answer.conf`, commun avec la location du SSR.
- `lodb_archive` (`snippets/archive-cache.conf`) ne garde que les rendus du SSR : 2 Go et 16 Mo
  de clés au lieu de 20 Go et 64 Mo (`LODB_ARCHIVE_CACHE_SIZE`, `LODB_ARCHIVE_CACHE_KEYS_SIZE`).
- Mesure du 2026-10-27 : 445 000 URL distinctes servies par jour depuis le stockage, 1,13
  demande par URL, 19 % de `HIT` ; nginx à 506 Mio sur 512 (420 Mio d'inodes et de dentries
  des fichiers du cache). Décision : ADR 0011, amendement du 2026-10-27.
