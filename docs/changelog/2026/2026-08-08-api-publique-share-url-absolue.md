---
date: 2026-08-08
type: fix
scope: back
title: API publique — share_url devient une URL complète
summary: Le champ share_url des builds renvoyés par l'API publique contient désormais l'adresse complète de la page de partage, plus seulement un chemin.
tags: [api]
---

## Ce qui change

Les builds renvoyés par `GET /v1/builds` portaient un `share_url` de la forme `/b/Kf3xQ9pLmT2c`. Le
champ contient maintenant l'adresse complète : `https://league-of-data-base.com/b/Kf3xQ9pLmT2c`.

## Pourquoi

Les clients de l'API appellent depuis leur propre domaine : un chemin relatif y pointait chez eux, pas
sur le site. Le champ annonçait « url » tout en livrant un chemin.

## Détails

**Changement de contrat.** Un client qui préfixait lui-même l'origine du site doit cesser de le faire :
il obtiendrait sinon une adresse doublée. La documentation de l'API a été mise à jour.

## Technique

- `go-api` : origine lue depuis `PUBLIC_SITE_URL` (défaut = production), déclarée dans `compose.yaml`
  et surchargée en dev par `compose.override.yaml`. Sans cette surcharge, dev et staging distribuaient
  des liens pointant vers la production.
- `docs/architecture/api-publique.md` mis à jour (exemple + description du champ).
- Les pannes de la couche de stockage répondent désormais 503 (`service temporarily unavailable`) et
  non plus 500 sur les endpoints de contenu, alignés sur `/v1/usage` — 500 signale un bug applicatif,
  503 une dépendance indisponible.
