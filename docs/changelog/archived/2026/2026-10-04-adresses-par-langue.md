---
date: 2026-10-04
type: feat
scope: full-stack
title: La langue du site apparaît désormais dans l'adresse de chaque page
summary: Chaque page existe dans votre langue à sa propre adresse, et vos anciens liens mènent toujours au bon endroit.
tags: [langues, liens, referencement]
---

## Ce qui change

La langue fait maintenant partie de l'adresse : `/fr/champions/Ahri` en français,
`/en/champions/Ahri` en anglais, et ainsi pour les 21 langues du site. Un lien partagé
s'ouvre donc dans la langue de celui qui l'a envoyé, et les moteurs de recherche proposent
la page dans la langue de chacun.

Les anciennes adresses restent valables : favoris, liens partagés et résultats de recherche
redirigent vers la nouvelle adresse de la même page. Les liens de builds partagés (`/b/…`)
ne changent pas.

## Pourquoi

Avec une seule adresse pour toutes les langues, un lien pouvait s'ouvrir dans une autre
langue que celle attendue, et les moteurs de recherche ne voyaient qu'une version de chaque
page.

## Détails

- Le sélecteur de langue change l'adresse de la page affichée.
- À la racine du site, la langue de votre navigateur est proposée d'office.
- Les pages de compte (connexion, inscription, profil) passent sous `/<langue>/account/`.
- Les langues qui s'écrivent de droite à gauche s'affichent dans ce sens.
