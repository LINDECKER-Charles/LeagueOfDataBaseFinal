# Lot 5 — Builds et tendances

Objectif du lot : les builds (règles, éditeur, partage, import sur un autre patch, votes) et
la page des tendances, à parité.

Critère de sortie local : les builds de l'ancienne base s'affichent à l'identique,
fantômes compris. Le jeu d'essai (builds publics et privés, sur plusieurs patchs et modes,
dont des fantômes) est inséré en SQL dans une copie de la base de dev de l'ancienne stack,
migrée par `migrate` (L1.4) ; les deux stacks lisent cette même base. Un script Playwright
extrait de chaque page `/b/{token}` les faits affichés (champion, patch, mode, runes, objets
par étape, marqueurs de fantôme) et les compare automatiquement entre les deux stacks.

## L5.1 — Règles et API des builds, votes, import

- **Objectif** : les règles des builds en domaine pur, et toute l'API des builds, du
  partage, des votes et des tendances.
- **À lire** : `app/src/Controller/Build/**`,
  `app/src/Controller/Concern/{OwnsBuilds,RendersBuildEditor}.php`, `app/src/Service/Build/**`
  (validateur, porte de catalogue, normaliseur, projecteur, assembleurs),
  `app/src/Service/Community/**`, `app/src/Repository/{BuildRepository,
  BuildVoteRepository}.php`, `app/assets/vue/builds/runes/runeRules.ts`,
  `app/assets/vue/builds/steps/stepList.ts`, tests PHPUnit `Service/Build/**`,
  `heritage.md` § 6 (Builds).
- **Périmètre** : `src/LoDb.Domain/Builds/**`, `src/LoDb.Api/Modules/{Builds,Trends}/**`,
  tests associés.
- **Conception** :
  - Domaine pur : nom 3 à 80 caractères, description ≤ 2000 ; 1 à 10 étapes, libellé ≤ 40,
    note ≤ 300, 1 à 8 objets par étape, 40 au plus au total, doublons permis ; runes :
    4 choix primaires dans les emplacements 0 à 3 de l'arbre primaire (0 = clé de voûte),
    2 choix secondaires dans des rangées différentes hors clé de voûte, arbre secondaire
    différent du primaire, éviction FIFO d'un troisième choix secondaire ; mode → carte
    (`sr` 11, `aram` 12, `nexus_blitz` 21, `arena` 30) ; objets Classic et objets
    indisponibles sur la carte refusés.
  - Un build est épinglé à son patch et à son mode. Un élément disparu est un **fantôme**
    (`missing`, l'id tient lieu de nom), jamais supprimé automatiquement.
  - Import sur un autre patch : runes gardées entières ou réinitialisées, objets disparus
    ou injouables retirés, champion manquant signalé, rapport de ce qui a été retiré.
  - Endpoints : mes builds ; création et modification (e-mail vérifié exigé) ; lecture
    pour édition et suppression par le propriétaire (sinon 404) ; aperçu d'import
    `?to=<version>` ; vote `up|down` (le même vote l'annule, builds publics seulement,
    score net seul exposé) ; partage `GET /api/share/{token}` (jeton `[a-f0-9]{24}`,
    12 octets aléatoires), rendu sur le patch du build, lisible même privé (lien non
    listé) ; tendances `?champion&mode&language&page` (24 par page, builds publics classés,
    propriétaires bannis exclus, badge de soutien).
  - Appels d'audit des actions sur les builds (création, modification, suppression,
    vote), par `IAuditLog` (L4.1).
- **Tests** : règles pilotées par table (cas des specs `stepList` et `runeRules` et des tests
  PHPUnit repris) ; chaque endpoint en nominal et en erreur ; fantômes ; import ; votes.
- **Dépend de** : lot 4. **Taille** : L.

## L5.2 — Éditeur de builds

- **Objectif** : l'éditeur et la liste « mes builds », en rendu client.
- **À lire** : `app/templates/build/{editor,index}.html.twig`, `app/assets/vue/builds/**`
  (`BuildEditor`, `ChampionPicker`, `RuneBoard`, `StepEditor`, `ItemArmory`,
  `CatalogState`, `editor/`, `runes/`, `steps/`, `items/`, `catalog/`) et leurs specs,
  `app/assets/styles/builds/**`.
- **Périmètre** : `src/LoDb.Web/src/app/features/builds/{editor,mine}/**` (dont
  `editor/editor.routes.ts`, créé par L3.1), `tests/LoDb.E2E/specs/builds-editor/**`.
- **Conception** : liste (import vers un patch, suppression confirmée, édition) ; éditeur
  (nom, description, public ; champion ; plateau de runes avec la règle FIFO ; étapes en
  glisser-déposer CDK avec repli par boutons ; armurerie d'objets en dialogue, *bottom
  sheet* sur mobile, catégories, marqueurs « absent de ce patch » et « exclu par le mode ») ;
  cache de catalogue par version et mode ; messages de validation du serveur.
- **Tests** : cas des specs `stepList`, `runeRules`, `useDragReorder`, `useStepEditing`,
  `itemCategories` repris sur des services et fonctions pures ; E2E création, édition,
  import et suppression d'un build.
- **Dépend de** : L5.1. **Taille** : L.

## L5.3 — Partage, votes et page des tendances

- **Objectif** : la page de partage et les tendances.
- **À lire** : `app/templates/build/show.html.twig`, `app/templates/trends/**`,
  `app/assets/vue/components/community/VoteScore.vue` et `community/voteState.ts`,
  `app/assets/vue/components/ui/CopyLink.vue`, `app/assets/styles/{builds/share,
  community/community}.css`.
- **Périmètre** : `src/LoDb.Web/src/app/features/builds/{share,trends,shared}/**` (dont
  `share/share.routes.ts` et `trends/trends.routes.ts`, créés par L3.1 ; le score de vote,
  utilisé par le partage et les tendances, vit dans `shared/` : même feature, donc permis
  par la règle de lint), `tests/LoDb.E2E/specs/{builds-share,trends}/**`, le script de
  comparaison du critère de sortie.
- **Conception** :
  - `/b/{token}` (hors locale) : sceau du champion, propriétaire, puce de patch, runes, ordre
    d'achat, copie du lien, score de vote (builds publics, mise à jour optimiste).
    **Toujours `noindex` et sans JSON-LD**, public ou non : c'est la décision de l'ADR 0005
    et de `heritage.md` § 6 ; l'actuel indexe les builds publics avec un JSON-LD `Article`,
    écart assumé. Langue d'interface : celle du build (`builds.language` → locale), à
    défaut `en`.
  - `/{locale}/trends` en SSR : filtres dans l'URL, pagination, badge de soutien, appel vers
    l'éditeur, BreadcrumbList + ItemList.
- **Tests** : état de vote optimiste (cas de la spec repris), choix de la langue
  d'interface, filtres d'URL des tendances ; E2E du partage (public et privé) et des
  tendances.
- **Dépend de** : L5.1. **Taille** : M.
