---
date: 2026-08-24
type: feat
scope: full-stack
title: Deux identites de plus — Noxus et Spirit Blossom
summary: Quatre villes de Runeterra habillent desormais le site, et un selecteur les met cote a cote pour choisir.
tags: [theme, design-system, typographie, accessibilite]
---

## Ce qui change

Le site compte maintenant **quatre identités visuelles**, et le sélecteur du
haut de page est devenu une **fenêtre de choix** : chaque option y montre sa
crête, son nom, la ville dont elle vient et un échantillon de sa palette, posés
sur son propre fond. On choisit sur ce que le thème *a l'air d'être*, pas sur un
mot.

**Noxus** est une plaque d'acier martelé sous un ciel gris. Les coins ne sont ni
taillés ni arrondis : ils restent vifs, et c'est un **bandeau cramoisi le long
du bord gauche** — la hampe d'une bannière — qui marque chaque panneau. Laiton
antique pour les filets, os pour le texte, pointes de lame partout où les autres
thèmes posent un losange.

**Spirit Blossom** ne dessine aucun cadre. Un panneau y est une **feuille de
papier posée sur le noir** : pas de bordure, un arc en haut comme une tablette
votive, et une arête lumineuse là où la lumière des esprits l'accroche. Indigo
spectral, cyan de feu follet, violet akana, un ruban qui va du cyan au sakura.

## Détails

- Le sélecteur s'ouvre au clic sur la crête, se ferme à l'Échap, au clic hors du
  cadre ou sur la croix. Le thème s'applique immédiatement et se mémorise.
- Chaque identité amène sa police d'affichage, et le navigateur ne télécharge
  que celle du thème actif — aucune n'est chargée sous Hextech.
- Les couleurs qui appartiennent au **jeu** ne bougent toujours pas : voies de
  runes, types de dégâts, raretés.
- Le panneau d'administration reste en Hextech.

## Technique

### Ce qui sépare vraiment les quatre

Le principe de conception : les quatre répondent différemment à **une seule
question — de quoi est fait le bord d'un panneau ?**

| Identité | Bord | Face d'affichage | Trame |
|---|---|---|---|
| Hextech | coin taillé en facette de gemme | Beaufort | hexagones |
| Zaun | deux coins pliés en ferronnerie | Grenze | grille en ogive |
| Noxus | coins vifs, **bord** estampé | Archivo | chevrons |
| Spirit Blossom | **aucun bord** — une feuille éclairée | Shippori Mincho | seigaiha |

### Noxus — le canon contre l'intuition

**Noxus n'est pas rouge.** Le film de faction et le splash de région ne
contiennent quasiment aucun rouge : ce sont des olives, kakis et ardoises
désaturés. Le cramoisi est ce que l'empire *appose* — bannière, drapé, sceau.
Un thème qui en inonde l'écran trahit le canon en croyant le servir, et atterrit
dans le template « gaming » noir-et-rouge-sang. Ici le cramoisi ne devient jamais
une surface : c'est une marque, sur quelques pourcents de l'écran.

L'or noxien canonique (`#c8a96a`) est à **un point** de l'or Hextech
(`#c8aa6e`) : le garder aurait rendu les deux identités semblables là où ça
compte le plus, sur chaque filet et chaque lien. Il est poussé vers le brun.

Un romain épigraphique (Cinzel, Marcellus) a été essayé et écarté : des
capitales gravées sur noir avec du cramoisi et de l'or, c'est exactement le
costume heroic-fantasy à éviter, et ça aurait mis Noxus dans la même famille de
serifs que les trois autres. Archivo dit *empire* sans dire *dragon*.

### Spirit Blossom — deux mesures qui décident

Quantifié sur 32 splash arts officiels, l'univers n'est pas violet mais
**indigo** : la couleur la plus fréquente est `#1E1E36` (1,89 % de tous les
pixels). Et **57 % des pixels saturés tiennent dans H225-270** — laissée seule,
cette monochromie fait fondre lien, actif, sélection et erreur dans la même
brume. La séparation passe donc par la **luminosité**, pas par la teinte.

L'univers ne fournit **aucun rouge** : sa bande chaude plafonne à des tons
rompus sous 3,7:1, inutilisables en signal. Le rose spectral d'erreur est
dérivé, pas canonique — c'est le seul endroit du fichier où la lisibilité l'a
emporté sur le canon, et c'est écrit dans la feuille.

Le piège kitsch de cet univers est la **police pinceau**. Riot ne l'utilise
jamais : ses supports restent sur les faces système et laissent les *motifs*
porter la culture. Un mincho japonais respecte exactement cette règle — le
pinceau est dans l'histoire de la lettre, pas dans un costume posé dessus.

### Le sélecteur

- `<dialog>` natif : `showModal()` apporte le piège de focus, l'Échap,
  l'inertie de la page derrière et le `::backdrop`. Rien n'est réimplémenté.
- Un clic dont la cible **est** l'élément `<dialog>` est un clic sur le fond :
  tout le contenu vit dans des enfants.
- Les pastilles de palette lisent des variables `--sw-*` que chaque thème
  déclare **hors de son propre sélecteur**, sinon un échantillon ne s'afficherait
  qu'une fois son thème déjà actif. Une source unique par thème, colocalisée.
- Le nom du thème a été essayé à côté de la crête puis retiré : « Spirit
  Blossom » composé dans un mincho large poussait le logotype hors de la barre.
  Sur quatre identités et 21 locales, c'est un risque de mise en page permanent
  pour un libellé que l'infobulle porte déjà.

### Structure

`assets/styles/theme/` passe en un dossier par identité (tokens, type, frames,
ornament) : quatre fichiers par thème à plat auraient fait éclater la limite de
dix fichiers par dossier. Les polices de thème passent en
`public/fonts/<famille>/`, licences OFL jointes, pour la même raison.

`base.html.twig` précharge la sous-police latine du **thème actif** seulement —
sans quoi chaque titre clignote en Beaufort avant de basculer.
