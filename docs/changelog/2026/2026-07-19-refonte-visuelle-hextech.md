---
date: 2026-07-19
type: feat
scope: front
title: Passe de fidélité visuelle Hextech (accueil, listes, tendances, builds, API, don)
summary: L'accueil et plusieurs pieces maîtresses du site gagnent des portraits de champions, des cartes plus lisibles et des boutons d'action en or plein, sans rien changer à leur contenu.
tags: [ui, hextech, design, accueil, listes]
---

## Ce qui change

L'encyclopédie s'aligne plus finement sur la charte « Hextech ». Les changements
les plus visibles :

- **Accueil** : un bandeau de patch enrichi, des compteurs par catégorie, des
  portails plus parlants, et surtout des **champions en portrait** (splash) au
  lieu de simples vignettes carrées.
- **Liste des champions** : mêmes portraits en grille, titres en dégradé d'or,
  compteur mis en valeur dans un cartouche.
- **Runes** : l'icône de chaque arbre devient un médaillon rond ; le filtre
  redondant (un onglet par arbre) a été retiré — la recherche suffit.
- **Sorts d'invocateur** : le temps de recharge ressort désormais en cyan
  « hextech ».
- **Tendances / Builds / API / Don** : les boutons d'action principaux passent en
  **or plein** (plus francs), la barre de filtres des tendances est encadrée, et
  le sélecteur « build public » devient un vrai interrupteur.

Aucune donnée, aucun parcours ni aucune fonctionnalité ne change : c'est une
mise à niveau purement visuelle.

## Détails

- Les pages de détail (objet, rune, sort) suivaient déjà la charte : elles sont
  laissées telles quelles.
- Les portraits de champions sont servis directement depuis le CDN Data Dragon
  (comme la fiche champion), pas ingérés.

## Technique

- Traduction des maquettes du dossier `design/` (format « Design Canvas ») vers
  les templates Twig réels **en idiome du design system** (tokens `--color-*`,
  primitives, utilitaires Tailwind) — aucun hex ni police en dur. Constat : les
  templates implémentaient déjà fidèlement les maquettes (souvent en plus riche),
  d'où une **passe de fidélité ciblée** plutôt qu'une réécriture.
- Nouvelles primitives : `hx-btn-gold` (CTA or plein), `hx-switch` (toggle CSS pur
  sur checkbox native, soumission no-JS intacte), `hx-chip-hex` (chip cyan :
  verbe HTTP, plan actif, badge « actuel »). Nouveau module `list.css`
  (`list-count`, `hatch-gold`). Jauge de quota API passée en cyan.
- Contrats préservés : clés i18n, variables, îlots Vue (`build-editor`,
  `vote-score`, `copy-link`, `resource-filter`), fallback no-JS, blocs SEO,
  `data-search`/`data-tags` du filtre client. `entity_card` étendu par un
  paramètre optionnel `img_round` (rétro-compatible).
- Garde-fous : `lint:twig` (85 fichiers OK), `phpunit tests/Unit` (427 verts),
  `vue-tsc --noEmit` OK, `vite build` OK, rendu HTTP 200 vérifié sur accueil,
  listes, tendances, développeurs et don.
