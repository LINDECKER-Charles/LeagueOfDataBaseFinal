---
date: 2026-07-19
type: fix
scope: front
title: Affichage mobile net — plus de dézoom ni d'en-tête tronqué sur tablette
summary: Formulaires, page de dons, développeurs, accueil et fiches champion tiennent pile dans la largeur de l'écran, sur tous les téléphones et sur iPad.
tags: [mobile, responsive, forms, donate, header]
---

## Ce qui change

- Sur téléphone, plus aucune page ne s'affiche « dézoomée » : la page de dons, la
  page développeurs, l'accueil et les fiches de champion tiennent désormais pile
  dans la largeur de l'écran, y compris sur les petits mobiles (320–360 px).
- Au focus dans un champ de formulaire (connexion, inscription, contact, dons, filtres…),
  Safari iOS ne zoome plus automatiquement sur la page.
- Sur tablette en portrait (iPad, 768 px), la barre du haut n'est plus tronquée :
  le sélecteur de version/langue reste entièrement visible.

## Pourquoi

Plusieurs éléments forçaient une largeur supérieure à celle de l'écran — la page
apparaissait alors rétrécie et déplaçable latéralement : le titre + la mention
« paiement sécurisé » de la page de dons, le grand titre de la page développeurs,
la rangée d'onglets de sorts d'une fiche champion, et l'en-tête complet à partir de
768 px. En parallèle, les champs de saisie rendus à 14 px déclenchaient le zoom
automatique d'iOS au focus.

## Technique

Audit responsive Playwright (7 sous-agents, 8 formats de 320 à 844 px) complété par
une **sonde de largeur réelle vs device** : la métrique classique `scrollWidth − innerWidth`
est aveugle à l'expansion du *layout viewport* sous émulation `isMobile` (le navigateur
élargit `innerWidth` au lieu de scroller), ce qui masquait ces dézooms. 5 correctifs,
desktop strictement préservé, chacun re-vérifié à la sonde :

- `assets/styles/primitives.css` — `.hx-input`/`.hx-select` → `font-size: 1rem` sous
  640 px (anti-zoom iOS ; source unique qui couvre aussi les selects du switcher header
  et les champs du dialog contact) ; `.codex-header` `flex-wrap` + `__meta`
  `flex-basis: 100%` sous 480 px (la page de dons ne force plus ~450 px de large).
- `templates/partials/header.html.twig` — labels donate/compte `md:inline` → `lg:inline`
  (densification : le cluster header tient à 768 px, la tablette conserve la nav du haut).
- `templates/developers/index.html.twig` — h1 `text-2xl min-[400px]:text-3xl sm:text-4xl`
  (le token insécable « LeagueOfDataBase » ne force plus la largeur).
- `templates/home/_preview_header.html.twig` — `flex-wrap` sur la ligne d'en-tête de
  section (le lien « voir tout » passe sous le titre en dessous de ~360 px).
- `assets/styles/showcase.css` — `.kit__rail`/`.kit__tab` réduits sous 374 px (la rangée
  de sorts P/Q/W/E/R ~370 px tenait mal sur les plus petits écrans).

Note : le support **RTL** (arabe) reste une limitation connue non traitée ici — `dir`/`lang`
restent statiques et la structure n'est pas mise en miroir (aucun débordement néanmoins).
