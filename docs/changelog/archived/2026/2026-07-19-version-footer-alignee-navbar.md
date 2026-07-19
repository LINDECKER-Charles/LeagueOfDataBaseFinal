---
date: 2026-07-19
type: fix
scope: front
title: Version affichée dans le pied de page alignée sur celle de la barre de navigation
summary: Le pied de page affiche désormais la même version applicative que la barre du haut, et ses liens pointent le bon patch.
tags: [footer, version, i18n, navigation]
---

## Ce qui change

La version de l'application affichée dans le pied de page correspond maintenant
exactement à celle du bandeau en haut de page (le petit badge à côté du logo).
Auparavant le pied de page indiquait une version figée qui pouvait ne plus
correspondre à la version réelle du site.

Les liens du pied de page (Champions, Objets, Runes, Invocateurs) mènent aussi
désormais vers le **même patch** que celui affiché sur la page en cours, y compris
lorsque la version est choisie via l'URL.

## Pourquoi

Deux endroits affichaient/utilisaient la version de façon indépendante et
pouvaient diverger : le pied de page n'était pas branché sur la source unique.

## Technique

- `footer.project.version` paramétré (`%version%`) dans les 21 catalogues ;
  la valeur provient du global Twig `app_version` (`%app.version%`), source unique
  déjà utilisée par le badge du header — fini le `Beta 2.0` codé en dur.
- Résolution du patch des liens du footer alignée sur `page_selection()`
  (`PageContextResolver`, path > `?version=` > session), comme header/bottom-nav ;
  remplace la ré-dérivation par template qui ignorait la query et pouvait drifter.
