---
date: 2026-07-19
type: feat
scope: front
title: Un indicateur de chargement apparaît sur les pages de détail qui tardent
summary: Après un changement de version, cliquer sur une fiche (champion, objet, rune, sort) affiche désormais un indicateur si la page met un instant à venir, au lieu d'un écran figé.
tags: [loader, ux, navigation]
---

## Ce qui change

Quand une page de détail met un moment à s'afficher — typiquement le premier accès
à une fiche après avoir changé de version, quand ses données doivent encore être
récupérées — un indicateur de chargement apparaît maintenant à l'écran, avec la
version en cours de récupération. Fini le clic sans réaction où l'on se demande si
quelque chose se passe.

## Pourquoi

Les listes (champions, objets…) avaient déjà leur écran de chargement, mais pas les
pages de détail : sur une version encore « froide », le clic pouvait rester plusieurs
secondes sans aucun retour visuel, le temps que le serveur récupère les données. Rien
ne bougeait, l'attente paraissait anormale.

## Détails

- L'indicateur ne s'affiche que si la navigation dépasse un court seuil — les pages
  déjà prêtes (ou préchargées au survol) s'ouvrent instantanément, sans clignotement.
- Il réutilise l'écran de chargement existant, en version épurée (animation + version
  récupérée), cohérent avec celui des listes.

## Technique

`useLoaderStream` gagne un mode « slow visit » : pour toute visite Turbo **non gatée**
(pages de détail, ou sélection version/langue non résolue), `onBeforeVisit` arme un
timer `SLOW_VISIT_DELAY` (200 ms) au lieu de laisser filer sans retour. Passé le seuil,
l'overlay partagé s'ouvre en mode indéterminé (`indeterminate` — pas de manifeste ni de
flux de noms, barre en shimmer, sous-titre suffixé de la version cible) ; `turbo:load`
le masque. Le seuil est l'anti-flash : les visites chaudes/préchargées/cachées se
rendent avant 200 ms et ne surfacent jamais l'overlay.

Timing validé contre la source Turbo 8 (`Visit#loadResponse`) : `turbo:before-cache` /
`turbo:before-render` — donc le teardown d'îlots de `main.ts` — ne se déclenchent
qu'**après** réception de la réponse. L'îlot loader (`data-turbo-permanent`) reste donc
monté pendant toute l'attente du rendu synchrone froid, et le timer peut surfacer
l'overlay ; au retour, teardown + rendu le masquent pile quand la page apparaît.

Le chemin gaté (SSE liste/home) est inchangé : `onBeforeVisit` `preventDefault` +
`startPrepare` exactement comme avant. Non couvert volontairement : le rechargement
plein écran du switcher depuis une page de détail (`data-turbo="false"`) — navigation
navigateur native, hors périmètre Turbo. Spec `ResourceLoader.spec.ts` étendue
(affichage après seuil + invariant anti-flash sous le seuil).
