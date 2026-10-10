---
date: 2026-10-10
type: devops
scope: apps
title: Les mises à jour de l'app Android sont signées dans une étape isolée
summary: Les clés qui signent l'app Android et ses mises à jour ne côtoient plus la construction de l'app.
tags: [securite, android, mises-a-jour]
---

## Ce qui change

Rien ne change dans l'app : elle s'installe et se met à jour comme avant. En coulisses, les
clés qui garantissent qu'une mise à jour vient bien de nous sont désormais utilisées dans une
étape à part, qui ne fait que signer. La construction de l'app, qui fait appel à de nombreux
outils tiers, n'y a plus accès.

## Pourquoi

Une mise à jour n'est acceptée par l'app que si elle porte notre signature. Garder ces clés
loin de tout outil tiers protège cette garantie.

## Technique

- `.github/workflows/release-android.yml` : le job `build` (npm, Angular, Gradle) n'entre plus
  dans l'environnement `android-release` et ne reçoit aucun secret ; il livre l'AAB, l'APK de
  release et le front non signés (artefact `android-unsigned`).
- Le nouveau job `sign`, seul avec `play` à recevoir les clés, signe les bundles de live update
  (`bundles.sh`), l'AAB et l'APK (`sign-release.sh`). Il n'extrait du commit que
  `tools/android-release` et `tools/live-update`, ne restaure aucun cache et n'exécute ni npm
  ni Gradle : Node, `zip`, le JDK et les outils du SDK Android traitent les fichiers comme des
  données.
- Artefacts publiés, noms des assets et étapes suivantes (`stage`, `gate`, `publish`, `play`)
  inchangés. Guide : `docs/guides/release-android.md` § Isolation de la signature.
