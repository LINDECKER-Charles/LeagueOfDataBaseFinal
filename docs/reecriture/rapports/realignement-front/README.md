# Réalignement du front sur l'ancien site : état au 2026-09-27

L'utilisateur n'était pas satisfait du front Angular. La demande : comparer le nouveau front
(`src/LoDb.Web`) à l'ancien (Symfony/Twig/Vue, `app/`) et corriger le nouveau pour qu'il
rejoigne l'ancien. **L'ancien site est la cible** : aspect, mise en page, contenu, interactions et
comportement responsive. Restent volontaires les divergences d'URL et de SSR des ADR (locale en
préfixe, URLs id-slug, pas de session serveur).

Travail interrompu à la demande de l'utilisateur, dont la machine saturait. Tout est committé
localement et **rien n'est poussé**. Consigne donnée pour la fin : lancer `/commit-and-push`
une fois tout terminé.

## 1. Méthode

1. **Captures.** Les deux stacks tournent : l'ancienne sur :8080, `lodb-next` sur :18080. 29
   paires de pages sont capturées en 1440 et 390 px (`donnees/paires-de-pages.json`,
   `outils/capture.mjs`).
2. **Audit.** 12 zones sont auditées en parallèle, chaque auditeur étant suivi d'un vérificateur
   adverse. Le résultat tient en **339 écarts vérifiés** : 9 bloquants, 109 majeurs, 153
   mineurs et 68 finitions ; un seul a été réfuté. Le détail de chaque écart est dans
   `donnees/ecarts.json` : description ancien/nouveau avec fichier:ligne, preuve, correctif
   vérifié, fichiers touchés et lot attribué (champ `wp`).
3. **Découpage.** Les écarts sont répartis en lots par propriété de fichiers
   (`outils/plan.mjs`, qui lit `ecarts.json` renommé en `assigned.json`).
   - Vague 1 : API, DS, SHELL, I18N.
   - Vague 2 : LISTS, DETAIL, ENTITYDET, HOME, EDITORIAL, ACCOUNT, BUILDS, ADMIN.
4. **Correction.** Chaque lot a son worktree `F:/Git/lodb-parite/<lot>` (branche
   `wt/parite-<lot>`). Un correcteur l'implémente et vérifie visuellement contre l'ancien site via
   un aperçu local (`outils/preview.mjs` sert le build du worktree et proxifie `/api` et `/cdn`
   vers `lodb-next`). Un relecteur adverse passe ensuite, puis l'intégration fusionne dans
   `docs/reecriture-dotnet-angular`.

## 2. Vague 1 : intégrée

Fusions sur `docs/reecriture-dotnet-angular`, puis `36709519 chore(api): regenerer le contrat
et le client du realignement du front`. Les rapports des correcteurs et des relecteurs sont dans
`donnees/vague-1-rapports.json`.

| Lot | Contenu | État |
|---|---|---|
| API | Contrat additif. Cartes de champion : `loadingArt`, `blurb`. Objets : `upgrades`, `related`, `ItemUpgrade.gold`, `depth`. `neighbours` sur les 4 fiches, dans l'ordre des listes (69/69 conformes à l'ancien rel=prev/next). Admin : `shareToken`, `isBanned`, `riotTagline`, `geoAvailable`, `objects`/`bytes` de la sonde de stockage. `/v1` inchangé. | intégré, relu |
| DS | Fond d'ambiance, thèmes qui atteignent de nouveau la console de filtres et le journal, hampe Noxus, cadre de dialogue (surtitre, largeurs `wide`/`picker`), couleurs `<font>` de Data Dragon, palette admin (`good`, `bad`, `series-*`), boutons `danger` et `small`. | intégré, relu |
| SHELL | Pastille de version et « Version » du pied de page, libellé « Account », liens qui gardent le patch épinglé et `?lang=`, états actifs, barre du bas, toasts, progression de navigation, préchargement des polices, sélecteur patch/langue (libellés, toast, choix retenu, `/` vers la locale retenue par nginx), admin hors du chrome public et en Hextech, changelog relié dans l'image `web-ssr`. | intégré, relu |
| I18N | Clés brutes des puces de la fiche champion, course SSR sur les portées (plus de rendu mis en cache avec un catalogue manquant), pluriels `=1`, faute « touts ». | intégré, relu |

Contrôles de l'intégration :

- typecheck et ESLint verts ;
- tests des règles de lint : 9/9 ;
- suite front 240/240 sur deux passages ;
- `build:web` vert, le bundle initial étant à 672 ko, en avertissement comme avant ;
- `lodb-next` reconstruite à `36709519` et cache de pages purgé.

## 3. Vague 2 : en cours

Les branches existent et tout leur travail est committé. **Aucune n'est fusionnée.** Les rapports
des correcteurs sont dans `donnees/vague-2-rapports.json`, et la passation de la vague 1 vers la
vague 2 dans `donnees/vague-2-notes.md`.

| Lot | Branche | Correcteur | Relecteur | Remarque |
|---|---|---|---|---|
| LISTS | `wt/parite-lists` (14 commits) | terminé : 62/62 | **à faire** | cartes portrait, filtres à droite, Copy link, états vides… |
| DETAIL | `wt/parite-detail` (8) | terminé : 26/26 | **à faire** | pagers SSR depuis `neighbours`, badge de temps sur les 4 fiches |
| ADMIN | `wt/parite-admin` (2) | terminé : 53/53 | **à faire** | un seul commit de 129 fichiers : à découper, ou au moins à justifier en relecture |
| ACCOUNT | `wt/parite-account` (16) | terminé : 39 corrigés, 1 partiel, 2 sans objet | **interrompu** | relecture à relancer |
| BUILDS | `wt/parite-builds` (14) | terminé : 35 corrigés, 1 sans objet | **à faire** | |
| ENTITYDET | `wt/parite-entitydet` (1) | **interrompu** | à faire | dernier commit = sauvegarde non vérifiée (`chore(front/catalogue): sauvegarder le travail en cours…`) |
| HOME | `wt/parite-home` (2) | **interrompu** | à faire | idem : dernier commit = sauvegarde non vérifiée |
| EDITORIAL | `wt/parite-editorial` (0) | **pas commencé** | à faire | inclut le bloquant du h1 de `/developers` sur téléphone |

## 4. Reprise, dans l'ordre

1. **Stacks.** Chaque commande est à lancer depuis la racine.

   ```bash
   docker compose up -d
   MSYS_NO_PATHCONV=1 docker compose exec -T php chown -R www-data:www-data var /srv/storage
   docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --wait
   ```

2. **Outils.** Les scripts de `outils/` codent en dur le chemin du scratchpad de la session
   précédente, dans les variables `S` et `LOCKS`. Copier `outils/` et `donnees/` dans le
   scratchpad de la nouvelle session, puis corriger ces chemins. `ecarts.json` y reprend son nom
   de travail `assigned.json`, et les lots se régénèrent par `node plan.mjs`, qui écrit `wp/<LOT>.json`.
   Les comptes de test et la clé TOTP de l'admin sont **hors dépôt**, dans
   `F:/Git/lodb-parite/COMPTES-LOCAUX.txt` et `F:/Git/lodb-parite/admin-totp/`.
3. **Terminer la vague 2.**
   - Relecteurs de LISTS, DETAIL, ADMIN, ACCOUNT et BUILDS.
   - Correcteurs d'ENTITYDET et HOME : dire qu'une tentative précédente a laissé un commit de
     sauvegarde à reprendre.
   - Correcteur d'EDITORIAL, puis leurs relecteurs.

   `outils/wave2.js` contient les consignes complètes, sans mots de passe. Pour ne pas relancer
   les correcteurs terminés, écrire un script qui ne lance que les agents manquants, en passant
   les rapports de `vague-2-rapports.json`.
4. **Intégrer la vague 2.**
   - Fusions `--no-ff`, puis typecheck, ESLint, suite front bridée et `build:web`.
   - Reconstruire `lodb-next` **service par service** :
     `APP_REVISION=<sha> IMAGE_TAG=<sha> docker compose -p lodb-next … build api`, puis
     `web-ssr` et `nginx`, puis `up -d --wait`. Purger ensuite le cache de pages :
     `docker exec lodb-next-nginx-1 sh -c 'rm -rf /var/cache/nginx/pages/*'`.
   - La vague 2 ne change pas le contrat : pas de régénération, sauf si un lot touche l'API.
5. **Second audit.** Recapturer les 29 paires et rejouer un audit ciblé sur les écarts restants.
   Corriger jusqu'à ce qu'il ne remonte plus rien.
6. **Garde-fous complets.**
   - `dotnet build LoDb.slnx -c Release` et `dotnet test`.
   - Suite E2E complète : lancer `docker restart lodb-next-api-1` avant, à cause du quota
     d'inscriptions.
   - Remesure Lighthouse : les portraits et résumés des champions et les descriptions des
     objets alourdissent les listes, et un budget ne se relâche jamais.
7. **Fin.** Lancer `/commit-and-push`, comme l'utilisateur l'a demandé.

## 5. Règles et pièges constatés

- **Mémoire.** Ne jamais saturer la machine : c'est la cause de l'arrêt.
  - Vitest lance par défaut un worker par CPU logique, soit 24. Une suite monte ainsi à 4,8 Go,
    contre 1,9 Go avec 3 workers (`outils/vitest-limited.config.mjs` ou
    `VITEST_MAX_WORKERS=3`), pour la même durée.
  - Toutes les commandes lourdes passent par `outils/heavy.sh`, un sémaphore machine à 2 places
    qui coupe aussi MSBuild et Roslyn résidents.
  - Au plus 4 agents à la fois, et même moins si la machine rame.
  - `dotnet build-server shutdown` après tout travail .NET.
  - Ne laisser aucun aperçu ni navigateur orphelin.
- **Worktrees et jonctions.** Les worktrees partagent les `node_modules` du checkout principal
  par **jonctions NTFS**, et le lien du changelog (`features/editorial/changelog/published`,
  extrait en simple fichier texte car `core.symlinks=false`) y est remplacé par une jonction
  masquée à git (`skip-worktree`). Le checkout principal a lui aussi cette jonction. **Toujours
  lancer `outils/unprep-worktree.sh <worktree>` avant `git worktree remove`**, sinon la
  suppression pourrait traverser la jonction et effacer les `node_modules` principaux.
- **CRLF.** Le checkout est en CRLF (`core.autocrlf=true`).
  - `prettier --check .` signale environ 149 fichiers non touchés : juger avec
    `--end-of-line auto`.
  - 21 tests .NET qui comparent du texte échouent déjà sur la base.
  - Vitest réécrit deux `.snap` de `core/seo/json-ld` en LF : les restaurer, ne pas les committer.
- **OpenAPI sous Windows.** Les descriptions sortent avec `\r\n`, tirés des commentaires XML
  du checkout CRLF. Les normaliser en `\n` avant de committer, ce qu'a fait `36709519`. Idéalement,
  `tools/next/api` devrait le faire lui-même.
- **Reprise d'un workflow.** Seul le préfixe inchangé des appels est rejoué depuis le cache :
  modifier le prompt d'un agent relance tous ceux qui le suivent.
- **Ports et specs.**
  - Le port 4310 est pris par un conteneur d'un autre projet ; la vague 2 utilisait 4400 à 4470.
  - `core/layout/nav/chrome-links.spec.ts` a échoué une fois sur trois passages complets, puis
    est passée seule et deux fois en suite complète : à surveiller.
  - Sur un aperçu local, les specs E2E `routing/` et `legacy/` échouent, car elles exigent nginx.
    Ne jamais lancer `register` ni `contact` (quota de 5 par heure).
