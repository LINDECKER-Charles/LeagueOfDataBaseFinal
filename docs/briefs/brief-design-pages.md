# Brief de design — pages à challenger

> **Destinataire : Claude Design (ou tout designer / agent de refonte visuelle).**
> Ce document fixe **ce qui doit se trouver sur chaque page** (contenu, données, features, états)
> et **la charte graphique du site** (couleurs, typo, thème, ton). Il ne prescrit **pas** la mise
> en page : la composition, les proportions et les micro-interactions sont à ta main.
>
> Objectif : produire des maquettes qui **cohabitent sans rupture** avec les surfaces déjà abouties
> du site (fiche champion, profil, header/nav). Le rendu doit rester « du même codex ».

---

## 0. Contrat de lecture

| Imposé (ne pas dévier) | Libre (à proposer) |
|---|---|
| Le **contenu** de chaque page : blocs fonctionnels, données affichées, features, états. | La **mise en page** : grilles, colonnes, ordre visuel, densité, responsive. |
| La **charte** (§1) : palette de tokens, typo, grammaire des formes, motion, ton. | Les **compositions** : hiérarchie visuelle, respiration, hero, cartes, tableaux. |
| Les **invariants techniques** (§3) : SSR-first, i18n, résolution version/langue, îlots globaux. | Les **micro-interactions** : hovers, transitions, révélations (dans le cadre motion §1.6). |

- **Périmètre challengé** : Home, Tendances, Builds (liste / éditeur / partage), API (Développeurs + Portail),
  Listes (champions / objets / runes / sorts d'invocateur), Détails (objet / rune / sort d'invocateur), Don.
- **Hors périmètre mais = références de cohérence** : **fiche champion** (expression la plus complète du
  design system : héros splash, section-nav sticky, jauges), **profil**, **header/nav**, **auth**, **pages légales**.
  Tout nouveau design doit pouvoir se poser à côté d'elles.
- **Langue** : toute l'UI est **française** et **traduite en 21 langues** — aucun texte en dur, tout passe par i18n.

---

## 1. Charte graphique (contraignante — toutes les pages)

### 1.1 Règle d'or

Le site possède un design system « **Hextech** » complet dans `app/assets/styles/*.css`. **Réutilise-le.**
Jamais de couleur, de police, d'ombre ou de rayon en dur : compose à partir des **tokens** (§1.2/1.3) et des
**primitives** (§1.5) existantes. Une couleur qui n'est pas dans la palette n'a pas sa place sur une maquette.

### 1.2 Palette (tokens `--color-*`)

Hexs alignés sur le style guide Riot LoL (or 1→6, « hextech magic » cyan, gris profonds).

| Groupe | Token | Hex | Rôle |
|---|---|---|---|
| **Surfaces** | `--color-hextech-black` | `#010a13` | Noir absolu — overlays, fonds de puce, badges |
| | `--color-void` | `#0a1428` | **Canvas** (fond de page) |
| | `--color-abyss` | `#091016` | Creux, fonds gravés |
| | `--color-panel` / `--color-panel-2` | `#0f1b2d` / `#162033` | Panneaux, cartes |
| **Or** | `--color-gold` | `#c8aa6e` | **Or primaire** : filets, accents, libellés, or « pièce » |
| | `--color-gold-bright` | `#f0e6d2` | Or clair : titres, valeurs saillantes |
| | `--color-gold-light` | `#cdbe91` | Or intermédiaire |
| | `--color-gold-rich` | `#c89b3c` | Or riche : badge supporter, indicateurs de dépôt |
| | `--color-gold-deep` | `#785a28` | Filets/bordures discrètes |
| | `--color-gold-dark` | `#463714` | Or sombre, ombrage |
| **Cyan Hextech** | `--color-hex` | `#0ac8b9` | **Cyan interactif** : hover, focus, liens actifs, vote ▲, CTA secondaire |
| | `--color-hex-bright` | `#cdfafa` | Cyan clair : **métriques vedettes** (ex. cooldown d'un sort) |
| | `--color-hex-deep` | `#0397ab` | Cyan profond |
| | `--color-blue-steel` / `--color-blue-abyssal` | `#005a82` / `#0a323c` | Bleus structurels profonds |
| **Feedback** | `--color-danger` | `#c24b4b` | **Rouge sobre** : erreurs, vote ▼ — volontairement muet, jamais décoratif |
| **Texte** | `--color-text` | `#f0e6d2` | Corps |
| | `--color-text-muted` | `#a09b8c` | Secondaire |
| | `--color-text-dim` | `#8f8d82` | Eyebrow / labels (calibré WCAG AA 4.5:1) |

**Sémantique couleur (à respecter pour la cohérence)**
- **Or** = structure, titres, prix/or, accents nobles.
- **Cyan** = ce qui est vivant/interactif : hover, focus, liens, vote ▲, métriques « magiques ».
- **Rouge danger** = erreurs et vote ▼ **uniquement**.
- **Widget de vote** : ▲ cyan · ▼ rouge · score positif or · négatif `text-muted` · zéro `text-dim`.
- **Jauges de profil champion** (référence, si réutilisées) : attaque `#f0a868`, défense `#3fb96f`,
  magie `--color-hex`, difficulté `#c99bff`.

### 1.3 Typographie (tokens `--font-*`)

| Token | Police | Emploi |
|---|---|---|
| `--font-beaufort` | **Beaufort for LoL** (serif display) | Titres, eyebrows, libellés de section — **CAPITALES + letter-spacing** |
| `--font-spiegel` | **Spiegel** (sans body) | Corps de texte, descriptions |
| `--font-mono` | mono système / JetBrains Mono | **Données** : ids, versions, nombres, labels techniques, chips |

- Grands titres : traitement **`text-gold-grad`** (dégradé parchemin→or sur Beaufort caps).
- **Eyebrow** : Beaufort, uppercase, `letter-spacing: 0.28em`, taille ~0.7rem, or — surtitre récurrent.

### 1.4 Grammaire des formes & atmosphère

La grammaire vient de Riot : **le carré cadre l'information, le losange (◆) est l'accent qui guide l'œil,
le cercle est réservé aux moments de focus.** Respecte cette sémantique.

- **Fond de page** : `--color-void` + deux halos radiaux fixes (cyan en haut-centre, or en haut-droite).
- **Cadre signature `hextech-frame`** : dégradé bleu nuit + **filet or** + **coins biseautés (14px)** +
  glow cyan au hover. C'est l'écrin par défaut de tout panneau qui « pèse ».
- **Ornements** : `hx-corners` (équerres or gravées), `gold-rule` / `gold-hairline` (filets or),
  **`hx-rule`** (séparateur horizontal à **nœud losange** central — un vrai break de contenu).
- **Héros de détail `detail-hero`** : l'image de l'entité est **répétée floutée en écho** derrière le titre.
- **Effets de focus** : `hex-glow` (halo cyan), `hx-breathe` (pulsation d'ambiance lente).

### 1.5 Primitives & composants réutilisables (le vocabulaire disponible)

Compose avec ces briques plutôt que d'en réinventer. Ne les **redéclare pas** — elles existent déjà.

**Conteneurs & séparateurs** : `hextech-frame` (+`-hover`), `hx-corners`, `hx-rule`, `gold-rule`,
`codex-header` (marqueur losange + titre small-caps Beaufort + filet — en-tête de section canonique),
`section_header` (logo + eyebrow + H1 + compteur cyan — en-tête de page).

**Données & étiquettes** : `hx-plate` (plaque de fait gravée : label mono + valeur Beaufort), `hx-chip`
(puce mono), `stat-cell`, `eyebrow`, `text-gold-grad`, `gauge` (jauge en 10 losanges hex).

**Interactifs** : `hx-btn` (bouton or plein), `hx-btn-ghost` (fantôme cyan au hover), `hx-input`,
`hx-select`, `hx-check`, `nav-link`, `section-nav` (barre d'ancres sticky avec scrollspy).

**Composants de page** (Twig, `app/templates/components/`) : `entity_card` (carte ressource : image ou
monogramme 2-lettres + nom + sous-titre + corps variable + footer optionnel), `empty_state` (état vide
unifié), `detail_pager` (‹ précédent · **hub losange retour liste** · suivant ›), `list_filter`,
`_supporter_badge` (sceau or gemme-cœur ~14px), `_vote_score` (widget vote net).

**Îlots Vue globaux** (ne pas re-designer, ils sont montés une fois pour tout le site) : `resource-loader`
(loader SSE), `toaster` (flash), `load-time` (badge serveur+client sur les détails). Îlots locaux :
`resource-filter`, `build-editor` (+ pickers), `vote-score`, `copy-link`.

### 1.6 Motion & accessibilité (non négociable)

- **Easing** : `--ease-hextech` `cubic-bezier(0.22, 1, 0.36, 1)` pour toute transition.
- **Reveal au scroll** : les sections montent/apparaissent en entrant dans le viewport (`data-reveal`).
- **`prefers-reduced-motion`** : **toujours** un rendu statique équivalent. Aucune info portée par la seule animation.
- **SSR-first / no-JS** : chaque page rend son contenu **côté serveur** ; les îlots sont de l'**enrichissement
  progressif**. Un utilisateur sans JS voit et utilise la page (formulaires natifs, liens réels).
- **Contraste** : WCAG **AA (4.5:1)** minimum sur le texte.
- **Focus** : anneau `focus-visible` cyan/or visible sur tout élément interactif.
- **Tactile** : champs de formulaire ≥ 16px (évite le zoom iOS). Scrollbar fine or.

### 1.7 Ton éditorial & vocabulaire

- Univers **codex / forge / Hextech**. Le lexique est assumé : *forger* un build, *offrande à la forge*,
  *parchemin de stratégie*, *sceau* d'invocation, *clé de voûte* (keystone), *constellation* de runes,
  *pierre angulaire*, *l'armurerie*, *le pouls de la Faille*.
- **Honnêteté sémantique** : les libellés dérivent de la donnée réelle (ex. la couleur d'un chroma vient de
  sa teinte, pas d'un nom marketing Riot). Pas de fausse promesse (ex. le don n'offre **aucune** contrepartie).

### 1.8 États systémiques transverses (à prévoir sur toutes les pages concernées)

- **Résolution version/langue** : `query → session`, **sans redirection**. Les pages Data Dragon portent un
  eyebrow **« Data Dragon · {version} »** et un couple `{version} · {locale}`.
- **Loader SSE global** : au clic sur un lien de ressource, avant la visite Turbo, les images de la page cible
  sont préchauffées et la progression streamée (barre + noms). C'est un îlot **global** — n'en refais pas la
  logique, prévois juste qu'il existe.
- **Flash** : succès/erreurs remontent via le **toaster global**.
- **Robustesse data** : une panne upstream **ne casse jamais une page** — soit dégradation en **fantôme**
  (« indisponible sur ce patch/mode », données Data Dragon manquantes dans les builds), soit **redirection
  accueil + flash** (listes/détails). Prévois donc les états dégradés autant que l'état nominal.

---

## 2. Contenu attendu par page

Structure de chaque fiche : **Rôle · Contenu obligatoire · Données & source · Interactions · États · SEO/i18n · ⚠️**.

---

### 2.1 Home — `/`

**Rôle** — Hub d'accueil et vitrine indexable. Portail vers les 4 datasets, calé sur la version/langue Data
Dragon courante. 100 % public, aucune donnée utilisateur.

**Contenu obligatoire**
- **Hero** : eyebrow « Patch {version} » · titre de marque · accroche · **4 portails catégorie**
  (Champions, Objets, Runes, Sorts d'invocateur) menant à leurs listes.
- **4 aperçus** (un par dataset), chacun : en-tête (titre + lien « Voir tout » → liste) + **4 entrées**.
  - Champion : nom + **titre lore** + portrait.
  - Objet : nom + id + icône.
  - Sort d'invocateur : nom + id + icône.
  - Rune : nom d'arbre + key + icône.

**Données & source** — Data Dragon (`champion / item / summoner / runesReforged.json`), images
content-addressed + WebP. 4 éléments par section (chaque section est isolée : une panne manager → section vide,
jamais toute la page).

**Interactions** — Liens/CTA uniquement. **Aucun** filtre/recherche/îlot sur la home même. Le sélecteur
version/langue vit dans le header (global).

**États** — Section vide par dataset (message d'invitation).

**SEO/i18n** — `seo.home.title/description` (avec `%version%` et « 21 langues »), og `/preview/home.png`.

**⚠️ À corriger au passage** — (1) l'accroche hero actuelle ne cite que **2 des 4** catégories ; (2) la clé
`homepage.runes.no_data` est référencée mais **absente** du catalogue ; (3) les aperçus Objets/Sorts n'ont
**aucun message d'état vide**. Le brief attend les 4 catégories cohérentes et un état vide par section.

---

### 2.2 Tendances — `/trends`

**Rôle** — Classement communautaire de **tous les builds publics**, rangés par **score net de votes**
(« le pouls de la Faille »). Entièrement SSR → indexable.
*(À ne pas confondre avec l'endpoint API `/v1/trends`, qui lui expose les entités les plus **consultées** — analytics de vues.)*

**Contenu obligatoire**
- **Header** : eyebrow (« le pouls de la Faille ») · H1 « Trending builds » · **compteur de résultats**.
- **Filtres** (formulaire GET, sans JS) : select **Champion** (uniquement les champions ayant ≥ 1 build public)
  + select **Mode de jeu** (Faille / ARAM / Nexus Blitz / Arène), cumulables, état dans l'URL.
- **Liste classée** (24 / page), une ligne par build portant :
  - **Widget de vote** : flèche ▲ · **score net signé** · flèche ▼ (jamais le détail pour/contre).
  - **Identité** : portrait champion (+ **médaillon keystone** incrusté), nom du build, nom du champion → lien `/b/{token}`.
  - **Méta** : auteur « Par {pseudo} » (lien vers profil public si activé) + **badge supporter** éventuel ;
    puce **mode** ; puce **patch**.
  - **Extrait d'objets** : jusqu'à **6** icônes en ordre d'achat.
- **Pagination** : précédent / suivant + « page / pages ».
- **État vide** : « Aucun build public pour ce filtre — forgez le vôtre » + CTA vers la forge.

**Données & source** — Score = `SUM` des votes (Postgres), builds à 0 vote **inclus** ; périmètre = build public
**et** propriétaire non banni ; tri `score DESC, createdAt DESC, id DESC`. Noms/images (champion, keystone,
objets) = Data Dragon (fantômes si absents du patch).

**Interactions** — Vote via îlot (update optimiste + rollback, `aria-pressed`) ; **toggle** : revoter la même
flèche retire le vote, l'autre flèche remplace ; fallback POST natif sans JS ; anonyme → **liens de connexion**.
Filtres/pagination en GET.

**États** — Vide (filtre ou aucun build) ; non connecté (flèches → login) ; fantômes patch.

**SEO/i18n** — `seo.trends.title/description` ; JSON-LD **BreadcrumbList** + **ItemList** dynamique (20 premiers builds).

---

### 2.3 Builds — `/builds` (liste), éditeur (`/builds/new` · `/builds/{id}/edit`), partage (`/b/{token}`)

Accès CRUD sous `ROLE_USER` ; **création/édition exigent un e-mail confirmé**. La page de partage `/b/{token}`
est **publique** : le token **est** la capacité d'accès (un build privé reste consultable par lien, mais n'est ni
listé, ni votable, ni indexé).

#### a) Liste « Mes builds » — `/builds`
- **Rôle** — Liste personnelle (builds publics **et** privés), triée par dernière modification. `noindex`.
- **Contenu** — En-tête (eyebrow « L'armurerie » + titre « Mes builds » + bouton « Nouveau build » si liste non
  vide) ; **état vide** (message + CTA « Forger mon premier build ») ; une ligne par build : portrait champion
  (ou initiales) + médaillon keystone, nom du build, nom du champion + « Modifié le {date} », **chip visibilité**
  (public/privé), actions **Modifier** + **Supprimer** (confirmation).
- **Données** — Postgres (build) + Data Dragon résolus sur le **patch courant** (fantômes possibles).
- **Interactions** — CRUD uniquement (pas de filtre/tri/pagination/vote ici).

#### b) Éditeur « La forge » — `/builds/new`, `/builds/{id}/edit`
- **Rôle** — Formulaire de forge : champs serveur + îlot `build-editor` qui possède le contexte de jeu, les
  pickers, et maintient un input caché `structure` **re-validé côté serveur**.
- **Contenu**
  - En-tête : eyebrow « La forge » + titre create/edit.
  - **Identité** : Nom (requis, 3–80) · Description (≤ 2000) · case **« Build public »**.
  - **Contexte de jeu** : select **Patch** (30 derniers + patch épinglé) · select **Mode** (Faille / ARAM /
    Nexus Blitz / Arène). La disponibilité des objets suit le mode.
  - **Champion** : recherche live + grille de portraits ; champion choisi ; fantôme si absent du patch.
  - **Runes** : voie principale (**clé de voûte** + 3 mineures sur 4 rangées) + voie secondaire (**2 runes de 2
    rangées différentes**, règle d'éviction FIFO) ; chips fantôme pour perks disparus.
  - **Ordre d'achat** : étapes ordonnées ; par étape : **intitulé** (≤ 40, presets Départ / Premier retour /
    Cœur / Situationnel / Finale) · **note** (≤ 300) · médaillons d'objets · **chip coût d'étape** (or ◆) ·
    recherche d'objet type-ahead (nom + coût) · compteurs.
  - Fallback `<noscript>` + inputs cachés (`structure`, `game_version`, `game_mode`).
  - Pied : « Retour à mes builds » + submit create/update.
- **Interactions** — Pickers `/api/picker/{champions,items,runes}` ; **glisser-déposer** (réordonner étapes /
  objets, transfert inter-étapes, drop depuis la recherche) **+** boutons ↑↓ / ‹› / × (chemin clavier/tactile
  conservé) ; annonces `aria-live`.
- **Quotas** (validés serveur) — nom 3–80 · desc ≤ 2000 · runes 4 primaires + 2 secondaires · étapes 1–10 ·
  intitulé ≤ 40 · note ≤ 300 · objets/étape 1–8 · **total ≤ 40 objets**.
- **États** — Chargement / erreur + retry par picker ; validation → **re-render 422 sans perte de saisie** ;
  **gate e-mail non confirmé** ; fantômes « indisponible sur ce patch » vs « … dans ce mode ». `noindex`.

#### c) Partage « Parchemin de stratégie » — `/b/{token}`
- **Rôle** — Face publique d'un build, atteignable par lien. **Rendu figé sur le patch épinglé** du build.
- **Contenu**
  - En-tête : portrait champion (+ titre lore éventuel) · nom du build · ligne méta : « Par {auteur} » (lien
    profil public si activé) + **badge supporter** + date + **chip mode** + **chip patch** (« Forgé sur le
    patch X — actuel : Y » si divergence).
  - **Widget de vote** (builds publics uniquement) : ▲ · score net · ▼.
  - **Copier le lien** (îlot : Copier / Copié ! / fallback manuel).
  - **Description** (multi-lignes).
  - **Section Runes** : voie principale (clé de voûte + mineures) + voie secondaire.
  - **Section Ordre d'achat** : étapes (intitulé, coût ◆, note, médaillons) + **coût total**.
- **États** — Token inconnu → 404 ; build privé → pas de vote, `noindex`, pas de JSON-LD ; fantômes ;
  propriétaire banni → hors classements mais l'URL fonctionne toujours.
- **SEO/i18n** — Title « {build} · {champion} », description (desc tronquée 160 ou fallback), og `article`,
  `index` si public ; JSON-LD **BreadcrumbList** + **Article**.

---

### 2.4 API — page Développeurs (`/developers`, publique) + Portail (`/profile/api`, privé)

Deux familles distinctes. Toute la copie vit dans le domaine i18n `api` (21 langues).

#### a) Développeurs — `/developers` (publique, indexable, stateless)
- **Rôle** — **LA** documentation de l'API (la page *est* la référence, il n'y a pas de repo externe).
- **Contenu obligatoire**
  - En-tête : eyebrow « Développeurs » · H1 « L'API LeagueOfDataBase » · intro · CTA **« Créer ma clé »**
    (→ portail) + lien **« Tarifs »** (ancre) + hint « moins d'une minute ».
  - **Authentification** : format `lodb_` + 40 hex ; deux en-têtes acceptés (`Authorization: Bearer` **ou**
    `X-Api-Key`) ; seul le SHA-256 est stocké ; URL de base.
  - **Endpoints** (5, tous **GET**) — chacun : chip méthode + chemin + description :
    `GET /healthz` · `GET /v1/profiles/{username}` · `GET /v1/champions/{championId}/builds` ·
    `GET /v1/trends/{type}` · `GET /v1/usage`. + **2 exemples curl** copiables + **1 exemple de réponse JSON**.
  - **Quotas & rate limiting** : quota mensuel → crédits prépayés → `429` ; token bucket + en-têtes
    `X-RateLimit-*` ; reset au mois calendaire ; annuels portés en quota mensuel.
  - **Erreurs** : enveloppe uniforme + table des codes (`unauthorized` 401, `forbidden` 403, `not_found` 404,
    `rate_limited`/`quota_exceeded` 429, `invalid_request` 400, `internal` 500/503).
  - **Grille tarifaire** (ancre `#pricing`) : colonnes Offre / Prix / Volume / Débit — voir table §3.3.
  - **Facturation & gestion des clés** : clés gérées depuis le profil, Stripe Checkout, « jamais de donnée
    bancaire côté site » + CTA final.
- **SEO/i18n** — `seo.developers.title/description` + BreadcrumbList + canonical. Indexable.

#### b) Portail — `/profile/api` (privé, `ROLE_USER`, `noindex`)
- **Rôle** — Tableau de bord client : sa **clé**, son **usage**, sa **facturation**.
- **Contenu obligatoire**
  - En-tête : eyebrow « API développeur » · H1 « Clés API » · liens (« Documentation & tarifs », « Retour au profil »).
  - **Bandeau clé fraîche** (one-shot, après création/régénération) : **secret complet** en lecture seule +
    avertissement « affichée une seule fois » + bouton **Copier**.
  - **Bandeau statut Stripe** : `pack_success` / `plan_success` / `cancelled`.
  - **État « aucune clé »** : si e-mail confirmé → corps + formulaire (Nom optionnel + **« Créer ma clé »**) ;
    si **non confirmé** → message de gate (pas de formulaire).
  - **Clé active (overview)** : chip **plan** ; **6 plaques** — clé masquée (`keyPrefix…`), nom, créée le,
    **consommation mensuelle** (used / quota + **barre** + restantes), crédits prépayés, débit req/min
    (+ « abonnement actif ») ; actions **Régénérer** + **Révoquer** (avec textes d'aide).
  - **Facturation « Booster votre clé »** : si non configuré → message ; sinon **packs** (S / M / L : prix +
    requêtes + « Acheter ») + **abonnements** (nom + prix /mois|/an + req/mois + req/min + « S'abonner »,
    désactivé si déjà abonné ; bandeau « déjà abonné »).
  - **Usage « 30 derniers jours »** : total du mois + **tableau** (Jour · Requêtes) ou état vide.
- **Données & source** — `ApiKey` (Postgres) + `api_usage` (écrit par le micro-service Go, latence ~1 min).
  **Une seule clé active par compte.** Le secret n'est **jamais** persisté (SHA-256) et n'apparaît **qu'une fois**.
- **États** — Non connecté (login) · e-mail non confirmé (gate création) · aucune clé · clé active · quota
  dépassé (barre à 100 %, restantes 0) · paiements désactivés · déjà abonné · flash d'erreurs.

---

### 2.5 Listes — Champions · Objets · Runes · Sorts d'invocateur

**Architecture commune** — Rendu **SSR de toute la collection**, puis un îlot **`ResourceFilter`** prend la main
côté client : **recherche live** + **facette multi-select** (OR) + **pagination client (12 / 24 / 48 / All)**.
Résolution version/langue sans redirect. Quand l'image n'est pas encore chaude (lazy / préchauffage des 12
premières), la carte affiche un **monogramme d'initiales** de secours.

**Blocs communs** — `section_header` (logo + eyebrow « Data Dragon · {version} » + H1 + **compteur cyan**) ·
barre de filtres · **grille de cartes** (`entity_card`) · **état vide** (serveur *et* client, messages distincts).

**Carte commune** — image (ou monogramme) + nom + sous-titre + corps variable + footer CTA optionnel ; porte
`data-search` / `data-tags` pour le filtre.

**Spécificités par type**

| Type | Sous-titre | Corps de carte | Cellules de stats | Facette | Footer CTA | Volume |
|---|---|---|---|---|---|---|
| **Champions** | titre lore | puces de **classes** + blurb (3 lignes) | — | classes (Fighter/Mage/…) | **oui** | ~168 |
| **Objets** | id numérique | **3 accordéons** : Catégories · **Évolutions** (icône+nom+lien) · Description (HTML riche) | **Coût total** + **Revente** | tags d'objet | non | plusieurs centaines |
| **Sorts d'invocateur** | id | **2 accordéons** : Modes compatibles (+ count) · Description | **Cooldown** + **Niveau invocateur** | modes de jeu (whitelistés) | non | ~16 |
| **Runes** | key d'arbre | aperçu des **keystones du slot 0** | — | nom d'arbre *(1 tag/carte — facette quasi dégénérée)* | **oui** | 5 (1 carte = 1 arbre) |

**Données & source** — Data Dragon (JSON en cache MinIO, images content-addressed + WebP), repli `en_US`.

**États** — Vide serveur (« Aucun résultat » + Retour / Accueil) · vide client (« Aucun résultat ne correspond
à vos filtres ») · erreur data → **redirection accueil + flash** (jamais de page cassée).

**SEO/i18n** — `seo.{champion|item|rune|summoner}.list.title/description` avec `%count%` et `%version%` ;
og `/preview/{champions|objects|runes|summoners}.png` ; JSON-LD **ItemList** (20 premiers).

**⚠️** — La **facette Runes** est quasi inutile (chaque arbre porte son propre nom comme unique tag) : à repenser
si une vraie facette est voulue. Le texte riche (blurb, descriptions) contient du **HTML Data Dragon brut**.

---

### 2.6 Détails — Objet · Rune · Sort d'invocateur

**Ossature commune** — **Héros** (`detail-hero` + écho flouté de l'image) → **sections de contenu**
(en-têtes `codex-header` à losange) → **pager voisins** (‹ précédent · **hub losange retour liste** · suivant ›)
→ **badge de temps de chargement** (serveur + client). Eyebrow méta commun : **« {collection} ◆ {version} · {locale} »**.

> **⚠️ Important** — Il n'y a **aujourd'hui aucun bouton favori ni partage** sur ces pages (seul un marqueur
> invisible de temps de chargement). Les favoris vivent **uniquement dans l'espace profil**. Si tu veux prévoir
> favori/partage sur les détails, ce sont des **ajouts nets** à signaler explicitement.

#### a) Objet
- **Héros** : icône encadrée (ou initiales) · **nom** (H1 or dégradé) · **tagline courte** (plaintext) ·
  **prix total** en gros chiffre + pièce d'or (+ « Indisponible en boutique » si non achetable).
- **Recette / arbre de craft** : **« Composants »** = arbre **récursif descendant** (racine allumée non
  cliquable + composants liés jusqu'aux objets de base ; par nœud : coût total + **coût de combinaison** `+{combine}`) ;
  **« Évolutions possibles »** = objets que celui-ci compose (cartes liées : icône + nom + coût).
- **Description** : balisage **riche localisé Data Dragon** (stats / passive / active).
- **Aside** (jusqu'à 4 plaques) : **Statistiques** (label + icône + valeur) · **Registre d'or** (total / base /
  revente) · **Disponibilité** (palier, `requiredChampion`/`requiredAlly`, cartes : Faille / ARAM / Nexus Blitz /
  Arène / Brawl) · **Catégories** (tags).
- **États** : 404 slug inconnu · erreur data → accueil + flash · tout bloc conditionnel.

#### b) Rune — **fiche d'ARBRE entier** (Precision · Domination · Sorcery · Resolve · Inspiration)
- **Ambiance colorée** dérivée de la key de l'arbre.
- **Héros** : icône de l'arbre + **nom** (teinté à la couleur de l'arbre).
- **Constellation** : **Pierres angulaires** (slot 0) = icône + nom + **description longue** ;
  **Emplacements mineurs** (slots 1–3) = médaillon + nom + **résumé** (short) + **« Description complète »**
  repliable (long, si différent).
- **Interactions** : le **pager navigue entre arbres** ; les runes internes **ne sont pas des liens**.
- **États** : 404 key inconnue · `empty_state` si l'arbre n'a pas de slots · erreur → accueil.

#### c) Sort d'invocateur
- **Héros « sceau »** : icône dans un **sceau** (cercle = focus) · **nom** · **cooldown** en **métrique vedette**
  (gros chiffre **cyan** + « s »).
- **Plaques gravées** (données significatives uniquement, bruit `-1`/`No Cost`/WIP normalisé) : **Portée**
  (ou « Globale » si ≥ 25000) · **Niveau** (« Débloqué au niveau N ») · **Coût** (si non gratuit) ·
  **Charges** (maxammo si > 0).
- **Description** : balisage riche Data Dragon.
- **Modes compatibles** : puces **whitelistées** (Classic / ARAM / Arène / Brawl / Nexus Blitz / URF /
  One for All / Swiftplay / Ultimate Spellbook / Practice Tool / Tutorial) + compteur.
- **États** : 404 id inconnu · erreur → accueil · blocs conditionnels.

---

### 2.7 Don — `/donate` (+ `success`, `cancel`)

**Rôle** — Don **unique** via **Stripe**, **sans compte requis**. La page insiste sur l'**absence de contrepartie**.

**Contenu obligatoire**
- **En-tête codex** : eyebrow « Offrande à la forge » + méta « stripe · paiement sécurisé ».
- **Panneau** : eyebrow + H1 « Soutenir League Of Data Base » · **accroche** (couvre l'hébergement, garde
  l'encyclopédie gratuite/rapide/sans pub) · **pledge** (« aucun avantage en jeu, aucun privilège — seulement
  notre gratitude »).
- **Si Stripe non configuré** : bloc « Dons bientôt disponibles » + bouton **désactivé**.
- **Sinon** (formulaire POST, hors Turbo) : **paliers 3 € / 5 € / 10 € / 25 €** (Étincelle / Gemme / Blason /
  Relique ; **5 € coché par défaut**) + **montant libre** (**1 € → 500 €**, remplace le palier sélectionné) +
  CTA « **Faire un don via Stripe** ».
- **Pied légal** : « Paiement sécurisé par Stripe — aucune donnée bancaire ne transite par ce site » + liens
  Confidentialité / Conditions.

**Pages de retour**
- **Succès** (`noindex`) : **sceau allumé** · eyebrow « Offrande reçue » · H1 « Merci, invocateur » · corps +
  « Stripe vous envoie votre reçu par e-mail » · CTA retour. **Générique — aucun montant affiché.**
- **Annulation** (`noindex`) : **sceau éteint** · « Offrande retirée » / « Don annulé » · « Rien n'a été
  débité » · CTA **Réessayer** + **Accueil**.

**Données & features** — Don unique (pas de récurrence) ; redirection 303 vers Stripe hébergé ; le **badge
supporter** est attribué **côté serveur (webhook)** si le donateur est connecté — **la page ne le promet ni ne
l'affiche**.

**États** — Succès · annulation · non configuré · erreurs (CSRF, indisponible, montant invalide, passerelle injoignable).

**SEO/i18n** — `seo.donate.title/description` + BreadcrumbList (page principale indexable).

---

## 3. Annexes

### 3.1 Fichiers de référence (charte)

- `app/assets/styles/tokens.css` — **tokens** couleur/typo/motion + `hx-breathe`.
- `app/assets/styles/primitives.css` — **primitives** (`hextech-frame`, `hx-corners`, `eyebrow`, `codex-header`,
  `hx-plate`, `hx-btn`, `hx-rule`, `hx-chip`, `hx-input/select/check`…).
- `app/assets/styles/base.css` — canvas, halos de fond, reveal, chrome de formulaire.
- `app/assets/styles/detail.css` — toolkit des détails (`detail-hero`, `hero-name`, `gauge`, `section-nav`).
- `app/assets/styles/community.css` — tendances (`trend-*`), vote (`vote-*`), badge supporter.
- `app/assets/styles/{builds,donate,detail,nav}.css` — vocabulaires de page.
- Composants Twig : `app/templates/components/`.

### 3.2 Invariants techniques à ne pas casser

- **SSR-first + enrichissement progressif** : contenu rendu serveur, îlots Vue en surcouche, no-JS fonctionnel.
- **i18n obligatoire** : aucun texte en dur (21 locales).
- **Résolution version/langue sans redirect** (query → session).
- **`/build/` est réservé aux assets Vite** (nginx) — la vue de partage vit sur **`/b/{token}`**.
- **Splash / skins champion** servis **directement depuis le CDN Data Dragon** (hotlink assumé), pas via MinIO.
- **Données & images Data Dragon hors base** (MinIO) — Postgres = données utilisateur uniquement.
- **Îlots globaux** (loader SSE, toaster, badge load-time) montés une fois dans `base.html.twig` — ne pas dupliquer.
- **Pas de `<Transition>` + `v-show` pour le loader** (toggle de classe CSS déterministe).

### 3.3 Grille tarifaire API (valeurs concrètes, pour la §2.4)

Prix en euros entiers. Logique de dégressif : **1 €/1000 req** (packs) → **÷3** (mensuel) → **÷5** (annuel).

| Offre | Prix | Volume | Débit |
|---|---|---|---|
| **Gratuit** | 0 € | 500 req/mois | 10 req/min |
| **Pack S** | 5 € | 5 000 req | 60 req/min |
| **Pack M** | 10 € | 10 000 req | 60 req/min |
| **Pack L** | 20 € | 20 000 req | 60 req/min |
| **Mensuel** | 5 €/mois | 15 000 req/mois | 120 req/min |
| **Mensuel+** | 15 €/mois | 45 000 req/mois | 120 req/min |
| **Annuel** | 48 €/an | 20 000 req/mois | 300 req/min |
| **Annuel+** | 144 €/an | 60 000 req/mois | 300 req/min |

> Ces valeurs sont dérivées d'enums côté serveur (pas d'un back-office) : elles sont la **source de vérité**
> pour la grille affichée. Une seule clé active par compte ; packs consommés après le quota mensuel.

### 3.4 Récap des points de contenu à corriger au passage

1. **Home** — accroche hero à aligner sur les 4 catégories ; clé `homepage.runes.no_data` manquante ;
   états vides absents sur Objets/Sorts.
2. **Détails** — pas de favori/partage aujourd'hui (voir §2.6) : décision à prendre si on veut les introduire.
3. **Don** — le badge supporter n'est pas valorisé à l'écran (effet back-office) : à clarifier si on veut le mettre en avant.
4. **Listes** — facette Runes dégénérée.

---

*Ce document est un brief interne de design ; il ne fait pas l'objet d'une entrée `docs/changelog/`
(pas d'impact joueur direct tant qu'aucune refonte n'est livrée).*
