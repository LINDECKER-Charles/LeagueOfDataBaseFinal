---
date: 2026-08-24
type: feat
scope: full-stack
title: Un second theme — Zaun, la Cite de Fer et de Verre
summary: Un bouton dans la barre du haut fait basculer tout le site de Piltover a Zaun, et s'en souvient.
tags: [theme, design-system, typographie, accessibilite, pwa]
---

## Ce qui change

L'encyclopédie a désormais **deux identités visuelles**, et un sélecteur dans la
barre du haut pour passer de l'une à l'autre. Ce n'est pas une variante de
couleur : les deux villes ne sont pas dessinées de la même main.

**Hextech** reste l'identité par défaut. C'est Piltover, la ville du dessus :
bleu nuit, or, cyan arcanique, une trame d'hexagones, des cadres taillés en
biseau comme une facette de gemme, des capitales largement espacées — le
vocabulaire Art déco d'une ville qui se dessine au compas.

**Zaun** est la ville du dessous, et elle est courbe là où Piltover est
géométrique. Noir verdi, vert chimique, violet Shimmer, laiton corrodé. La
lumière ne tombe plus du ciel, elle **remonte du Sump**. La trame d'hexagones
cède la place à une **grille de ferronnerie** — les arcs en ogive et la courbe
en coup de fouet de l'Art nouveau, qui est ce dont ses bâtiments sont faits. Les
cadres ne sont plus biseautés : ils sont **pliés** à deux coins et **boulonnés**
aux deux autres, avec un reflet de verre teinté en travers. Les losanges
deviennent des **rivets** et des **volants de vanne**, les filets deviennent des
**conduites** avec leur bride de raccord. Et le titrage change de police.

Le choix est mémorisé : on retrouve son thème à la visite suivante, sur le même
navigateur, et la couleur de la barre système du téléphone suit.

## Détails

- Sélecteur à deux crans dans la barre du haut, à côté du sélecteur de patch et
  de langue — c'est un réglage d'affichage, il vit avec les autres réglages. Sur
  téléphone il se replie dans ce même panneau, faute de place dans la barre.
- Aucun clignotement au chargement : la page arrive déjà dans le bon thème.
- Les couleurs qui appartiennent au **jeu** ne changent jamais : les cinq voies
  de runes gardent leurs couleurs officielles, et les libellés de dégâts (dégâts
  bruts, bouclier, vitesse, statut…) gardent celles de Riot. Un thème repeint
  l'interface, pas la donnée.
- Les illustrations, portraits et icônes d'objets ne reçoivent aucune teinte :
  la palette de Zaun est volontairement désaturée pour que l'art reste le point
  de mire.
- Le panneau d'administration reste en Hextech.

## Technique

### Le parti pris de forme

Le canon est contre-intuitif et c'est lui qui décide. COLLINS, qui a construit
l'identité d'Arcane, décrit Piltover par « des symétries dorées et des
géométries complexes » (Art déco, Streamline Moderne) et Zaun par « les qualités
fluides de l'Art nouveau et les courbes en coup de fouet […] sinueux et
curvilignes, avec une touche sinistre d'acuité et d'irisation ». **C'est Zaun la
ville courbe**, pas l'inverse. Un premier passage avait fait le contraire
(biseau en escalier orthogonal) : corrigé.

### Ce qui bascule

- **Type.** `--font-beaufort` passe à **Grenze** sous Zaun, avec Beaufort en
  repli. Face variable (200-900, 3 fichiers, 75 Ko), condensée, à empattements
  ciselés, à la frontière du romain et de la gothique. `unicode-range` ne couvre
  que le latin : sur les 19 autres locales le navigateur ne télécharge même pas
  Grenze et Beaufort compose, glyphe par glyphe. Auto-hébergée — le CSP émet
  `font-src 'self'`. OFL 1.1, licence dans `public/fonts/Grenze-OFL.txt`.
- **Interlettrage.** Les capitales largement espacées sont *la* signature Art
  déco, et le thème par défaut en met partout (0,15em sur la nav, 0,18em sur le
  logotype et les puces de section, 0,14em dans les menus). Sans ce reset, la
  voix de Piltover continuait de parler sous le visage de Zaun.
- **Registre des libellés.** Les eyebrows et titres de section quittent la face
  d'affichage pour la **mono** : Grenze est superbe à 3rem et illisible à
  0,7rem, et Zaun étiquette comme un atelier — au tampon, pas à la gravure.
  Bénéfice collatéral : ces libellés restent sur une face qui couvre les 21
  locales.
- **Forme.** `--hx-bevel` passe à `none` et les six surfaces qui le consommaient
  reçoivent `--hx-iron-radius` (22px 3px 22px 3px). Le `clip-path` est libéré
  parce qu'un clip ne peut pas rendre une **bordure** courbe, et c'est la ligne
  de fer autour de la plaque qui fait tout le travail.
- **Trame.** Impossible à thémer en CSS seul : les deux identités demandent une
  *géométrie* différente, pas une couleur différente. Les deux `<pattern>` sont
  rendus dans `partials/main/gradient` et le thème masque le `<rect>` inutile.
  Un motif dont le rect est masqué n'est jamais rastérisé.
- **`hx-corners`** cesse de décorer et se met à expliquer : la plaque est pliée
  à deux coins, donc les deux autres — restés vifs — portent un rivet. Une
  volute en coup de fouet avait été dessinée là ; supprimée après l'avoir
  regardée, elle lisait comme un gribouillis à 46 px. Le cadre et la grille
  portent déjà l'Art nouveau sans que personne ait à tracer une boucle.
- **`hx-pip`** : le `◆` décoratif des eyebrows est un losange, donc une gemme,
  donc l'argument de Piltover. La classe ne fait rien par défaut et, sous Zaun,
  réduit le caractère à zéro pour poser un rivet. Les pages de build gardent
  leur `◆` : là ce n'est pas un séparateur, c'est le symbole de l'or.

### Persistance et plomberie

- **Cookie `lod_theme`**, écrit en JS, lu en PHP, validé contre un enum fermé.
  Délibérément hors de `lod_prefs`, qui est `httpOnly` donc inécrivable par le
  sélecteur.
- **Pas de FOUC, et pas de script inline.** Le CSP émet `script-src 'self'` sans
  `unsafe-inline` ni nonce : un bootstrap inline aurait marché en dev et été
  refusé en prod. C'est le rendu serveur de `data-theme` sur `<html>` qui fait
  le travail.
- **Turbo Drive** ne remplace jamais `<html>` : l'attribut survit aux visites.
  Les `<meta>` sont refusionnés, donc `theme-color` suit le serveur tout seul.
- **Service worker** en `lodb-v3` : le cache indexe par URL et Symfony n'émet
  pas `Vary: Cookie`. Le module client réaffirme le cookie sur `turbo:load`, ce
  qui ferme aussi le cas multi-onglets.

### Accessibilité

- Contrastes vérifiés sur les trois surfaces de Zaun : texte 15,6:1, secondaire
  7,8:1, atténué 6,1:1, accent 8,1:1. Deux tokens ne portent jamais de texte et
  c'est documenté dans la feuille : `--color-hex-deep` (4,25:1) et
  `--color-gold-deep` (2,96:1).
- Les surcharges d'animation du thème sont **encadrées par
  `prefers-reduced-motion: no-preference`**. Sans ça elles gagnaient en
  spécificité contre les règles `reduce` du socle et réarmaient silencieusement
  les animations pour ceux qui les avaient coupées.

### Prérequis livré avec la feature

~90 littéraux de couleur codés en dur (des bleus marine, invisibles tant qu'on
ne changeait pas de palette) remplacés par
`color-mix(in srgb, var(--token) N%, transparent)` — une identité exacte en
srgb, donc rendu Hextech inchangé. Cinq tokens de surface ajoutés, plus des
glows pré-composés parce que la grammaire des valeurs arbitraires Tailwind
refuse espaces et virgules, donc `color-mix()` — mais accepte `var()`.

`.gitattributes` épingle `*.css`, `*.ts`, `*.vue`, `*.twig` en LF : un checkout
CRLF transformait les continuations de ligne des data-URI SVG de
`primitives/controls.css` en échappements invalides, et Tailwind refusait de
parser la feuille — un clone neuf sous Windows ne buildait pas.
