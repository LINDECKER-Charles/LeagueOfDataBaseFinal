---
date: 2026-07-17
type: feat
scope: full-stack
title: Page Tendances — les builds publics classés par les votes de la communauté
summary: Une nouvelle page /trends classe tous les builds publics par score net de votes (▲/▼), filtrable par champion et mode de jeu.
tags: [communauté, builds, votes, tendances]
---

## Ce qui change

Une nouvelle page **Tendances** (lien dans le menu et la barre mobile) rassemble
tous les builds publics de la communauté, classés par leur **score net** de
votes. Chaque ligne montre le sceau du champion avec sa rune clé, le nom du
build, son créateur (avec lien vers son profil public), le mode de jeu, le
patch d'origine et un aperçu des premiers objets. Connecté, vous votez ▲ ou ▼
directement depuis la liste ou depuis la page d'un build partagé : revoter la
même flèche retire votre vote, voter l'autre flèche change votre avis. Seul le
score net est affiché — jamais le détail des pour/contre.

## Détails

- Filtres par champion (seuls les champions ayant au moins un build public
  sont proposés) et par mode de jeu, cumulables, avec pagination par 24.
- État vide invitant : « Aucun build public pour ce filtre — forgez le vôtre »
  avec bouton vers la forge.
- Votre propre vote reste surligné (▲ cyan / ▼ rouge) partout, et le score se
  met à jour instantanément au clic.
- Sans JavaScript, les flèches restent de vrais boutons qui fonctionnent ;
  visiteurs non connectés, elles mènent à la connexion.
- Seuls les builds **publics** sont votables et listés : un lien de partage
  privé reste privé, sans compteur.
- Page traduite dans les 21 langues et référencée (titre/description dédiés,
  sitemap).

## Technique

- Entité `BuildVote` (UNIDIRECTIONNELLE vers Build/User — aucun mapping ajouté
  aux entités possédantes), UNIQUE (build_id, voter_id), CASCADE des deux
  côtés ; migration `Version20260717180000` (partagée avec les dons).
- `BuildVoteRepository` : `applyVote` upsert-toggle (même valeur = retrait,
  autre valeur = remplacement in place), `scoreFor` (une requête
  SUM/GROUP BY), `valuesFor` (état « mon vote » d'une page), `topPublicBuilds`
  (LEFT JOIN — les builds à 0 vote apparaissent — tri `score DESC,
  createdAt DESC, id DESC`, filtres champion/mode partagés avec le COUNT).
  Testé contre du vrai SQL via SQLite in-memory
  (`tests/Unit/Support/InMemoryOrm`, `BuildVoteRepositoryTest`, 8 tests).
- `POST /builds/{id}/vote` (`BuildVoteController`, sous l'access_control
  ^/builds existant, CSRF stateless `submit`) : content negotiation simple —
  `Accept: application/json` → `{score, myVote}`, sinon 303 vers le Referer
  UNIQUEMENT s'il pointe sur l'hôte courant (sinon /trends — pas d'open
  redirect). Build inconnu OU privé → 404 (pas d'oracle).
- `GET /trends` (`TrendsController` + `TrendsViewAssembler`) : SSR complet,
  résolution champion/keystone via `BuildViewAssembler::listRow` et extrait
  d'objets (max 6) en UNE résolution catalogue par page, dégradation ghost
  best-effort. Options champion dérivées de
  `BuildRepository::distinctPublicChampionIds()`.
- Îlot Vue `vote-score` (registry + `VoteScore.vue` + module pur
  `community/voteState.ts`) : update optimiste + rollback si non-2xx ou
  payload malformé, `aria-pressed`, fallback no-JS = mini-forms POST natifs
  dans le div de l'îlot. 13 specs vitest (module + composant).
- Styles `.trend-*` / `.vote-*` dans `assets/styles/community.css` (nouveau
  module importé par app.css) ; i18n `community.*` ×21 (messages) et
  `trends.*` ×21 (seo) ; entrée sitemap ; liens nav header + bottom nav.
