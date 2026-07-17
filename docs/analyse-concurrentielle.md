# Analyse concurrentielle — écosystème data League of Legends

> **Date de la recherche** : 17 juillet 2026 · **Patch de référence** : 26.14 (nom public) / 16.14 (Data Dragon)
> **Objet** : cartographier tous les acteurs qui exploitent les données League of Legends, comparer leur offre à celle de LeagueOfDataBase, et identifier les créneaux inoccupés.
> **Statut** : document de veille interne. Pas de changelog associé (aucun impact joueur — cf. `CLAUDE.md`).

---

## Sommaire

- [0. Résumé exécutif](#0-résumé-exécutif)
- [1. Méthodologie et conventions](#1-méthodologie-et-conventions)
- [2. Point de référence — ce que fait LeagueOfDataBase](#2-point-de-référence--ce-que-fait-leagueofdatabase)
- [3. Cartographie de l'écosystème — 6 familles](#3-cartographie-de-lécosystème--6-familles)
- [4. Matrice de comparaison globale](#4-matrice-de-comparaison-globale)
- [5. Famille A — Généralistes stats / tracking](#5-famille-a--généralistes-stats--tracking)
- [6. Famille B — Infrastructure de données](#6-famille-b--infrastructure-de-données)
- [7. Famille C — Wikis et encyclopédies](#7-famille-c--wikis-et-encyclopédies)
- [8. Famille D — Explorateurs et viewers spécialisés](#8-famille-d--explorateurs-et-viewers-spécialisés)
- [9. Famille E — Outils de niche](#9-famille-e--outils-de-niche)
- [10. Famille F — Esport (marché verrouillé)](#10-famille-f--esport-marché-verrouillé)
- [11. Consolidation capitalistique du marché](#11-consolidation-capitalistique-du-marché)
- [12. Contraintes Riot — ce qui borne le terrain de jeu](#12-contraintes-riot--ce-qui-borne-le-terrain-de-jeu)
- [13. Positionnement de LeagueOfDataBase](#13-positionnement-de-leagueofdatabase)
- [14. Opportunités — où on a une carte à jouer](#14-opportunités--où-on-a-une-carte-à-jouer)
- [15. Analyse finale — ce que personne ne fait](#15-analyse-finale--ce-que-personne-ne-fait)
- [16. Limites de cette recherche](#16-limites-de-cette-recherche)

---

## 0. Résumé exécutif

**LeagueOfDataBase n'a pas de concurrent direct.** Ce n'est pas une figure de style : sur ~60 acteurs recensés, aucun ne combine les trois axes qui définissent le projet — **UI moderne dédiée + navigation multi-patch + couverture multi-langue étendue**, sur les données statiques (champions / objets / runes / sorts).

Les cinq constats les plus structurants :

1. **L'écosystème entier vit dans le présent.** Aucune des ~15 grandes plateformes stats n'offre de sélecteur d'archive de patch fonctionnel. Le pattern universel est « patch courant + delta vs précédent ». Data Dragon conserve pourtant **494 versions** exploitables (testé jusqu'à `5.5.1`, mars 2015 — soit ~11 ans de rétention). Personne ne l'expose.
2. **Personne ne sert de *snapshot*, tout le monde sert des *deltas*.** Les deux meilleures sources historiques (wiki officiel `/Patch_history`, `riftpatchnotes.com` — 405 patchs) présentent des listes de changements chronologiques à cumuler mentalement. **Reconstituer « Ezreal exactement au patch 9.1 » est impossible en un clic, partout.** L'architecture `data/{version}/{lang}/{type}.json` du projet rend cela trivial.
3. **Le multilingue est mal servi là où il compte.** Le wiki officiel (la meilleure source de données statiques) est **anglais uniquement**. Les 16 locales historiques sont restées sur Fandom, dont **14 sont marquées « OUT OF DATE » ou pire par la source officielle elle-même** — le wiki FR affiche encore un contenu figé début 2025 (170 champions vs 173 aujourd'hui).
4. **Aucune API publique n'existe dans tout le secteur.** Zéro. Lolalytics le reconnaît explicitement (« *we do hope in the future to release a public API* »), Mobalytics et DPM.LOL interdisent le scraping par ToS, Tracker.gg a une API mais **exclut LoL**. Data Dragon reste la seule source ouverte — et elle est **explicitement exemptée des rate limits Riot**.
5. **Le marché est bien moins fragmenté qu'il n'y paraît, et deux de ses quatre groupes de contrôle sont en détresse financière documentée** (Enthusiast Gaming → U.GG : action à 0,045 CAD ; M.O.B.A. Network → Porofessor/LeagueOfGraphs/Mobafire : **cotation suspendue depuis janvier 2026**).

**La carte à jouer la plus forte** : être le seul endroit au monde où l'on peut voir un champion tel qu'il était, dans sa langue, à n'importe quel patch depuis 2013 — et le comparer visuellement à aujourd'hui. Détail en [§15](#15-analyse-finale--ce-que-personne-ne-fait).

---

## 1. Méthodologie et conventions

Recherche menée par quatre agents de recherche parallèles (stats/tracking, wikis/bases statiques, écosystème développeur, périphérie), chacun fanant en sous-agents, avec passe de vérification adversariale sur les affirmations à fort enjeu. Volume : plusieurs centaines de requêtes, 150+ pages fetchées directement.

**Conventions de fiabilité** — reprises telles quelles dans tout le document :

| Marqueur | Signification |
|---|---|
| **VÉRIFIÉ** | Source primaire ouverte et lue directement (citation exacte quand pertinent) |
| **INFÉRENCE** | Déduction de snippets de recherche ou de sources secondaires convergentes, sans lecture directe |
| **NON TROUVÉ** | Recherché activement, sans résultat fiable — **jamais comblé par une estimation** |

**Réserves à garder en tête** :

- Les chiffres de trafic (SimilarWeb / SEMrush / HypeStat) divergent d'un facteur 2 à 6× selon la source pour un même site. Traités partout comme **ordres de grandeur**, jamais comme des valeurs auditées.
- Reddit s'est révélé inaccessible en fetch direct — le sentiment communautaire provient de sources qui *rapportent* des discussions, pas de threads lus.
- Aucun bilan comptable n'a été consulté ; les montants de rachat viennent de presse spécialisée (Forbes, TechCrunch, PC Gamer) ou de communiqués réglementaires.

### ⚠️ Découverte transverse à intégrer au produit — la double numérotation de patch

**VÉRIFIÉ, et directement actionnable.** Riot utilise en 2026 **deux numérotations parallèles** :

| Numérotation | Valeur (juillet 2026) | Où elle apparaît |
|---|---|---|
| **Publique / marketing** | `26.14` | Patch notes officiels, op.gg, u.gg, Blitz, Mobalytics — ce que le joueur reconnaît |
| **Séquentielle historique** | `16.14` | `versions.json`, Data Dragon, CommunityDragon, wiki interne |

Le préfixe public est passé à `année.patch` (26 = 2026) tandis que Data Dragon poursuit la suite historique (13.x = 2023, 14.x = 2024, 15.x = 2025, 16.x = 2026). **Les deux désignent le même patch.**

> **Impact pour le projet** : LeagueOfDataBase consomme `versions.json` nativement et affiche donc `16.14` là où le joueur cherche `26.14`. Un utilisateur peut légitimement croire le site en retard de 10 versions. **Recommandation : afficher le nom public comme libellé principal, la version DDragon en secondaire** (`26.14` · *ddragon 16.14*). Corollaire bénéfique : un site affichant « 16.13 » (Lolalytics) n'est pas en retard sur un site affichant « 26.14 » — c'est la même donnée.
> Source croisée : [escorenews.com — « League of Legends patch notes 26.14 (16.14) »](https://escorenews.com/en/lol/news/79334-league-of-legends-patch-notes-26-14-16-14-full-preview-changes-to-jayce-senna-seraphine-corki).

---

## 2. Point de référence — ce que fait LeagueOfDataBase

Rappel synthétique pour ancrer les comparaisons (source : `README.md`, `CLAUDE.md`, `docs/architecture.md`).

| Axe | État |
|---|---|
| **Périmètre données** | Champions, objets, runes reforgées, sorts d'invocateur — + skins et **chromas** (CommunityDragon) |
| **Multi-version** | ✅ Natif — toute version de `versions.json`, stockage `data/{version}/{lang}/{type}.json` |
| **Multi-langue** | ✅ 21 locales UI, catalogues `messages.<loc>.yaml` ; locale UI = langue Data Dragon |
| **Source** | Data Dragon (vérité) + CommunityDragon (chromas), egress **exclusivement** via passerelle Go (allowlist SSRF) |
| **Stockage** | MinIO S3 content-addressed (`blobs/{sha256}.{ext}`), dédup O(1), sibling WebP, manifeste read-merge-write |
| **Base de données** | ❌ Aucune — cache intelligent multi-niveaux devant le CDN |
| **Assets lourds** | Splash / skins **hotlinkés** depuis le CDN DDragon (choix TTFB assumé) |
| **Front** | Twig + îlots Vue 3 / TS / Vite, Turbo Drive, design system « Hextech », loader SSE déterminé |
| **Modèle** | Non commercial, CC BY-NC 4.0, **zéro publicité** |
| **Ce qu'il ne fait PAS** | Pas de profil invocateur, pas de match history, pas de winrate/tier list, pas d'overlay, pas de TFT, pas d'esport, pas de PBE |

**Lecture stratégique** : le projet est une **encyclopédie de données statiques**, pas un tracker. C'est ce qui le sort du bain de sang concurrentiel du §5 — et ce qui le place dans un créneau où la concurrence est étonnamment faible.

---

## 3. Cartographie de l'écosystème — 6 familles

L'écosystème ne se répartit pas en « concurrents » mais en six familles aux modèles disjoints. LeagueOfDataBase n'appartient qu'à une seule d'entre elles — et c'est la moins peuplée.

```
                    ┌─────────────────────────────────────┐
                    │  A · GÉNÉRALISTES STATS / TRACKING   │  ← 90% du trafic
                    │  op.gg, u.gg, Mobalytics, Blitz…     │     0% du scope projet
                    │  Données DYNAMIQUES (joueur, méta)   │
                    └─────────────────────────────────────┘
                                      │ consomment
                                      ▼
┌───────────────────────┐   ┌──────────────────────┐   ┌────────────────────────┐
│ B · INFRASTRUCTURE     │   │ C · WIKIS            │   │ D · VIEWERS SPÉCIALISÉS │
│ Data Dragon (officiel) │   │ wiki.lol (officiel)  │   │ skinexplorer, modelviewer│
│ CommunityDragon        │   │ Fandom (obsolète)    │   │ lolskin.info, loldb.info │
│ Meraki Analytics       │   │ Leaguepedia (esport) │   │ heimerdinger.lol        │
│ ▸ Aucune UX            │   │ ▸ EN seulement       │   │ ▸ mono-sujet, mono-langue│
└───────────────────────┘   └──────────────────────┘   └────────────────────────┘
        ▲ source du projet          ▲ voisin le plus proche      ▲ concurrents partiels

┌───────────────────────┐   ┌──────────────────────────────────────────────────┐
│ E · OUTILS DE NICHE    │   │ F · ESPORT                                        │
│ patchdelta.gg          │   │ GRID (exclusif Riot), Leaguepedia, gol.gg        │
│ calculateurs de dégâts │   │ Oracle's Elixir, lolesports                      │
│ simulateurs de draft   │   │ ▸ MARCHÉ VERROUILLÉ B2B — à ne pas adresser      │
└───────────────────────┘   └──────────────────────────────────────────────────┘
        ▲ recoupements ponctuels
```

**Le point clé de cette carte** : entre la famille **B** (données brutes, zéro UX) et la famille **C** (wiki anglais uniquement), il y a un **trou**. C'est exactement là que se trouve LeagueOfDataBase — et il y est quasiment seul.

---

## 4. Matrice de comparaison globale

Notation : ✅ complet · 🟡 partiel · ❌ absent · ❓ non vérifié

| Acteur | Données statiques | UI dédiée | **Multi-patch** | **Multi-langue** | Chromas | Sans pub | API publique | Modèle |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|---|
| **LeagueOfDataBase** | ✅ | ✅ | **✅ ~494 versions** | **✅ 21 locales** | ✅ | ✅ | ❌ *(cf. §14)* | CC BY-NC |
| Data Dragon | ✅ | ❌ *(CDN brut)* | ✅ natif | ✅ 28 locales | ❌ *(booléen)* | ✅ | ✅ *(fichiers)* | Officiel Riot |
| CommunityDragon | ✅ | ❌ *(CDN brut)* | ✅ (7.1 → 16.14) | 🟡 par fichier | ✅ | ✅ | ✅ *(fichiers)* | Bénévole/Patreon |
| Meraki Analytics | 🟡 *(champ+items)* | ❌ | ❓ | 🟡 | ❌ | ✅ | ✅ *(fichiers)* | Bénévole |
| wiki.leagueoflegends.com | ✅ | 🟡 *(MediaWiki)* | ✅ *(deltas)* | **❌ EN seul** | ✅ | ✅ | 🟡 MediaWiki | Riot finance, communauté édite |
| Fandom LoL wiki | 🟡 *(figé 01/2025)* | 🟡 | ✅ *(deltas)* | 🟡 *(14/16 obsolètes)* | 🟡 | ❌ | 🟡 | Fandom (pub) |
| **OP.GG** | 🟡 | ✅ | ❌ | ✅ **24** | 🟡 | ❌ | ❌ *(sauf MCP)* | Pub + $3-3,99/mois |
| **U.GG** | 🟡 | ✅ | ❓ *(filtre, profondeur ?)* | ❌ *(EN seul)* | ❌ | ❌ | ❌ | Pub + $2,49-3,99/mois |
| **Mobalytics** | 🟡 | ✅ | ❌ | ✅ 17 | ❌ | ❌ | ❌ | Pub + $5,83-7,99/mois |
| **Blitz.gg** | 🟡 | ✅ | ❓ | ❓ | ❌ | ❌ | ❌ | Pub + ~$4,99/mois |
| **Lolalytics** | ❌ | ✅ | ❌ | 🟡 ❓ | ❌ | ❌ | ❌ *(« un jour »)* | Pub seule |
| **Metasrc** | 🟡 | ✅ | 🟡 *(« League Classic » S3)* | ❓ | 🟡 *(base Skins)* | ❌ | ❌ | Pub seule |
| **League of Graphs** | ❌ | ✅ | 🟡 *(infographics)* | ✅ 20 | ❌ | ❌ | ❌ | Pub seule |
| **Porofessor** | ❌ | ✅ *(overlay)* | ❌ *(30 j glissants)* | ✅ 21 | ❌ | ❌ | ❌ | Pub + premium ❓ |
| **DPM.LOL** | 🟡 | ✅ | ❌ | ✅ 18 | ❌ | 🟡 *(app premium)* | ❌ *(ToS interdit)* | Pub + 3,99-35,99 € |
| **DeepLoL** | ❌ | ✅ | ❓ | ❓ | ❌ | ❌ | ❌ | Pub + B2B |
| **Tracker.gg (LoL)** | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ **LoL exclu** | Pub + $3/mois |
| **Champion.gg** | — | — | — | — | — | — | — | **☠️ mort** |
| lol-db.com | ✅ | ❌ *(grille brute)* | ❌ *(live/PBE)* | ✅ **~48** | ❓ | ❌ **pop-ups** | ❌ | Pub agressive |
| riftpatchnotes.com | 🟡 *(deltas)* | ✅ | ✅ **405 patchs** | ❌ EN | ❌ | ✅ | ❌ | Gratuit |
| patchdelta.gg | 🟡 *(deltas)* | 🟡 *(générique)* | ✅ **391 patchs** | ❌ EN | ❌ | ✅ | ❌ | Gratuit |
| skinexplorer.lol | 🟡 *(splashs)* | ✅ | ❌ | ❌ EN | 🟡 | ✅ | ❌ | Open source |
| lolskin.info | 🟡 *(2 111 skins)* | ✅ | ❌ | ✅ **23** | ✅ | 🟡 | ❌ | Sponsoring |
| loldb.info | 🟡 *(cosmétiques)* | ✅ | ❌ | ❌ EN | ✅ **6 954** | 🟡 | ❌ | Don |
| modelviewer.lol | 🟡 *(3D)* | ✅ | ❌ *(PBE)* | ❓ | ✅ *(3D)* | ❌ | ❌ | Pub + Patreon |
| heimerdinger.lol | 🟡 | ✅ | ❌ | ❌ EN | 🟡 | ✅ | ❌ | Open source |

**Lecture de la matrice** : la colonne **Multi-patch** et la colonne **Multi-langue** ne sont **jamais toutes les deux vertes** sur une même ligne, sauf pour LeagueOfDataBase et les CDN bruts (qui n'ont pas d'UI). C'est tout le sujet de ce document.

---

## 5. Famille A — Généralistes stats / tracking

> **Verdict de famille** : **ce ne sont pas des concurrents.** Ils vendent du *dynamique* (mon rang, ma winrate, la méta du patch) ; le projet vend du *statique* (ce que fait ce champion, à ce patch, dans ma langue). Recoupement fonctionnel réel : **~10 %** (page champion, images). Ils sont cités ici pour comprendre le marché, les attentes UX qu'ils ont créées, et surtout **ce qu'ils ne font pas**.

### 5.1 OP.GG — le leader, le seul indépendant

- **URL** : [op.gg](https://op.gg/) · **Société** : indépendante (Corée) — **le seul grand resté indépendant**, et même acquéreur net (rachat d'**OGN**, ex-première chaîne esport coréenne, juin 2022).
- **Features** : profil / historique (**rétention 2-5 mois seulement** — [help.op.gg](https://help.op.gg/hc/en-us/articles/31088608024729)), tier list multi-filtres, builds avec « AI tips summary (Beta) », spectate pro live, overlay in-game avec auto-réglage des runes, **OP.GG AI Voice** (agent vocal IA), marketplace de coachs humains (`gigs.op.gg`), TFT (app « AllT »), esport complet, app mobile, client desktop Win+macOS, extension navigateur.
- **Source** : VÉRIFIÉ, citation directe — « *All game data currently used by OP.GG is collected through official APIs provided by each game company* » ([help.op.gg](https://help.op.gg/hc/en-us/articles/31091405109401)). Cité par le blog d'ingénierie Riot dès 2016 comme succès de l'écosystème API.
- **Modèle** : gratuit + AdSense ; premium ad-free — **écart non résolu entre deux sources primaires** : $3/mois ([member.op.gg](https://member.op.gg/membership)) vs $3,99/mois ou $39,99/an (App Store). Hypothèse : surcharge IAP iOS 30 %, non confirmée.
- **Stack** : peu documentée. Signature **Next.js** détectée. Seul dépôt officiel : `opgginc/opgg-mcp` (TypeScript/Docker, MIT).
- **Langues** : **24 VÉRIFIÉES** (sélecteur + [op.gg/lol/about](https://op.gg/lol/about) : « *5 Continents and Over 23 Different Languages… over 170 countries* »).
- **Multi-patch** : ❌ **aucun sélecteur d'archive** — filtres uniquement temporels (Today / 7 j / mois).
- **API** : « *this data is not provided to third parties* ». **Sauf** un différenciateur 2026 notable : **serveur MCP open source** (`opgginc/opgg-mcp`, MIT) orienté agents IA, sans quotas documentés.
- **Trafic** : 47,1 M visites/mois (SimilarWeb avril 2026, rang #1 064) ; autre snapshot mai = 74,4 M ; auto-déclaré « 55 M visiteurs / 460 M pages vues ». Écarts non arbitrés.
- **Critiques** : « OP Score » jugé peu fiable, réglage auto des runes intrusif, CPU élevé, **dark pattern publicitaire signalé depuis août 2024** (bouton fermer redirigeant vers l'annonceur) — un dépôt `op-gg-remove-ads` existe. App Store 3,5/5 sur 818 avis.

> **Ce qu'on en retient** : OP.GG a résolu l'i18n (24 langues) mais **pas** l'historique. Il prouve que le multilingue massif est rentable sur ce marché — argument en faveur des 21 locales du projet.

### 5.2 U.GG — le plus « propre » statistiquement, le moins international

- **URL** : [u.gg](https://u.gg/) · **Société** : **Outplayed, Inc.**, filiale d'**Enthusiast Gaming** depuis le 23/11/2021 (~$44-57 M avec earnout — [Forbes](https://www.forbes.com/sites/mikestubbs/2021/11/23/enthusiast-gaming-acquires-ugg-for-44-million-to-enter-league-of-legends-space/)). Fondé 2017 à Austin par Shinggo Lu et Alan Liang.
- **Features** : profil/historique, tier lists (générale, ARAM Mayhem, Duo, Personal en PLUS), builds filtrables lane/elo/patch/région, « Role Quest », live game via app desktop **Windows uniquement** (badge « Riot Games Compliant »), TFT complet. **Pas d'app mobile** — reconnu explicitement par leur FAQ : « *We initially operated under the assumption that anyone playing League of Legends is playing on a non-mobile device* ». Pas de coaching, pas d'extension navigateur.
- **Source** : VÉRIFIÉ — « *All of our data comes from Riot's API* » / « *We analyze every game available from Riot's API* » ([u.gg/faq](https://u.gg/faq)).
- **Modèle** : gratuit + pub (régie **Playwire**) ; **U.GG PLUS** $2,49/mois annuel ($29,88/an) ou $3,99/mois, essai 7 j.
- **Stack** : offre d'emploi archivée (2020-21) → **Elixir/Phoenix, Express, Elasticsearch, PostgreSQL, DynamoDB sur AWS** back ; **React/Redux + GraphQL** front.
- **Langues** : ❌ **aucun sélecteur trouvé sur 11 pages fetchées** — anglais uniquement selon toute vraisemblance.
- **Multi-patch** : ❓ un filtre patch existe (FAQ : « *We offer filters in game type, role, rank, region and patch* ») mais **profondeur d'archive non confirmée**.
- **⚠️ Contexte financier** : Enthusiast Gaming (TSX : EGLX) est un **penny stock en grande difficulté** — action à **0,045 CAD**, capitalisation 11,14 M CAD, 80 employés (09/07/2026, [stockanalysis.com](https://stockanalysis.com/quote/tsx/EGLX/)).

> **Ce qu'on en retient** : le n°2 mondial du secteur est **monolingue**. Le projet couvre 21 locales. C'est un écart de couverture, pas de qualité — mais il est réel.

### 5.3 Mobalytics — le coaching IA, sous pavillon saoudien

- **URL** : [mobalytics.gg](https://mobalytics.gg/) · **Société** : Gamers Net, Inc. (2016). **Racheté par ESL FACEIT Group**, annoncé 17-18/03/2025, opère « *as a standalone business* », montant non divulgué ([esportsinsider](https://esportsinsider.com/2025/03/esl-faceit-group-mobalytics-acquisition), [AGC Partners](https://www.agcpartners.com/transactions/agc-partners-advises-mobalytics-on-its-acquisition-by-esl-faceit-group)). ESL FACEIT Group → Savvy Games Group → **Public Investment Fund d'Arabie Saoudite** ([SportsPro](https://www.sportspro.com/news/savvy-gaming-group-saudi-arabia-pif-esl-faceit-acquisition-merger-esports/)). *(Rachat par OP.GG = fausse piste explicitement écartée après vérification.)*
- **Features** : **Gamer Performance Index (GPI)** — scoring ML sur 8 axes (Fighting, Farming, Vision, Aggression, Toughness, Teamplay, Consistency, Versatility) ; Live Companion (overlay + extension Twitch) ; Smart Highlights ; Challenges. Tier list « *curated by our team of challenger experts* » distincte d'un onglet Stats pur. TFT complet. Client desktop bâti sur **Overwolf**. **LoR abandonné** (« *WE'RE SUNSETTING LEGENDS OF RUNETERRA* »).
- **Modèle** : gratuit + pub + **Plus** $7,99/mois, $69,99/an (~$5,83/mo). Collector's Edition $199,99 (épuisée).
- **Stack** : React/TypeScript/GraphQL + Redux/MobX/Apollo front ; Node/Express ou Next.js + Redis back. Site marketing sur **WordPress 6.7.2**.
- **Langues** : **17 VÉRIFIÉES** ([mobalytics.gg/translation](https://mobalytics.gg/translation/) : « *Here are the languages we're currently targeting (based on languages available in Riot Games)* ») — **traduites par des bénévoles communautaires**, pas une équipe interne.
- **Multi-patch** : ❌ patch courant explicite par page, aucun sélecteur d'archive. *(Observé au fetch : tier list affichant patch **26.10** « MAJ il y a 4 jours » quand U.GG affichait 26.14 « il y a 2h » — décalage réel.)*
- **Critiques** : arbitrage obligatoire + renonciation aux actions collectives (ToS US) ; pics de RAM ; dark pattern de résiliation (~35 min en moyenne) ; avis Trustpilot suspicieusement homogènes (100 % 5★, ton générique).

### 5.4 Blitz.gg — le seul à documenter du reverse engineering

- **URL** : [blitz.gg/lol](https://blitz.gg/lol) · **Société** : **Swift Media Entertainment** — **même holding que TSM**. Fondé 2018 par **Andy Dinh** (fondateur TSM) et Adil Virani, racheté par Swift dès novembre 2018 ([ESPN](https://www.espn.com/gaming/story/_/id/25845083/team-solomid-parent-company-swift-buys-blitz-esports-app)).
- **Features** (VÉRIFIÉ, liste exhaustive) : Auto-Import Runes & Builds, Profile/Match History, Benchmarking Overlay, ARAM Health Timers, Skill Order, Tier List, Arena Augments, Post Match Analysis, Item Value Overlay, Minimap/Ultimate Timers, Jungle Pathing Overlay, Suggested Picks & Bans, Loading Screen Overlay, Trinket Reminder, ProBuilds. **Détection du champion en champ select par vision par ordinateur** — signature produit. **Pas d'app Overwolf** (installeur propre).
- **⚠️ Source des données — le point le plus notable de tout le benchmark** : le support déclare « *Blitz fetches its data directly from the official APIs provided by game developers* » ([support.blitz.gg](https://support.blitz.gg/hc/en-us/articles/4415419714201)), **MAIS** un billet co-écrit par un data engineer Blitz sur le blog de leur partenaire **Databricks** révèle : « *fetching unique data through our expertise in **reverse engineering** the data generated by games like League of Legends… that are not readily available through official APIs… Fetching is done through a sophisticated and advanced **scraping backend*** » ([databricks.com/blog](https://www.databricks.com/blog/how-blitz-and-databricks-are-powering-new-era-competitive-gaming)). Une offre « Reverse Engineer » active corrobore. **Blitz est la seule grande plateforme à documenter publiquement cette pratique.**
- **Stack** : migration **Snowflake → Databricks lakehouse**, S3, **Airflow**, **MLflow**. Org GitHub `theblitzapp` (44 dépôts, majoritairement **Elixir** + fork Rust d'un client API Riot).
- **⚠️ Controverse VÉRIFIÉE** : **NetEase (éditeur de Marvel Rivals) a interdit Blitz en jeu compétitif à partir du 21/02/2025**, le qualifiant de « *cheating software* » — contrepoint direct à la conformité revendiquée : **la tolérance n'est pas universelle selon l'éditeur**.
- **Fraîcheur suspecte** : un fetch de la racine a affiché « Patch 14.24 » (~2 ans de retard) — artefact de cache SSR probable, mais non confirmé.

### 5.5 Lolalytics — le plus honnête sur l'absence d'API, le plus contesté sur la méthode

- **URL** : [lolalytics.com](https://lolalytics.com/) · Entité « LoLalytics Limited ». Satellites au même habillage, domaines distincts : **pros.lol** (pro builds) et **xdx.gg** (profils). **Pas de TFT.**
- **Features** : tier list globale + ARAM/Arena, builds (« Highest Win Build » vs « Most Common Build »), runes, matchups/counters avec **seuil explicite de 100 games minimum**, leaderboard 14 régions, filtre elo et fraîcheur (7j/14j/Today/Smooth), module « Patch Notes Champions Performance ». **Profil et pro builds externalisés** : « *Lighting fast summoner profiles can be found at the new dedicated site XDX.gg* ».
- **⚠️ Critique méthodologique documentée** : « **Asymmetric Sampling** » — une partie n'est comptée dans un bracket « Emerald+ » que si **le joueur du champion étudié** est Emerald+, indépendamment du rang adverse, ce qui **peut gonfler artificiellement les winrates perçus** ([zleague.gg](https://www.zleague.gg/theportal/decoding-league-of-legends-statistics-a-closer-look-at-lolalytics-winrate-data/)).
- **API — la citation la mieux documentée du secteur** : « *This is a private API... may not be used by third parties... In light of that **we do hope in the future to release a public API** with a managable portion of our data* » ([a3.lolalytics.com](https://a3.lolalytics.com/)). Résultat direct : prolifération de scrapers non officiels (PyPI `lolalytics-api`, GitHub `khorn89/LolAlytics.py`) — **aucun ne propose de paramètre de sélection de patch**, confirmant l'absence d'archive.
- **Modèle** : **pub seule, aucun premium trouvé** malgré recherches répétées par deux agents indépendants.
- **Trafic** : rang #8 626 (SimilarWeb juin 2026, **en baisse** depuis #7 577), -6,84 %/mois. Top pays : USA, Japon, Allemagne, Brésil, **France**.

### 5.6 Metasrc — le seul avec une vraie archive… d'une autre époque

- **URL** : [metasrc.com](https://www.metasrc.com/) · METASRC LLC, « Made with ♥ in Boulder, CO ».
- **⚠️ Rétraction de périmètre VÉRIFIÉE** : Valorant et WoW **retirés** — [metasrc.com/retired](https://www.metasrc.com/retired) : « *Changes to Game Coverage... This includes Valorant and World of Warcraft tier lists, guides, stats pages... have been fully removed and will not receive updates* ». Ne couvre plus que **LoL et TFT**.
- **Features** : tier list S+→D, compteur d'échantillon live (« Champions Processed: 343,5K »), builds/counters/synergies, **Counter Picker** interactif régionalisable, **bases Items et Skins**, Duos. Modes : Ranked, Arena, ARAM, ARAM Mayhem. **Pas de profil invocateur**, pas de live game, pas d'esport, pas d'app mobile.
- **🔍 Trouvaille notable — « League Classic »** : mode restaurant les statistiques **Season 3 (~2013)** avec tier list / builds / items / runes / **masteries** propres, versionné « Patch 3.13 » distinct du patch moderne ([metasrc.com/lol/classic](https://www.metasrc.com/lol/classic)). **C'est la seule tentative crédible d'archéologie de données du secteur stats** — mais c'est un one-shot thématique (une ère figée), pas une navigation continue.
- **Stack** : **HTMX** détecté côté front (choix rare et notable). `cdn.metasrc.com`, `og.metasrc.com` (génération dynamique d'images OG).
- **Langues** : ❓ **non résolu**. Meta-tags confirment au moins `ar_AE`. Une liste de ~18-19 langues est apparue deux fois en recherche **sans URL source citable ni sélecteur observé dans le HTML** → signal de résultat non fiable, **délibérément écarté du statut VÉRIFIÉ**.

> **Ce qu'on en retient** : « League Classic » prouve qu'il **existe un appétit pour les données historiques** — un acteur commercial a investi dans la restauration d'une ère vieille de 13 ans. Mais il l'a fait en dur, pour une seule ère. Le projet peut le faire **pour les 494 versions, par construction**.

### 5.7 Porofessor + League of Graphs — même groupe, même fondateur

**Réponse VÉRIFIÉE à la question de départ : oui, même groupe.** Preuves directes :

- FAQ Porofessor, citation exacte : « *What kind of partnership do you have with leagueofgraphs.com?* » → « ***I'm the creator of League of Graphs :)*** » ([porofessor.gg/faq](https://porofessor.gg/faq)).
- Interview du fondateur **Jean-Nicolas Mastin** ([gamezo.gg](https://gamezo.gg/interview-with-porofessor-gg-and-league-of-graphs-founder-jean-nicolas/)) : LoG créé en décembre 2013 à la sortie de l'API Riot ; « *It was around 2017 that Porofessor was born initially as a part of League of Graphs and then separately as its own domain* ».
- Footer LoG : branding **M.O.B.A. Network**, marques sœurs listées (Mobafire, CounterStats, WildRiftFire, SmiteFire, DOTAFire…), **CDN mutualisé** (`lolg-cdn.porofessor.gg`).
- **Rachat 2023 confirmé multi-sources** ([PC Gamer](https://www.pcgamer.com/corporation-buys-a-popular-league-of-legends-app-for-dollar55-millionits-made-by-one-guy/), [TechCrunch](https://techcrunch.com/2023/06/15/wargraphs-a-gaming-startup-with-only-one-employee-and-no-outside-funding-sells-for-54m/)) : **Wargraphs SAS (1 seul employé)** rachetée par **M.O.B.A. Network AB** (Nasdaq First North) pour **jusqu'à 50 M€ / ~54-55 M$**, clôturé le 30/05/2023.

**Porofessor** — [porofessor.gg](https://porofessor.gg/) : desktop **Overwolf uniquement** (« *The Porofessor app is not available on smartphones!* »). Suggestions bans/counterpicks en champ select, Matchup Review, Player Tags, Pro Replays, LFG, résumés de patch notes in-game, overlay (timers jungle/inhibs), TFT Team Builder, **LoR Deck Tracker**. Fenêtre : **30 derniers jours glissants uniquement**. **21 locales VÉRIFIÉES**. Trafic : 3,2 M visites/mois (SimilarWeb mai 2026).

- **⚠️ Controverse VÉRIFIÉE avec conséquence réglementaire** : backlash Reddit documenté par [Dexerto](https://www.dexerto.com/league-of-legends/popular-league-of-legends-add-on-criticized-for-adding-cheat-feature-players-think-should-be-banned-3142237/) après l'ajout d'un **tracker de cooldown d'ultimate ennemi cliquable** — terme « *parasitic software* » dans le débat. **Conséquence : le 13 mars 2025, Riot a interdit la fonctionnalité « Enemy Ultimate Timer » dans toutes les apps tierces, sous peine de désactivation de clé API.** Un patcher anti-pub communautaire existe (`CallumMcLoughlin/AdfreePorofessor`).

**League of Graphs** — [leagueofgraphs.com](https://www.leagueofgraphs.com/) : copyright « 2013-2026 ». Tier list, builds/runes, leaderboards, flux communautaire de replays récents. **Live-game externalisé vers Porofessor.** **20 locales VÉRIFIÉES**. Archive : section « Infographics » (résumés statiques remontant possiblement à 2015-2016, profondeur non confirmée). Modèle : **pub seule** + page `/donate` (Bitcoin/PayPal).

- **⚠️ Contexte financier** : M.O.B.A. Network AB — action **-97,6 % sur un an**, **suspension de cotation annoncée le 22/12/2025, effective au 01/01/2026**, cession de « Union For Gamers » début 2026. Repositionnement du 21/05/2026, citation exacte : « *deepening engagement and monetization in the **League of Legends and Teamfight Tactics** ecosystem, with **subscriptions** as a core and growing revenue layer* » → les actifs LoL/TFT sont le **cœur conservé** de la restructuration, pas des actifs en cession.

> **⚠️ Mythe communautaire déconstruit** : LeagueOfGraphs, Porofessor et Counterstats sont souvent cités comme les alternatives « propres ». Ils appartiennent au **même réseau M.O.B.A. Network** et partagent la même infrastructure ad-tech — **337 partenaires IAB confirmés** sur le bandeau cookies. Voir [§14.5](#145--le-positionnement--sans-pub---crédible-mais-à-prouver).

### 5.8 DPM.LOL — la croissance la plus rapide, portée par des créateurs

- **URL** : [dpm.lol](https://dpm.lol/) · Spin-off esport : [rft.gg](https://rft.gg/) (« le HLTV du LoL » selon le CEO). Fondateur/CEO **Juliano** ; **Caedrel** rejoint ~1 mois après le lancement ; **Kameto** (CEO Karmine Corp, streamer FR le plus suivi) devenu **co-actionnaire en mars 2025** — croissance utilisateurs quasi doublée après son arrivée ([esports.gg](https://esports.gg/news/league-of-legends/how-dpm-lol-is-transforming-lol-analytics/)).
- **Features** : recherche unifiée, **DPM Score**, Peak elo temps réel, tier list (Ranked/Duo/Arena/ARAM), builds, **matchups vidéo (30 000+)**, leaderboards + OTP leaderboards, Data Studio, esport LEC/LCK/LCS/LPL/Worlds avec pickems. **App desktop Windows** avec overlays (Objective Pills, Gold Difference, Win Probability), assistant de draft IA, Game Recorder — explicitement **pas construit sur Overwolf, sans publicité** (citation CEO : « *a whole new platform with **no ads, no Overwolf***»), réservée aux abonnés 6 mois/1 an. **Pas de TFT** (« *one day* », pas avant 2027).
- **Source** : déclaration officielle directe — « *it's in the Riot API!* » ([@dpmlol](https://x.com/dpmlol/status/1886360493973029359)). L'interview CEO révèle un objectif 2026 de « *reduce its dependence on Riot Games* » — confirmant en creux une dépendance actuelle forte.
- **Modèle** : gratuit + AdSense + **Premium** (prix Stripe exacts) : **3,99 €/mois, 19,99 €/6 mois, 35,99 €/an**. Auto-déclaré : « *30M page views/month, 2M unique visitors/month* » ([dpm.lol/advertise](https://dpm.lol/advertise)).
- **Stack** : **Next.js/React**, hébergement **Hetzner** (Allemagne), OVH registrar, Cloudflare DNS, Stripe.
- **Langues** : **18 VÉRIFIÉES** — « *Stats, builds, esport, matchups—now available in 18 languages!* » ([@dpmlol, 02/01/2025](https://x.com/dpmlol/status/1874835027013304513)). Processus : **première passe IA puis affinage par locuteurs natifs joueurs de LoL** — méthodologie intéressante pour le projet.
- **API** : ToS explicites — « *Automated queries, including **scraping and crawling, are strictly prohibited*** unless express written permission is granted* » ([dpm.lol/tos](https://dpm.lol/tos)).
- **⚠️ Risque réglementaire** (facteur, pas violation avérée) : la politique Riot de mai 2025 interdisant les overlays « qui simulent une prise de décision » s'approche de la zone grise des suggestions live de DPM (Win Probability, suggestions items).
- **Critique** : fil francophone « [Le site DPM est HORRIBLE](https://www.jeuxvideo.com/forums/42-19163-76789515-1-0-1-0-le-site-dpm-est-horrible.htm) » ciblant le DPM Score jugé trop flatteur.

> **Ce qu'on en retient** : DPM.LOL prouve qu'**un nouvel entrant peut percer en 2 ans** (0 → 3 M+ visites/mois entre juillet 2024 et l'été 2025) sur un marché prétendument saturé. Le levier n'a pas été technique — c'est la **distribution par créateurs** (Caedrel, Kameto). Enseignement direct pour la mise sur orbite du projet.

### 5.9 Les autres — panorama condensé

| Acteur | Essentiel |
|---|---|
| **DeepLoL** ([deeplol.gg](https://www.deeplol.gg/)) | GameEye Corp (Corée). **AI Score** + Tier Prediction (signature). `pro.deeplol.gg` = B2B avec **contrats payants confirmés auprès de 5 équipes esport** (AR, BR, JP, KR, US — [Korea Herald](https://www.koreaherald.com/article/2843387)). #1 en Corée catégorie jeux. Route « PATCH HISTORY » (`/champions/{x}/history`) **existe mais contenu non vérifiable** (SPA, 3 tentatives). Compte X officiel **suspendu** au 17/07/2026 (cause non trouvée). |
| **Tracker.gg (LoL)** ([tracker.gg/lol](https://tracker.gg/lol)) | **Point le mieux vérifié** : `tracker.gg/developers` cite « *an Apex Legends stats API, a CSGO stats API, a Division 2 stats API and a Splitgate stats API* » — **LoL N'EST PAS inclus**. Pas de tier list, pas de builds. **Signal clé** : les 5 premiers mots-clés organiques du domaine sont tous **Valorant** — LoL est une section secondaire. Premium $3/mois. |
| **Champion.gg** | **☠️ Mort fonctionnellement.** Domaine répond, mais `<title>` de toutes les pages = littéralement « **Redirecting...** ». **92,83 % des liens sortants classés « Adult »** (SimilarWeb) — signature d'un site abandonné rempli par des régies bas de gamme. Trafic résiduel : ~15,9 K visites/3 mois, **session moyenne 27 s**. Cause : **absorption corporate progressive vers Blitz** (l'équipe transférée en nov. 2018 — « *the app is being transferred today to the team behind Champion.gg and Probuilds* »), **pas** une coupure d'API Riot. Arrêt des MàJ constaté dès **début 2019** (patch 9.6). |
| **Riftfeed** ([riftfeed.gg](https://riftfeed.gg/)) | Site **média/éditorial**, pas un outil de stats. Esports Media GmbH (Munich), réseau EarlyGame, soutenu par *kicker*. **2 langues (EN/DE)**. **⚠️ Contenu gelé** : le plus récent trouvé sur toutes les pages = « LoL Patch 14.15 Preview », **daté du 24/07/2024**. Aucune trace de Worlds 2024/2025 ni MSI 2025/2026. **Piège de nom** : `riftfeed.com` pointe vers une app norvégienne de formation pro sans rapport. |
| **LoLVVV** ([lolvvv.com](https://www.lolvvv.com/)) | Hypothèse coréenne/chinoise **infirmée** : audience réelle = **Turquie (1er), Brésil, Pologne**. Registrant allemand. **18 langues VÉRIFIÉES** (dont ko/zh — comme 2 parmi 18, pas comme marché). Next.js. |
| **Overwolf** ([overwolf.com](https://www.overwolf.com/)) | **Infrastructure SDK, pas un tracker.** 18 « Popular Apps » + 41 autres en catégorie LoL. Héberge Porofessor, OP.GG, Mobalytics. **Blitz et DPM.LOL n'en dépendent PAS** (installeur propre — argument de vente des deux). Rémunération : « ***App creators get 70% of the ad revenue*** ». **113 M MAU réseau entier** (Forbes, nov. 2025) ; **300 M$ versés aux créateurs en 2025**, objectif « *$1 billion in annual creator payouts by 2030* ». Point technique : Overwolf **n'a aucun accès privilégié aux serveurs Riot** — son `pseudo_match_id` est documenté comme « *an Overwolf-generated code unrelated to Riot Games* ». |
| Écosystème coréen | **FOW.KR** — doyen (janvier 2012, ~1 an **avant** OP.GG), **en déclin apparent** (3 liens au fetch direct). Famille **PlayXP Inc.** : DAK.GG, PORO.GG, LoLCHESS.GG, MAPLE.GG. |
| Écosystème chinois | Serveur national **Tencent**, pas d'API REST Riot officielle. `lol.qq.com` (officiel Tencent), `lol.17173.com` (fondé 2001, racheté Sohu), **wanplus.cn** — cas rare de transparence : source **explicitement déclarée comme scraping** (« *数据来自网页内容分析...爬虫技术的应用* »). |

### 5.10 Synthèse de famille — les patterns communs

**Source des données : quasi-unanimité déclarée sur l'API Riot, une exception documentée.**
OP.GG, U.GG, Mobalytics, DPM.LOL, Metasrc, Champion.gg (historique) déclarent tous explicitement l'API Riot officielle — citations directes trouvées pour chacun. **Blitz.gg est le seul à documenter publiquement du reverse engineering** (via le blog de son propre partenaire Databricks). En Chine, **wanplus.cn** assume ouvertement le scraping. **Aucune plateforme ne mentionne Data Dragon ou CommunityDragon comme source nommée**, bien que l'usage d'assets répliqués sur CDN propre (OP.GG, U.GG, DPM.LOL, Metasrc) suggère une pratique équivalente en interne (INFÉRENCE).

**Modèle économique : freemium publicitaire, quasi sans exception.**
Gratuit + pub display/vidéo + palier « Premium/Plus/Ad-free » optionnel. Fourchette : **$1/mois (Mobafire) à $8/mois (Mobalytics)**, majorité entre **$2,49 et $5/mois**. **Aucune plateforme n'a de paywall total.** Deux exceptions à l'absence de premium : **Lolalytics et League of Graphs** (pub pure).

**Features quasi-universelles vs différenciantes.**

- *Quasi-universel* : tier list, builds/runes, profil + match history, overlay in-game, TFT.
- *Différenciant* : scoring IA propriétaire (OP.GG AI Voice, Mobalytics GPI, DeepLoL AI-Score, DPM Score — **chacun avec sa marque et sa méthodologie non standardisée**) ; esport structuré (fort chez OP.GG et DPM/RFT, **quasi absent** chez U.GG/Tracker/Metasrc/Lolalytics) ; computer vision en champ select (**unique à Blitz**) ; **desktop sans Overwolf** (argument de vente revendiqué par DPM.LOL **et** Blitz face à Porofessor/Mobalytics/OP.GG qui en dépendent).
- *Absent quasi partout* : coaching humain structuré (seul OP.GG, via `gigs.op.gg`).

**⚠️ Angle mort n°1 du secteur : l'historique par patch.**
**Aucune des ~15 plateformes n'offre de sélecteur d'archive de patchs confirmé et fonctionnel.** Pattern universel : « patch courant + delta vs précédent » au mieux. Seules exceptions partielles : **Metasrc « League Classic »** (Season 3, cas isolé et thématique) et **League of Graphs « Infographics »** (archive statique, profondeur non confirmée). U.GG et Blitz ont des indices de filtre patch sans profondeur vérifiable. **C'est un angle mort structurel de tout le secteur** — et l'architecture `data/{version}/{lang}/{type}.json` du projet le résout par construction.

**⚠️ Angle mort n°2 : l'API publique / open data.**
**Aucune plateforme ne propose d'API publique documentée et librement accessible.** Lolalytics est la plus honnête sur ce manque. Mobalytics et DPM.LOL **interdisent explicitement le scraping par ToS**. Tracker.gg a une vraie API… **qui exclut LoL**. Le seul point d'ouverture réel est le **serveur MCP open source d'OP.GG** (orienté agents IA) — approche 2026 radicalement différente d'un REST classique. **Data Dragon, exemptée des rate limits par design officiel, reste la seule source ouverte et stable du secteur** — c'est structurellement ce sur quoi repose déjà LeagueOfDataBase.

**i18n : très inégal, contrairement à l'image d'un secteur mondialisé.**
**OP.GG (24) · Porofessor (21) · League of Graphs (20) · DPM.LOL (18) · LoLVVV (18) · Mobalytics (17)** contre **Riftfeed (2), U.GG, Blitz, Tracker.gg** apparemment anglais seul. Corrélation trafic/i18n réelle mais **non absolue** (Mobalytics est mieux traduit qu'U.GG et fait moins de trafic).

**Critiques récurrentes — consensus transversal :**

1. **Publicité intrusive** — friction n°1 unanime, **systématiquement le premier argument de vente de chaque premium**.
2. **Débat fairness / « triche »** autour des overlays live (conséquence réglementaire concrète en mars 2025) — registre de critique **spécifique à ce secteur**.
3. **Disputes méthodologiques** sur les scores propriétaires composites (OP Score, DPM Score, Asymmetric Sampling) — **les stats brutes, elles, ne sont presque jamais contestées**.
4. **Surconsommation RAM/CPU** des apps Overwolf — au point qu'un article « Best LoL Companion Apps Without Overwolf » existe.
5. **Absence quasi totale de controverse RGPD / vie privée** — résultat négatif notable : la controverse de ce secteur est **exclusivement une controverse de fair-play compétitif**, pas de protection des données.

---

## 6. Famille B — Infrastructure de données

> **Verdict de famille** : ce sont les **fournisseurs** du projet, pas ses concurrents. Mais leur absence totale d'UX est précisément l'espace où le projet existe.

### 6.1 Data Dragon — la fondation

| Point | Constat |
|---|---|
| **URL** | `https://ddragon.leagueoflegends.com/` — `/api/versions.json`, `/cdn/languages.json`, `/cdn/{version}/data/{lang}/{type}.json`, `/cdn/dragontail-{version}.tgz`. **VÉRIFIÉ** |
| **Périmètre** | Champions, objets, sorts d'invocateur, runes, icônes de profil, splash arts, loading screens, sprites, tarball complet par patch. **Chromas réduits à un booléen** (`skins[].chromas`) — aucun détail couleur/nom. **VÉRIFIÉ** |
| **Multi-version** | ✅ **494 entrées** dans `versions.json` — **recompté par mes soins sur le fichier fetché** (17/07/2026). Composition exacte : **9 builds alpha** (`0.151.2` → `0.154.3`), **98 entrées `lolpatch_X.Y`** (jusqu'à `lolpatch_3.7`, saison 3 / 2013), le reste en numérotation moderne. **Test direct** : `cdn/5.5.1/data/en_US/summoner.json` (mars 2015 — `5.5.1` **confirmé présent dans la liste**) renvoie un JSON complet et valide, avec des modes disparus (**Dominion / Ascension / Poro King**) — preuve que ce n'est **pas** un alias vers les données actuelles. **VÉRIFIÉ : ~11 ans de rétention fonctionnelle** |
| **Multi-langue** | **28 locales** live (`languages.json`, fetch direct) : ar_AE, cs_CZ, de_DE, el_GR, en_AU/GB/PH/SG/US, es_AR/ES/MX, fr_FR, hu_HU, id_ID, it_IT, ja_JP, ko_KR, pl_PL, pt_BR, ro_RO, ru_RU, th_TH, tr_TR, vi_VN, zh_CN/MY/TW. **VÉRIFIÉ**. *(La doc `developer.riotgames.com/docs/lol` diverge d'une entrée — mentionne `ms_MY`, absent du fichier live. Changement `vn_VN`→`vi_VN` depuis le patch 13.10.1.)* |
| **Rate limit** | ⭐ **Citation officielle exacte : « *calls to the static data API do not count against the application rate limit* » — Data Dragon est explicitement EXEMPTÉE.** C'est l'avantage architectural le plus sous-estimé du projet. |
| **⚠️ Fraîcheur** | Mise à jour **manuelle** après chaque patch, « *not always immediate* » (texte officiel). Délai communautaire estimé jusqu'à 2 jours. **Démonstration en direct pendant cette recherche** : le 17/07/2026 — soit **2 jours après la sortie du patch 26.14/16.14** — la tête de `versions.json` était encore **`16.13.1`**, alors que la doc officielle citait déjà `dragontail-16.14.1.tgz` en exemple « Latest » et que CommunityDragon exposait déjà un dossier `16.14`. **Les fichiers du patch existent avant que `versions.json` ne les annonce.** *(Un agent a relevé `16.14.1` en tête lors d'un autre fetch — l'écart n'a pas pu être arbitré, mais il illustre précisément la latence de publication.)* → **Implication produit : ne jamais traiter `versions.json[0]` comme « le patch actuel » sans réserve** ; les sites stats sont à jour en ~2 h là où DDragon peut accuser 2 jours. |
| **API** | Public total, **sans clé, sans authentification**. `runesReforged.json` fonctionne mais **n'est pas documenté** officiellement. |
| **⚠️ Limites documentées** | • **Splash arts / loading screens NON versionnés par patch** (confirmé officiellement) → 130 splashs mal associés au patch 11.1, encore 73 au patch 10.22 ([issue #348](https://github.com/RiotGames/developer-relations/issues/348)) — *pertinent : le projet hotlink les splashs*<br>• `partype` vide pour Bel'Veth au lancement ([#648](https://github.com/RiotGames/developer-relations/issues/648))<br>• stats `info` à 0 pour Akshan/Rell/Seraphine/Vex encore au patch 14.4.1 ([#896](https://github.com/RiotGames/developer-relations/issues/896))<br>• titre Xayah vide en `es_AR` ([#895](https://github.com/RiotGames/developer-relations/issues/895))<br>• pas de date de patch, pas de winrate/pickrate |

**Endpoint `static-data` — statut définitif** : `lol-static-data-v3` **déprécié et retiré le 27 août 2018**. Citation officielle du Change Log : « *Data Dragon as the **sole source of truth** for static data* » ([riotgames.com/en/DevRel/riot-games-api-change-log](https://www.riotgames.com/en/DevRel/riot-games-api-change-log)) — corroboré indépendamment par une issue GitHub contemporaine ([LeagueJS#11](https://github.com/Colorfulstan/LeagueJS/issues/11)). La liste actuelle des APIs actives (fetchée le 17/07/2026) ne contient **aucun** endpoint static-data. **Dépréciation stable depuis ~8 ans, aucun signe de réintroduction.**

> **Conclusion** : le choix de Data Dragon comme source de vérité unique n'est pas un choix par défaut — c'est **le seul choix conforme**, et il est **exempté des rate limits**. L'architecture du projet est alignée sur la doctrine officielle de Riot.

### 6.2 CommunityDragon — la dépendance grise

| Point | Constat |
|---|---|
| **URL** | `raw.communitydragon.org` (CDN brut), `cdn.communitydragon.org` (**dépréciation annoncée, non actée**), `www.communitydragon.org` (doc), `github.com/CommunityDragon`. Actif depuis 2017, outillage **CDTB**. |
| **Apport vs DDragon** | ✅ **Chromas** (couleurs hex + images par chroma — vs simple booléen), assets bruts (textures, portraits, bordures, émotes), previews vidéo de sorts, ward skins, **avance PBE** (`/pbe/`). |
| **Exclusions déclarées** | ❌ **voice lines audio** (explicite), ❌ **prix boutique**, ❌ **noms officiels des chromas** (issue GitHub ouverte confirmant le manque). |
| **Multi-version** | ✅ Dossiers continus par patch de **`7.1` (22/07/2019) à `16.14` (15/07/2026)** sans trou visible sur 7 ans, + `/latest/` et `/pbe/`. |
| **Fraîcheur** | ⭐ **Excellente** — patch le plus récent daté **2 jours avant la recherche** ; dépôts `CDTB`/`Data` poussés **la veille** (16/07/2026) ; `status.live.txt` et `status.pbe.txt` « running » le jour même. Cadence bihebdomadaire respectée depuis ≥2019. |
| **Gouvernance** | Collectif bénévole (Discord + PR GitHub, **pas de mainteneur individuel identifiable**), financé par **Patreon**. **Appel de fonds actif constaté** (matériel serveur vieillissant). |
| **⚠️ Statut légal** | Auto-déclaration : créé « *under Riot Games' "Legal Jibber Jabber" policy [...] Riot Games does not endorse or sponsor this project* », se décrit lui-même « *within a gray area* ». **Sa prétention d'avoir été « acknowledged by Riot » est une auto-déclaration non corroborée — aucune mention de CommunityDragon trouvée sur un domaine officiel Riot.** |

> **⚠️ Risque à documenter pour le projet** : **CommunityDragon n'apparaît dans AUCUNE documentation officielle Riot** — ni autorisation, ni restriction. Contrairement à Data Dragon, qui est **nommément listée** comme asset sanctionné. C'est une dépendance en **zone grise structurelle**, bénévole, sans SLA, avec une infra vieillissante. Le projet en dépend **uniquement pour les chromas**. **Recommandation : documenter cette dépendance comme limitation connue plutôt que comme droit acquis**, et prévoir un dégradé propre (chromas absents ≠ page cassée).

### 6.3 Meraki Analytics — le voisin qui ralentit

- **URL** : `cdn.merakianalytics.com/riot/lol/` (CDN JSON/msgpack). `merakianalytics.com` répond mais **page vide** (parquée).
- **Périmètre volontairement limité à champions + items** — citation du README `lolstaticdata` : « *Data other than that for champions and items should be covered by the data that Riot provides, or by the CDragon project* ».
- **⚠️ Particularité méthodologique majeure** : les données de compétences sont **reconstruites en parsant le wiki Fandom**, pas les fichiers client Riot. Les mainteneurs jugent DDragon « *inaccurate* » et CDragon « *incredibly complex and cryptic* ». **Conséquence : la qualité dépend de la fraîcheur d'un wiki communautaire lui-même obsolète** (cf. §7.2).
- **Apport unique** : `attributeRatings`, multiplicateurs ARAM, **cooldowns en courbe continue**, et surtout **`championrates.json`** (pick rates par patch, **absent partout ailleurs**).
- **⚠️ Fraîcheur mitigée** : `items.json` daté 11/02/2026, mais **`champions.json` (le plus consulté) stagne depuis août 2025** — ~11,5 mois de retard. Code source `lolstaticdata` : dernier commit 12/11/2025. `cassiopeia` (Python) plus vivant : 05/02/2026. `orianna` (Java) **quasi à l'abandon** : 08/12/2023.

### 6.4 Bibliothèques et wrappers — l'écosystème dev

| Bibliothèque | Langage | Statut | Dernière activité | ★ |
|---|---|---|---|---|
| Cassiopeia | Python | Active | v5.1.2 (12/05/2024) | 582 |
| Riot-Watcher | Python | Active | v3.3.1 (08/03/2025) | 558 |
| orianna | Java | **Ralentie** (rc9 depuis 3 ans) | 06/08/2023 | 195 |
| riven | Rust | Active | crates.io 2.25.0 | 116 |
| camille | **C#** *(pas Python/JS)* | Incertain | non trouvé | 104 |
| golio | Go | Active, **module `datadragon` dédié** | v1.1.0 (18/06/2025) | 90 |
| shieldbow | TypeScript | ⚠️ **conflit non résolu** *(voir ci-dessous)* | v2.1.1 (29/06/2023) | 32 |
| twisted-fate | Python — **LoR seulement, pas LoL** | Quasi à l'arrêt | PyPI 0.1.5 | 14 |

**⚠️ Conflit non résolu (shieldbow)** : un fetch direct rapporte « *archived by the owner on Apr 13, 2026* » ; la vérification indépendante (bloquée par 429) a trouvé des signaux contraires (releases récentes, doc live, branche v3 WIP). **À vérifier manuellement avant toute décision.**

**Libs Data-Dragon-only** : npm `ddragon` est **déprécié** (VÉRIFIÉ). `@lolmath/ddragon` existe, version incertaine.

**⭐ Écosystème PHP/Composer — directement pertinent (Symfony)** :

| Package | Rôle | Dernière synchro | Traction |
|---|---|---|---|
| `dolejska-daniel/riot-api` | Metapackage PHP 8 | v5.0.0, 11/05/2025 | **113 ★, 24 009 installs** |
| `dolejska-daniel/riot-api-datadragon` | Wrapper DDragon dédié | 20/12/2024 | — |
| `spiregg/riot-api-league` | Fork actif | v3.1.2, 27/06/2025 | 863 installs |
| **`spiregg/riot-api-datadragon`** | Wrapper DDragon (fork) | **25/01/2026 — l'activité PHP la plus récente trouvée** | — |
| `zeggriim/riot-api-*` | Fork tertiaire | 09/01/2026 | 7 installs, 0 ★ |

Le reste (`elogank/php-lol-api`, `lpphan/riot-api`, `strebl/laravel-league-api`) est **abandonné depuis 2014-2016**.

> **Note pour le projet** : `AbstractManager` + `GoFetcherClient` réimplémentent en propre ce que `spiregg/riot-api-datadragon` fournit. **Ce n'est pas une erreur** — aucune de ces libs ne gère l'egress via passerelle, ni le stockage content-addressed, ni l'ingestion différée/SSE. Le choix maison reste justifié ; mais leur existence est utile comme **oracle de test** (comparer un parsing de dataset à une implémentation tierce).

**Projets open source comparables** : `meraki-analytics/lolstaticdata` (99★, actif), `noxelisdev/LoL_DDragon` (98-100★, MàJ quotidienne — **mais dépôt de fichiers sans UI**), `pacexy/poro` (29★, agrégateur Leaguepedia+ddragon+CDragon — **lib, pas UI**), `icepick4/league-viewer` (2★, mono-langue, sans historique), `kade-robertson/ddragon` (Rust), `timfernix/tierlist`, `craftersmine/League.CommunityDragon`. Liste curatée de référence : **[`CommunityDragon/awesome-league`](https://github.com/CommunityDragon/awesome-league)** (638★, CC0, MàJ 25/06/2026).

> **🎯 Constat capital** : **aucune entrée de `awesome-league` — la liste de référence officieuse de tout l'écosystème — ne combine multi-patch consultable + i18n complet + UI.** La niche est **non couverte par l'open source existant** au 17 juillet 2026.

---

## 7. Famille C — Wikis et encyclopédies

> **Verdict de famille** : c'est ici que se trouve le **voisin le plus proche** du projet. Et son plus gros défaut est exactement la force du projet.

### 7.1 wiki.leagueoflegends.com — le concurrent le plus sérieux

| Point | Constat |
|---|---|
| **URL** | `https://wiki.leagueoflegends.com/en-us/` — plateforme **MediaWiki 1.45.3** propre, opérée par **Weird Gloop** (même opérateur que les wikis RuneScape/OSRS). **Pas Fandom.** |
| **Modèle** | **Hybride** : hébergement/branding **payés par Riot Games depuis octobre 2024**, contenu et édition **100 % communautaires/bénévoles** (mêmes éditeurs qu'avant, autonomie éditoriale conservée). |
| **Périmètre** | Champions, objets, runes, skins/chromas/Eternals, lore/Universe, TFT, LoR, Wild Rift, Riftbound. |
| **⭐ Multi-version** | **Triple mécanisme — la source la plus profonde de toute cette recherche** : historique MediaWiki natif ; sous-page **`/Patch_history`** (changelog chiffré patch par patch — testé sur **Ezreal : remonte à V1.0.0.79, mars 2010**, jusqu'à V26.14) ; sous-page **`/History`** (« Past versions » — anciens kits pré-rework complets avec valeurs chiffrées, testé sur Karma). |
| **⚠️ Multi-langue** | **ANGLAIS UNIQUEMENT.** La table officielle `League_of_Legends_Wiki:Localization` (dernière édition 10/2024) recense 16 locales historiques ; **seules English et Русский sont marquées « UP TO DATE »** — le reste « OUT OF DATE » / « UNDER CONSTRUCTION » / « DELETED » (finnois supprimé pour inactivité). La promesse d'ouverture progressive de portails non-anglais (annonce octobre 2024) **ne s'est pas matérialisée 21 mois plus tard**. |
| **Pubs** | ✅ **Aucune** — confirmé explicitement par Riot : « *free from third-party hosting and obnoxious ads* ». |
| **Fraîcheur** | ⭐ Excellente : « *As of 24 June 2026, there are 173 champions... most recent release is Locke* » lu directement. Chaque champion tagué par son dernier patch modifié. |
| **API** | MediaWiki standard, **présomption forte** mais non testée avec succès (échecs `api.php`, probablement rate-limit outil). |

**⚠️ Limite structurelle partagée** : `/Patch_history` présente une **liste de deltas chronologiques**, **pas un snapshot pré-calculé** des stats complètes à un patch T. Reconstituer « Ezreal exactement au patch 9.1 » exige de **cumuler manuellement 30+ changements**.

> **🎯 C'est LE point d'entrée du projet.** Le wiki officiel gagne sur la profondeur et la richesse éditoriale. Il perd sur **deux axes que le projet gagne par construction** : **(1) le multilingue** — il est anglais seul, alors que le projet couvre 21 locales ; **(2) le snapshot** — il donne des deltas à cumuler, le projet donne l'état exact en un clic. **Ce ne sont pas des concurrents : ce sont des compléments.** Un lien croisé du projet vers `/Patch_history` (le « pourquoi ça a changé ») serait cohérent — le projet fournit le « qu'est-ce que c'était ».

### 7.2 Fandom — le zombie bien référencé

- **URL** : `leagueoflegends.fandom.com` — toujours en ligne, **aucune redirection** vers le wiki officiel : **coexistence des deux plateformes**. MediaWiki 1.43.8 (plus ancien que le 1.45.3 officiel).
- **⚠️ Contenu figé** : `List_of_champions` affiche littéralement « *As of 23 January 2025 there are currently **170** released champions, with the latest being **Mel*** » — soit **~17 mois de retard** sur le wiki officiel (173 champions, Locke, juin 2026). Un widget de rotation gratuite affiche même « **Last Checked: July 2021** ».
- **Pubs** : confirmées pour visiteurs non connectés. Témoignage d'un éditeur de longue date (The Game of Nerds, 15/11/2025) : « ***It feels like I'm trying to read through a slot machine*** ».
- **⚠️ Le vrai danger** : Fandom reste **bien référencé par les moteurs de recherche**. Un joueur peut y atterrir sans savoir qu'il lit du contenu vieux de 17 mois. C'est aussi **la seule implantation vivante en langues non-anglaises** — d'où le paradoxe : *les non-anglophones sont renvoyés vers la source la plus obsolète*.

**Inventaire multilingue consolidé** (source : table officielle `wiki.leagueoflegends.com/.../Localization`, VÉRIFIÉ) :

| Locale | Statut affiché |
|---|---|
| English | ✅ UP TO DATE *(wiki officiel)* |
| Русский | ✅ UP TO DATE *(Fandom)* |
| Deutsch, Español, **Français**, Italiano, Português (BR), Česko-Slovenská | ⚠️ **OUT OF DATE** |
| Polski, 中文 | ❓ divergence entre les deux tables trouvées — **non tranché** |
| Magyar, 日本語, Slovenčina, Türkçe, Tiếng Việt | 🚧 UNDER CONSTRUCTION |
| Suomi | ☠️ **DELETED** (auto-pruned pour inactivité) |

> **🎯 Sur 16 locales recensées par la source officielle elle-même, 2 sont créditées « à jour ».** Le français — marché historique fort de LoL — est explicitement **« OUT OF DATE »**. **C'est le gap le plus mesurable de tout ce document.**

### 7.3 Universe of League of Legends — le lore officiel

- **URL** : `universe.leagueoflegends.com` — actif, aucune redirection, domaine autonome. Bios champions, régions de Runeterra, comics, contenu « Alt Universe » (Star Guardian, Odyssey, K/DA), carte interactive (`map.leagueoflegends.com`). À jour (Locke intégré).
- **Multi-version** : ❌ **aucun mécanisme d'archive** — le lore retconné est simplement remplacé.
- **Multi-langue** : structurellement présent (`/fr_FR/` testé, intégralement traduit) mais liste exhaustive **non confirmée** (un sélecteur ~16 langues détecté une fois, non reproduit). Indexation Google quasi exclusivement `en_US`.
- **API** : ❌ **aucune API JSON publique documentée** pour le contenu éditorial.
- **⚠️ Ironie documentaire** : le FAQ support officiel qui décrit Universe ([support.riotgames.com](https://support.riotgames.com)) est **daté du 10 décembre 2019, non révisé depuis, et contredit le site actuel** (affirme l'absence de contenu « alternate » type Star Guardian, qui existe pourtant). **Le site est vivant ; sa documentation Riot est abandonnée depuis ~7 ans.**

### 7.4 Leaguepedia — à exclure du comparatif

`lol.fandom.com` — **VÉRIFIÉ : contenu 100 % esport** (LCK/LPL/LEC/LCS, transferts, calendriers, résultats), **zéro contenu champion/objet/lore générique**. Reste sur Fandom (pas de migration Weird Gloop, contrairement au wiki jeu). Une source tierce confirme explicitement la séparation : « *Leaguepedia covers only esports, while the official LoL wiki... is the correct source for game data* ». **Hors périmètre — à ne pas confondre avec `leagueoflegends.fandom.com`.**

---

## 8. Famille D — Explorateurs et viewers spécialisés

> **Verdict de famille** : **les vrais concurrents partiels.** Chacun fait bien **une** chose du périmètre du projet. Aucun ne fait l'ensemble. Aucun n'a d'historique. Presque aucun n'a de multilingue.

| Site | Périmètre | Multi-version | Multi-langue | Modèle | Fraîcheur |
|---|---|---|---|---|---|
| **[skinexplorer.lol](https://skinexplorer.lol)** | Splash arts uniquement (173 champions), regroupement par « Universe »/skinline via métadonnées CDragon | ❌ patch courant affiché, pas de navigation | ❌ EN seul | Gratuit, **open source** (`preyneyv/lol-skin-explorer`) | ✅ suit le PBE |
| **[lolskin.info](https://lolskin.info)** | **2 111 skins**, chromas, skinlines, rareté, prix, artistes, tri multi-critères, tague le PBE | ❌ suit le PBE, pas d'archive | ✅ **23 locales** | Gratuit, sponsoring/affilié | ✅ excellente |
| **[loldb.info](https://loldb.info)** | **Le plus complet des sites cosmétiques** : 172 champions, 1 920 skins, **6 865-6 954 chromas**, 218 couleurs, emotes, icônes, wards, items, Account Value Calculator, RP→USD, Mythic Shop Rotation, et **« Longest Skin Wait »** (skins jamais soldés depuis le plus longtemps — fonctionnalité originale) | ❌ saison/shop courant | ❌ `en_US` seul | Don (~60 $/mois d'hébergement **affichés publiquement**) | ✅ blog actif jusqu'à mi-juin 2026 |
| **[modelviewer.lol](https://modelviewer.lol)** (Khada) | **Viewer 3D** champions/skins/chromas/formes alternatives + TFT, téléchargement de modèles 3D, apps iOS/Android | ❌ suit le PBE | ❓ | Freemium — **pubs display confirmées** + Ko-Fi/Patreon | ⭐ exceptionnelle (supporters datés du **16/07/2026**, la veille) |
| **[heimerdinger.lol](https://heimerdinger.lol)** | Champions/skins/assets/rotation boutique | ❌ aucun sélecteur | ❌ EN seul (`og:locale: en`) | Gratuit, **open source** (`rico-vz/HeimerdingerLoL`) | ✅ « *updated roughly every 12h* », suit le PBE |
| **[lol-db.com](https://lol-db.com)** | Grand tableau de données brutes (« DataTables »), **comparaison side-by-side de 2 langues** | ❌ **live/PBE seulement** — aucune archive **malgré le nom** | ✅ **~48 locales détectées dans le DOM** — le record | Gratuit | ⚠️ **pop-ups/redirections publicitaires agressifs** constatés en navigation live (nouveaux onglets non sollicités). Tooltips **non résolus** : affiche `@PHealingRatio*100@%` tel quel |
| **Clean Cuts** (`blossomishymae.github.io/clean-cuts/`) | Non précisé (SPA) | ❓ | ❓ | Gratuit, hobbyiste | ❓ |

### 8.1 Le cas `lol-db.com` — le miroir déformant du projet

C'est le site le plus proche du projet **en intention**, et le plus instructif **en contre-exemple** :

| | lol-db.com | LeagueOfDataBase |
|---|---|---|
| Multi-langue | ✅ **~48 locales** *(record du secteur)* | ✅ 21 locales |
| Multi-version | ❌ **live/PBE seulement** | ✅ **~494 versions** |
| UI | ❌ grille brute type DataTables | ✅ design system Hextech dédié |
| Tooltips | ❌ **non résolus** (`@PHealingRatio*100@%`) | ✅ rendus |
| Publicité | ❌ **pop-ups, redirections non sollicitées** | ✅ zéro |

> **Il a le nom, il a les langues, il n'a rien d'autre.** C'est la preuve que le multilingue seul ne suffit pas — et que le créneau est **techniquement occupé, qualitativement vacant**.

### 8.2 ⚠️ Zone à éviter — les sites skins à modèle douteux

**`lolskinshop.com`, `lolskinstore.com`, `lolskinsale.com`, `lol-skins.com`, `divineskins.gg`** : catalogues skins/chromas doublés d'une **boutique de vente de comptes smurfs, de codes de skins rares et de boosting ELO rémunéré**, ou de distribution d'outils de modification client (« ModSkin »). **Pratiques violant explicitement les ToS de Riot** (anti-trading de comptes / boosting). Réputation Trustpilot très dégradée (~2,0/5). Fraîcheur incohérente (bannière « 2025 » en pleine 2026, carrousel « nouveautés » montrant des skins de 2023).

**⚠️ Piège de nommage à connaître** : cluster de domaines quasi-homonymes de natures radicalement différentes —

- `lolskin.info` ✅ légitime, excellent
- `lolskin.net` ☠️ **mort** (fetch vide ×2 ; historiquement un site de **mods**, pas une base de données)
- `skins.gg` ☠️ **mort** (redirige vers une page GoDaddy « domaine à vendre »)
- `lolskin.one/.fit/.cv`, `skinlolchanger.com`, `divineskins.gg` ⚠️ sites de **mods** répliquant visuellement des skins payants
- `lolskinshop.com` / `lolskinstore.com` 🚫 **revendeurs de comptes**

### 8.3 Datamining PBE — un créneau à fort taux d'épuisement

- **⚠️ Surrender at 20** (référence historique depuis 2011) est **VÉRIFIÉ MORT depuis le 14 novembre 2022** — arrêt confirmé par les tweets des auteurs eux-mêmes (**épuisement/burnout**) et par deux fetches montrant un contenu figé à nov. 2022.
- **Successeurs identifiés** (INFÉRENCE, indices indirects solides) : **JungleDiff** (né explicitement en réaction à l'arrêt de S@20, posts datés jusqu'au 14/07/2026) et **SurrenderAt15** (hommage au nom, détail par chroma).
- Délai leak → sortie officielle : **2 à 4 semaines** (exemple croisé : ~3 semaines pour le patch 26.14).

> **Recommandation** : l'architecture Data Dragon (données confirmées/live uniquement) **exclut structurellement** ce registre. **À documenter comme un choix assumé, pas comme un angle mort accidentel.** La mort de Surrender at 20 par burnout montre que c'est un créneau à coût humain élevé et récurrent.

---

## 9. Famille E — Outils de niche

> **Verdict de famille** : c'est ici que se trouvent les **meilleures idées à récupérer** — et la preuve que la demande d'historique existe.

### 9.1 ⭐ Comparateurs de patch — la trouvaille la plus actionnable

| Outil | Détail |
|---|---|
| **[patchdelta.gg](https://patchdelta.gg)** | **VÉRIFIÉ indépendamment par deux méthodes distinctes** (WebFetch + navigateur Chrome réel, résultats concordants) : **391 patchs suivis depuis 2009**, **18 215 modifications de stats indexées pour LoL**, **calcul du delta net cumulé entre deux patchs choisis librement** (code couleur buff/nerf orienté joueur), pages par champion, à jour au 26.14. Gratuit, sans pub. **⚠️ Limite : UI générique multi-jeux** — pas d'icônes de sorts, **pas de reconstruction de tooltip complet**, pas d'intégration éditoriale. |
| **[riftpatchnotes.com](https://riftpatchnotes.com/lol)** | « **405 patches and counting** depuis 2009 », champions ET objets, page par champion listant tous les changements avec **valeurs exactes avant/après** (testé sur Ezreal : **96 patchs** tracés depuis le 1.0.0.79 du 16/03/2010). Fonctionnalités « Champions & Items Explorer » et « **Since** » (comparateur pour joueur revenant après une pause — bonne idée UX). UI moderne type SaaS, gratuit. **❌ Anglais uniquement.** |
| **Ddragon Patch Diff** (`patchdiff.lolmath.net`) | Diff **JSON brut**, développeur-only. |
| **`patchdiff.lol`** | ☠️ **Inaccessible** sur 4 tentatives via 2 méthodes — probablement mort/jamais déployé malgré son indexation Google. *(Le nom est libre côté produit.)* |
| **lolstats.gg** (onglet patch-notes) | Style before/after coloré, mais **historique très court** (4 patchs pour un champion testé). |

> **🎯 Constat central, confirmé indépendamment par deux agents** : **aucun outil ne combine calcul de delta + reconstruction visuelle du tooltip + intégration dans une fiche champion complète.** `patchdelta.gg` **prouve la demande et la faisabilité** (18 215 deltas indexés, sans pub, maintenu) mais reste un outil **générique, autonome, anglophone**. Et **ni op.gg ni u.gg n'ont de fonctionnalité d'historique de patch par champion**.

### 9.2 Calculateurs de dégâts — un marché plein de zombies

**Piège documenté** : plusieurs outils **bien référencés sur Google sont abandonnés mais toujours visibles**.

| Actifs (patch 26.14 vérifié) | Zombies confirmés |
|---|---|
| **LoL Math** (`lolmath.net`) — gratuit, changelog GitLab jusqu'au **24/06/2026** | **calc.gg** — figé au patch **11.12.1 (~2021)**, roster s'arrêtant **avant Gwen/Rell/Viego**. *Reste le plus complet fonctionnellement du créneau — et il est mort.* |
| **statcheck.lol** — gratuit, **runes explicitement non implémentées** (dixit le dev) | **LoLSolved** — figé au patch **14.10 (~mi-2024)** |
| **LoL Alchemy Lab** (`lolalchemylab.com`) — **le plus complet** : EHP/DPS/TTK, optimiseur, comparateur d'items, carte interactive, **sans pub** | **teryd.net** — patch **4.18 (2014)** |
| **GamerStation**, **LoLSim** (`lolsimulator.com`, implémentation par sort) | **Theorycraftr.com** — salué par PC Gamer en 2016, **disparu** |

**Comparateurs d'objets** : `item.lol` (gold efficiency calculator, méthodologie ML ou poids fixes), **LoL Alchemy Lab** (fonctionnalité « **Real impact** » — recalcul DPS/EHP avec/sans un item), `TutorLoL` (bugs auto-déclarés), `CalculatorsUniverse` (s'auto-déclare « Season 2025/Patch 14.x » — **retard assumé par le site lui-même**).

**⚠️ Scaling / theorycrafting** : le wiki officiel documente exhaustivement les formules de croissance **en tableaux statiques, sans aucune interactivité**. **Constat négatif explicite : aucun des 4 grands généralistes (op.gg / u.gg / lolalytics / mobalytics) n'a de courbe de scaling interactive par niveau.** La fonctionnalité « Power Spikes » de LoL Alchemy Lab semble la plus proche mais **n'a pas pu être vérifiée** (rate-limit).

> **Note projet** : l'îlot `StatScaler` existant est déjà sur ce terrain. C'est un créneau **où les quatre leaders sont absents**.

### 9.3 Simulateurs de draft

| Outil | Statut |
|---|---|
| **DraftGap** (`draftgap.com`) | ⭐ **Seule preuve tangible et datée de maintenance active** : release **v3.2.1 le 1er février 2026**, 61 releases, **open source** |
| **Drafter.lol** | Fearless/Ironman/First Selection, UI stream/OBS, gratuit |
| **DRAFT VISION** (`loldraftvision.fun`) | Draft + Battle Mode + Map Tool + Tier List Maker, **bilingue EN/KO**, patch 16.13.1, dons PayPal |
| **uDrafter** (`udrafter.eu`) | « Phase de correction » unique, gratuit sans compte, « © 2026 » |
| **ProComps.gg** | App Overwolf **300k+ téléchargements**, freemium+pub — mais **blog à l'arrêt depuis nov. 2024** |
| **draftlol.dawe.gg** | Référence historique citée par les autres outils. **Page 100 % JS, non auditable en fetch** — à rouvrir manuellement |
| **LoLDraftAI** (`loldraftai.com`) | Revendique **65,6 % de précision vs 56,5 % pour DraftGap** — ⚠️ **chiffre auto-déclaré, non vérifié indépendamment** |
| **ProDraft** (`prodraft.leagueoflegends.com`) | Legacy, sous-domaine officiel, UI datée |

### 9.4 Outils pédagogiques — le gap le plus béant du secteur

- Le « How to Play » officiel de Riot reste **superficiel** (VÉRIFIÉ).
- Écosystème tiers **fragmenté et mono-mécanique** : **LoL Dodge Game** (dodge/skillshots/last-hit), **LastHit.net** / **Smiterino** (entraîneurs de smite — **la sous-mécanique la mieux servie**, ce qui en dit long), **MOBA Trainer** (`mobatrainer.com` — puzzles macro par coachs LEC/ERL, ~9,99 €/mois, sponsorisé par le compte LPL English — **INFÉRENCE, à revérifier**), **Doran's Lab** (jungle pathing — **figé depuis mai 2019**, projet universitaire NC State).
- **⚠️ Constat négatif explicite et notable** : **zéro simulateur interactif trouvé pour le wave management ou le trading en lane** — deux mécaniques fondamentales. Seulement des guides texte.

### 9.5 TFT et modes annexes — déjà bien servis

**Contexte** : Set actuel = **Set 17 « Space Gods »**, patch 17.7 (14/07/2026). Set 18 « Enchanted Wilds » en PBE le **28/07/2026**.

| Créneau | État |
|---|---|
| **TFT** | Bien servi : `tactics.tools` (dev solo, Patreon), `metatft.com`, `lolchess.gg` (PlayXP), `TFTactics.gg`, `TFTAcademy` (curatée par Dishsoap + Frodan), `legends.tools` (bêta de refonte de tactics.tools) |
| **Wild Rift** | Mature : **WildRiftFire** (140+ champions, réseau M.O.B.A. Network), **WR-META** (ex-« Wild Rift Wiki », Patreon freemium) |
| **ARAM** | **La catégorie la mieux couverte des modes annexes.** `aramgg.com` (ARAM:Mayhem, patch 26.14, **2,7 M parties échantillonnées**), `arammayhem.com` (ARAM:Mayhem + classique + Arena), `aram.zone` (⚠️ anomalie : affiche « 16.14 » mais assets ddragon « 16.7.1 ») |
| **Arena** | Plus jeune : **Arena Tracker**, **Arena Sweats** (classement via algorithme **OpenSkill**, pour pallier le retrait du ranked officiel par Riot) — non vérifiés directement |

**⭐ Signal réglementaire à retenir** : `aramgg.com` a **retiré les winrates bruts de sa page d'accueil** « *following feedback from Riot's Community Tech team* ». **Preuve d'une interaction directe Riot ↔ site tiers sur l'affichage de stats dérivées** — argument supplémentaire pour rester sur les données statiques.

---

## 10. Famille F — Esport (marché verrouillé)

> **Verdict de famille** : **à ne pas adresser.** Marché consolidé et verrouillé B2B. Cette section existe pour documenter la décision de ne pas y aller.

- **⭐ GRID (`grid.gg`)** — **le fait structurant** : `bayesesports.com` **redirige aujourd'hui vers `grid.gg`** (canonical confirmé). **Bayes Esports en insolvabilité en 2025** (tribunal de Charlottenburg/Berlin, « *illiquid and over-indebted* » au 01/08/2025) ; **GRID en a récupéré les actifs IP**. **VÉRIFIÉ sur `riotesportsdata.com` (portail officiel Riot)** : GRID est le **partenaire data exclusif mondial de Riot** pour LoL Esports + VALORANT depuis un accord du **30 novembre 2023** (Riot aurait pris une participation au capital — INFÉRENCE, 3 sources concordantes). Modèle : **gratuit pour équipes pro, payant sur devis** pour betting/fantasy/médias. Clients confirmés : bet365, Pinnacle, Kambi, PrizePicks, DAZN, Genius Sports, GG.BET. Programme « **Open Access** » gratuit **ne couvre PAS encore LoL** (CS2/Dota2 seulement).
- **lolesports.com / API officielle** — **aucune API publique documentée**. L'ancienne API communautaire (`api.lolesports.com`) est **explicitement abandonnée depuis 2020**, confirmé par citation directe de **Tim Sevenhuysen** (fondateur d'Oracle's Elixir) : « *The lolesports API is no longer being maintained as of 2020[…] for non-commercial/community projects **there is no programmatic solution at the moment*** ».
- **Oracle's Elixir** (`oracleselixir.com`) — Tim Sevenhuysen a **rejoint GRID en 2024** ; le site fonctionne désormais « *powered by GRID's official data platform* ». Datasets CSV gratuits non-commerciaux, 2014→aujourd'hui. ⚠️ Caveat 2026 : changement de schéma CSV + erreurs possibles de recensement de drafts liées aux règles **Fearless Draft**.
- **gol.gg** — solo dev « Bynjee » depuis 2014. **Patreon 3 paliers : 1 €/mois (sans pub), 5 €/mois (betting), 9 €/mois (analyste)**. ⚠️ **Alerte fraîcheur** : bandeau sur 3 pages — « *stats are not updated since 4th April because of a bug with Riot Match History* ». Nuance : les matchs individuels EWC 2026 sont à jour, mais 2 widgets agrégés reviennent vides — le bug touche des agrégats, pas l'ingestion brute.
- **Autres** : Leaguepedia (§7.4), Trackingthepros (actif, « © 2026 Beckstar LLC »), esports.op.gg, probuilds.net (Swift Media, comme Blitz), probuildstats.com (Outplayed/U.GG), **lolpros.gg** — ⚠️ **correction** : ce n'est **pas** un site de builds, c'est un **traqueur de comptes solo queue et d'historique d'équipe des pros** (type « smurf finder »), don PayPal.

> **🎯 Conclusion de famille** : **aucune donnée esport LoL exhaustive n'est accessible sans passer soit par du scraping communautaire toléré informellement, soit par une négociation B2B payante avec GRID.** Ce n'est pas un terrain jouable pour un projet non commercial. **Décision : ne pas adresser.**

---

## 11. Consolidation capitalistique du marché

**C'est le résultat le plus contre-intuitif de cette recherche.** Le marché *semble* fragmenté (une quinzaine de marques). Il est en réalité **contrôlé par quatre groupes, et OP.GG est le seul grand resté indépendant**.

| Groupe | Marques possédées | Santé |
|---|---|---|
| **Indépendant (Corée)** | **OP.GG** + OGN | ✅ Leader, acquéreur net |
| **Enthusiast Gaming** (TSX : EGLX) | **U.GG**, probuildstats.com, Icy Veins, Fantasy Football Scout, The Sims Resource, Addicting Games, Pocket Gamer | 🔴 **Penny stock — action à 0,045 CAD, cap. 11,14 M CAD, 80 employés** (09/07/2026) |
| **ESL FACEIT Group** → Savvy Games Group → **PIF (Arabie Saoudite)** | **Mobalytics** | 🟡 Fonds souverain, opère « standalone » |
| **M.O.B.A. Network AB** (Nasdaq First North) | **Porofessor**, **League of Graphs**, **Mobafire**, **CounterStats**, WildRiftFire, RuneterraFire, SmiteFire, DOTAFire, HeroesFire, LeagueSpy.net *(Brésil)* | 🔴 **-97,6 % sur un an, cotation SUSPENDUE depuis le 01/01/2026**, cession de « Union For Gamers ». Repositionnement 21/05/2026 : **les actifs LoL/TFT sont le cœur conservé**, pas en cession |
| **Swift Media Entertainment** (holding TSM / Andy Dinh) | **Blitz.gg**, **ProBuilds.net**, héritage **Champion.gg**, « LoL Counter » | 🟡 |
| **DAK.GG / PlayXP Inc.** (Corée) | lolchess.gg, dak.gg, poro.gg, maple.gg | 🟡 |
| **Outplayed Inc.** *(filiale Enthusiast)* | u.gg, probuildstats.com | *cf. Enthusiast* |
| Réseau affilié non nommé | lolskin.info, loldb.info, kkmet.com, hexfuser.com | 🟡 |

> **🎯 Deux des quatre groupes de contrôle traversent une détresse financière sévère et documentée publiquement.** Cela signifie : (1) le marché stats est **moins solide qu'il n'y paraît** ; (2) l'agrégation publicitaire massive **ne garantit pas la pérennité** ; (3) **la survie d'un projet non commercial et à faible coût opérationnel n'est pas un handicap — c'est une résilience.** MinIO + Data Dragon exemptée de rate limits + zéro base de données = un coût marginal proche de zéro là où les concurrents brûlent des levées.

**Ownership map — pièges à connaître** :

- **`poro.gg` ≠ Porofessor** (deux entités distinctes malgré le nom).
- **`probuilds.net` (Swift) ≠ `probuildstats.com` (Outplayed/U.GG)** — aucune preuve de rachat de probuilds.net par u.gg.
- **`pros.lol` et `xdx.gg` sont des satellites de Lolalytics** (CDN/assets partagés).
- **`rft.gg` est le spin-off esport de DPM.LOL**, à ne pas confondre avec `riftfeed.gg` ni `riftdaily.com`.
- Homonymes hors gaming rencontrés : « Blitz » (blitzgg.com organisateur de tournois, logiciel IAM russe, Blitzy.com IA). Aucun outil LoL nommé « KeyForge » n'existe.

---

## 12. Contraintes Riot — ce qui borne le terrain de jeu

> ⚠️ **Ceci n'est pas un avis juridique.** Faits sourcés, à valider par un juriste avant toute décision de conformité.

### 12.1 Rate limits et clés API

| Clé | Limite | Confiance |
|---|---|---|
| **Development** | Expire **24 h** ; limite alignée sur Personal | Expiration VÉRIFIÉE ; le chiffre vient d'une annonce 2017, non retrouvé littéralement sur la page actuelle |
| **Personal** | **20 req/1 s, 100 req/2 min**, par région. « *You may not run your application for public consumption* » | ✅ VÉRIFIÉ (double confirmation) |
| **Production** | Départ **500 req/10 s, 30 000 req/10 min**, extensible | ⚠️ Le second chiffre n'a été corroboré qu'à ~50 % — **à re-confirmer avant implémentation** |
| **⭐ Data Dragon** | ⭐ **« *calls to the static data API do not count against the application rate limit* » — EXEMPTÉE** | ✅ VÉRIFIÉ |

**Architecture à 3 strates** (application / method / service), headers `X-App-Rate-Limit(-Count)`, `X-Method-Rate-Limit(-Count)`, `Retry-After`. **Aucun tableau exhaustif à jour des limites par méthode n'existe** pour match-v5/league-v4 — le seul tableau détaillé date de l'ère match-v3 (2019).

**⚠️ Incohérence documentaire** : les API Terms de **2013** (toujours le document légal en vigueur, `developer.riotgames.com/terms`) citent **10 appels/10 s** pour la Development Key — **jamais mis à jour** pour refléter le système actuel à 3 niveaux.

**Process Production Key** : formulaire « Register Product » → **vérification de domaine obligatoire** → prototype fonctionnel exigé (« *we are unable to accept **Github repositories and source code in lieu of a functioning application/site*** ») → délai « *typically weekly, up to three weeks* », **sans SLA**. Une clé = un produit = un jeu. **Clause explicite : obtenir une clé de production n'est PAS une reconnaissance officielle.**

### 12.2 Legal Jibber Jabber et monétisation

**Document** : [riotgames.com/en/legal](https://www.riotgames.com/en/legal), « Last Updated: **August 2018** » — VÉRIFIÉ deux fois.

- Licence « ***non-commercial [...] community use*** ».
- **Disclaimer obligatoire** si diffusion publique : « *[Titre] was created under Riot Games' "Legal Jibber Jabber" policy using assets owned by Riot Games. Riot Games does not endorse or sponsor this project.* »
- **Monétisation interdite par défaut**, sauf 3 exceptions : publicité passive (plus dans les propriétés Riot depuis mai 2025) ; dons/abonnements sur stream ; **projet enregistré/approuvé avec clé API dédiée** (abonnements, dons, crowdfunding autorisés). **Palier gratuit obligatoire.**
- **Interdits explicites** : « *No cryptocurrencies, blockchain, or NFTs whatsoever* » ; gambling/paris ; « *Products cannot closely resemble Riot's games or products in style or function* » ; « *Products should use supported services from Riot Games for data ingestion* » (**la règle anti-scraping la plus proche trouvée**).

### 12.3 ⚠️ Hotlinking et statut des sources — points sensibles pour le projet

| Point | Constat |
|---|---|
| **Data Dragon** | **Nommément listée** comme asset sanctionné pour le développement/marketing d'un produit tiers. **Mais aucune clause trouvée n'autorise ni n'interdit explicitement le hotlink direct** (vs auto-hébergement). → **Tolérance implicite, pas autorisation écrite explicite.** |
| **CommunityDragon** | **N'apparaît dans AUCUNE liste officielle d'assets sanctionnés.** Statut **plus fragile** que Data Dragon — dépend uniquement de la licence générale Legal Jibber Jabber. |
| **Liste de « partenaires officiels »** | ❌ **N'existe pas.** Le support déclare explicitement : « *Riot can't provide a full list of which third-party applications are safe to use* ». Le seul partenaire officiel réel est **GRID** — et c'est pour la donnée esport broadcast, pas les stats joueur. |

> **🎯 Traduction pour le projet** : les pratiques actuelles (hotlink DDragon des splashs, CommunityDragon pour les chromas) sont **cohérentes avec une zone tolérée, non contractuellement garantie**. La licence **CC BY-NC 4.0** du projet est **parfaitement alignée** avec le « *non-commercial community use* » du Legal Jibber Jabber. **Recommandations** : (1) vérifier que le **disclaimer Legal Jibber Jabber exact** figure sur le site (pas seulement le README) ; (2) documenter la dépendance CommunityDragon comme limitation connue.

### 12.4 Précédents documentés

| Date | Fait |
|---|---|
| 2021 | **C&D contre Chronoshift** |
| Nov. 2018 | Champion.gg/ProBuilds absorbés par Swift → Blitz |
| Nov. 2022 | **Surrender at 20 s'arrête** (burnout des auteurs) |
| Oct. 2024 | **Riot officialise le LoL Wiki** (prend en charge l'hébergement, éditorial resté communautaire) |
| **13 mars 2025** | ⭐ **Riot interdit la fonctionnalité « Enemy Ultimate Timer »** dans toutes les apps tierces, sous peine de **désactivation de clé API** — réponse directe aux critiques ciblant Porofessor |
| Mars-mai 2025 | **Menace de coupure de clé API pour Blitz / Porofessor / Mobalytics** suite au changement de politique |
| Mai 2025 | Interdiction des **overlays « qui simulent une prise de décision »** ; fin de la pub tierce dans les propriétés Riot |
| 21 fév. 2025 | **NetEase interdit Blitz** sur Marvel Rivals (« *cheating software* ») |
| Annuel | Hackathon Riot × AWS « **Rift Rewind** » — **Riot encourage activement les projets tiers** sur les données de match |

> **🎯 Lecture** : **tous les précédents de sanction visent les overlays live et les données dynamiques.** Aucun ne vise une encyclopédie de données statiques. **Le périmètre du projet est structurellement le plus sûr de tout l'écosystème.** C'est un avantage stratégique rarement formulé : pendant que les leaders négocient leur survie réglementaire patch après patch, le projet est **hors de la ligne de tir par construction**.

**Discord officiel** : `discord.gg/riotgamesdevrel` — canal temps réel prioritaire pour annonces et dépréciations.

---

## 13. Positionnement de LeagueOfDataBase

### 13.1 Le test des 3 critères

Question posée à la recherche : **existe-t-il un acteur combinant UI moderne + multi-version profond + multi-langue étendu, sur les données statiques ?**

**Réponse : NON TROUVÉ**, sur ~25 requêtes croisées (anglais, français, chinois, coréen) et navigation live sur tous les candidats sérieux.

| Candidat | UI moderne | Multi-version | Multi-langue | Score |
|---|:---:|:---:|:---:|:---:|
| **LeagueOfDataBase** | ✅ | ✅ ~494 versions | ✅ 21 locales | **3/3** |
| wiki.leagueoflegends.com | 🟡 *(MediaWiki)* | ✅ *(le plus profond — 2010)* | ❌ **EN seul** | 2/3 · **et hors périmètre** *(officiel Riot, pas tiers ; son multilingue est délégué à Fandom, wikis séparés, pas de switcher unifié)* |
| riftpatchnotes.com | ✅ | ✅ 405 patchs | ❌ EN seul | **2/3** |
| patchdelta.gg | 🟡 *(générique multi-jeux)* | ✅ 391 patchs | ❌ EN seul | 1,5/3 |
| op.gg/lol/champions | ✅ | ❌ *(2 derniers patchs)* | ✅ 24 | 2/3 |
| lol-db.com | ❌ *(grille brute + pop-ups)* | ❌ *(live/PBE)* | ✅ ~48 | **1/3** |
| CommunityDragon | ❌ *(CDN brut)* | ✅ | 🟡 *(par fichier)* | Échoue **par conception** — pas un produit consultable |
| Data Dragon | ❌ *(CDN brut)* | ✅ | ✅ 28 | Échoue **par conception** |

> **Les deux profils les plus proches sont structurellement opposés et complémentaires** : riftpatchnotes.com a la profondeur + une UI propre mais reste anglophone ; lol-db.com a la couverture linguistique record mais zéro profondeur et une UX dégradée par la pub. **Aucune preuve d'un acteur ayant résolu les 3 dimensions à la fois n'a été trouvée.**

### 13.2 Forces réelles (et sous-exploitées)

| Force | Pourquoi c'est un avantage, pas juste une feature |
|---|---|
| **~494 versions consultables** | Aucun des 15 grands ne l'a. Le meilleur du marché (wiki, riftpatchnotes) ne donne que des **deltas à cumuler**. |
| **21 locales** | Le wiki officiel = EN seul. Le wiki FR = **« OUT OF DATE »** dixit la source officielle. Le n°2 mondial (U.GG) = EN seul. |
| **Data Dragon exemptée de rate limits** | Avantage architectural **structurel** : scalabilité sans négociation de clé, sans quota, sans dépendance à une approbation Riot. Aucun concurrent stats n'a ça — ils vivent tous sous rate limit. |
| **Zéro publicité, zéro dark pattern** | Friction n°1 unanime du secteur. **337 partenaires IAB** chez les « alternatives propres » supposées. |
| **Coût opérationnel marginal ~0** | Pas de base de données, dédup content-addressed, cache multi-niveaux. Deux des quatre groupes concurrents sont en détresse financière **malgré** des levées massives. |
| **Hors de la ligne de tir réglementaire** | Tous les précédents de sanction Riot visent les **overlays live** et le **dynamique**. Aucun ne vise le statique. |
| **CC BY-NC 4.0** | Exactement aligné sur le « *non-commercial community use* » du Legal Jibber Jabber. |
| **Chromas via CommunityDragon** | DDragon n'expose qu'un booléen. Le projet a déjà la donnée que 95 % du marché n'a pas. |

### 13.3 Faiblesses honnêtes

| Faiblesse | Réalité |
|---|---|
| **Aucune notoriété** | Les concurrents partent de 3 à 74 M visites/mois. DPM.LOL prouve qu'on peut percer — **mais par la distribution créateurs, pas par la technique.** |
| **Pas de données dynamiques** | Un joueur qui veut « la meilleure build » n'a aucune raison de venir. **C'est un choix, pas un manque — mais il faut l'assumer explicitement dans le discours produit.** |
| **⚠️ Numérotation de patch trompeuse** | Affiche `16.14` là où le joueur cherche `26.14`. **Bug UX réel, corrigeable en une itération.** |
| **Dépendance CommunityDragon en zone grise** | Bénévole, Patreon en appel de fonds, infra vieillissante, absente de toute doc officielle Riot. |
| **Hotlink splashs non versionnés** | ⚠️ DDragon **ne versionne PAS les splash arts par patch** (confirmé officiellement — 130 splashs mal associés au patch 11.1). **Conséquence directe : un champion consulté au patch 9.1 affichera le splash art d'aujourd'hui.** C'est une **incohérence visible** dans un produit qui vend l'exactitude historique. |
| **Latence DDragon post-patch** | MàJ manuelle Riot, « *not always immediate* », jusqu'à ~2 jours. Les sites stats sont à jour en 2 h. |
| **Pas d'API publique** | Alors que **c'est le deuxième angle mort du secteur** — cf. §14.2. |

---

## 14. Opportunités — où on a une carte à jouer

Classées par **ratio impact / effort**, en tenant compte de l'architecture existante.

### 14.1 ⭐⭐⭐ Le snapshot historique — la carte maîtresse

**Le gap** : personne ne sert d'état figé. Tout le monde sert des deltas.

| Acteur | Ce qu'il donne |
|---|---|
| wiki `/Patch_history` | Liste de ~96 deltas chronologiques à cumuler mentalement |
| riftpatchnotes.com | Idem, 405 patchs, mais en anglais |
| patchdelta.gg | Delta net cumulé entre 2 patchs — **le plus avancé**, mais UI générique, sans tooltip, sans fiche |
| op.gg / u.gg / lolalytics / mobalytics | ❌ **Rien** |
| **LeagueOfDataBase** | ⭐ **L'état exact, en un clic, dans 21 langues — par construction** |

**Pourquoi c'est faisable ici et nulle part ailleurs** : `data/{version}/{lang}/{type}.json` **est** un snapshot. Aucune reconstruction, aucun cumul, aucun parsing de patch notes. Les concurrents devraient rétro-ingénierer 15 ans de deltas ; le projet fait un `GET`.

**Ce qu'il manque pour le concrétiser** :

1. Un **sélecteur de patch mis en avant** (pas seulement dans le popover header) — c'est la feature, elle doit être visible.
2. Un **diff visuel A↔B** : deux versions côte à côte sur une fiche champion, avec surlignage des valeurs changées. `patchdelta.gg` prouve la demande (18 215 deltas indexés) ; le projet peut le faire **avec les tooltips rendus, les icônes de sorts, et dans 21 langues** — les trois choses qui manquent à patchdelta.
3. Une **entrée par le temps** : « Ce champion il y a 1 an / 3 ans / à sa sortie ». L'idée « **Since** » de riftpatchnotes (comparateur pour joueur revenant après une pause) est excellente et **transposable directement**.
4. **⚠️ Résoudre le splash art non versionné** — sinon le produit ment visuellement. Options : (a) ne pas afficher de splash sur les patchs anciens ; (b) mention explicite « visuel actuel » ; (c) ingérer les splashs par patch depuis CommunityDragon (qui, lui, versionne par dossier de patch depuis 7.1). **(c) est la seule vraie solution, et elle contredit le choix TTFB actuel — arbitrage à faire.**

> **Formulation produit** : *« Le seul endroit où voir League of Legends tel qu'il était. »* Aucun concurrent ne peut répondre à ça sans réécrire son architecture.

### 14.2 ⭐⭐⭐ L'API publique — l'angle mort du secteur entier

**Le gap** : **zéro API publique dans tout l'écosystème.**

- Lolalytics : « *we do hope in the future to release a public API* » — **aveu explicite**.
- Mobalytics, DPM.LOL : **scraping interdit par ToS**.
- Tracker.gg : a une API… **qui exclut LoL**.
- OP.GG : « *this data is not provided to third parties* » — **sauf** son serveur **MCP open source**, le seul point d'ouverture du marché.
- Conséquence directe et mesurable : **prolifération de scrapers non officiels** (`lolalytics-api` sur PyPI, `Zadag/simple-u.gg-api`, `khorn89/LolAlytics.py`) — **la demande est prouvée par le contournement**.

**Pourquoi le projet peut le faire** : il n'a **rien à protéger**. Ses données viennent d'une source publique exemptée de rate limits, sous licence CC BY-NC. **Exposer `GET /api/{version}/{lang}/champion/{id}` ne coûte rien et n'a aucun équivalent.**

**Deux formats à considérer** :

1. **REST classique** — ce que tout l'écosystème dev réclame (cf. la vitalité de `spiregg/riot-api-datadragon`, MàJ 25/01/2026).
2. **⭐ Serveur MCP** — OP.GG l'a fait en 2026 et c'est **son seul geste d'ouverture**. Un MCP « données statiques LoL multi-patch multi-langue » n'existe nulle part. Coût faible, différenciation forte, aligné sur l'époque.

> **Ce serait le premier open data de l'écosystème LoL au-dessus du niveau CDN brut.** Positionnement fort, coût quasi nul, et parfaitement cohérent avec CC BY-NC.

### 14.3 ⭐⭐ L'explorateur de chromas — angle mort confirmé

**Le gap, confirmé par deux agents indépendants** : **aucun outil mono-fonction « chromas-first » n'existe.**

| Candidat | Ce qu'il fait | Ce qui manque |
|---|---|---|
| **modelviewer.lol** (Khada) | Viewer 3D généraliste avec chromas | Pas de comparateur, chromas noyés dans un viewer 3D |
| **loldb.info/chromas** | **6 954 chromas, 218 couleurs**, règles d'acquisition | **Pas de comparateur visuel côte-à-côte**, EN seul |
| Data Dragon | ❌ **booléen seul** | tout |
| CommunityDragon | ✅ couleurs hex + images | ❌ **noms officiels absents** (issue GitHub ouverte) |

**Personne ne répond à** : *« je choisis un skin → je vois toutes ses teintes d'un coup d'œil, côte à côte »*.

Le projet a déjà `ChromaStrip` et la source CommunityDragon. **L'écart entre l'existant et un vrai comparateur est petit.** Et le choix documenté du projet — *label couleur dérivé de la teinte, honnête, pas un nom produit Riot* — est **la bonne réponse au problème que CommunityDragon documente lui-même** (noms officiels absents).

### 14.4 ⭐⭐ Le visualiseur de scaling interactif

**Constat négatif explicite** : **aucun des 4 grands généralistes (op.gg / u.gg / lolalytics / mobalytics) n'a de courbe de scaling interactive par niveau.** Le wiki officiel documente les formules **en tableaux statiques, sans interactivité**. Le seul candidat sérieux (« Power Spikes » de LoL Alchemy Lab) **n'a pas pu être vérifié**. Et le créneau des calculateurs est **jonché de zombies** : calc.gg (le plus complet fonctionnellement) est **figé au patch 11.12.1 depuis ~2021**, LoLSolved au 14.10, teryd.net au 4.18.

**L'îlot `StatScaler` du projet est déjà sur ce terrain.** Le prolonger (courbe par niveau, comparaison entre patchs, comparaison entre champions) exploite des données déjà servies. **Et le twist unique : personne ne peut faire "compare le scaling de Yasuo au patch 8.1 vs aujourd'hui" — le projet, si.**

### 14.5 ⭐⭐ Le positionnement « sans pub » — crédible, mais à prouver

**Le marché perçoit à tort** LeagueOfGraphs / Porofessor / Counterstats comme des alternatives « propres ». Ils appartiennent au **même réseau M.O.B.A. Network** et tournent sur la même ad-tech : **337 partenaires IAB confirmés** sur le bandeau cookies.

**Sites réellement vérifiés sans aucune pub** : LoL Alchemy Lab, LoL Math, statcheck.lol, shyv.net, item.lol, patchdelta.gg, wiki officiel, skinexplorer.lol, heimerdinger.lol. **Tous des projets indépendants sans notoriété virale documentée.**

**La pub est la friction n°1 unanime du secteur** — c'est *systématiquement* le premier argument de vente de chaque premium ($2,49 à $8/mois **juste pour ne pas voir de pub**). Le projet l'offre gratuitement.

> **⚠️ Mais** : « sans pub » est une **promesse invérifiable par l'utilisateur**. Elle doit être **prouvée, pas affirmée** — code open source, absence de bandeau cookies (les sites réellement propres n'en ont pas), pas de tiers dans le réseau. C'est un différenciateur **structurel** (le projet est CC BY-NC : il *ne peut pas* monétiser), pas juste une posture.

### 14.6 ⭐ La timeline lore ↔ data

**Gap confirmé** : **aucun outil ne fait le pont skin ↔ arc narratif ↔ date de sortie avec précision des deux côtés.**

- `Universe:Calendar` / `Universe:Timeline` du wiki officiel : liste générique, **sans granularité skin-par-skin**.
- **skinexplorer.lol** : regroupe par « Universe »/skinline via métadonnées CDragon, **sans éditorialisation narrative ni dates d'événements**.
- Universe officiel : **pas de versioning du lore**, pas d'API.

Le lien transmedia ↔ data le plus concret vérifié : **TFT Set 13 « Into the Arcane »** (20/11/2024 → 25/03/2025) et **16 skins Arcane sur 10 champions**. Faisable par **enrichissement de métadonnées existantes, sans production de contenu narratif original**. Effort modéré, différenciation réelle — mais **priorité inférieure** aux §14.1-14.3.

### 14.7 Ce qu'il ne faut PAS faire

| Créneau | Pourquoi s'abstenir |
|---|---|
| **Données esport / pro** | Marché **verrouillé B2B par GRID** (exclusif Riot depuis 2023, a absorbé Bayes Esports en 2025). Aucune donnée exhaustive sans scraping toléré ou contrat payant. **Pas un terrain jouable pour un projet non commercial.** |
| **Datamining PBE** | **Surrender at 20 est mort de burnout en nov. 2022** après 11 ans. Créneau à **coût humain élevé et récurrent**. L'architecture DDragon (données confirmées) **l'exclut structurellement** — à assumer comme choix, pas comme lacune. |
| **Tier lists / winrates** | u.gg et lolalytics sont **très matures**, sous rate limit, avec des équipes dédiées. Y aller = abandonner l'avantage « exempté de rate limits » et entrer dans la ligne de tir réglementaire. |
| **Overlays in-game** | **Tous les précédents de sanction Riot sont là** (mars 2025 : Enemy Ultimate Timer interdit ; mai 2025 : overlays « simulant une décision » interdits ; NetEase a banni Blitz). |
| **ARAM / Wild Rift / TFT** | Déjà bien servis (`aramgg.com` : 2,7 M parties échantillonnées ; WildRiftFire ; tactics.tools). |
| **Calculateurs de dégâts de base** | Marché fragmenté mais **occupé** par plusieurs actifs (LoL Alchemy Lab, LoL Math). *Sauf* l'angle historique — cf. §14.4. |

---

## 15. Analyse finale — ce que personne ne fait

> *Section demandée : au-delà du comparatif, ce que je vois que les gens ne font pas.*

### 1. Personne ne se souvient.

C'est **le** constat de cette recherche, et il est presque absurde une fois formulé.

Data Dragon conserve **494 versions**. J'ai vérifié `5.5.1` — mars 2015 — qui renvoie un JSON complet, valide, avec Dominion, Ascension et Poro King dedans. **Onze ans de mémoire, publique, gratuite, sans clé, et explicitement exemptée des rate limits par Riot.**

Personne ne l'expose.

Pas op.gg (24 langues, 47-74 M visites/mois). Pas u.gg. Pas Mobalytics. Pas Lolalytics. Pas Blitz. **Aucune des ~15 plateformes n'a de sélecteur d'archive fonctionnel.** Le pattern universel est « patch courant + delta vs précédent ». Metasrc a fait « League Classic » — une restauration en dur de la Season 3 — ce qui **prouve que la demande existe** et qu'un acteur commercial a jugé rentable d'investir dans une ère vieille de 13 ans. Mais il l'a fait **une fois, pour une ère**.

Et les deux acteurs qui *ont* l'historique — le wiki officiel (remonte à **mars 2010**) et riftpatchnotes.com (**405 patchs**) — le servent tous les deux en **deltas chronologiques à cumuler mentalement**. Reconstituer « Ezreal exactement au patch 9.1 » demande de lire et d'additionner 30+ changements. **À la main. En anglais.**

**Le secteur entier vit dans un présent perpétuel, alors que la mémoire est disponible en `GET`.**

### 2. Personne ne se souvient *dans votre langue*.

C'est le deuxième pli, et il est plus dur encore.

Le wiki officiel — la meilleure source de données statiques au monde, financée par Riot depuis octobre 2024, sans pub — est **anglais uniquement**. Ses 16 locales historiques sont restées sur Fandom, où **14 sur 16 sont marquées « OUT OF DATE », « UNDER CONSTRUCTION » ou « DELETED » par la source officielle elle-même**. Le français est explicitement **« OUT OF DATE »**. Le wiki FR affiche 170 champions quand il y en a 173.

Le n°2 mondial du secteur (U.GG) est **monolingue**. Le seul site qui a le multilingue record (~48 locales, `lol-db.com`) affiche des tooltips **non résolus** (`@PHealingRatio*100@%`) et ouvre des **pop-ups publicitaires non sollicités**.

Data Dragon, elle, sert **28 locales, pour les 494 versions, gratuitement, depuis toujours**.

**La donnée historique multilingue existe. Elle est publique. Elle est gratuite. Personne ne l'a jamais montrée à un humain.**

### 3. Personne n'ouvre.

Zéro API publique dans tout l'écosystème. Pas une. Lolalytics l'admet à voix haute (« *we do hope in the future...* »). Mobalytics et DPM.LOL **l'interdisent par ToS**. Tracker.gg a une API — **qui exclut LoL**, son propre jeu. Le seul geste d'ouverture du marché en 2026 est le serveur **MCP d'OP.GG**.

Et la demande est **prouvée par le contournement** : `lolalytics-api` sur PyPI, `Zadag/simple-u.gg-api`, `khorn89/LolAlytics.py` — des gens écrivent des scrapers parce qu'il n'y a pas de porte.

Tout le monde protège une donnée dérivée d'une source publique.

### 4. Ce que ces trois absences ont en commun.

Elles ne sont pas des oublis. **Ce sont des conséquences d'architecture.**

Un site de stats est une **machine à ingérer du dynamique** : match-v5, rate limits, pipelines, agrégats. Sa valeur est dans la fraîcheur. L'historique est un **coût** pour lui — du stockage mort, sans requête, sans pub à vendre. Son modèle (pub au pageview) récompense le trafic de masse sur « la meilleure build ce patch », pas la consultation contemplative de Ryze en 2015. Et son API, il la ferme parce que **sa donnée agrégée est son seul actif** : sans elle, il n'est qu'un front devant l'API de Riot.

**Aucun de ces trois arguments ne s'applique à LeagueOfDataBase.**

Le stockage est content-addressed et dédupliqué — un patch de plus coûte quasiment rien. Il n'y a pas de pub à optimiser. Il n'y a pas de rate limit à ménager (Data Dragon est **exemptée**). Il n'y a pas de donnée propriétaire à protéger — tout vient d'une source publique, sous CC BY-NC.

**Ce que le marché ne fait pas n'est pas ce qu'il n'a pas eu le temps de faire. C'est ce qu'il ne peut pas se permettre de faire.** Et c'est exactement ce que le projet peut faire **gratuitement**.

### 5. Le corollaire inconfortable.

Il faut le dire honnêtement : **si personne ne le fait, c'est peut-être aussi que la demande est faible.**

Le contre-argument existe, et il est solide :

- Metasrc a **investi dans « League Classic »** — un acteur publicitaire a jugé la Season 3 rentable.
- `riftpatchnotes.com` (405 patchs) et `patchdelta.gg` (**18 215 deltas indexés**) sont **maintenus, gratuits, sans pub** — deux personnes ont trouvé ça assez important pour le construire et le tenir à jour.
- La fonctionnalité « **Since** » de riftpatchnotes (« qu'est-ce qui a changé depuis mon départ ? ») cible un besoin **massif et récurrent** : le joueur qui revient après 1, 3, 5 ans. Il y en a des millions.
- Et le wiki officiel maintient `/Patch_history` **depuis 2010, bénévolement**, ce qui ne se fait pas sans lecteurs.

**La demande n'est pas absente. Elle est mal servie** — en anglais, en deltas, sans images, sans tooltips, dans des UI d'archive.

### 6. Ce qu'il reste à faire.

Le projet a déjà le plus dur : **l'architecture qui rend le reste trivial**. `data/{version}/{lang}/{type}.json` **est** la réponse. Ce qui manque n'est pas technique — c'est de **transformer une capacité en promesse** :

1. **Afficher `26.14`, pas `16.14`.** Aujourd'hui le site a l'air en retard de 10 versions. *(Une itération.)*
2. **Sortir le sélecteur de patch du popover.** C'est **la** feature, elle est cachée dans un header. Personne au monde ne l'a — elle doit être la première chose qu'on voit.
3. **Régler le splash art non versionné.** DDragon ne versionne pas les splashs (**confirmé officiellement** : 130 mal associés au patch 11.1). Un produit qui vend l'exactitude historique **ne peut pas afficher le visuel de 2026 sur une fiche de 2015**. C'est le seul endroit où le projet ment, et il faut le corriger ou le dire.
4. **Ouvrir une API** — REST et/ou MCP. Coût quasi nul, aucun équivalent, premier open data de l'écosystème au-dessus du CDN brut.
5. **Le diff A↔B sur la fiche champion.** `patchdelta.gg` a prouvé la demande sans avoir ni tooltips, ni icônes, ni langues. Le projet a les trois.

Et une leçon non technique, la plus importante : **DPM.LOL est passé de 0 à 3 M+ visites/mois en un an** sur un marché soi-disant saturé. Pas grâce à sa stack (Next.js sur Hetzner). **Grâce à Caedrel et Kameto.** La technique n'a jamais été le facteur limitant de ce marché — la distribution, si.

---

### En une phrase

> **Le secteur data LoL a construit quinze façons de regarder le présent et zéro façon de regarder le passé — alors que Riot conserve onze ans de mémoire, en 28 langues, gratuitement, sans limite de débit. LeagueOfDataBase est le seul projet dont l'architecture rend ce passé consultable en un clic. C'est la carte à jouer, et personne d'autre ne l'a en main.**

---

## 16. Limites de cette recherche

- **Trafic** : SimilarWeb / SEMrush / HypeStat / StatShow divergent d'un **facteur 2 à 6×** pour un même site. Traités partout comme ordres de grandeur. Jamais audités.
- **Rate-limit d'outil** : des HTTP 429 persistants ont empêché la vérification directe de plusieurs candidats secondaires — signalés INFÉRENCE au cas par cas. Notamment : `metatft.com` (SPA opaque), `draftlol.dawe.gg`, `op.gg/lol/skins`, Arena Tracker/Sweats, `mobatrainer.com`, « Power Spikes » de LoL Alchemy Lab.
- **Reddit** : largement inaccessible en fetch direct dans cet environnement. Le sentiment communautaire provient de sources qui *rapportent* des discussions, pas de threads lus.
- **Conflits non résolus** (signalés plutôt qu'arbitrés arbitrairement) :
  - Tarif premium OP.GG : **$3/mois** (member.op.gg) vs **$3,99/mois** (App Store) — deux sources primaires fetchées, divergentes.
  - Statut d'archivage de **shieldbow** — signaux contradictoires, 429 sur la re-vérification.
  - Rate limit exact d'une Production Key (**30 000/10 min** corroboré à ~50 % seulement).
  - Destination réelle de la redirection JS de **champion.gg** (nécessiterait un rendu navigateur).
  - Nombre de langues **Metasrc** — liste de ~18-19 apparue deux fois **sans source citable ni sélecteur observé dans le HTML** → écartée du statut VÉRIFIÉ (signal classique de résultat non fiable).
- **Financier** : montants de rachat et levées issus de presse spécialisée (Forbes, TechCrunch, PC Gamer, esportsinsider) ou de communiqués réglementaires. **Aucun bilan comptable audité consulté.**
- **Juridique** : le §12 n'est **pas un avis juridique**. À valider par un juriste avant toute décision de conformité.
- **Aucun chiffre n'a été inventé.** Toute donnée non confirmée est marquée INFÉRENCE ou NON TROUVÉ.

---

## Sources principales

**Officiel Riot** — [ddragon.leagueoflegends.com](https://ddragon.leagueoflegends.com/) · [developer.riotgames.com/docs/lol](https://developer.riotgames.com/docs/lol) · [developer.riotgames.com/apis](https://developer.riotgames.com/apis) · [riotgames.com/en/DevRel/riot-games-api-change-log](https://www.riotgames.com/en/DevRel/riot-games-api-change-log) · [riotgames.com/en/legal](https://www.riotgames.com/en/legal) · [developer.riotgames.com/policies/general](https://developer.riotgames.com/policies/general) · [wiki.leagueoflegends.com](https://wiki.leagueoflegends.com/en-us/) · [universe.leagueoflegends.com](https://universe.leagueoflegends.com/) · [riotesportsdata.com](https://riotesportsdata.com/en-us/)

**Infrastructure communautaire** — [raw.communitydragon.org](https://raw.communitydragon.org/) · [communitydragon.org/documentation](https://www.communitydragon.org/documentation) · [github.com/CommunityDragon/awesome-league](https://github.com/CommunityDragon/awesome-league) · [cdn.merakianalytics.com](https://cdn.merakianalytics.com/) · [github.com/meraki-analytics](https://github.com/meraki-analytics)

**Plateformes stats** — [op.gg](https://op.gg/) · [help.op.gg](https://help.op.gg/) · [u.gg/faq](https://u.gg/faq) · [mobalytics.gg](https://mobalytics.gg/) · [blitz.gg/lol](https://blitz.gg/lol) · [lolalytics.com](https://lolalytics.com/) · [a3.lolalytics.com](https://a3.lolalytics.com/) · [metasrc.com](https://www.metasrc.com/) · [metasrc.com/retired](https://www.metasrc.com/retired) · [porofessor.gg/faq](https://porofessor.gg/faq) · [leagueofgraphs.com](https://www.leagueofgraphs.com/) · [dpm.lol](https://dpm.lol/) · [deeplol.gg](https://www.deeplol.gg/) · [tracker.gg/lol](https://tracker.gg/lol) · [overwolf.com](https://www.overwolf.com/) · [dev.overwolf.com](https://dev.overwolf.com/)

**Explorateurs / niche** — [riftpatchnotes.com](https://riftpatchnotes.com/lol) · [patchdelta.gg](https://patchdelta.gg) · [skinexplorer.lol](https://skinexplorer.lol) · [lolskin.info](https://lolskin.info) · [loldb.info](https://loldb.info) · [modelviewer.lol](https://modelviewer.lol) · [heimerdinger.lol](https://heimerdinger.lol) · [lol-db.com](https://lol-db.com) · [lolmath.net](https://lolmath.net) · [draftgap.com](https://draftgap.com)

**Esport** — [grid.gg](https://grid.gg/) · [gol.gg](https://gol.gg/esports/home/) · [oracleselixir.com](https://oracleselixir.com/) · [lol.fandom.com](https://lol.fandom.com/) · [lolesports.com](https://lolesports.com/)

**Presse / analyse** — [Forbes — Enthusiast acquiert U.GG](https://www.forbes.com/sites/mikestubbs/2021/11/23/enthusiast-gaming-acquires-ugg-for-44-million-to-enter-league-of-legends-space/) · [TechCrunch — Wargraphs → M.O.B.A. Network](https://techcrunch.com/2023/06/15/wargraphs-a-gaming-startup-with-only-one-employee-and-no-outside-funding-sells-for-54m/) · [PC Gamer — rachat 55 M$](https://www.pcgamer.com/corporation-buys-a-popular-league-of-legends-app-for-dollar55-millionits-made-by-one-guy/) · [ESPN — Swift rachète Blitz](https://www.espn.com/gaming/story/_/id/25845083/team-solomid-parent-company-swift-buys-blitz-esports-app) · [Esports Insider — ESL FACEIT × Mobalytics](https://esportsinsider.com/2025/03/esl-faceit-group-mobalytics-acquisition) · [Databricks — reverse engineering Blitz](https://www.databricks.com/blog/how-blitz-and-databricks-are-powering-new-era-competitive-gaming) · [Dexerto — controverse Porofessor](https://www.dexerto.com/league-of-legends/popular-league-of-legends-add-on-criticized-for-adding-cheat-feature-players-think-should-be-banned-3142237/) · [zleague.gg — Asymmetric Sampling](https://www.zleague.gg/theportal/decoding-league-of-legends-statistics-a-closer-look-at-lolalytics-winrate-data/) · [esports.gg — DPM.LOL](https://esports.gg/news/league-of-legends/how-dpm-lol-is-transforming-lol-analytics/) · [stockanalysis.com — EGLX](https://stockanalysis.com/quote/tsx/EGLX/) · [dotesports — OP.GG rachète OGN](https://dotesports.com/league-of-legends/news/league-stats-site-op-gg-buys-ogn)

