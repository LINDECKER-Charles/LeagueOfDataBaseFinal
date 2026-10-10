---
date: 2026-10-10
type: devops
scope: apps
title: Les mises à jour de l'app desktop sont signées dans une étape isolée
summary: Les certificats qui signent l'app desktop ne côtoient plus ni la construction de l'app ni ses tests.
tags: [securite, desktop, mises-a-jour]
---

## Ce qui change

Rien ne change dans l'app : elle s'installe et se met à jour comme avant, sous Windows comme
sous macOS. En coulisses, les certificats qui prouvent qu'une version vient bien de nous ne
servent plus que dans une étape dédiée à la signature. La construction de l'app, qui fait
appel à de nombreux outils tiers, et le test de mise à jour qui la lance, n'y ont plus accès.

## Pourquoi

Windows et macOS font confiance à l'app parce qu'elle porte notre signature. Garder ces
certificats loin de tout outil tiers protège cette confiance.

## Technique

- `.github/workflows/release-desktop.yml`, par RID : `build` (`dotnet publish`, sans
  environnement ni secret) → `pack` (seul job de l'environnement `desktop-release` : N-1,
  signature, `vpk pack`, sans build ni cache restauré) → `gate` (porte N-1 → N, sans secret)
  → `publish`. Seul un RID qui a passé la porte est transmis à `publish`, comme avant.
- `tools/desktop-update-e2e/pack.sh` est scindé : `build.sh` publie, `pack.sh` ne fait plus
  que `vpk pack`. `local.sh` enchaîne les deux. Le dossier publié passe de `build` à `pack`
  en tar, pour garder les bits d'exécution ; le trousseau macOS est supprimé dès le pack fini.
- Noms des artefacts publiés, assets et promotion inchangés. Guide :
  `docs/guides/release-desktop.md` § Ce que fait le workflow.
