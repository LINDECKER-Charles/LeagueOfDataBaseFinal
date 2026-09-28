# Réalignement du front sur l'ancien site : état au 2026-09-28

L'utilisateur n'était pas satisfait du front Angular. La demande : comparer le nouveau front
(`src/LoDb.Web`) à l'ancien (Symfony/Twig/Vue, `app/`) et corriger le nouveau pour qu'il
rejoigne l'ancien. **L'ancien site est la cible** : aspect, mise en page, contenu, interactions et
comportement responsive. Restent volontaires les divergences d'URL et de SSR des ADR (locale en
préfixe, URLs id-slug, pas de session serveur), les champs de saisie d'au moins 16 px, les
propriétés CSS logiques (RTL) et le chrome traduit là où l'ancien site restait en anglais.

**Le chantier est terminé et intégré** dans `docs/reecriture-dotnet-angular` : deux vagues de
corrections, un second audit, ses corrections et les garde-fous complets. Les points que ce
chantier ne tranche pas sont listés en section 6.

## 1. Méthode

1. **Captures.** Les deux stacks tournent : l'ancienne sur :8080, `lodb-next` sur :18080. 29
   paires de pages sont capturées en 1440 et 390 px (`donnees/paires-de-pages.json`,
   `outils/comparaison/capture.mjs`).
2. **Premier audit.** 12 zones auditées, chacune suivie d'un vérificateur adverse : **339 écarts
   vérifiés** (9 bloquants, 109 majeurs, 153 mineurs, 68 finitions), un seul réfuté. Détail dans
   `donnees/ecarts.json` (description ancien/nouveau avec fichier:ligne, preuve, correctif vérifié,
   fichiers, lot).
3. **Corrections par lots.** Les écarts sont répartis par propriété de fichiers
   (`outils/comparaison/plan.mjs`). Chaque lot a son worktree et sa branche ; un correcteur
   l'implémente et le vérifie contre l'ancien site sur un aperçu local
   (`outils/comparaison/preview.mjs`), puis un relecteur adverse repasse avant la fusion.
4. **Second audit** sur la stack intégrée, puis corrections, même procédé (section 4).

## 2. Vague 1 : API, DS, SHELL, I18N

Contrat additif de l'API (art de chargement et résumé des cartes de champion, évolutions et
voisins des fiches, champs de l'admin), design system (fond d'ambiance, thèmes, dialogues, palette
admin), chrome (en-tête, pied, barre du bas, sélecteur patch/langue, admin hors du chrome public)
et i18n (clés brutes, course SSR sur les portées, pluriels). Rapports :
`donnees/vague-1-rapports.json`.

## 3. Vague 2 : les huit lots de pages

Interrompue le 2026-09-27 (machine saturée, section 7), reprise et terminée le 2026-09-28. Deux
commits de sauvegarde (ENTITYDET, HOME) avaient embarqué les 18 JSON du changelog à travers la
jonction `published` : défaits (`reset --soft`), le travail repris et committé proprement.
Rapports des correcteurs et des relecteurs : `donnees/vague-2-rapports.json`.

| Lot | Écarts | Correcteur | Relecteur |
|---|---|---|---|
| LISTS | 62 | 62 corrigés | rien à reprendre |
| DETAIL | 26 | 26 corrigés | 4 problèmes corrigés (visionneuse de chromas en paysage, tests) |
| ENTITYDET | 21 | 21 corrigés, `app.config.ts` rendu intact | 3 problèmes, dont un test manquant |
| HOME | 13 | 11 corrigés, 2 couverts par LISTS | 3 problèmes (repli RTL, conventions) |
| EDITORIAL | 18 | 17 corrigés, 1 déjà fait par DS | rien à reprendre |
| ACCOUNT | 42 | 39 corrigés, 1 partiel, 2 sans objet | 2 régressions corrigées (axe `list`, pseudo à 390 px) |
| BUILDS | 36 | 35 corrigés, 1 sans objet | 5 problèmes corrigés |
| ADMIN | 53 | 53 corrigés | 8 problèmes corrigés ; commit de 129 fichiers découpé en 11 |

Fusions `--no-ff` dans l'ordre LISTS, DETAIL, ENTITYDET, HOME, BUILDS, ACCOUNT, ADMIN, EDITORIAL.
Conflits résolus en gardant les deux apports : chemins de `ui/cards` (déplacés par LISTS),
aperçus de l'accueil (carte partagée de LISTS et portrait de HOME), tailles de dialogue (DETAIL et
ACCOUNT).

## 4. Second audit et corrections

Six groupes de pages (shell/DS/i18n, accueil et listes, fiches détail, éditorial, compte et
builds, admin), chacun audité puis vérifié par un adversaire : **64 écarts vérifiés** (4 majeurs,
30 mineurs, 30 finitions), 3 réfutés. Les 339 écarts du premier audit y sont recontrôlés
(`donnees/second-audit/anciens-ecarts.json`). L'erreur d'hydratation de `/en/items`
(`__ngContext__`) avait disparu : le texte riche de Riot contient des `<li>` nus, qui fermaient
les cellules de la grille en `ul/li` ; la grille est en `div role=list` depuis LISTS.

| Lot | Écarts | Correcteur | Relecteur |
|---|---|---|---|
| SHELL (A, D) | 18 | 17 corrigés, 1 partiel (A2-06) | 4 problèmes corrigés |
| CATALOGUE (B, C) | 19 | 19 corrigés | 2 régressions à 320 px et un écart DRY corrigés |
| APPS (E) | 11 | 9 corrigés, 1 partiel, 1 pris par SHELL | 3 problèmes corrigés |
| ADMIN (F) | 16 | 16 corrigés | rien à reprendre |

Écarts, rapports et relectures : `donnees/second-audit/`. APPS ajoute un point d'API additif,
`POST /api/account/reset-password/check`, qui dit si un lien de réinitialisation est encore
valable sans le consommer (contrat et client régénérés à l'intégration). ADMIN corrige la sonde
PostgreSQL à la source (`ServiceProbes.cs`), sans changer le contrat.

## 5. Intégration et garde-fous

État final : `c8c5a6ed`, 178 commits depuis l'état consigné la veille (`664926d0`), dont 12
fusions ; `lodb-next` reconstruite service par service (`api`, `web-ssr`) à ce commit.

- **Front** : typecheck, ESLint, suite Vitest complète (247 fichiers, 2 199 tests), `build:web`,
  Prettier sur les 500 fichiers touchés : verts. Seul avertissement : le bundle initial (686 kB
  pour un seuil d'avertissement de 500 kB, déjà dépassé avant ; section 6).
- **.NET** : `dotnet build -c Release`, 0 avertissement ; `dotnet test`, 2 928 réussis. Les 22
  échecs sont ceux de l'environnement : les 21 comparaisons de texte du checkout CRLF, et
  `PhotinoIsolationTests`, qui cherche `LoDb.slnx` en remontant depuis sa sortie et échoue quand
  `--artifacts-path` la met ailleurs (vert avec la sortie par défaut).
- **E2E** : suite complète contre `lodb-next`, 318 réussis, 1 ignoré, 0 échec.
- **Lighthouse** (`docs/reecriture/rapports/lighthouse.md`) : les cartes portrait avaient fait
  passer le CLS de la liste des champions de 0 à 1,198. Cause : quand le cache de transfert manque,
  la liste affiche son squelette, fait de tuiles de 9rem deux à trois fois plus basses que les
  cartes ; la page s'effondre, et les halos du fond, placés en pourcentage de sa hauteur, bougent
  avec elle. Le squelette d'une grille de portraits a désormais la hauteur d'une carte
  (`c8c5a6ed`) : CLS 0,001. Les cinq pages manquaient déjà les budgets Performance et LCP avant le
  chantier ; la liste des champions garde un LCP plus lent (5,3 s contre 3,6 s), dû à l'art de
  chargement hébergé par Data Dragon (section 6).
- **Contrat** : `api:check` signale une dérive sous Windows, faite uniquement des `\r\n` que les
  commentaires XML d'un checkout CRLF laissent dans les descriptions. Les documents committés sont
  normalisés en `\n` (`outils/comparaison/normalize-openapi.mjs`), comme une génération sous Linux.
- **Grammaire d'URL** : `tools/next/routing/check-urls.sh` n'a pas tourné, faute de `jq` sur le
  poste ; les specs E2E `routing/` et `legacy/` sont vertes.

## 6. À trancher, et restes connus

- **Budget du bundle initial.** Un correcteur l'avait relevé à 700 kB ; c'est annulé (`c50b33fe`) :
  relever un budget revient au propriétaire du projet. Deux voies : garder l'avertissement, ou
  réduire la feuille globale en sortant les fonctionnalités chargées à la demande du balayage
  Tailwind global (`@source not`, feuille chargée avec la fonctionnalité). Les classes des panneaux
  CDK (`dialog-picker.css`, `lightbox.css`) doivent rester globales.
- **LCP de la liste des champions.** Deux leviers : un `preconnect` vers
  `ddragon.leagueoflegends.com` (ni l'ancien ni le nouveau front n'en ont), et une mesure locale
  plus juste. En local, nginx transmet `Host: $host` sans le port : l'origine que le SSR range dans
  le cache de transfert (`http://localhost`) diffère de celle du navigateur
  (`http://localhost:18080`), le cache manque et la liste se redessine à l'hydratation. En
  production, sur le port standard, les deux coïncident. Transmettre `$http_host` au SSR rendrait
  la mesure locale fidèle ; à vérifier contre `LODB_ALLOWED_HOSTS`.
- **Divergences consignées dans `heritage.md`.** H7 : après plusieurs sauts de section, Retour
  rembobine l'adresse sans défiler (l'ancien site remontait en haut à chaque fois). H8 : les pages
  prérendues affichent « Patch » jusqu'au chargement de `/api/meta` (parité complète = amender
  l'ADR 0005). H9 : `auth.register.password_help` n'est pas affiché.
- **Arabe.** 407 clés manquent à `ar.json` (comme dans l'ancien `messages.ar.yaml`) : les textes de
  repli anglais sont isolés en `dir=ltr`, pas traduits.
- **Admin.** À 320 px, `/admin/monitoring` déborde de 24 px sur le nom de table insécable
  `pg_total_relation_size` (l'ancienne admin déborde davantage). Les 11 commits du découpage ADMIN
  de la vague 2 ne compilent pas un à un ; seul le dernier est vérifié.
- **Outillage et hygiène.** `tools/next/api` devrait normaliser lui-même les `\r\n` ;
  `check-urls.sh` demande `jq` ; cinq copies de `initials-of.ts` ; `ui/navigation` compte 10
  fichiers, la limite.

## 7. Ressources : ce qui paralysait la machine

Le premier passage (quatre agents) avait figé le poste. Mesures faites pendant la reprise :

- **Le disque.** Le dépôt, les worktrees et `node_modules` sont sur F:, un disque dur SMR (Seagate
  ST8000DM004) qui porte aussi E:, où est installé League of Legends. Les écritures aléatoires y
  font monter la latence de tout le poste. Les sorties de build et de Playwright (`dist`,
  `test-results`) sont redirigées vers le SSD (C:) par des jonctions
  (`outils/worktrees/ssd-outputs.sh`) ; les builds .NET prennent `--artifacts-path` sur C:.
- **Windows Defender** relit jusqu'à 260 Mo/s pendant un build ; une exclusion des dossiers de
  travail est à la décision de l'utilisateur.
- **Le sémaphore** `outils/ressources/heavy.sh` : au plus deux commandes lourdes sur la machine,
  en priorité basse (`nice`, soit BelowNormal), Vitest à 3 workers, build Angular, esbuild et
  MSBuild bridés ; il attend tant que F: sature (5 min au plus, sonde `watch-disk.ps1`), que la
  RAM libre est sous 12 Go, ou qu'une partie de League of Legends tourne (mode jeu, sans limite ;
  `game-guard.ps1` passe alors navigateurs et aperçus des agents en priorité Idle).
- **Le pool noyau.** Le cache de noms du Filter Manager (`FMfn`, 6,8 Go) et les FCB NTFS montent
  avec le nombre de fichiers touchés : huit worktrees lisant `node_modules` par huit chemins de
  jonction. Peu de worktrees à la fois ; `outils/ressources/pool-tags.ps1` donne le détail.
- **Recherche** : `git grep`, jamais `grep -r` sur un worktree (les jonctions `node_modules`).
- Au plus trois agents à la fois ; aucun aperçu ni navigateur orphelin.

## 8. Données et outils

- `donnees/` : `ecarts.json` (premier audit), `paires-de-pages.json`, rapports des vagues 1 et 2,
  `vague-2-notes.md` (passation), `second-audit/` (écarts vérifiés, anciens écarts recontrôlés,
  rapports des corrections).
- `outils/comparaison/` : captures, aperçu local d'un build, découpage en lots, normalisation des
  documents OpenAPI.
- `outils/ressources/` : sémaphore, sondes disque et RAM, mode jeu, pool noyau.
- `outils/worktrees/` : préparation (jonctions `node_modules` et changelog), sorties sur SSD,
  retrait des jonctions. **Toujours lancer `unprep-worktree.sh` avant `git worktree remove`**.
- `outils/workflows/` : les scripts des workflows d'agents. Ils codent en dur le scratchpad de la
  session (variables `S`, `LOCKS`) : à corriger avant réemploi. Les comptes de test et la clé TOTP
  de l'admin restent hors dépôt (`F:/Git/lodb-parite/`).
