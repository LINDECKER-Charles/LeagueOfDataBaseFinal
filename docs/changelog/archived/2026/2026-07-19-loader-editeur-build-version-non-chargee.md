---
date: 2026-07-19
type: feat
scope: full-stack
title: L'écran de chargement couvre le changement de version dans l'éditeur de build
summary: Changer de version du jeu dans l'éditeur de build affiche l'écran de chargement Hextech pendant que la version se charge, puis révèle les sélecteurs avec leurs vraies icônes.
tags: [loader, builds, versions, ux]
---

## Ce qui change

Dans l'éditeur de build, quand tu sélectionnes une version du jeu qui n'a pas
encore été chargée, l'écran de chargement Hextech apparaît le temps que les
champions, objets et runes de cette version soient préparés. Quand il disparaît,
les sélecteurs affichent directement les vraies icônes — plus de vignettes vides
qui se remplissent après coup.

## Pourquoi

Charger une version encore inconnue prend un moment (des centaines d'images à
récupérer). Avant, rien ne l'indiquait : les listes se peuplaient de placeholders
puis les icônes surgissaient plus tard, donnant l'impression d'un éditeur cassé.
L'écran de chargement rend cette attente visible et honnête, exactement comme lors
d'un changement de version depuis les listes ou l'accueil.

## Détails

- La barre de progression détaille le chargement réel (champions, objets, runes).
- Une version déjà chargée pendant la session ne réaffiche pas l'écran : le retour
  vers elle est instantané.
- Si la version est déjà en cache côté serveur, l'écran ne s'affiche pas du tout —
  seul un vrai travail de chargement le déclenche.

## Technique

- Pont inter-îlots `assets/vue/loader/warmBridge.ts` : `requestWarm(version, lang, path)`
  émet un `CustomEvent` que l'îlot `ResourceLoader` réclame via `preventDefault`.
  Absent (pas d'îlot monté), la promesse se résout aussitôt → dégradation propre.
- `useLoaderStream` gagne un mode « warm-in-place » : `startPrepare(opts.onComplete)`
  rejoue la machine SSE `gate-then-visit` mais, au `done`, rend la main à l'appelant
  au lieu de naviguer (aucun `Turbo.visit`). `activeKeys` explicite le manifeste
  (champions/objets/runes, sans invocateurs). Un run qui en supersède un autre
  résout le callback en attente (pas de `await` suspendu).
- `useBuildEditor` : le `watch(gameVersion)` passe par `switchVersion()` qui gate
  la version derrière `requestWarm(BUILD_WARM_PATH)` avant de recharger les catalogues.
  La version initiale (montage) reste non gatée (spinner par section), chaque version
  n'est gatée qu'une fois par session.
- Backend : `PageContextResolver::loaderSteps('/builds/editor')` renvoie le plan
  champion/item/runesReforged en `perPage 0` (jeu complet, `collectPlan` sans slice),
  ingéré en flux par `LoaderController`. `BUILD_WARM_PATH` est un jeton *hors route*
  (pas de gating de navigation), miroir de la constante côté front.
