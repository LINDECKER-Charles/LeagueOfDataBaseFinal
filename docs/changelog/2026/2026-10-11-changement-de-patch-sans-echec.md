---
date: 2026-10-11
type: fix
scope: full-stack
title: Passer à un ancien patch quand le site est chargé ne finit plus sur un échec
summary: Le chargement d'un ancien patch se termine normalement même quand le site est très sollicité ; les images qui manquent encore arrivent ensuite sur la page.
tags: [disponibilite]
---

## Ce qui change

Quand plusieurs visiteurs passent en même temps à d'anciens patchs, le site peut atteindre la
quantité d'images qu'il s'autorise à préparer chaque minute. Jusqu'ici, la fenêtre de
chargement s'arrêtait alors sur un échec après de longues secondes de progression, et sa barre
retombait à zéro. Désormais, le chargement se termine normalement dès que le patch est prêt :
la page s'ouvre, et les images qui manquent encore apparaissent au fil de leur arrivée. La
barre ne revient plus en arrière, même quand le chargement est interrompu.

## Technique

- Warm-up (`Modules/Catalog/WarmUp/`) : une fois le catalogue ouvert, un refus du travail
  d'images (budget `ColdImagesPerMinute`, emplacement d'ingestion) arrête de demander des
  images et clôt le flux par `done`. Les images non admises restent en attente, sans verdict
  (`settled` < `total`) ; les listes les confient à la file de fond. Un catalogue qui ne
  s'ouvre pas (porte froide, budget des datasets, aucune place de flux) ou un flux expiré
  finit toujours en `failed`. Contrat OpenAPI inchangé.
- Toutes les trames d'un flux viennent de `WarmUpTracker` : une trame `failed` garde les
  comptes atteints au lieu de les remettre à zéro.
- Un seul refus compté par flux (`lodb_ingestion_on_demand_refused_total{reason}`), aucune
  ligne de journal par flux.
- Chargeur (`core/context/warm-up/`) : la barre garde la part atteinte d'un flux `failed` ou
  d'un `done` incomplet (`percentOf`) ; la pause de 450 ms reste réservée à une barre pleine
  (`isComplete`), la visite part aussitôt sinon.
