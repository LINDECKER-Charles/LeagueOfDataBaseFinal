---
date: 2026-08-24
type: feat
scope: full-stack
title: Un second theme — Zaun, la Cite de Fer et de Verre
summary: Un bouton dans la barre du haut fait basculer tout le site de Piltover a Zaun, et s'en souvient.
tags: [theme, design-system, accessibilite, pwa]
---

## Ce qui change

L'encyclopédie a désormais **deux identités visuelles**, et un sélecteur dans la
barre du haut pour passer de l'une à l'autre.

**Hextech** reste l'identité par défaut : c'est Piltover, la ville du dessus —
bleu nuit, or, cyan arcanique, une lumière qui tombe du ciel.

**Zaun** est la ville du dessous. Noir verdi, vert chimique, violet Shimmer,
laiton corrodé — et la lumière ne tombe plus, elle **remonte du Sump** : le halo
du fond de page part désormais du bas de l'écran. Les cadres changent de
grammaire au passage : là où Hextech taille ses coins en biais comme la facette
d'une gemme, Zaun les découpe à angle droit, comme une plaque de tôle.

Le choix est mémorisé : on retrouve son thème à la visite suivante, sur le même
navigateur, et la couleur de la barre système du téléphone suit.

## Détails

- Sélecteur à deux crans dans la barre du haut, à côté du sélecteur de patch et
  de langue — c'est un réglage d'affichage, il vit avec les autres réglages.
- Aucun clignotement au chargement : la page arrive déjà dans le bon thème.
- Les couleurs qui appartiennent au **jeu** ne changent jamais : les cinq voies
  de runes gardent leurs couleurs officielles, et les libellés de dégâts
  (dégâts bruts, bouclier, vitesse, statut…) gardent celles de Riot. Un thème
  repeint l'interface, pas la donnée.
- Les illustrations, portraits et icônes d'objets ne reçoivent aucune teinte :
  la palette de Zaun est volontairement désaturée pour que l'art reste le point
  de mire.
- Le panneau d'administration reste en Hextech.

## Technique

- **Contrat de tokens.** `foundation/tokens.css` devient la source unique : un
  thème n'est plus qu'un bloc qui réassigne les mêmes noms. Tailwind v4 compile
  `bg-panel` en `background-color: var(--color-panel)` et `bg-gold/25` en
  `color-mix(… var(--color-gold) 25% …)` — les deux sont lus au paint, donc la
  bascule ne recompile rien.
- **Prérequis livré avec la feature** : ~90 littéraux de couleur codés en dur
  (des bleus marine, invisibles tant qu'on ne changeait pas de palette)
  remplacés par `color-mix(in srgb, var(--token) N%, transparent)` — une
  identité exacte en srgb, donc rendu Hextech inchangé. Cinq tokens de surface
  ajoutés (`panel-raised`, `ink`, `sink`, `shadow`, `gold-pale`), plus des glows
  pré-composés (`--hx-glow-accent`, `--hx-shadow-*`, `--hx-ring-accent-2`) parce
  que la grammaire des valeurs arbitraires Tailwind refuse espaces et virgules,
  donc `color-mix()` — mais accepte `var()`.
- **Persistance : cookie `lod_theme`, écrit en JS, lu en PHP.** Délibérément
  hors de `lod_prefs`, qui est `httpOnly` (donc inécrivable par le sélecteur) et
  signé (une signature sur une valeur que le client est censé écrire ne prouve
  rien — elle est validée contre un enum fermé au retour).
- **Pas de FOUC, et pas de script inline.** Le CSP émet `script-src 'self'` sans
  `unsafe-inline` ni nonce : un bootstrap inline aurait marché en dev et été
  refusé en prod. C'est le rendu serveur de `data-theme` sur `<html>` qui fait
  le travail.
- **Turbo Drive** ne remplace jamais `<html>` (son renderer ne synchronise que
  `lang`/`dir`) : l'attribut survit aux visites. Les `<meta>` en revanche sont
  refusionnés à chaque visite, donc `theme-color` suit le serveur tout seul.
- **Service worker** : `VERSION` passé à `lodb-v3`. Le cache indexe les pages par
  URL et Symfony n'émet pas `Vary: Cookie` (l'ajouter invaliderait le cache à
  chaque rotation de session) : les pages mises en cache avant la feature
  seraient rejouées sans `data-theme`. Le module client réaffirme le cookie sur
  `turbo:load`, ce qui ferme aussi le cas multi-onglets.
- **Contrastes vérifiés** sur les trois surfaces de Zaun : texte 15,6:1,
  secondaire 7,8:1, atténué 6,1:1, accent 8,1:1. Deux tokens ne portent jamais
  de texte et c'est documenté dans la feuille : `--color-hex-deep` (Shimmer
  canonique, 4,25:1) et `--color-gold-deep` (vert-de-gris, 2,96:1).
- **Choix assumé** : le vert chimique est à la fois l'accent primaire et la
  couleur de succès (modèle GitHub). L'alternative — Shimmer en primaire —
  plafonne à 4,25:1, inutilisable pour un lien. Primaire et succès se
  distinguent par la forme, jamais par la seule teinte.
- **`.gitattributes`** : `*.css`, `*.ts`, `*.vue`, `*.twig` épinglés en LF. Un
  checkout CRLF transformait les continuations de ligne des data-URI SVG de
  `primitives/controls.css` en échappements invalides, et Tailwind refusait de
  parser la feuille — un clone neuf sous Windows ne buildait pas.
