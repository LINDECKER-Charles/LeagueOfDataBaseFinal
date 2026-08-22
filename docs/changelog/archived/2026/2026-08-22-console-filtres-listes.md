---
date: 2026-08-22
type: feat
scope: full-stack
title: Console de filtres latérale et comptage par option sur les listes
summary: Les quatre listes (champions, objets, runes, sorts) gagnent un rail de filtres épinglé avec compteurs par option, jauge de résultats et feuille mobile repensée.
tags: [codex, filtres, listes, mobile, ux]
---

## Ce qui change

Sur les listes de champions, d'objets, de runes et de sorts d'invocateur, les filtres quittent la
barre au-dessus de la grille pour une **console latérale** qui reste visible pendant le défilement
(à droite de la grille sur grand écran). Elle regroupe la recherche, une jauge « n / total » et
les catégories de filtres sous forme de sections repliables : les axes principaux (rôle,
catégorie, voie, mode de jeu…) sont ouverts d'emblée, les statistiques se déplient à la demande.

Chaque option affiche désormais **combien de résultats** elle donnerait compte tenu des autres
filtres déjà posés ; une option qui ne laisserait aucun résultat reste visible mais grisée. Les
filtres engagés sont rappelés au-dessus des résultats sous forme de puces retirables, et une
liste vide propose directement d'effacer les filtres.

Sur mobile, une barre de recherche épinglée en haut de page ouvre une feuille de filtres dont le
bouton principal indique « Voir N résultats » en direct.

## Détails

- Console épinglée (desktop ≥ 1024 px) : en-tête avec badge du nombre de filtres et « Effacer
  tout », recherche accessible au clavier par la touche `/`, jauge de résultats, sections
  repliables avec marqueur et badge par section, bouton « Copier le lien » en pied.
- Puces comptées et désactivées quand le compte tombe à zéro ; bascule « Cumul : au moins un /
  tous » quand plusieurs catégories d'objets sont cochées.
- Plages numériques : valeur courante en surbrillance et croix pour relâcher la plage.
- Filtres booléens (achetable, consommable) rendus par un interrupteur avec leur compte.
- Tête de résultats : nombre de résultats, page courante, tailles de page en segments joints,
  pagination.
- Mobile : barre épinglée (recherche + bouton « Filtres » avec badge) et feuille basse avec
  poignée, jauge, sections et bouton « Voir N résultats ».

## Technique

- Layout Twig : `components/codex/list_filter.html.twig` devient un `{% embed %}` à bloc `grid`
  (grille CSS à zones nommées, côté du rail via `data-rail`) ; l'îlot `resource-filter` monte dans
  le slot du rail et téléporte barre mobile, tête de résultats et état vide dans les slots
  `#<grid>-bar` / `#<grid>-head` / `#<grid>-empty`, et la feuille `<dialog>` dans `body` (le slot du
  rail est `display:none` sous le point de rupture et priverait le top layer de boîte).
- Comptage contextuel pur dans `assets/vue/filter/facetCounts.ts` (convention de la recherche à
  facettes : chaque valeur est comptée sous tous les autres axes engagés ; en mode « tous », la
  sélection courante est conservée puisqu'elle ne peut que restreindre).
- Le store de facettes est fourni par `provide/inject` (`useFacetState`) aux contrôles, au lieu
  d'une chaîne d'emits sur trois niveaux.
- CSS : `codex/filter.css` (vocabulaire des contrôles) + `codex/filter-layout.css` (rail, barre
  mobile, tête, état vide, feuille — déplacée depuis `foundation/nav.css`).
- i18n : nouvelles clés `filter.page`, `gauge`, `empty_title`, `empty_cta`, `clear_all`,
  `results_one`, `show_results`, `show_results_one`, `match_mode` (fr + en ; les autres locales
  retombent sur en). Singulier/pluriel choisi côté client (`pluralTemplate`), pas d'ICU.
- Corrections issues de la revue : le slot de la barre mobile est lui-même `position: sticky`
  (un enfant sticky ne voyage pas au-delà de son parent) ; le gestionnaire global de clic du
  dialogue de contact (`fx/contactDialog.ts`) ne ferme plus **tous** les `<dialog>` mais
  uniquement le sien — il fermait la feuille de filtres au moindre tap dans sa marge ; un
  îlot dont le chunk est encore en vol est libéré au `turbo:before-cache` (`main.ts`), sinon
  la page restaurée ne montait plus jamais le filtre ; anneaux de focus en `inset` (les
  `clip-path` avalaient l'outline) ; champs min/max validés au `change` seulement.
