# Écran de pré-rendu : refonte de l'expérience d'administration

> **Livrée** le 2026-10-21 (PR #87, en prod par #93) : l'écran décrit ici est celui de
> `features/admin/panels/management/prerender/`. Les passages « Aujourd'hui » (§ 0 et les
> constats des lots) décrivent l'écran et le code d'avant la refonte. La vitesse réglable et la file
> automatique (2026-10-22) l'ont complété : leur bloc est décrit dans la
> [spécification du catalogue pré-rendu](pre-rendu-catalogue-cdn.md), lot 2. Le 2026-10-24, la
> page est passée en six vues derrière des tuiles-onglets :
> [amendement](#amendement-du-2026-10-24--la-page-en-six-vues), qui l'emporte sur les § 2.1 à
> 2.3 et 5.1.

Spécification de conception du 21 octobre 2026 pour la page `/admin/prerender` (« Précalcul du
catalogue »). Elle complète le lot 2 de la [spécification du catalogue
pré-rendu](pre-rendu-catalogue-cdn.md) (§ 4, « Administration ») et l'[ADR
0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md), sans rien changer à la passe
elle-même : ni à son ordre par (version, langue), ni à son stockage, ni à la bascule d'epoch par
variable et redéploiement. Les [pièges connus](../architecture/pieges.md) (§ « Catalogue
pré-rendu et CDN ») restent vrais.

Elle est écrite pour que cinq lots l'implémentent en parallèle sans se parler : chaque
fichier a un propriétaire (§ 8), chaque contrat entre lots est donné à la lettre (§ 6 pour
l'API, § 7.1 pour les éléments publics du front), chaque texte a sa clé (§ 7.3).

Révision du 21 octobre 2026, après relecture critique : estimation du reste par version
(§ 6.3), rendu d'une passe en pause (§ 4.4, § 4.5), epoch prête malgré des échecs définitifs
(§ 6.5), couverture consciente des types (§ 6.5, § 6.6), modèle de sélection complété
(§ 3.3.2), périmètre compact dans les listes (§ 6.3), dialogue de bascule sans commande
dangereuse (§ 5.5), arborescence à dix fichiers par dossier (§ 7.1), découpage en cinq lots
aux fichiers disjoints (§ 8).

Abréviations de chemins, utilisées partout :

| Abréviation | Chemin réel |
|---|---|
| `prerender/` | `src/LoDb.Web/src/app/features/admin/panels/management/prerender/` |
| `admin/` | `src/LoDb.Web/src/app/features/admin/` |
| `Prerender/` | `src/LoDb.Api/Modules/Admin/Prerender/` |
| `Persistence/Prerender/` | `src/LoDb.Infrastructure/Persistence/Prerender/` |
| `i18n/admin/` | `src/LoDb.Web/public/i18n/admin/` (`fr.json`, `en.json`) |

Sommaire : [0. Constat et principes](#0-le-constat-et-ce-que-la-refonte-change) ·
[1. Parcours](#1-parcours-de-lopérateur) · [2. La page](#2-architecture-de-la-page) ·
[3. La modale de lancement](#3-la-modale-de-lancement) · [4. Le suivi en direct](#4-le-suivi-en-direct) ·
[5. Couverture, stockage, historique](#5-couverture-stockage-et-coûts-historique) ·
[6. Contrat d'API](#6-contrat-dapi) · [7. Architecture front](#7-architecture-front) ·
[8. Lots parallèles](#8-découpage-en-lots-parallèles).

## Amendement du 2026-10-24 : la page en six vues

**Constat.** Sous l'en-tête s'empilaient le moniteur, les quatre tuiles, les blocs des deux
files automatiques et de la vitesse (ajoutés les 22 et 23 octobre), puis les quatre onglets :
ceux-ci commençaient à près de trois écrans du haut, la file des 503 vivait en deux endroits
(bloc et onglet), et « Couverture » dépliait d'office toutes les saisons à manques, soit 403
lignes sur une pile neuve et quelque 20 000 px de page.

**La page.**

| # | Zone | Composant |
|---|---|---|
| 1 | En-tête, inchangé : lancer, actualiser, fraîcheur, raison du blocage et « Suivre » | `PageHead` + `LaunchCta` |
| 2 | Bandes d'état, inchangées | `PrerenderBands` |
| 3 | Six tuiles-onglets, à la fois chiffres clés et navigation : Passe, Couverture, Files automatiques, Vitesse, Stockage et coûts, Historique | `StatusTabs` (`admin/widgets/status-tabs/`), `tilesOf` (`overview/tiles/`) |
| 4 | La vue choisie, seule visible : toutes sont rendues, les autres masquées, et gardent leur état (filtres, sélection, page de liste) | `lodb-tab` |

- **Passe**, vue par défaut : le moniteur, le récapitulatif ou la carte « aucune passe », comme
  avant. Les actions de la passe (pause, reprise, annulation) et leur résultat montent en tête
  du moniteur, au bout de la ligne de son origine : elles ne sont plus sous la matrice.
- **Couverture** : l'ancien onglet. Les saisons à manques ne s'ouvrent d'office que tant
  qu'elles tiennent en 60 lignes, la plus récente toujours (`openSeasonsOf`) ; « Tout déplier »
  et « Tout replier ».
- **Files automatiques** : les blocs des deux files côte à côte, puis la liste « Pages de la
  file des refusées (503) », l'ancien onglet « File des 503 ».
- **Vitesse** : le bloc « Vitesse du précalcul ».
- **Stockage et coûts** : l'ancien onglet, qui reprend ce que seules les tuiles disaient :
  l'epoch servie et sa variable, la part de la file dans le total des pages.
- **Historique** : l'ancien onglet.

**Les tuiles** (`tilesOf`, fonctions pures) : la part de la passe active, celle du titre du
document, ou la fin d'une passe de moins de 24 h, ou « Aucune » et si les promotions en ouvrent
une ; la couverture de l'epoch servie et ses patchs complets, ou l'epoch prête ; les pages en
attente des deux files ; le mode de vitesse, puis le rythme en vigueur, la cible ou la plage ; le
volume stocké, son coût par mois et ses pages ; le nombre de passes et la plus récente. Un
point dit qu'une vue a quelque chose à dire : cyan animé (`live`) quand une passe tourne ou que
la file des 503 rend ; or pour une passe en pause, le frein, une cible au-delà de ce que tiennent
les moteurs, une file sans relevé ; rouge pour une passe en pause d'elle-même ou un hôte non
configuré ; vert pour une epoch prête ; gris pour une file en veille. Le texte de la tuile le
dit aussi : jamais la couleur seule (WCAG 1.4.1). Les tuiles sont des onglets ARIA nommés par
leur vue et décrits par leur chiffre ; deux par ligne sur téléphone, sans leur ligne de
détail, trois sur tablette, six au-delà de 1280 px.

**La vue dans l'URL** : `?view=coverage|queues|speed|storage|history`, rien pour la passe. Elle
remplace l'entrée de l'historique au lieu d'en ajouter une : un rechargement ou un lien partagé
rouvre la vue, « Précédent » quitte la page. « Suivre » (en-tête, historique, détail d'une
passe) ramène à la vue de la passe et y porte le focus. Les files et la vitesse sont lues par
des magasins de la page (`DemandReportStore`, `RevisionReportStore`, `SpeedReportStore`) : une
tuile et son bloc montrent la même réponse, lue une fois.

## 0. Le constat, et ce que la refonte change

**Aujourd'hui** (`prerender/prerender-panel.html`) la page empile, sous quatre tuiles, une carte
de passe active (`pass/current-pass-card`, une barre `<progress>` de 8 px sur les unités et cinq
relevés), un formulaire plat (`launch/launch-form` : epoch, deux champs texte « plus ancienne /
plus récente », **un** type de page, **une** langue, une zone de texte de priorités), le
stockage (`storage/storage-section`) puis l'historique (`pass/pass-history-table`). Le panneau se
relit toutes les 20 s pendant une passe (`prerender-poll.ts`, `poll-while.ts`), d'un bloc, rapport
complet compris. Ce qu'on ne peut pas faire : choisir 37 patchs précis, viser trois langues, voir
quelle unité tourne, savoir pourquoi rien n'avance, voir quels patchs manquent à l'epoch servie,
relancer ce qui a échoué.

**Après la refonte**, la page répond dans l'ordre à trois questions :

1. **Que se passe-t-il maintenant ?** Un moniteur en direct en tête de page quand une passe est
   active (barre segmentée, pourcentage, rythme instantané et moyen, fin estimée, l'unité en
   cours et sa propre barre, ce qui attend et pourquoi, la matrice version × langue, le fil
   d'activité, les actions avec leurs conséquences), lu toutes les 5 s par un point d'accès léger
   et incrémental.
2. **Qu'est-ce qui manque, et combien ça coûte ?** Des tuiles (epoch servie, couverture des
   anciens patchs, volume, coût), puis trois onglets : Couverture, Stockage et coûts, Historique.
3. **Que lancer ?** Une modale en trois étapes : choisir les patchs (liste par saison, clic,
   Maj+clic pour une plage, saisons à trois états, filtres, clavier complet, priorités, liste
   collée depuis le journal de l'edge), le périmètre (langues, types, epoch écrite, « ignorer ce
   qui est déjà complet »), puis un récapitulatif chiffré (unités, pages, volume, durée, coût
   ajouté, avertissements) avant de lancer.

### 0.1 Principes de conception

- **Le direct d'abord.** Une passe dure des jours (13 à 19 jours au rythme adaptatif de la
  voie `prerender`, spécification § 4 lot 2, « Débit ») : quand elle tourne, c'est elle qu'on vient
  voir. Rien d'autre ne passe au-dessus d'elle, sauf une bande qui exige une action.
- **Toute action dit sa conséquence avant d'agir.** Lancer montre le récapitulatif chiffré ;
  mettre en pause, reprendre une passe arrêtée d'elle-même et annuler ouvrent un dialogue qui dit
  ce qui va se passer, avec les chiffres de la passe.
- **Une estimation se marque.** Tout chiffre estimé porte « ≈ », et un pli « Comment c'est
  estimé » donne la formule et ses données. Un chiffre lu n'en porte pas.
- **Rien ne casse pendant un redéploiement.** Une `502` garde la dernière valeur à l'écran, dit
  depuis quand elle date, et réessaie avec un recul (comportement déjà voulu par
  `prerender-panel.ts`, `linkedSignal` `isRunning` : « a 502 while the API redeploys must not end
  the polling »).
- **Le clavier est un citoyen de première classe** dans la modale : toute la sélection se fait
  sans souris (§ 3.3.5).
- **Aucune donnée inventée.** Les signaux internes de la tâche de fond (attentes de catalogue, SSR
  injoignable) ne sont montrés que si l'instance qui répond les observe ; sinon la page le dit
  (§ 4.5).
- **Les conventions de la maison** (`AGENTS.md`) : composants autonomes, signaux, `OnPush`,
  sans zone, `inject()`, jetons Hextech seulement (`src/LoDb.Web/src/styles/foundation/tokens.css`),
  propriétés logiques (RTL), champs à 16 px au moins, client généré seulement, fichiers ≤ 300
  lignes, fonctions ≤ 30 lignes, ≤ 3 paramètres, un élément public par fichier.

### 0.2 Vocabulaire

| Terme | Sens dans ce document | Où dans le code |
|---|---|---|
| Passe | une exécution qui écrit un périmètre dans une epoch ; une seule active (en cours ou en pause) | `PrerenderPass`, index `ux_prerender_pass_active` |
| Unité | une (version, langue) d'une passe : ses pages, son curseur, ses compteurs | `PrerenderUnit`, clé `(pass_id, version, locale)` |
| Ancien patch (épinglé) | toute version de Data Dragon plus ancienne que la dernière | `IPrerenderCatalog.PinnedVersionsAsync` |
| Epoch servie | le dossier `pages/<N>/` que lit nginx, `LODB_ARCHIVE_SERVE_EPOCH` | `PrerenderOptions.ServedEpoch` |
| Epoch en attente | plus récente que la servie, écrite, pas servie | `EpochStates.Standby` |
| Cellule | l'état d'une (version, langue), dans une passe (progression) ou dans une epoch (couverture) | § 6.4, § 6.7 |
| Couverture d'une epoch | la part des (ancien patch, langue) qu'une unité y a terminée sur tous les types de pages | § 6.5 |
| Types tenus | pour une (version, langue) d'une epoch, les types de pages dont la dernière unité terminée qui les couvrait n'a raté aucune page ; un masque de 4 bits | § 6.5 |
| Manque | une cellule de couverture `-` (absente) ou `p` (partielle) : ce qu'une passe « Compléter » vise | § 6.5 |
| Échec définitif | une cellule `f` : l'unité a échoué (version inconnue de Data Dragon, catalogue jamais arrivé) ; le SSR sert ces pages, une relance peut les reprendre | § 6.5 |
| Saison | les patchs d'un même premier segment (`16.x` = 2026) | § 3.3.1 |

## 1. Parcours de l'opérateur

Cinq parcours, chacun avec son déclencheur, ses étapes et ce qu'il doit voir. Les numéros de
section renvoient à l'endroit où chaque besoin est servi.

### P1 — Lancer une passe ciblée

**Déclencheurs.** Le journal de l'edge (LogsQL, `stats by (version)`) montre une demande sur des
patchs non couverts ; la couverture de l'epoch servie montre des trous (une passe annulée, des
unités en échec) ; un correctif du front impose une epoch neuve (guide
`docs/guides/configuration.md` § 4.8, « Correctif de sécurité du front ») ; le staging a besoin
de quelques patchs pour un test.

**Étapes.** Bouton « Lancer une passe » de l'en-tête (ou « Pré-rendre la sélection » depuis la
couverture, ou « Relancer » depuis l'historique, ou « Compléter l'epoch N » depuis une bande) →
modale, étape 1 : cocher les patchs (Maj+clic pour une plage, une saison d'un coup, « Non
couverts »), en marquer quelques-uns prioritaires ou coller la liste du journal → étape 2 : les
langues, les types, l'epoch, « ignorer ce qui est déjà complet » → étape 3 : lire le
récapitulatif et ses avertissements → « Lancer la passe (N unités) » → la modale se ferme, une
bande dit « Passe n°12 lancée… », le moniteur apparaît en tête avec la nouvelle passe.

**Ce qu'il doit voir.** Pour chaque patch : sa couverture dans l'epoch visée (n/21 langues et
lesquelles), son volume, son statut ; le nombre de patchs choisis et de prioritaires ; à tout
moment, en pied de modale, le volume estimé (unités, pages, durée) ; au récapitulatif, le coût
mensuel ajouté et ce qui sera ignoré ou refait ; en cas de refus, quel champ et pourquoi, à
l'étape concernée. Servi par § 3.

### P2 — Suivre une passe en cours

**Déclencheur.** Une passe tourne (lancée par l'opérateur, par une promotion ou par la ligne de
commande `prerender unit`).

**Étapes.** Ouvrir la page → le moniteur montre l'avancement et se met à jour seul toutes les
5 s → au besoin, ouvrir la matrice, une version, le fil d'activité → mettre en pause, reprendre
ou annuler.

**Ce qu'il doit voir.** « À combien on est » : le pourcentage et la barre écrites / sautées / en
échec / restantes ; les unités terminées sur le total ; le rythme maintenant et en moyenne, une
courbe des 15 dernières minutes ; la fin estimée, relative et absolue ; le temps actif et le
volume écrit ; l'unité en cours, sa barre (curseur / pages), depuis quand et sa dernière page ;
les unités suivantes ; ce qui attend (catalogue absent, version qui vient de quitter le dernier
rang) ; la fraîcheur des données (« mis à jour il y a 4 s »). Servi par § 4.

### P3 — Diagnostiquer un échec

**Déclencheurs.** Des cellules rouges dans la matrice, un compteur « en échec » qui monte, une
passe en pause qu'on n'a pas mise en pause, un rythme tombé à zéro.

**Étapes.** Le moniteur affiche une bande d'arrêt ou de blocage avec sa cause probable → « Voir
les échecs » liste les unités en échec ou avec des pages en échec, avec la raison de la dernière
page ratée → un clic sur une version ouvre ses 21 unités → une fois la cause corrigée :
« Reprendre » (passe arrêtée d'elle-même) ou « Relancer 17 unités (≈ 19 000 pages) » (nouvelle
passe préremplie, le libellé chiffre ce qui sera refait, § 4.6).

**Ce qu'il doit voir.** Pourquoi la passe s'est mise en pause d'elle-même (clé du stockage
refusée, hôte public refusé par le SSR : `PageResult.IsMisconfigured`, aujourd'hui seulement
journalisé par `UnitRunner.StopAsync`) ; pourquoi aucune page n'avance (SSR injoignable,
catalogue en attente avec l'heure d'abandon à 30 min, `UnitWaits.Longest`, tâche de fond coupée
sur l'instance) ; pour chaque unité en échec, la raison (aujourd'hui seulement dans la ligne
`UnitCompleted` du journal, `UnitTally.LastFailure`). Servi par § 4.5, § 4.6, § 6.9.

### P4 — Vérifier ce qui est stocké et ce que ça coûte

**Déclencheurs.** La facture du mois, une décision de retrait d'epoch, une question « quels patchs
sont servis depuis le stockage ? ».

**Étapes.** Tuiles (stocké, coût estimé) → onglet « Stockage et coûts » : les epochs, leur
couverture, leur volume, leur coût, les bundles, la facture Scaleway réelle à côté de
l'estimation, ce que coûterait une epoch neuve → onglet « Couverture » : chaque ancien patch,
complet, partiel ou absent dans l'epoch choisie.

**Ce qu'il doit voir.** Ce qui est compté et ce qui ne l'est pas (sortie, epochs retirées,
expiration du staging : textes existants `admin.prerender.storage.*`, gardés) ; la couverture
en pourcentage d'unités et en patchs complets ; pour chaque patch, ses langues présentes. Servi
par § 5.2 et § 5.3.

### P5 — Basculer l'epoch servie

**Déclencheurs.** Une epoch plus récente n'a plus de manque (bande « L'epoch 3 est prête »,
avec ses éventuels échecs définitifs) ; ou il faut basculer tout de suite sur une epoch neuve
(correctif de sécurité du front).

**Étapes.** Bande « prête » → « Comment basculer ? » → dialogue de bascule : l'état de l'epoch
(prête, avec ce qui passera par le SSR : échecs définitifs, pages en échec ; ou couverte à X %),
la variable à poser, copiable, le redéploiement, et l'étape du retrait **en texte seulement**
→ après le redéploiement, la page affiche la nouvelle epoch servie, l'ancienne devient
« retirée » et sa ligne de « Stockage et coûts » propose « Retirer… » : les variables du
retrait, copiables, maintenant qu'elle n'est plus servie (§ 5.3, § 5.5). Si l'epoch a encore
des manques : bande « presque prête » → « Compléter l'epoch 3 » → modale préremplie (§ 3.1).

**Ce qu'il doit voir.** Qu'aucun bouton ne bascule d'ici (nginx lit l'epoch à son démarrage,
ADR 0011) ; la valeur exacte à poser ; ce qui manque encore ; qu'une passe écrit encore cette
epoch, le cas échéant. Servi par § 2.4 (bandes) et § 5.5.

## 2. Architecture de la page

### 2.1 Les zones, dans l'ordre

| # | Zone | Composant (lot) | Visible quand |
|---|---|---|---|
| 1 | En-tête : sur-titre, titre, chapeau, **« Lancer une passe »**, « Actualiser » et la fraîcheur du rapport (`LiveBadge` compact, § 2.5) | `PageHead` existant (`admin/layout/page-head.ts`, gagne une entrée `documentTitle`, § 2.5) + `LaunchCta` (F4) | toujours |
| 2 | Bandes d'état : non configuré, epoch prête, epoch presque prête | `PrerenderBands` (F4) | selon l'état (§ 2.4) |
| 3 | Moniteur en direct **ou** récapitulatif de fin **ou** carte « aucune passe » | `LiveMonitor` / `PassRecap` (F3), `IdleCard` (F4) | un des trois, toujours |
| 4 | Quatre tuiles : epoch servie, couverture servie, stocké, coût / mois | `PrerenderKpis` (F4) | dès le premier rapport |
| 5 | Onglets « Couverture », « Stockage et coûts », « Historique » | `lodb-tabs` existant (`src/app/ui/tabs/tabs.ts`) + trois onglets (F4) | dès le premier rapport |

La bande des notices d'action (`NoticesOutlet`, `admin/layout/notices-outlet.ts`) reste au-dessus
de tout, posée par le cadre de l'admin : elle dit « Passe n°12 lancée… » ou un refus du
lancement. Elle s'efface quand le moniteur voit la passe finir (`ended`) : « Passe n°2 lancée »
au-dessus de « Passe n°2 terminée » se contredirait. La pause, la reprise et l'annulation ne
l'utilisent pas : l'admin a cliqué au bas du moniteur, souvent bien plus bas (§ 4.7).

L'ancienne tuile « Passe active » disparaît : le moniteur la remplace. Les anciennes règles
`admin.rules.current_pass` et `admin.rules.launch` disparaissent avec les sections qu'elles
séparaient.

**Pourquoi des onglets** (et pas la longue page actuelle) : les trois vues du bas sont de la
consultation, rarement ensemble ; le moniteur reste ainsi à moins d'un écran de tout. Le composant
`lodb-tabs` rend tous les panneaux et cache les autres (`src/app/ui/tabs/tab.ts`, « Every panel
is rendered ») : chaque onglet charge donc ses données lui-même ou les reçoit du panneau, jamais
« à l'ouverture de l'onglet ». Pas de paramètre d'URL : `Tabs` garde son choix en interne et la
page n'a pas besoin de lien profond vers un onglet.

### 2.2 Bureau (colonne de 78rem, `admin/frame/admin-frame.css`, `.admin-column`)

Passe en cours, epoch 3 prête à servir n'étant pas le cas ici, epoch 2 servie :

```
GESTION
Précalcul du catalogue                                        [⟳ Actualiser] [▶ Lancer une passe]
Les pages des anciens patchs, rendues d'avance et servies      Une passe est en cours (n°12) :
sans le SSR.                                                   une seule à la fois. Suivre ↓
──────────────────────────────────────────────────────────────────────────────────────────────
┌ PASSE N°12 · EPOCH 2 ─────────────────────────────── ● En direct · mis à jour il y a 4 s ─┐
│ [en cours]  lancée le 18/10/2026 08:00 par charles · 212 patchs · 21 langues · tous types   │
│             · 3 prioritaires                                                               │
│                                                                                            │
│  71.4 %                                         1 532 410 / ≈ 2 146 000 pages              │
│  ██████████████████████████████████▒░░░░░░░░░░░░░░░░                                        │
│  ◆ écrites 1 512 300  ◆ sautées 19 840  ◆ en échec 270  ◆ restantes ≈ 613 590               │
│  Unités : 3 024 / 4 452 — 1 en cours · 1 410 à venir · 17 en échec                          │
│                                                                                            │
│  RYTHME                 FIN ESTIMÉE               TEMPS ACTIF        ÉCRIT                  │
│  246 pages/min ▁▂▃▅▆▅▆  23/10/2026 14:00          3 j 4 h            58.98 GB               │
│  moyenne 231 pages/min  dans 2 j 3 h (moyen)       pauses exclues    ≈ 39.0 kB par page     │
│                         au rythme actuel : 1 j 22 h                                        │
│ ────────────────────────────────────────────────────────────────────────────────────────── │
│ EN CE MOMENT                                     │ ACTIVITÉ                                │
│ 16.18.1 · fr (français)                          │ 08:42 ✓ 16.18.1 · en  1 140 p · 6 min   │
│ ███████░░░░░░░░░░░░░░  312 / 1 140 pages (27 %)  │ 08:36 ✓ 16.18.1 · el  1 138 p · 6 min   │
│ depuis 4 min · dernière page il y a 2 s          │ 08:30 ⚠ 16.18.1 · de  2 pages en échec  │
│ Ensuite : 16.18.1 · hu  id  it  ja  ko           │ 08:29 ⊕ 16.20.1 a rejoint la passe (21) │
│ ⏳ 16.3.1 attend : son catalogue n'est pas encore │ 08:12 ✕ 15.2.1 · th  en échec           │
│    stocké, depuis 3 min ; abandon à 09:12         │ [Tout afficher (14)]                    │
│ ────────────────────────────────────────────────────────────────────────────────────────── │
│ ⚠ 17 unités en échec ou avec des pages en échec   [Voir les échecs ▾] [Relancer 17 unités…] │
│ ────────────────────────────────────────────────────────────────────────────────────────── │
│ MATRICE  [Matrice|Tableau]  [Tout déplier]   ■ terminée ◧ en cours □ à venir ▣ échecs ✕     │
│               ar cs de el en es fr hu id it ja ko pl pt ro ru th tr vi zhs zht              │
│ ▸ 16.x · 2026 ▓▓ ▓▓ ▓▓ ▓▓ ▓▓ ▓▓ ▒▒ ░░ ░░ ...           19 patchs · 17 terminés · 1 en cours │
│ ▾ 15.x · 2025                                            24 patchs · 20 terminés · 3 échecs │
│   15.24.1     ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■               │
│   15.23.1     ■  ■  ■  ▣  ■  ■  ■  ✕  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■  ■               │
│ ▸ 14.x · 2024 ░░ ░░ ...                                  24 patchs · 0 terminé              │
│ ────────────────────────────────────────────────────────────────────────────────────────── │
│                                              [⏸ Mettre en pause]  [Annuler la passe…]      │
└────────────────────────────────────────────────────────────────────────────────────────────┘
┌ EPOCH SERVIE ──┐┌ COUVERTURE SERVIE ─┐┌ STOCKÉ ───────────┐┌ COÛT / MOIS ──────────────┐
│ 2              ││ 71.4 %             ││ 58.98 GB          ││ 0,47 €                    │
│ LODB_ARCHIVE_… ││ 151 / 212 patchs   ││ 1 532 410 pages   ││ hors sortie ; détail sous │
└────────────────┘└────────────────────┘└───────────────────┘└───────────────────────────┘
 COUVERTURE   STOCKAGE ET COÛTS   HISTORIQUE
 ═══════════
 (contenu de l'onglet, § 5)
```

Les chiffres des maquettes sont illustratifs : ils supposent un plafond relevé
(`LoDb:Prerender:RendersPerMinute`, 120 par défaut en prod, 30 au staging). Au plafond par
défaut, une passe entière prend 10 à 16 jours, et le rythme affiché ne dépasse pas le plafond
(une page sautée a été rendue elle aussi).

Sans passe active, la zone 3 devient (carte `IdleCard`) :

```
┌ AUCUNE PASSE EN COURS ─────────────────────────────────────────────────────────────────────┐
│ Le stockage ne reçoit rien. Les patchs promus ouvrent une passe d'eux-mêmes.                │
│ Dernière passe : n°11, terminée le 17/10/2026 22:14 · 4 452 unités · 2 146 000 pages [Détail]│
│ L'epoch servie couvre 97.4 % des unités des anciens patchs : 11 patchs ont des manques. [Les compléter] │
│                                                                   [▶ Lancer une passe]      │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

Passe en pause, arrêtée d'elle-même (le moniteur garde sa structure ; bande en tête, rythme
remplacé, matrice grisée de 30 %) :

```
┌ PASSE N°12 · EPOCH 2 ────────────────────────────────── ● En direct · mis à jour il y a 12 s ┐
│ ┌ La passe s'est mise en pause d'elle-même : The store refused the writer key (403).        │
│ │ Corrigez la configuration du stockage ou du SSR (clé, politique, hôte public), puis      │
│ │ reprenez.                                                            [Reprendre…]        │
│ └──────────────────────────────────────────────────────────────────────────────────────── │
│ [en pause]  depuis 2 h 12 · lancée le 18/10/2026 08:00 par charles · …                     │
│  71.4 %  ████████████████████████████████████▒░░░░░░░░░░░░░░ (barre désaturée)             │
│  RYTHME à l'arrêt   FIN ESTIMÉE suspendue pendant la pause   TEMPS ACTIF 3 j 4 h   …        │
│  REPRENDRA PAR 16.18.1 · fr, arrêtée à 312 / 1 140 pages (cellule « z », sans halo)       │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Téléphone (360 px, gouttières de 16 px, aucun défilement horizontal de la page)

```
GESTION
Précalcul du catalogue
Les pages des anciens patchs…
[▶ Lancer une passe        ]   ← pleine largeur, désactivé
Une passe est en cours (n°12) : une seule à la fois.
[⟳ Actualiser]
┌ PASSE N°12 · EPOCH 2 ──────┐
│ ● il y a 4 s               │
│ [en cours] 212 patchs ·    │
│ 21 langues · tous types    │
│ 71.4 %                     │
│ ██████████████▒░░░░░░      │
│ 1 532 410 / ≈ 2 146 000 p. │
│ Unités 3 024 / 4 452       │
│ ────────────────────────── │
│ RYTHME   246 p/min ▁▃▅▆    │
│ FIN      dans 2 j 3 h      │
│          23/10/2026 14:00  │
│ ACTIF 3 j 4 h · 58.98 GB   │
│ ────────────────────────── │
│ EN CE MOMENT               │
│ 16.18.1 · fr        27 %   │
│ █████░░░░░░░ 312 / 1 140   │
│ ⏳ 16.3.1 attend (3 min)    │
│ ────────────────────────── │
│ ▸ Activité (14)            │
│ ▸ Échecs (17)              │
│ ▸ Matrice (212 patchs)     │
│ [⏸ Pause] [Annuler…]       │
└────────────────────────────┘
┌ Epoch ───┐┌ Couverture ┐
│ 2        ││ 71.4 %      │
└──────────┘└─────────────┘
┌ Stocké ──┐┌ Coût / mois ┐
│ 58.98 GB ││ 0,47 €      │
└──────────┘└─────────────┘
 COUVERTURE  COÛTS  HISTORIQUE  ← « Coûts » sur téléphone ; un débord s'estompe au bord
```

Règles du téléphone (sous `sm`, 640 px ; `(max-width: 639.98px)` comme
`features/builds/editor/items/armory-opener.ts`) :

- le CTA prend toute la largeur ; sa raison de blocage s'écrit dessous, jamais en infobulle ;
- les quatre relevés du moniteur passent en liste à deux colonnes (libellé / valeur) ;
- « Activité », « Échecs » et « Matrice » sont des replis (`FoldToggle`-like, `aria-expanded`)
  fermés par défaut ; la matrice dépliée défile horizontalement **dans son cadre** si 21 colonnes
  ne tiennent pas (cellules de 9 à 16 px, § 4.4) ;
- les tuiles passent en grille 2 × 2 (`.kpi-grid`, `minmax(11rem, 1fr)`, déjà en place) ;
- les dialogues s'ouvrent en feuille du bas (`DialogService.openSheet`), comme l'armurerie.

### 2.4 Tous les états de la page

| État | Comment on le sait | Ce qui s'affiche |
|---|---|---|
| Premier chargement | rapport sans valeur ni échec | `PanelState` forme `cards` (squelettes, `admin/state/panel-state.html`) ; CTA désactivé, raison « Chargement de l'état du précalcul… » |
| Échec du premier chargement | rapport en échec, sans valeur | bande rouge de `PanelState` avec « Réessayer » (et « Se reconnecter » sur `session`) ; rien d'autre |
| Échec d'un rafraîchissement | rapport en échec, avec une valeur | **pas de bande** : données gardées, le `LiveBadge` de l'en-tête passe en `late` puis `lost` (« Hors ligne depuis 2 min (HTTP 502) ») ; le moniteur a son propre indicateur. Un redéploiement pendant une passe de plusieurs jours n'allume donc aucune fausse alarme. Seule une `401`/`403` monte la bande « Se reconnecter » (ligne « Session expirée ») |
| Non configuré | `report.isConfigured === false` | bande rouge `overview.bands.dormant` (ce qu'il faut poser) ; CTA désactivé (`locked_dormant`) ; carte « aucune passe » sans CTA ; onglets visibles |
| Aucune passe | `report.current === null`, pas de passe finie récente non masquée | `IdleCard` |
| Passe finie récemment | la plus récente passe est `completed`/`cancelled`, `finishedAt` il y a moins de 24 h, pas masquée par ce visiteur | `PassRecap` (§ 4.8) à la place de l'`IdleCard` |
| En cours | `current.state === 'running'` | `LiveMonitor` complet, lu toutes les 5 s |
| En pause par un administrateur | `paused`, `pauseReason === null` | moniteur : badge « en pause depuis… », rythme « à l'arrêt », fin « suspendue », « Reprendra par » au lieu d'« En ce moment » (l'unité restée `running` en base est rendue arrêtée, code `z`, § 4.4, § 4.5), bouton « Reprendre » (or), lecture toutes les 30 s |
| En pause d'elle-même | `paused`, `pauseReason !== null` | idem + bande rouge en tête du moniteur avec la raison ; « Reprendre… » ouvre un dialogue d'avertissement |
| Bloquée | en cours, aucune page écrite depuis 3 min (`stallOf`, § 4.5) | bande dorée dans le moniteur : « Aucune page écrite depuis 4 min » + cause probable |
| Unité en échec en masse | en cours, un cinquième au moins des pages de l'unité en cours en échec, sur 10 pages au moins (`failingUnitOf`, § 4.5) | bande rouge dans le moniteur : unité, pages en échec, dernière raison, « Détail du patch », « Mettre en pause » |
| Démarrage | en cours, ouverte il y a moins de 90 s, aucune unité commencée | ligne « En attente de la tâche de fond : elle prend une passe à son prochain tour (30 s au plus) » (`PrerenderWorker.Period`) |
| Epoch prête | `report.readyEpoch !== null` : plus aucun manque (§ 6.5) | bande verte : la variable copiable + « Comment basculer ? » (§ 5.5) ; si des cellules `f` restent : « 12 unités en échec définitif passeront par le SSR » (et les pages en échec des cellules `w`) |
| Epoch presque prête | une epoch `standby` non écrite, avec des manques, `(attendues − manques) / attendues ≥ 90 %` | bande verte : ce qui manque + « Compléter l'epoch N » (modale préremplie) |
| 409 au lancement | réponse de `POST …/passes` | dans la modale (§ 3.6), jamais sur la page |
| Session expirée | `401`/`403` | bande de `PanelState` « Se reconnecter » ; toutes les lectures s'arrêtent |

Une seule bande d'epoch à la fois : « prête » l'emporte sur « presque prête ».

### 2.5 Stratégie de rafraîchissement

Quatre lectures, chacune à son rythme. Toutes passent par `injectLivePoll` (F1, § 7.1), qui
remplace `poll-while.ts` : un minuteur relancé **après** la réponse précédente (jamais deux lectures
en vol), suspendu ou ralenti quand l'onglet est masqué (`hiddenEvery`), avec recul sur erreur.

| Donnée | Point d'accès | Lue quand | Cadence | Onglet masqué | Sur erreur |
|---|---|---|---|---|---|
| Rapport | `GET /api/admin/prerender` (existant) | ouverture ; après chaque action ; à la fin d'une passe observée ; « Actualiser » | 60 s si une passe est active, 120 s sinon (une promotion peut en ouvrir une, `VersionEnqueuer`) | **ralentie à 300 s** (pour que le titre de l'onglet et la notice de fin restent vrais) ; relue au retour si la dernière lecture a plus d'une cadence | recul `pollDelay` ; la valeur reste ; aucune bande (§ 2.4) |
| Progression | `GET …/passes/{id}/progress?since=` (§ 6.7) | tant qu'une passe est active | 5 s en cours, 30 s en pause | suspendue ; au retour, lecture **complète** si la dernière a plus de 10 min, sinon incrémentale | 5 → 10 → 20 → 40 → 60 s ; `401`/`403` : arrêt ; `404` : relire le rapport |
| Couverture | `GET …/patches?epoch=` (§ 6.6) | ouverture de la page (l'onglet Couverture est rendu d'emblée, `tab.ts`) ; changement d'epoch dans l'onglet ou la modale ; fin d'une passe ; « Actualiser » | aucune | — | bande dans l'onglet, « Réessayer » |
| Détail d'une version | `GET …/passes/{id}/versions/{version}` (§ 6.8) | ouverture du dialogue | 10 s tant qu'une de ses unités tourne | suspendue | message dans le dialogue |

La facture Scaleway garde son propre chargement (`scaleway-bill/inject-scaleway-bill.ts`) :
la page ne l'attend jamais.

**Le titre de l'onglet.** L'opérateur laisse l'onglet en arrière-plan pendant les 13 à 19 jours
d'une passe : le titre du document porte donc l'avancement, tiré du rapport
(`passTitleOf(report)`, F4, pur), par l'entrée `documentTitle` que F4 ajoute à `PageHead`
(qui la passe à `injectAdminTitle`, `admin/layout/admin-title.ts`, sans changer le `<h1>`) :

| État | Titre (avant « · Admin · LODB ») |
|---|---|
| en cours | « 71 % · Précalcul du catalogue » |
| en pause | « ⏸ 71 % · Précalcul du catalogue » |
| terminée il y a moins de 24 h, non masquée | « ✓ Passe n°12 terminée · Précalcul du catalogue » |
| annulée il y a moins de 24 h, non masquée | « ✕ Passe n°12 annulée · Précalcul du catalogue » |
| sinon | « Précalcul du catalogue » |

Le pourcentage est celui du moniteur (§ 4.2), arrondi à l'unité, jamais en recul pour une même
passe.

**Recul** (`pollDelay(failures, base)`, F1) : `min(base × 2^failures, max(60 000, 5 × base))` ms ;
`failures` est le nombre d'échecs consécutifs ; remis à 0 au premier succès. Pour la progression
(base 5 s) : 10, 20, 40, 60, 60… ; pour le rapport (base 60 s) : 120, 240, 300… Pas d'aléa : un
seul onglet par opérateur, des tests déterministes.

**Lectures uniques** (`every` nul : le récapitulatif, le détail d'une passe, le détail d'une
version avant sa première réponse) : un échec avant toute réponse ne les arrête pas, sans quoi
une `502` pendant un redéploiement ferait disparaître pour de bon « Relancer N unités » ou le
détail. Elles réessaient sur une base de 5 s, avec le même recul (10, 20, 40, 60 s…) jusqu'à
la première réponse, et leurs dialogues offrent « Réessayer maintenant » à côté de l'erreur.

**Incrémental.** La progression d'une passe de ~400 versions × 21 langues (8 400 unités, ~8 450
chargements de catalogue d'après la spécification, lot 2, « Ordre ») tient en ~70 Ko de JSON
brut (~10 Ko gzip, nginx compresse `application/json`, `docker/nginx/nginx.conf`) ; une lecture
incrémentale en fait moins de 10 Ko brut. Le client envoie `since = generatedAt précédent − 30 s`
(un chevauchement qui couvre toute transaction validée après la lecture précédente) ; le serveur
renvoie seulement les versions dont une unité a changé depuis (§ 6.7). Toutes les 10 min, ou
après une absence, une lecture complète resynchronise.

**Fraîcheur** (`freshnessOf`, F1), affichée par `LiveBadge` dans l'en-tête du moniteur, calculée
sur l'heure **du client** au succès (pas d'écart d'horloge) :

| État | Condition (âge = maintenant − dernier succès) | Rendu |
|---|---|---|
| `live` | âge ≤ 2 × cadence + 2 s | point vert (`--color-good`) qui pulse (sans animation si `prefers-reduced-motion`) · « En direct · mis à jour il y a 4 s » |
| `late` | âge ≤ max(60 s, 4 × cadence) | point or (`--color-gold`) · « Mise à jour en retard : il y a 25 s » |
| `lost` | au-delà | point rouge (`--color-bad`) · « Hors ligne depuis 2 min (HTTP 502) · nouvel essai dans 18 s » + « Réessayer maintenant » |
| `hidden` | `document.visibilityState === 'hidden'` | point gris (`--color-text-dim`) · « En pause tant que l'onglet est masqué » (lu au retour) |

Le texte « il y a 4 s » avance chaque seconde (`injectNow(1000)`, F1, suspendu onglet masqué) ;
seul le **changement d'état** est annoncé (`aria-live="polite"`), jamais chaque seconde.

**Forme compacte** (en-tête de page) : le point et l'âge, l'état en toutes lettres dès qu'il
n'est plus `live` (« En retard · il y a 2 min », « Hors ligne · il y a 2 min » :
`common.live.compact_*`), jamais par la seule couleur du point, qui est `aria-hidden` (WCAG
1.4.1) ; la phrase entière en `title`. Une API en panne fait échouer le rapport et la
progression à la fois : tant que le moniteur est là (`lock.kind === 'active'`), le badge de
l'en-tête n'annonce rien (`announces` à `false`) et seul celui du moniteur parle.

**Transitions.**

- Une action réussie (lancer, pause, reprise, annulation) relit le rapport et la progression
  tout de suite (comme `inject-pass-actions.ts` le fait déjà pour le rapport, refus compris :
  un `409` veut dire que la passe a bougé entre-temps).
- La progression voit `pass.state` passer à `completed`/`cancelled` : arrêt de la lecture rapide,
  relecture du rapport et de la couverture, le moniteur cède la place au récapitulatif (§ 4.8).
- Le rapport voit une passe active que la page ne suivait pas (ouverte par une promotion ou un
  autre administrateur) : le moniteur apparaît et la progression démarre en lecture complète.
- Chaque lecture coûte une ligne au journal de l'edge (`docs/guides/observabilite.md`,
  « Journal de l'edge ») : 720 par heure pour un onglet visible à 5 s. C'est la raison de la
  suspension en arrière-plan, pas une limite : `/api/admin/` n'a aucun `limit_req`
  (`docker/nginx/sites/site.conf`, `location /api/`).

## 3. La modale de lancement

### 3.1 Ouverture et préremplissage

La modale s'ouvre par `LaunchOpener.open(preset, report)` (F2, § 7.1) : un dialogue de taille
`wide` (56rem, `src/styles/primitives/dialog.css`, `.hx-dialog--wide`, hauteur
`min(88vh, 100dvh − 2rem)`) sur bureau, une feuille du bas à 92 dvh sous 640 px
(`.hx-sheet--wide`), comme l'armurerie (`features/builds/editor/items/armory-opener.ts`). Aucune
nouvelle taille de dialogue : le panneau défile, son pied reste collé (« Capped, the panel scrolls
and a sticky footer stays on screen », `dialog.css`). La promesse rend la passe ouverte
(`PrerenderPassStarted`) ou `null` si l'opérateur ferme.

Chaque entrée pose un `LaunchPreset` (F1, `common/scope/launch-preset.ts`) :

```ts
/** What a launch starts from: the page fills it, the dialog edits it. */
export interface LaunchPreset {
  /** Where the dialog was opened from, for its first step and its wording. */
  readonly origin: 'header' | 'idle' | 'coverage' | 'complete-epoch' | 'relaunch' | 'failures';
  /**
   * The versions ticked: 'all' for every older patch, 'incomplete' for those with a gap in
   * the epoch (a cell - or p, resolved once its coverage loads), [] for none.
   */
  readonly versions: readonly string[] | 'all' | 'incomplete';
  /** The versions to render first, in this order; a subset of `versions`. */
  readonly priority: readonly string[];
  /** The locales, in the site's order; null for all 21. */
  readonly locales: readonly string[] | null;
  /** The page types; null for all four. */
  readonly pageTypes: readonly string[] | null;
  /** The epoch written; null for the served one. */
  readonly epoch: number | null;
  /** Whether the units already complete in that epoch are left out. */
  readonly skipComplete: boolean;
  /** The pass it was prefilled from, which the summary names; null otherwise. */
  readonly fromPass: number | null;
}
```

| Entrée (`origin`) | Où | `versions` | Autres champs | Étape d'arrivée |
|---|---|---|---|---|
| `header` | bouton de l'en-tête | `[]` | tout par défaut, `skipComplete: true` | 1 |
| `idle` | « Les compléter » de la carte sans passe | `'incomplete'` | epoch servie, `skipComplete: true` | 1 |
| `coverage` | « Pré-rendre la sélection » (§ 5.2) | la sélection | `epoch` = epoch lue dans l'onglet, `skipComplete: true` | 2 |
| `complete-epoch` | bande « presque prête », « Compléter » d'une epoch (§ 5.3), « Compléter les manques » (§ 5.2), récapitulatif d'une passe annulée (§ 4.8) | `'incomplete'` | `epoch` = cette epoch, `skipComplete: true` | 3 |
| `relaunch` | « Relancer ce périmètre » (§ 5.4) | le périmètre de la passe (`relaunchPreset`, liste complète lue par la progression complète, § 6.7 ; plage d'avant les listes convertie) | ses langues, types, priorités, son epoch ; `skipComplete: false` | 3 |
| `failures` | « Relancer N unités (≈ P pages) » (moniteur, récapitulatif, détail) | versions ayant une cellule autre que `c` dans la passe (`f`, `w`, et pour une passe annulée `.`, `i`, `z`) | langues et types de la passe, son epoch (l'epoch servie si la sienne a été retirée depuis, comme `relaunch` : l'API refuse d'écrire une epoch retirée), `skipComplete: true` (qui, conscient des types, ignore les langues déjà tenues, § 3.4) | 3 |

`'incomplete'` se résout dans la modale, une fois la couverture de l'epoch chargée : chaque patch
qui a un **manque**, une cellule `-` ou `p` (§ 6.5). Les cellules `f` (échec définitif) et `w`
(pages en échec) n'en font pas partie : les relancer est le rôle de `failures`, et une passe
« Compléter » qui les reprendrait sans fin échouerait de la même façon. Ainsi un appelant n'a pas
besoin de la couverture pour proposer « Compléter ».

`failuresPreset` (F1) rend aussi ce qu'il va refaire, pour que le bouton le chiffre : `units`
(cellules retenues), dont `failed` (`f`, `w`) et `gaps` (`.`, `i`, `z` d'une passe annulée), et
`pages` (≈ pour chaque version, `pagesTotal / unités listées` × cellules retenues). Libellé
(`retryLabel`, F1, partout où le préréglage est proposé) : « Relancer 17 unités (≈ 19 000
pages) » pour des échecs seuls ; « Finir 4 unités non rendues (≈ 4 316 pages) » quand rien n'a
échoué (ces unités ne sont pas des manques à part : ce sont elles que la passe n'a pas rendues) ;
« Relancer 2 en échec et 4 non rendues (≈ 6 500 pages) » pour les deux.

Seul le panneau (F4) ouvre la modale : le moniteur et le récapitulatif de fin (F3) émettent un
`LaunchPreset` par une sortie `launch`, que le panneau passe à `LaunchOpener.open`. F3 ne dépend
donc pas de F2.

Arriver à l'étape 3 ne saute rien : le pas-à-pas en tête permet de revenir aux étapes 1 et 2, et
le récapitulatif dit d'où vient le préremplissage (« Prérempli depuis la passe n°11 »).

À l'ouverture, la modale charge la couverture de l'epoch visée (`GET …/patches?epoch=`, via
`injectPatches`, F1) ; elle la recharge quand l'epoch change à l'étape 2. Pendant le chargement,
la liste montre six lignes squelettes (`Skeleton`, `src/app/ui/surfaces/skeleton.ts`) et le pied
dit « Chargement des anciens patchs… ».

La modale n'existe que si le lancement est possible : le CTA est désactivé tant qu'une passe est
active ou que l'hôte n'est pas configuré (§ 2.4). Une passe peut tout de même devenir active
pendant que la modale est ouverte (promotion, autre administrateur) : le `409` est traité en
place (§ 3.6).

### 3.2 Structure

Un en-tête (`DialogFrame` de `src/app/ui/overlays/dialog-frame.ts`, avec `eyebrow` :
« Précalcul » au-dessus de « Lancer une passe »), un pas-à-pas, le corps de l'étape, un pied
collé qui résume l'estimation et porte les boutons.

**Pas-à-pas.** Trois boutons `1 Patchs — 2 Périmètre — 3 Récapitulatif` dans un
`<nav aria-label="Étapes">` ; l'étape courante porte `aria-current="step"`. On peut aller en
arrière à tout moment, en avant seulement si l'étape courante est valide (au moins un patch ; au
moins une langue et un type). Sur téléphone : « Étape 1 sur 3 · Patchs » et les flèches du pied.

**Pied collé** (toujours visible, `position: sticky; inset-block-end: 0` dans le panneau) :

```
38 patchs · 21 langues · 4 types → ≈ 612 unités · ≈ 702 000 pages · ≈ 2 j 1 h
                                              [Annuler]  [Suivant : périmètre →]
```

Sans patch coché, la ligne dit à la place « Cochez au moins un patch pour continuer. »
(`footer_empty`), en or, et « Suivant » désactivé la désigne (`aria-describedby`) : jamais
« ≈ 0 unité · ≈ 0 page · ≈ 0 s » à côté d'un bouton grisé sans raison.

La ligne se recalcule à chaque changement (fonction pure `launchEstimate`, § 3.5). Elle est un
`<p aria-live="polite">` mis à jour au plus toutes les 500 ms (anti-rebond) pour ne pas noyer un
lecteur d'écran pendant une sélection au clavier.

#### Étape 1 — Patchs (bureau)

```
┌ PRÉCALCUL ─────────────────────────────────────────────────────────────────────────── ✕ ┐
│ Lancer une passe                                                                        │
│ ① Patchs ──── ② Périmètre ──── ③ Récapitulatif                                          │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ Couverture lue dans l'epoch 2 (servie) · changer d'epoch                                │
│ 16.20.1, le dernier patch, n'est jamais précalculé : le SSR le sert.                    │
│                                                                                         │
│ [Filtrer : 16.1, 15 …        ] Afficher : [Tous|Complets|Partiels|Absents|En échec]     │
│ De [16.19.1 ▾] à [16.19.1 ▾] [Sélectionner la plage]                                    │
│ Cocher : [Tout] [Aucun] [Inverser] [Non couverts] [Partiels] [↶ Défaire]                 │
│ 38 patchs sélectionnés (dont 5 masqués par le filtre) · 3 prioritaires                  │
│ ┌─────────────────────────────────────────────────────────────────────────────────────┐ │
│ │ ☑ ▾ 16.x · 2026   19 patchs · 19 sélectionnés          ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇        │ │
│ │   ☑ 16.19.1  ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇ 21/21   24 310 p   complet              ☆         │ │
│ │   ☑ 16.18.1  ▇▇▇▇▇▇▇▇▇▇▇▇░░░░░░░░░ 12/21   13 880 p   partiel              ★ 1       │ │
│ │   ☑ 16.17.1  ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇ 21/21   23 990 p   complet, échecs      ☆         │ │
│ │ ◩ ▾ 15.x · 2025   24 patchs · 11 sélectionnés                                       │ │
│ │   ☐ 15.24.1  ░░░░░░░░░░░░░░░░░░░░░  0/21        —   absent               ☆         │ │
│ │ ☐ ▸ 14.x · 2024   24 patchs · 0 sélectionné                                         │ │
│ └─────────────────────────────────────────────────────────────────────────────────────┘ │
│ Souris : clic pour cocher · Maj+clic pour cocher une plage · ☆ pour prioriser           │
│ Clavier : ↑ ↓ parcourir · Espace cocher · Maj+↑ ↓ étendre · P prioriser · / filtrer     │
│ ▸ Coller une liste de versions (journal de l'edge)                                      │
│ PRIORITAIRES, DANS L'ORDRE                                                              │
│  1. 16.18.1 [↑][↓][×]   2. 16.12.1 [↑][↓][×]   3. 15.2.1 [↑][↓][×]                      │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ 38 patchs · 21 langues · 4 types → ≈ 486 unités · ≈ 556 000 pages · ≈ 1 j 15 h          │
│                                              [Annuler]  [Suivant : périmètre →]         │
└─────────────────────────────────────────────────────────────────────────────────────────┘
```

#### Étape 2 — Périmètre (bureau)

```
│ LANGUES  21 / 21                                                     [Toutes] [Aucune]   │
│ [ar · arabe 30/38] [cs · tchèque 30/38] [de · allemand 29/38] [el · grec 30/38] …        │
│ … [zh-hant · chinois (traditionnel) 28/38]                                               │
│                                                                                         │
│ TYPES DE PAGES  4 / 4                                                                   │
│ [◆ champions ≈ 15 %] [◆ objets ≈ 58 %] [◆ runes ≈ 19 %] [◆ sorts d'invocateur ≈ 8 %]     │
│                                                                                         │
│ EPOCH ÉCRITE                                                                            │
│ ┌ (•) Compléter l'epoch servie · 2 ─────────┐ ┌ ( ) Écrire une nouvelle epoch · 4 ────────┐│
│ │ Chaque page est servie dès qu'elle est    │ │ Rien n'est servi avant la bascule : une   ││
│ │ écrite. Une page déjà stockée est         │ │ fois l'epoch complète, posez              ││
│ │ remplacée en place.                       │ │ LODB_ARCHIVE_SERVE_EPOCH=4 et redéployez. ││
│ └───────────────────────────────────────────┘ │ Complète, elle coûtera ≈ 0,47 €/mois en   ││
│ ┌ ( ) Continuer l'epoch 3 · en attente ─────┐ │ plus de la 2, jusqu'au retrait de la 2.   ││
│ │ Couverte à 64.2 %. Rien n'en est servi    │ └───────────────────────────────────────────┘│
│ │ avant la bascule.                         │                                            │
│ └───────────────────────────────────────────┘                                            │
│ [■] Ignorer ce qui est déjà complet dans l'epoch 2 — 126 unités ignorées                 │
│     Une unité terminée avec des pages en échec est refaite.                             │
```

#### Étape 3 — Récapitulatif (bureau)

```
│ Epoch 2 (servie) · 38 patchs · 21 langues · tous types · 3 prioritaires                 │
│ ┌ UNITÉS ──┐┌ PAGES ─────┐┌ VOLUME ───┐┌ DURÉE ─────┐┌ COÛT AJOUTÉ ──┐                 │
│ │ 486      ││ ≈ 556 000  ││ ≈ 21.7 GB ││ ≈ 1 j 15 h ││ + 0,17 € /mois│                 │
│ │ 38 × 21, ││ ≈ 1 144 par││ ≈ 39.0 kB ││ au dernier ││ prix One Zone │                 │
│ │ 312 ign. ││ unité      ││ par page  ││ rythme…    ││ 0,00803 €/Go  │                 │
│ └──────────┘└────────────┘└───────────┘└────────────┘└───────────────┘                 │
│ ▸ Comment c'est estimé                                                                  │
│ ⚠ Passe longue : ≈ 6 j. Les patchs promus pendant ce temps la rejoignent en tête.       │
│ Ordre : 16.18.1, 16.12.1, 15.2.1 d'abord, puis du plus récent au plus ancien.           │
│ Prérempli depuis la passe n°11.                                                         │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ 38 patchs · 21 langues · 4 types → ≈ 486 unités · ≈ 556 000 pages · ≈ 1 j 15 h          │
│                                       [← Retour]  [▶ Lancer la passe (486 unités)]      │
```

Le bouton de lancement est l'unique confirmation : le récapitulatif **est** la confirmation (plus
de `ConfirmButton` à deux temps). Entrée ne lance jamais depuis un champ : seul le bouton lance.

#### Téléphone (feuille du bas, 92 dvh)

```
┌──────────────────────────────┐
│ PRÉCALCUL                  ✕ │
│ Lancer une passe             │
│ Étape 1 sur 3 · Patchs       │
│ Epoch 2 (servie) · changer   │
│ [Filtrer…              ]     │
│ [Tous ▾]  [Outils ▾]         │
│ 38 sélectionnés · 3 prio.    │
│ ☑ ▾ 16.x · 2026      19/19   │
│   ☑ 16.19.1 ▇▇▇▇▇▇▇ 21  ☆    │
│   ☑ 16.18.1 ▇▇▇▇░░░ 12  ★1   │
│ ◩ ▸ 15.x · 2025      11/24   │
│ ▸ Coller une liste           │
│ ▸ Prioritaires (3)           │
│──────────────────────────────│
│ ≈ 486 unités · ≈ 1 j 15 h    │
│ [Annuler]   [Suivant →]      │
└──────────────────────────────┘
```

Sous 640 px : le filtre de statut devient un `<select>`, les cinq outils un menu « Outils »
(bouton à `aria-expanded` qui déplie une liste de boutons), les colonnes « Pages » et « Statut »
disparaissent (la bande de cellules et « n/21 » restent), les plages « De / à » passent dans
« Outils ». Les puces des langues gardent 44 px de haut (cible tactile).

### 3.3 Étape 1 : la liste des patchs

#### 3.3.1 Lignes et saisons

Une ligne par ancien patch du rapport de couverture (`PrerenderPatchesReport.patches`, plus récent
d'abord), groupées par **saison** : le premier segment de la version (`seasonOf('16.18.1')` →
`{ key: '16', major: 16, year: 2026 }`, F1). L'année vaut `2010 + major` pour `3 ≤ major ≤ 99`
(`4.x` = 2014, `13.x` = 2023, `16.x` = 2026) ; en dehors (versions `0.151.2` que Data Dragon liste
encore, `PatchVersion` les accepte : `src/LoDb.Domain/Versions/PatchVersion.cs`), pas d'année.
Libellé : `16.x · 2026`, ou `0.x`.

**Ligne de saison** : case à trois états (cochée, mixte, vide ; native au style Hextech,
`lodbField` → `hx-check` et son trait mixte, comme les cases de l'onglet Couverture, jamais la
case blanche du navigateur ; les boutons radio de la modale prennent `accent-hex scheme-dark`),
chevron (plié / déplié), libellé,
« 19 patchs · 11 sélectionnés », et une bande agrégée (part de cellules complètes par langue sur
la saison). **Ligne de patch** :

| Colonne | Contenu | Bureau | Téléphone |
|---|---|---|---|
| Case | cochée / vide (visuel ; l'état est `aria-selected` sur la ligne) | ✓ | ✓ |
| Version | `16.18.1`, police mono | ✓ | ✓ |
| Couverture | `CellStrip` : 21 segments dans l'ordre de `locales`, codes `c w p f -` (§ 4.4), puis « 12/21 » | ✓ | bande + nombre |
| Pages | pages stockées dans l'epoch visée (`figure`, `admin/format/figure.ts`) | ✓ | — |
| Statut | badge (`lodbBadge`) de `patchStatusOf(row)` : complet (good), complet avec échecs (warn), partiel (warn), absent (muted), en échec (bad) | ✓ | — |
| Priorité | bouton ☆ / ★ et son rang (« ★ 1 ») | ✓ | ✓ |

**Ligne d'en-tête** : au-dessus des lignes, une rangée `role="row"` de `role="columnheader"`
(« Cocher », lu seulement, puis « Version », « Couverture », « Pages », « Statut », « Priorité »,
clés `launcher.patches.columns.*`), sur la grille d'une ligne de patch : sans elle, « 2 158 » et
l'étoile n'ont de nom ni à l'écran ni pour un lecteur d'écran. Elle reste collée en haut du corps
de la modale pendant le défilement (`position: sticky`) ; sur téléphone, « Pages » et « Statut »
partent avec leurs colonnes. Aucune touche ne s'y arrête : le parcours ne connaît que les lignes.

`patchStatusOf(row)` (F1) : `complete` si toutes les cellules valent `c` ; `complete_with_failures`
si toutes valent `c` ou `w` avec au moins un `w` ; `failed` si aucune n'est `c`/`w` et au moins
une `f` ; `absent` si toutes valent `-` ; `partial` sinon.

**Dépliage par défaut** : la saison la plus récente, et toute saison qui contient un patch
sélectionné par le préremplissage ; les autres pliées. Le chevron et `→`/`←` plient et déplient.
Plier ne change pas la sélection.

#### 3.3.2 Le modèle de sélection, exactement

Fonction pure `selectionAfter(selection, action)` (F1, `common/selection/patch-selection.ts`), seule source
de vérité ; le composant ne fait qu'y envoyer des actions.

```ts
export interface PatchSelection {
  /** The versions ticked. */
  readonly selected: ReadonlySet<string>;
  /** The version a Shift range starts from: the last one toggled alone. */
  readonly anchor: string | null;
  /** The versions rendered first, in this order; always ticked, 100 at most. */
  readonly priority: readonly string[];
  /**
   * The Shift range being drawn: the ticks before its first extension from the anchor, and
   * the row it reaches. Each extension starts again from `base`, so rows a shrinking range
   * leaves get their state back; any other action ends it (null).
   */
  readonly extent: { readonly base: ReadonlySet<string>; readonly to: string } | null;
}

export type SelectionAction =
  | { readonly kind: 'toggle'; readonly version: string }
  | { readonly kind: 'extend'; readonly version: string; readonly rows: readonly string[] }
  | { readonly kind: 'season'; readonly versions: readonly string[] }
  | { readonly kind: 'set'; readonly versions: readonly string[]; readonly on: boolean }
  | { readonly kind: 'replace'; readonly versions: readonly string[] }
  | { readonly kind: 'invert'; readonly rows: readonly string[] }
  | { readonly kind: 'clear' }
  | { readonly kind: 'priority'; readonly version: string }
  | { readonly kind: 'move'; readonly version: string; readonly by: -1 | 1 }
  | { readonly kind: 'paste'; readonly versions: readonly string[]; readonly append: boolean };

export function selectionAfter(selection: PatchSelection, action: SelectionAction): PatchSelection;
```

| Action | Déclencheur | Effet | Ancre |
|---|---|---|---|
| `toggle` | clic sur une ligne, Espace, Ctrl/⌘+clic | inverse la ligne ; décochée, elle quitte `priority` | devient la ligne |
| `extend` | Maj+clic, Maj+Espace, Maj+↑/↓ | part de `extent.base` (ou, à la première extension, de `selected`, retenu comme `base`) ; toutes les lignes de `rows` entre l'ancre et la ligne (incluses) y prennent **l'état de l'ancre** (cochées si l'ancre l'est, décochées sinon) ; `extent.to` devient la ligne. Maj+↓ ×5 puis Maj+↑ ×3 rend leur état aux 3 lignes sorties de la plage. Sans ancre ou ancre absente de `rows` : se comporte comme `toggle` | inchangée |
| `season` | case de saison, Espace sur une ligne de saison | si tous les `versions` (patchs **visibles** de la saison) sont cochés : les décoche ; sinon : les coche tous | inchangée |
| `set` | « Sélectionner la plage » (De / à) | coche (`on`) ou décoche les `versions` | inchangée |
| `replace` | « Non couverts », « Partiels » | la sélection devient exactement `versions` ; `priority` garde ceux qui restent cochés | `null` |
| `invert` | « Inverser » | inverse chaque ligne de `rows` | inchangée |
| `clear` | « Aucun » | vide tout, priorités comprises (même les lignes masquées par le filtre) | `null` |
| `priority` | ☆, touche P | dans `priority` : l'en retire ; sinon la coche et l'ajoute en fin, si moins de 100 | inchangée |
| `move` | ↑ / ↓ du plateau des priorités | échange avec la voisine dans `priority` | inchangée |
| `paste` | « Appliquer » de la zone de collage | coche les `versions` (déjà filtrées sur les anciens patchs connus), et `priority` devient les 100 premières (`append: false`) ou leur ajout en fin, sans doublon (`append: true`) | inchangée |

**Deux règles communes à toutes les actions**, appliquées en fin de `selectionAfter`, une fois
l'effet propre calculé, et testées pour chaque action :

1. `priority = priority.filter(v => selected.has(v))` : une version décochée par n'importe quelle
   action (`toggle`, `extend` depuis une ancre décochée, `season` qui décoche, `set` avec
   `on: false`, `invert`, `replace`, `clear`) quitte les prioritaires. Sans cette règle, l'API
   l'ignorerait en silence (`UnitPlanner` : `Priority.Where(admitted.Contains)`) et le
   récapitulatif annoncerait un ordre faux.
2. Toute action autre qu'`extend` remet `extent` à `null`.

`rows` est l'ordre **visible** : le filtre texte et le filtre de statut s'appliquent, le pliage des
saisons ne s'applique pas (une plage de 14.1.1 à 16.3.1 couvre aussi la saison 15 pliée ; le
compteur de la saison le montre). Une ligne masquée par un filtre garde son état ; le compteur dit
« dont 5 masqués par le filtre ».

« Tout » coche les lignes visibles (`set`, `on: true`) ; « Non couverts » remplace par les lignes
visibles de statut `absent` ou `failed` ; « Partiels » par les visibles `partial` ou
`complete_with_failures`.

**Annuler.** `SelectionUndo` (F1, `common/selection/selection-undo.ts`) garde jusqu'à 20 états : chaque
action groupée (`extend`, `season`, `set`, `replace`, `invert`, `clear`, `paste`) empile l'état
d'avant ; une suite d'`extend` d'une même plage (`extent` non nul) n'empile qu'une fois, l'état
d'avant sa première extension. « ↶ Défaire » (et Ctrl/⌘+Z dans la liste) dépile : son texte
est visible, et pas « Annuler », que porte déjà le bouton du pied qui ferme la modale. Les `toggle` isolés ne s'empilent
pas : ils se défont d'un clic.

#### 3.3.3 Filtres

- **Texte** (`patchMatches(version, query)`, F1, partagé avec l'onglet Couverture) : la requête est découpée sur les espaces et les
  virgules ; une version correspond si elle **égale** un jeton ou **commence par** `jeton + '.'`
  (« 15 » → toute la saison 15 ; « 16.1 » → 16.1.1 mais pas 16.10.1 ; « 16.18.1 » → elle seule) ;
  un jeton finissant par `*` est un préfixe brut (« 16.1* » → 16.1.x et 16.1x.y). Requête vide :
  tout passe.
- **Statut** : Tous / Complets (`complete`) / Partiels (`partial`, `complete_with_failures`) /
  Absents (`absent`) / En échec (`failed`) ; groupe de boutons à `aria-pressed`, local (pas de
  paramètre d'URL, donc pas `SegmentBar`), précédé de « Afficher : » qui le nomme. Les outils de
  sélection, aux mots voisins (« Tout », « Partiels ») mais qui remplacent la sélection au lieu
  de filtrer, sont précédés de « Cocher : ».
- **Plage De / à** : deux `<select>` (classe `hx-select`, 16 px) de toutes les versions, plus
  récente d'abord ; « Sélectionner la plage » fait `set` de toutes les versions comprises,
  bornes incluses, **sans** tenir compte des filtres (l'opérateur a nommé les bornes). Les deux
  bornes partent du patch le plus récent : une plage d'un seul patch, jamais les 400 d'un clic.

Une saison dont aucun patch ne passe les filtres disparaît. Aucun résultat : « Aucun patch ne
correspond au filtre. » et un bouton « Effacer le filtre ».

#### 3.3.4 Rôles ARIA : une `treegrid`, et pourquoi

```html
<div role="treegrid" aria-multiselectable="true" aria-labelledby="launch-patches-title"
     aria-describedby="launch-patches-keys">
  <div role="row" data-picker-head>          <!-- collée en haut, jamais un arrêt de focus -->
    <div role="columnheader"><span class="sr-only">Cocher</span></div>
    <div role="columnheader">Version</div> … <div role="columnheader">Priorité</div>
  </div>
  <div role="row" aria-level="1" aria-expanded="true" tabindex="-1" data-season="16"
       aria-label="16.x · 2026, 19 patchs, 11 sélectionnés">
    <div role="gridcell"><input type="checkbox" tabindex="-1" [indeterminate]="…"
         aria-label="Sélectionner toute la saison 16.x"></div>
    <div role="rowheader">16.x · 2026</div> …
  </div>
  <div role="row" aria-level="2" aria-selected="true" tabindex="0" data-version="16.18.1">
    <div role="gridcell"><span class="box" aria-hidden="true"></span></div>
    <div role="rowheader">16.18.1</div>
    <div role="gridcell"><lodb-cell-strip …/></div>   <!-- role="img" + aria-label -->
    <div role="gridcell">13 880 pages</div>
    <div role="gridcell">partiel</div>
    <div role="gridcell"><button tabindex="-1" aria-pressed="true"
         aria-label="Prioritaire n°1 : retirer la priorité">★ 1</button></div>
  </div>
</div>
```

**Pourquoi `treegrid`** :

- un `listbox` multi-sélection serait le plus simple, mais ses options ne peuvent contenir aucun
  élément interactif (le bouton de priorité, la case de saison) et il n'a pas de niveau pliable ;
- une `<table>` de cases natives ferait parcourir 400 cases à la touche Tab ;
- une `treegrid` porte à la fois les niveaux pliables (`aria-level`, `aria-expanded`), la
  multi-sélection (`aria-multiselectable`, `aria-selected` par ligne) et des cellules
  interactives. On la tient en **mode ligne** seulement : le focus est toujours sur une ligne,
  jamais sur une cellule, ce qui garde l'implémentation simple. Le bouton de priorité est
  `tabindex="-1"` (souris et touche P) ; la case de saison aussi (souris et Espace).

Une ligne de saison n'a pas d'`aria-selected` (`mixed` n'existe pas pour `row`) : son état mixte
est dans son `aria-label` (« 11 sélectionnés ») et dans la case `indeterminate`.

Une région `aria-live="polite"` cachée annonce le compteur (« 38 patchs sélectionnés ») 500 ms
après la dernière action, jamais à chaque touche.

#### 3.3.5 Clavier

Focus itinérant (un seul `tabindex="0"`, sur la ligne courante) ; la liste est **un** arrêt de
tabulation.

| Touche | Sur une ligne de patch | Sur une ligne de saison |
|---|---|---|
| ↓ / ↑ | ligne visible suivante / précédente (saisons comprises, patchs des saisons pliées sautés) | idem |
| Maj + ↓ / ↑ | focus sur le patch suivant / précédent, **par-dessus** une ligne de saison, **et** `extend` jusqu'à lui : la plage suit le focus d'une saison ouverte à l'autre, en grandissant comme en rétrécissant ; sans patch plus loin, le focus seul bouge | idem (focus et `extend` sur le premier patch dans ce sens) |
| Espace | `toggle` | `season` |
| Maj + Espace | `extend` jusqu'à cette ligne | — |
| Entrée | `toggle` (indulgent) | plie / déplie |
| → | — | pliée : déplie ; dépliée : focus sur son premier patch |
| ← | focus sur sa saison | dépliée : plie |
| P | `priority` | — |
| Début / Fin | première / dernière ligne visible | idem |
| Page ↓ / Page ↑ | 10 lignes plus bas / plus haut | idem |
| Ctrl/⌘ + A | `set` de toutes les lignes visibles | idem |
| Ctrl/⌘ + Z | annule la dernière action groupée | idem |
| `/` | focus sur le filtre texte | idem |
| chiffres et `.` | recherche à la frappe : focus sur la première version qui commence par la saisie (tampon de 600 ms) | idem |
| Échap | ferme la modale (comportement du CDK) | idem |

Dans le filtre texte, Échap **efface le filtre** s'il n'est pas vide (`stopPropagation`, sinon le
CDK fermerait la modale), puis ferme au second appui ; ↓ envoie le focus à la première ligne.

`pickerActionOf(key, row, context)` (F2, `launch/picker/picker-key.ts`) traduit une touche en action :
fonction pure, testée seule ; le composant applique.

**Souris.** Clic n'importe où sur une ligne de patch : `toggle` (sauf le bouton ☆). Maj+clic :
`extend` (et `preventDefault` sur `mousedown` pour ne pas sélectionner le texte). Ctrl/⌘+clic :
`toggle`. Le Maj+clic est le cœur de la plage à la souris, et rien ne le montre : l'aide sous la
liste a une ligne « Souris » avant la ligne « Clavier », et après le premier clic simple, une
ligne dorée dit « Maj+clic sur un autre patch pour cocher toute la plage depuis 16.11.1. »
jusqu'au premier Maj+clic (masquée sur téléphone, sans touche Maj). Clic sur la case de saison : `season` ; sur son libellé ou son chevron : plier /
déplier.

**Ordre du focus à l'étape 1** : pas-à-pas → « changer d'epoch » → filtre texte → filtre de statut
→ De → à → « Sélectionner la plage » → Tout, Aucun, Inverser, Non couverts, Partiels, Annuler → la
liste (un arrêt) → « Coller une liste… » → plateau des priorités (↑, ↓, × de chaque entrée) →
Annuler → Suivant. À l'ouverture, le focus va au dialogue (annoncé par son titre,
`dialog-config.ts`), puis Tab mène au pas-à-pas.

#### 3.3.6 Priorités

Le plateau « Prioritaires, dans l'ordre » liste `priority` en `<ol>` : rang, version, « Monter »,
« Descendre », « Retirer » (boutons à `aria-label` complet : « Monter 16.12.1 »). Vide : « Aucune :
les patchs passent du plus récent au plus ancien. » Au-delà de 100 (`PassRequestReader.
MaxPriorityVersions`), ☆ est désactivé avec « 100 prioritaires au plus. ». L'API rend les
prioritaires d'abord, dans cet ordre, puis les autres du plus récent au plus ancien
(`Passes/UnitPlanner.cs`), et le récapitulatif le dit en clair.

#### 3.3.7 Coller une liste (journal de l'edge)

Repli « Coller une liste de versions (journal de l'edge) » : une zone de texte (mono, 16 px), deux
radios « Remplacer les priorités » (défaut) / « Ajouter à la suite », « Appliquer ».

- Extraction (`versionTokens(text)`, F1) : toutes les occurrences de
  `(?<![\d.])\d+(?:\.\d+)+(?![\d.])`, dans l'ordre, sans doublon (la première garde son rang).
  Elle lit aussi bien une version par ligne, une liste à virgules, la sortie tabulée de LogsQL
  (`16.18.1	12034`, le compte sans point est ignoré) que du JSON (`{"version":"16.18.1"}`).
- Tri (`pasteResultOf(tokens, report)`, F2) : connues (anciens patchs du rapport), dernier patch
  (`report.latestVersion`), inconnues.
- `paste` avec les connues ; le résultat s'affiche sous la zone : « 14 versions reconnues ·
  Ignorées : 16.20.1 (dernier patch, servi par le SSR), 3.15.9 (pas un ancien patch) » ; au-delà
  de 100, « Au-delà de 100, les versions sont cochées sans priorité. »
- La zone garde son texte (on peut corriger et réappliquer).

### 3.4 Étape 2 : le périmètre

**Langues.** 21 puces à bascule (`<button aria-pressed>`), dans l'ordre de `UiLocales.All`
(`src/LoDb.Domain/Languages/UiLocales.cs`, le même que `LOCALES`, `src/app/core/i18n/locales.ts`),
dans un `role="group"` titré « Langues ». Chaque puce : le code, le nom français
(`localeName(code)`, F1, `Intl.DisplayNames('fr', { type: 'language' })` sur l'étiquette BCP 47 :
`zh-hans` → `zh-Hans` → « chinois (simplifié) »), et « 30/38 complets » : parmi les patchs
cochés, ceux dont cette langue **tient** les types choisis (masque `types` de la couverture,
§ 6.6, qui inclut les types choisis) : le même critère que « Ignorer ce qui est déjà complet ». « Toutes » / « Aucune ». Aucune
langue : l'étape est invalide, message « Choisissez au moins une langue. » sous le groupe.

**Types de pages.** Quatre puces à bascule, avec l'icône existante (`champion`, `item`, `rune`,
`spell`, `src/app/ui/media/icon-name.ts`), le nom (`admin.prerender.page_types.*`, existant) et
« ≈ 58 % des pages » (`estimates.typeShares`, § 6.6). Un type au moins. Dès qu'un type manque, une
note dorée : « Une passe limitée à certains types n'accueille pas les patchs promus pendant qu'elle
tourne : ils attendent sa fin. » (`VersionEnqueuer` rend `UnitEnqueue.Deferred` quand la passe
active ne couvre pas tous les types, `Promotion/VersionEnqueuer.cs`) — affichée seulement si
`report.enqueuesPromotions`.

**Epoch écrite.** Un `role="radiogroup"` de cartes (`<label>` autour d'un `<input type="radio">`
natif, 16 px). Les choix viennent de `epochChoices(report)` (F2, pur) :

| Choix | Condition | Titre | Corps |
|---|---|---|---|
| servie | toujours | « Compléter l'epoch servie · N » | « Chaque page est servie dès qu'elle est écrite. Une page déjà stockée est remplacée en place. » |
| en attente | chaque epoch `standby` de `report.storage.epochs` | « Continuer l'epoch M · en attente » | « Couverte à X %. Rien n'en est servi avant la bascule. » |
| nouvelle | toujours | « Écrire une nouvelle epoch · K » | « Rien n'est servi avant la bascule : une fois l'epoch complète, posez LODB_ARCHIVE_SERVE_EPOCH=K et redéployez. Pour un correctif du front (CSP, chrome, sécurité) : l'epoch servie garde l'ancien build tant qu'elle est servie. » + « Complète, elle coûtera ≈ C / mois en plus de l'epoch N, jusqu'au retrait de celle-ci. » |

`K = 1 + max(toutes les epochs de report.storage.epochs, servie comprise)` : jamais le numéro d'une
epoch retirée, dont la règle de cycle de vie `retire-pages-<N>` effacerait la passe
(`tools/prerender/README.md` § 4). `C` = coût mensuel de l'epoch servie
(`PrerenderEpochVolume.monthlyCost`) : une epoch complète pèse à peu près autant que la servie. Changer d'epoch recharge la couverture : les statuts de
l'étape 1 et l'estimation changent, la sélection reste.

**Ignorer ce qui est déjà complet.** Un interrupteur (`<input type="checkbox" lodbField="switch">`,
`src/app/ui/controls/field.ts`) : « Ignorer ce qui est déjà complet dans l'epoch N — 126 unités
ignorées », sous-texte « Une langue est ignorée si elle tient déjà, sans page en échec, chacun
des types choisis. ». Activé par défaut ; caché pour une epoch nouvelle (rien n'y est complet).
**La règle est consciente des types** (`isHeld(row, locale, types)`, F1, pur ; même règle côté
API, § 6.4) : une (version, langue) est ignorée quand son masque de types tenus (§ 6.5) inclut
tous les types choisis. Ainsi une passe limitée aux objets, relancée pour ses échecs, ne refait
que les langues dont les objets ne sont pas tenus, et une passe « tous types » refait une langue
dont seuls les objets sont tenus.

### 3.5 Étape 3 : le récapitulatif, et les formules

`launchEstimate(draft, patches)` (F2, pur) rend tous les chiffres du pied et du récapitulatif.
Données : `PrerenderPatchesReport` (§ 6.6) de l'epoch visée — `patches[].cells`,
`patches[].types`, `patches[].pagesPerLocale`, `estimates.*` — et le brouillon (versions,
langues, types, `skipComplete`).

Notations : `V` les versions cochées ; `L` les langues choisies ; `T` les types choisis ;
`held(v, l)` vrai si le masque de types tenus de (v, l) inclut `T` (§ 3.4).

| Chiffre | Formule | Données |
|---|---|---|
| Unités | `Σ_v card{ l ∈ L : ¬(skip ∧ held(v, l)) }` | masques `types` |
| Unités ignorées | `card(V) × card(L) − unités`, dites « 4 déjà complètes, ignorées » dans la tuile Unités (jamais « 4 ignorées » sans raison) | — |
| Pages par unité de `v` | `ppl(v) = pagesPerLocaleOf(v, patches)` (F2, pur), dans cet ordre : (1) `row.pagesPerLocale`, mesuré pour `v` ; (2) la médiane des patchs mesurés de **sa saison**, s'il y en a 3 au moins ; (3) la mesure du patch mesuré **le plus proche** dans l'ordre des versions (à égalité, le plus ancien) ; (4) 500 | `pagesPerLocale` : `PagesTotal` maximal d'une unité terminée sur tous les types, dans **n'importe quelle** passe (§ 6.6). Les mesures viennent surtout des patchs récents (≈ 1 140 pages par unité, lot 2, « Nouveau patch ») alors qu'un ancien patch en a ≈ 300 à 500 (496 en moyenne sur 402 patchs, spécification, § 1, « Constat ») : une médiane globale surestimerait de ×2 à ×3 une passe sur les anciens patchs, d'où la saison puis le voisin. 500 ne sert que sans aucune mesure |
| Part des types | `share(T) = 1` si tous ; sinon `Σ_{t∈T} estimates.typeShares[t]`, ou `card(T) / 4` sans `typeShares` | `typeShares` (§ 6.6) |
| Pages | `round(Σ_v unités(v) × ppl(v) × share(T))` | — |
| Octets par page | `estimates.bytesPerPage ?? 39 000` | moyenne des unités terminées de l'epoch servie ; 39 000 = « 39 Ko en gzip » mesurés sur 41 554 pages de prod (spécification, § 1, « Constat ») |
| Volume | `pages × octets par page` | — |
| Rythme | `min(estimates.measuredPagesPerMinute, estimates.configuredRendersPerMinute)`, ou le plafond seul sans mesure | une passe mesurée sous un plafond plus haut que l'actuel (`LoDb:Prerender:RendersPerMinute` abaissé depuis, 120 par défaut, 30 au staging, `Options/PrerenderOptions.cs`) ne promet pas un rythme que le plafond interdit ; « au plafond configuré » est dit quand c'est lui qui borne ; sans mesure, la sous-ligne dit pourquoi : « aucune passe de plus de 10 min pour mesurer un rythme » (le rythme n'est retenu qu'au-delà de 600 s actives, `EstimateBasisReader.MeasuredSeconds`), jamais « aucune passe mesurée » quand des passes courtes ont tourné |
| Durée | `pages / rythme` minutes, écrite par `duration()` (`admin/format/duration.ts`) | — |
| Coût ajouté | `octetsAjoutés / 10⁹ × prix`, où `octetsAjoutés = round(Σ_(v,l) rendues ppl(v) × share(T ∖ tenus(v, l))) × octets par page` : chaque cellule lue **seule**, par son masque de types (`types`, § 3.4) — une `-` ou une `f` sans type tenu ajoute toute sa part, une `p` ou une `w` les types choisis qui lui manquent, une `c` rien ; refaire une page stockée la remplace (`StorageCensus` garde le maximum). Jamais par le total de la ligne : une langue en échec d'un patch dont d'autres langues sont stockées ajoute bien ses pages — pour une epoch nouvelle, toutes les unités | `estimates.pricePerGbMonthOneZone`, `currency` ; montant écrit par `monthlyCost` (`management/backups/monthly-cost.ts`) |

Les cinq tuiles tiennent sur une ligne de la modale (`repeat(auto-fit, minmax(9rem, 1fr))`,
au lieu des 11rem de la page), leurs chiffres en 1.35rem au lieu des 1.9rem d'une tuile de page
(projetés dans `lodb-kpi`, `value` nul) ; la tuile Coût dit « + 0,01 € » et porte « par mois,
au prix One Zone (…) » en sous-ligne, pour que son chiffre ne se coupe pas.

Qualité de l'estimation, dite sous les tuiles : « Pages par unité mesurées sur des passes
précédentes » (toutes les versions ont `pagesPerLocale`), « …mesurées pour 31 patchs sur 38 ;
7 extrapolés de leur saison ou du patch voisin », ou « Aucune mesure : estimation par défaut
(≈ 500 pages par unité) ».

« Comment c'est estimé » : un repli qui écrit chaque formule ci-dessus en français avec ses
valeurs (« 486 unités × 1 144 pages × 100 % des types = ≈ 556 000 pages »), et nomme les patchs
extrapolés et leur source (« 14.3.1 : médiane de la saison 14.x, 412 pages »).

**Avertissements** (`launchWarnings(draft, estimate, report)`, F2, pur ; chacun une clé
`launcher.warnings.*`, ton or, sauf `nothing` en rouge) :

| Code | Quand | Texte (fr) |
|---|---|---|
| `rerender` | `skipComplete` éteint et au moins une (v, l) choisie qui tient déjà les types choisis | « N unités déjà complètes seront refaites : ne désactivez « Ignorer… » que pour remplacer des pages (nouveau build, correctif). » |
| `long` | durée > 3 jours ou pages > 1 000 000 | « Passe longue : ≈ 6 j. Les patchs promus pendant ce temps la rejoignent en tête. » |
| `types` | types restreints et `report.enqueuesPromotions` | « Une passe limitée à certains types n'accueille pas les patchs promus… » |
| `new_epoch` | epoch nouvelle ou en attente | « Rien de cette passe n'est servi avant la bascule sur l'epoch K. » |
| `standby_exists` | epoch nouvelle choisie alors qu'une epoch en attente existe | « L'epoch M contient déjà des pages non servies : continuez-la plutôt que d'en ouvrir une nouvelle. » |
| `default_estimate` | aucune version de tout le rapport n'a `pagesPerLocale` | « Aucune mesure : l'estimation est grossière. » |
| `priority_capped` | collage qui porte les priorités au-delà de 100 : la liste seule, ou ajoutée (« Ajouter à la suite ») à celles déjà posées ; mesuré après coup, sur les priorités gardées | « Au-delà de 100, les versions sont cochées sans priorité. » |
| `nothing` | unités = 0 | « Rien à rendre : tout ce qui est sélectionné est déjà complet dans l'epoch N. » (le bouton de lancement est désactivé) |

### 3.6 La requête, la validation et les erreurs

**La requête** (`requestOfDraft(draft, patches)`, F2, pur) :

| Champ de `PassRequest` (§ 6.2) | Valeur |
|---|---|
| `epoch` | l'epoch choisie, **toujours explicite** (une bascule pendant que la modale est ouverte ne doit pas changer la cible) |
| `versions` | `null` si la sélection est exactement l'ensemble des anciens patchs du rapport (la passe reste « entière »), sinon la liste, plus récente d'abord |
| `locales` | `null` si les 21, sinon dans l'ordre du site |
| `pageTypes` | `null` si les 4, sinon dans l'ordre `champions, items, runes, summoners` |
| `priority` | la liste ordonnée, `null` si vide |
| `skipComplete` | l'interrupteur, `false` pour une epoch nouvelle |

**Validation côté client** (empêche seulement d'avancer, ne remplace pas l'API) : au moins un patch
(étape 1), au moins une langue et un type (étape 2), unités > 0 (étape 3).

**L'appel.** `injectLaunchCall()` (F2) appelle `startAdminPrerenderPass` (client généré) avec
`HttpClient` et `ADMIN_TIMEOUT` comme `AdminCommand` (`admin/shared/http/admin-command.ts`), mais
garde le problème (`problemOf`, `admin/shared/http/problem-of.ts`) au lieu de le poster dans la
bande de la page : la modale l'affiche en place.

**Réponses et leur rendu** (`launchErrorsOf(problem)`, F2, pur : du problème aux messages par
étape) :

| Réponse | Rendu |
|---|---|
| `201` | la modale se ferme avec `PrerenderPassStarted` ; l'ouvreur poste la notice `launcher.done` (unités, ignorées) et, si `ignoredVersions` n'est pas vide, `launcher.done_ignored` ; le panneau relit le rapport et la progression, fait défiler en haut et met le focus sur le titre du moniteur (`tabindex="-1"`) |
| `400` `validation-failed` | chaque champ en erreur s'affiche sous sa section (`launcher.errors.<champ>.<code>`), la modale revient à la première étape fautive (`versions`, `priority` → 1 ; `locales`, `pageTypes`, `epoch` → 2 ; `scope` → 3), le récapitulatif liste toutes les erreurs ; un code inconnu → `launcher.errors.unknown` avec les codes bruts |
| `409` `prerender-pass-active` | bande rouge en tête de la modale (`launcher.conflict.active`) + bouton « Fermer et suivre la passe » (ferme, relit le rapport) ; lancement désactivé |
| `409` `prerender-dormant` | bande `launcher.conflict.dormant` ; lancement désactivé |
| `401`/`403` | bande `launcher.session` + lien « Se reconnecter » (`ADMIN_PATHS.login`) |
| pas de réponse (délai, réseau) | bande `launcher.network` : « L'API n'a pas répondu : la passe a pu être créée. » + « Vérifier » : relit le rapport ; s'il montre une passe active dont l'`id` dépasse le plus grand identifiant de passe du rapport passé à l'ouverture (`report.passes[0]?.id ?? 0`, retenu par la modale), la modale se ferme comme sur un `201` ; sinon le lancement se réactive. On compare des identifiants du serveur, jamais l'heure du poste à `createdAt` (un poste en avance de quelques minutes ne reconnaîtrait jamais sa passe) |

Codes de champ possibles (§ 6.2) : `epoch` : `out-of-range`, `retired` ; `versions` : `malformed`,
`too-many`, `required`, `none-pinned` ; `locales` : `malformed`, `required` ; `pageTypes` :
`malformed`, `required` ; `priority` : `malformed`, `too-many` ; `scope` : `nothing-to-render`.

Le bouton de lancement est `busy` pendant l'appel (« Lancement… »), jamais cliquable deux fois.

## 4. Le suivi en direct

### 4.1 Les composants du moniteur

`LiveMonitor` (F3, `prerender/monitor/live-monitor.ts`) reçoit la passe active du rapport
(`PrerenderPassView`) et lit lui-même sa progression (`injectPassProgress`, § 4.9). Il est un
`AdminCard` (cadre Hextech, `admin/layout/admin-card.ts`), titre « Passe n°12 · epoch 2 »
(`tabindex="-1"`, cible du focus après un lancement), `id="prerender-monitor"` (cible du lien
« Suivre » de l'en-tête), et contient dans l'ordre :

| Bloc | Composant | Contenu |
|---|---|---|
| Tête | dans `live-monitor.html` | badge d'état (`prerenderTone`), `LiveBadge` (F1) à droite, ligne d'origine (« lancée le … par … » ou « ouverte le … par une promotion ou la ligne de commande », textes existants `current.launched*` repris), résumé du périmètre (`scopeSummary`, F1) |
| Bande d'arrêt | dans `live-monitor.html` | en pause d'elle-même (§ 4.7) ou bloquée (§ 4.5) |
| Avancement | `StackedBar` (F1) + chiffres | pourcentage, pages faites / total estimé, légende des segments, ligne des unités (§ 4.2) |
| Relevés | grille de quatre relevés | rythme + sparkline, fin estimée, temps actif, volume écrit (§ 4.3) |
| En ce moment | `NowBlock` (F3) | unité(s) en cours, suivantes, attentes, SSR (§ 4.5) |
| Activité | `ActivityFeed` (F3) | fil des unités terminées et des événements (§ 4.6) |
| Échecs | `FailuresList` (F3) | résumé + liste repliable + « Relancer N unités (≈ P pages) » (§ 4.6) |
| Matrice | `UnitMatrix` (F1) | version × langue, par saison (§ 4.4) |
| Actions | `PassActionsBar` (F3) | pause, reprise, annulation avec dialogues (§ 4.7) |

« En ce moment » et « Activité » sont côte à côte au-delà de 860 px (le seuil de l'admin,
`admin-page.css`, `.admin-grid`), l'un sous l'autre en dessous.

### 4.2 La barre d'avancement : segments et sens

`progressSegments(pass)` (F3, pur) rend quatre segments, en pages, dans cet ordre (du début de
ligne vers la fin, propriétés logiques : en RTL la barre part de la droite) :

| Segment | Valeur | Jeton | Sens |
|---|---|---|---|
| écrites | `pagesWritten` | `--color-good` | rangées dans le stockage, servies par nginx (epoch servie) |
| sautées | `pagesSkipped` | `color-mix(in srgb, var(--color-gold) 70%, transparent)` | rendues mais pas à stocker : une `301`, une `404`, une liste rendue avant ses images (pièges, « Une liste rendue avant ses images n'est pas stockée ») ; servies par le SSR |
| en échec | `pagesFailed` | `--color-bad` | ratées après leurs essais ; servies par le SSR |
| restantes | `pagesLeft` (§ 6.3) | `--color-track` | listées mais pas traitées, plus chaque unité pas encore listée à l'estimation de **sa version** (`pages_expected`, posé à la planification) ou, à défaut, à la moyenne des listées |

- **Total** = somme des quatre ; **pourcentage** = `(écrites + sautées + en échec) / total`,
  écrit par `figure(…, 'share')` (« 71.4 % »). Le pourcentage est en Beaufort 2.6rem, or vif.
- Tant qu'il reste des unités à venir (`units.pending > 0`), le total est une **estimation** : le
  texte dit « 1 532 410 / ≈ 2 146 000 pages » et une note au survol (et en `aria-describedby`)
  « Total estimé : les pages d'une unité ne sont listées qu'à son tour ; les autres sont
  estimées par version. » Sans unité à venir, le « ≈ » tombe. L'estimation est faite **par
  version** côté serveur (§ 6.3) : la passe va du plus récent au plus ancien, et ses premières
  unités (≈ 1 140 pages) pèsent deux fois les anciennes ; une moyenne des unités listées
  surestimerait le total d'environ ×2 pendant des jours.
- **Jamais à reculons.** Le total peut grandir (une version rejoint la passe, une unité liste
  plus de pages que prévu). Pour une même passe, le pourcentage affiché est
  `max(précédent affiché, calculé)` (`shownShare`, tenu par `mergeProgress`, § 4.9) et la barre
  ne rétrécit jamais ses segments faits ; le total, lui, s'écrit tel quel. Quand le total
  grandit de plus de 1 %, le fil d'activité l'écrit (« Total réévalué : + 24 000 pages · 16.20.1
  a rejoint la passe », § 4.6). Le pourcentage affiché rattrape le calcul dès que celui-ci le
  dépasse.
- `pagesLeft === null` (aucune unité listée encore) : la barre montre les **unités**
  (`completed / total`) et la ligne dit « En attente de la première unité ».
- Un segment non nul fait au moins 2 px (`stackedSegments`, F1), pour qu'une poignée d'échecs se
  voie sur 2 millions de pages.
- Sous la barre, une `Legend` (`admin/widgets/legend.ts`) avec la valeur de chaque segment, puis
  la ligne des unités : « Unités : 3 024 / 4 452 — 1 en cours · 1 410 à venir · 17 en échec »
  (exactes, `PrerenderUnitCounts`) ; en pause, l'unité restée `running` est arrêtée : « 1 en
  pause » (`live.units_paused`).
- Accessibilité : `StackedBar` porte `role="img"` et un `aria-label` qui dit tout (« 71.4 % :
  1 512 300 pages écrites, 19 840 sautées, 270 en échec, environ 613 590 restantes ») ; la
  légende répète les valeurs en texte.
- En pause : la barre passe en `filter: saturate(0.35)` ; terminée : pleine, sans « ≈ ».

### 4.3 Rythme, fin estimée, courbe

**Échantillons.** `PaceSamples` (F3, `monitor/progress/pace-samples.ts`, classe immuable) garde au plus
**360** échantillons `{ at, processed, bytes }` : `at` = `generatedAt` de la réponse (heure du
serveur), `processed` = écrites + sautées + en échec. 360 × 5 s = 30 min.

- Un échantillon n'est ajouté que si la passe est `running` et `at` dépasse le dernier.
- Changement de passe, ou `processed` qui **baisse** : remise à zéro.
- **Échantillon manquant** (onglet masqué, erreurs, recul) : rien n'est inventé. Les rythmes se
  calculent toujours sur l'écart réel `Δt` entre deux échantillons, donc un trou de 3 min donne
  le rythme moyen de ces 3 min. Un trou de plus de 2 min **coupe** la courbe : `rates()` rend un
  point `null` à cet endroit, et la sparkline dessine deux tracés.
- Pas d'historique côté serveur : à l'ouverture, la courbe est vide, puis se remplit (« mesure en
  cours… » tant qu'il y a moins de 20 s d'échantillons).

**Rythme instantané** = `(processed_dernier − processed_ref) / (at_dernier − at_ref)` × 60 000,
en pages par minute, où `ref` est le plus ancien échantillon des **2 dernières minutes** ; `null`
si l'écart fait moins de 20 s. Il sert à l'affichage seul. **Rythme récent** : la même formule
sur les **15 dernières minutes** (`null` sous 5 min d'échantillons) ; il sert à la fin « au rythme
actuel », car le rythme sur 2 min chute à chaque changement de version (un catalogue chargé
toutes les 21 unités) et pendant les attentes. **Rythme moyen** = `pass.pagesPerMinute` du serveur (pages de toute
la passe sur son temps actif, pauses exclues, `PassProgress.PagesPerMinute`, `null` avant une
minute). Affichage : « 246 pages/min » en grand (instantané, à défaut le moyen), « moyenne
231 pages/min » dessous.

**Ligne du régulateur** (`regulatorLine(pace, texts)`, pur, `monitor/progress/regulator/`). Le
rythme est réglé par le régulateur de la passe (spécification, lot 2, « Capacité adaptative ») :
la première ligne sous le rythme dit où il en est et, s'il retient la passe, pourquoi. Elle se
lit dans `pulse.pace`, que seule l'instance qui draine la passe renseigne : `pace` nul (autre
instance, rien à drainer) ou passe en pause, aucune ligne. Un état ou un motif inconnu n'en
donne pas non plus : jamais un jeton brut à l'écran.

| État | Ligne |
|---|---|
| `starting` | « démarrage prudent : 30 p/min » |
| `climbing` | « adapté : jusqu'à 90 p/min · CPU du VPS à 37 % » (le CPU seulement s'il est connu) |
| `steady`, motif `ceiling` | « au plafond : 240 p/min · CPU du VPS à 37 % » |
| `steady`, autre motif | « stable à 90 p/min : visiteurs actifs » |
| `slowed` | « ralenti à 45 p/min : rendus de la passe ralentis » |
| `held` | « en retenue jusqu'à 21/10/2026 08:05 UTC : l'hébergeur reprend du CPU » (heure en UTC, comme la fin estimée) |
| `fixed` | « rythme fixe : 120 p/min (régulation coupée) » |

Le motif `host-cpu` porte le chiffre quand le CPU est connu (« CPU du VPS à 62 % »), sinon il
se lit « CPU du VPS chargé ». Les textes sont sous `prerender.live.pace.regulator.*` et
`prerender.live.pace.reasons.*`.

**Courbe** : `rates()` rend le rythme de chaque intervalle, lissé par une moyenne glissante de 3,
sur les **15 dernières minutes** (180 points au plus) ; tracée par la `Sparkline` existante
(`admin/charts/sparkline.ts`, `--color-hex`), qui ne sait tracer qu'une ligne : F1 lui ajoute une
entrée `gaps` (indices de coupure) sans changer son rendu sans coupure. Décorative
(`aria-hidden`, comme aujourd'hui) : les chiffres à côté disent la valeur.

**Fin estimée** (`etaOf(pass, instantPace, serverNow)`, F3, pur) :

- au rythme moyen : `pass.estimatedEndAt` du serveur ;
- au rythme actuel : `serverNow + pagesLeft / rythmeRécent` ;
- tant que ni l'un ni l'autre n'est connu (une page rouverte sur une passe à peine reprise : le
  serveur attend une minute de marche, le rythme récent cinq), `serverNow + pagesLeft /
  rythmeInstantané`, le rythme que le bloc affiche : jamais « inconnue » à côté d'un rythme ;
- affichage : la date absolue (`stamp(…, 'minute')`, en UTC comme tout l'admin,
  `admin/format/stamp.ts`, sous le libellé « Fin estimée (UTC) »), « dans 2 j 3 h » (`duration`), et une seconde
  ligne « au rythme actuel : dans 1 j 22 h » avec **hystérésis** : elle apparaît quand les deux
  estimations diffèrent de plus de 15 %, disparaît quand l'écart retombe sous 5 %, et garde son
  état entre les deux (`etaOf` reçoit l'état précédent ; il est tenu par `MonitorState`) ;
- `null` partout (aucun rythme, pas même instantané) : « en cours d'estimation… » ; en pause :
  « suspendue pendant la pause ».

**Temps actif** : `duration(pass.activeSeconds)`, « pauses exclues ». **Écrit** :
`decimalBytes(pass.bytesWritten)` (`prerender/format/decimal-bytes.ts`) et « ≈ 39.0 kB par
page » (`bytesWritten / pagesWritten`).

**Heure du serveur** : `serverNow = generatedAt + (Date.now() − reçuÀ)`, où `reçuÀ` est l'heure
du client à la réception. Toutes les durées relatives du moniteur (« dernière page il y a 2 s »,
« bloquée depuis 4 min ») utilisent `serverNow` et les horodatages du serveur : jamais d'écart
d'horloge entre le poste et l'hôte.

### 4.4 La matrice

`UnitMatrix` (F1, `prerender/matrix/grid/unit-matrix.ts`) sert trois fois : le moniteur (mode `pass`),
le détail d'une passe de l'historique (mode `pass`), l'onglet Couverture (mode `coverage`).

**Colonnes** : les langues de la réponse (`locales`), dans l'ordre du site ; une passe restreinte à
trois langues n'a que trois colonnes. En-têtes : le code en mono 0.6rem (`zh-hans` et `zh-hant`
abrégés `zhs` et `zht` dans l'en-tête seulement, en entier dans l'infobulle et le texte
accessible). **Lignes** : les versions, groupées par saison, plus récente d'abord ; dans le
moniteur, un pli « Ordre de passage » (bouton) trie plutôt par `rank`.

**Codes de cellule**, communs au serveur et au client (`Http/Views/UnitCells.cs` côté API,
`matrix/grid/cell-state.ts` côté front) :

| Code | Mode | État | Rendu (jetons) | Forme (sans couleur) |
|---|---|---|---|---|
| `.` | pass | à venir | contour `--color-text-dim` à 70 % (3:1 au moins sur le panneau, WCAG 1.4.11), fond `--color-track` | carré vide |
| `r` | pass | en cours | contour `--color-hex-bright`, remplissage `--color-hex` de bas en haut à `cursor / pagesTotal` (pris dans `running`) ; halo qui pulse, coupé sous `prefers-reduced-motion` | carré à demi plein |
| `c` | les deux | terminée (sans échec) | plein `--color-good` | carré plein |
| `w` | les deux | terminée avec des pages en échec | plein `--color-good` + coin `--color-bad` (triangle en haut, côté fin de ligne) | carré plein à coin |
| `f` | les deux | en échec | plein `--color-bad` + barre diagonale `--color-hextech-black` | carré barré |
| `z` | pass | arrêtée (unité restée `running` en base dans une passe **en pause** : `PassControl` ne touche que la ligne de la passe, et `UnitRunner.StopAsync` rend `Interrupted` sans écrire l'unité) | comme `r`, remplissage à `cursor / pagesTotal`, **sans halo** et désaturé ; la reprise la reprend d'abord (`UnitQueue.NextAsync`) | carré à demi plein, sans halo |
| `i` | pass | interrompue (unité « en cours » d'une passe annulée ou finie) | hachures `--color-gold` | carré rayé |
| `p` | coverage | partielle (des pages, ou des types manquent) | moitié basse `--color-gold` | carré à moitié |
| `-` | coverage | absente | contour `--color-text-dim` à 70 %, fond `--color-track` | carré vide |
| `_` | pass | hors périmètre (la langue n'a pas d'unité pour cette version) | rien | espace |

**Légende** : elle nomme les états que la grille montre, et eux seuls, dans l'ordre `c r z . w f i`
(passe) ou `c w p f -` (couverture) : aucune cellule sans explication (une `i` d'une passe
annulée, une `z` d'une passe en pause), aucun « en cours » quand rien ne tourne.

**Taille** : `clamp(9px, (100cqi − 5.5rem) / 21 − 2px, 16px)` (requête de conteneur sur le cadre
de la matrice) ; espacement 2 px. Si même 9 px ne tiennent pas (passe de 21 langues sur 320 px),
le cadre défile horizontalement, jamais la page.

**Saisons** (`matrixRows(rows, open)`, F1, pur) : une ligne de saison porte un chevron, le libellé,
« 24 patchs · 20 terminés · 1 en cours · 3 en échec », et une rangée agrégée : par colonne, la
part de cellules `c`/`w` de la saison, peinte en `color-mix(in srgb, var(--color-good) X%,
transparent)` (même rampe que `heatColor`, `admin/charts/scale/heat-color.ts`), plus un point
`--color-hex` si une cellule de la colonne tourne et un point `--color-bad` si une a échoué.

**Dépliage par défaut** : si la passe compte 40 versions ou moins, tout est déplié ; sinon, seules
les saisons qui contiennent une cellule `r` ou `z`, une cellule `f`/`w`, ou la prochaine unité
(`next[0]`) sont dépliées. « Tout déplier » / « Tout replier ». Le choix de l'opérateur l'emporte ensuite, en
mémoire, par passe (pas de stockage).

**Infobulle** : une seule, déléguée (un écouteur `pointerover` sur la grille lit
`data-v`/`data-l` de la cellule, `cellTooltip(input)` F1 pur compose le texte), jamais un `title`
par cellule (jusqu'à 8 400 cellules) :

```
16.18.1 · fr (français) — en cours
312 / 1 140 pages · 12.17 MB · depuis 4 min
```

Selon ce que le client sait : les chiffres d'une unité viennent de `running`, `recent` ou
`failures` (§ 6.7) ; pour les autres cellules, l'état seul et, pour `.`, « rang 1 234 dans la
passe » ; pour `z`, « arrêtée à 312 / 1 140 pages, reprendra là ». En mode `coverage` :
« 16.18.1 · fr (français) — partielle · tient : objets, runes ».

**Clic** (mode `pass`) sur une cellule ou l'en-tête d'une version : `opened.emit(version)` → le
moniteur ouvre `VersionUnitsDialog` (F1, § 6.8). En mode `coverage`, l'en-tête de version
(case) bascule sa sélection (sortie `toggled`).

**Accessibilité.** La grille des cellules est `aria-hidden="true"`. Ce qui reste accessible :

- avant chaque saison, une phrase cachée (`sr-only`) : « Saison 15.x : 24 patchs, 20 terminés,
  1 en cours, 3 en échec, 0 à venir. » ;
- les boutons de saison (plier / déplier, `aria-expanded`) et les en-têtes de version (boutons
  « 15.23.1 : 19 / 21 langues terminées · détail ») dans les saisons dépliées ;
- **la vue tableau** : une bascule « Matrice | Tableau » (`aria-pressed`) remplace la grille par
  `MatrixTable` (F1), un vrai `<table>` : un `<tbody>` par saison avec son `<th scope="rowgroup">`,
  une ligne par version (Version, État, Langues terminées « 19 / 21 », En cours, En échec, Pages,
  Volume, bouton « Détail »). Le choix est mémorisé pour ce visiteur dans `localStorage`
  (`lodb.admin.prerender.matrix-view`, lu et écrit sous `try/catch`, sans effet s'il est
  indisponible).

**Coût du rendu** : une saison pliée est **une** ligne ; une passe de 400 versions toute dépliée
fait 8 400 `<span>` statiques (`@for … track $index`, `OnPush`), acceptable. La liste de la modale
et la table de couverture n'utilisent pas la matrice mais `CellStrip` (F1) : **un** élément par
ligne, peint par un `linear-gradient` de 21 arrêts (`stripGradient(cells)`, F1, pur), `role="img"`
et `aria-label` « 12 langues complètes sur 21 ».

### 4.5 « En ce moment »

`NowBlock` (F3) lit `running`, `next` et `pulse` de la progression (§ 6.7).

- **Unité en cours** (une en pratique : la tâche de fond rend une unité à la fois, ses pages en
  parallèle jusqu'à `MaxInFlight`, `Units/PageWindow.cs`) : « 16.18.1 · fr (français) », une
  `StackedBar` fine (écrites / sautées / en échec / restantes de l'unité, total `pagesTotal`),
  « 312 / 1 140 pages (27 %) », « depuis 4 min » (`serverNow − startedAt`) · « dernière page il y
  a 2 s » (`serverNow − updatedAt`). Bouton discret « Détail du patch » → `VersionUnitsDialog`.
- **Ensuite** : les 5 unités suivantes (`next`), en puces « 16.18.1 · hu ». Le serveur les prend
  dans l'ordre de `UnitQueue.NextAsync` **et** sans les versions qui attendent quand l'instance
  qui répond les voit (`pulse.waiting`, comme `NextAsync(pass, waiting, …)`) ; quand elle ne les
  voit pas (`pulse.observedAt === null`), il ne peut pas les exclure et le client marque
  « en attente » les puces d'une version de `pulse.waiting` s'il en connaît une.
- **En pause** (`pass.state === 'paused'`) : le bloc s'intitule « Reprendra par » au lieu
  d'« En ce moment ». L'unité restée `running` en base (cellule `z`) s'y montre sans « depuis »
  ni « dernière page » : « 16.18.1 · fr, arrêtée à 312 / 1 140 pages (27 %) », sa barre
  désaturée. « Ensuite » reste. Les attentes et le SSR injoignable ne s'affichent pas (la tâche
  de fond ne prend pas une passe en pause).
- **Attentes** (`pulse.waiting`) : « ⏳ 16.3.1 attend : son catalogue n'est pas encore stocké,
  depuis 3 min ; ses unités échouent à 09:12 s'il n'arrive pas. » (`givesUpAt`, 30 min après le
  début, `UnitWaits.Longest`). Raisons : `catalog-pending`, `not-pinned`, `settling`
  (`UnitRunner.WhyWaitAsync`).
- **SSR injoignable** (`pulse.ssrUnreachableSince`) : bande dorée « Le SSR est injoignable depuis
  2 min : la passe attend sans compter de page. » (`Rendering/SsrOutage.cs`).
- **Signaux absents** : `pulse.observedAt === null` → note grise « Les signaux de la tâche de
  fond ne sont pas visibles depuis cette instance. » ; `!pulse.isWorkerEnabled` → bande rouge
  « La tâche de fond est coupée sur cette instance (LoDb:Workers:Enabled=false) : la passe
  n'avance pas. » (`Workers/ConventionalWorkers.cs`).
- **Aucune unité en cours** et rien de ce qui précède : « Aucune unité en cours. »

**Unité en échec en masse** (`failingUnitOf(progress)`, F3, pur) : la liste des échecs attend
la fin d'une unité, et une page en échec réécrit son unité comme une page écrite ; sans cette
bande, un stockage qui refuse chaque page resterait invisible jusqu'à la fin de l'unité. Quand
l'unité en cours a traité 10 pages au moins et qu'un cinquième au moins d'entre elles sont en
échec, une bande rouge (`FailingBand`) s'affiche en tête du moniteur : « 16.18.1 · fr échoue :
8 pages en échec sur 14 (57 %). Dernière raison : » puis `unit.failure` en anglais, « Détail du
patch » et « Mettre en pause » (avec son dialogue de conséquences, § 4.7). Jamais en pause.

**Blocage** (`stallOf(progress, serverNow, writtenAt)`, F3, pur) : seulement si la passe est
`running` (jamais en pause : une unité `z` n'avance pas par construction).
Dernière activité = le plus récent de la dernière page écrite, de `pass.createdAt` et de
`pass.runningSince` (la dernière reprise : une reprise n'écrit aucune unité). La dernière page
écrite (`writtenAtOf`, `MonitorState.written`) est l'instant de la réponse qui a vu
`pagesWritten + pagesSkipped` grandir ; avant la première croissance vue, le plus récent de
`running[].updatedAt`, `recent[0].updatedAt` et `pass.createdAt` de la première réponse. Une
page en échec n'est pas une activité : elle réécrit pourtant `updatedAt`. Si `serverNow −
dernière activité ≥ 180 s`, la bande « Aucune page écrite depuis 4 min » s'affiche avec la
première cause vraie, dans cet ordre :

| Cause | Condition | Texte |
|---|---|---|
| `ssr` | `pulse.ssrUnreachableSince` | « Le SSR est injoignable : la passe reprendra seule à son retour. » |
| `held` | `pulse.pace.state === 'held'` | « La régulation retient la passe pour laisser le CPU au site et à l'hôte ; elle reprend seule au plancher. » |
| `worker_off` | `!pulse.isWorkerEnabled` | « La tâche de fond est coupée sur cette instance. » |
| `waiting` | `pulse.waiting` non vide et `running` vide | « Toutes les unités restantes attendent un catalogue. » |
| `remote` | `pulse.observedAt` nul ou vieux de plus de 2 min | « Aucune instance visible n'écrit la passe : une autre instance tient peut-être le verrou, ou l'API vient de redémarrer (la tâche reprend toutes les 30 s). » |
| `unknown` | sinon | « Cause inconnue : voir le journal de l'API (`prerender.*`). » |

**Ce qu'entend un lecteur d'écran** (`MonitorStatus`, `alarmOf`, F3) : les bandes (unité en
échec en masse, blocage) et les bandes de « En ce moment » (tâche coupée, SSR injoignable)
apparaissent dans un `@if` déjà remplies ; une région `role="status"` insérée avec son texte
n'est souvent pas annoncée (NVDA, JAWS). Elles ne portent donc aucun rôle : un seul
`<p role="status">` du moniteur, toujours présent, change de texte quand ce qui va mal change,
dans l'ordre unité en échec, blocage, tâche coupée, SSR injoignable. `alarmOf` ne retient rien
qui bouge chaque seconde (une durée, un compteur) : c'est dit une fois, quand ça commence. La
bande de pause automatique garde `role="alert"`.

**Démarrage** : passe ouverte il y a moins de 90 s sans unité commencée → ligne neutre « En
attente de la tâche de fond : elle prend une passe à son prochain tour (30 s au plus). »
(`Workers/Prerender/PrerenderWorker.cs`, `Period`), pas de bande de blocage.

### 4.6 Fil d'activité et échecs

**Événements** (`activityEvents(previous, next, seen)`, F3, pur) : à chaque réponse, le client
compare avec la précédente et produit :

| Événement | Source | Texte (fr) |
|---|---|---|
| unité terminée | `recent`, clé (version, langue, `updatedAt`) pas encore vue | « ✓ 16.18.1 · en terminée · 1 140 pages · en 6 min » (durée `updatedAt − startedAt`) |
| unité terminée avec échecs | idem, `pagesFailed > 0` | « ⚠ 16.18.1 · de terminée, 2 pages en échec » + raison en mono |
| unité en échec | idem, `state === 'failed'` | « ✕ 15.2.1 · th en échec » + raison |
| version jointe | une version apparaît dans `versions` (lecture incrémentale) | « ⊕ 16.20.1 a rejoint la passe (21 unités) » (promotion, `VersionEnqueuer`) |
| total réévalué | le total estimé (`processed + pagesLeft`) grandit de plus de 1 % d'une lecture à l'autre | « ↗ Total réévalué : + 24 000 pages » et, si une version a rejoint la passe dans la même lecture, « · 16.20.1 a rejoint la passe » ; explique pourquoi le pourcentage affiché marque le pas (§ 4.2) |
| pause, reprise, fin, annulation | `pass.state` change | « ⏸ Passe mise en pause », « ▶ Passe reprise », « ■ Passe terminée », « ✕ Passe annulée » |
| pause d'elle-même | `pass.state` devient `paused` avec `pauseReason` | « ⏸ La passe s'est mise en pause d'elle-même » + raison |

Le fil garde 50 événements en mémoire (le plus récent en haut), en montre 8, puis « Tout afficher
(14) ». À la première lecture, les 12 de `recent` y entrent ; les événements de passe n'y entrent
qu'à partir de la deuxième. Heure de chaque ligne : `stamp(…, 'minute')` réduit à `HH:mm` le jour
même. Le fil est `aria-live="off"` ; une région polie séparée annonce seulement une unité en échec,
une pause d'elle-même et la fin de la passe, au plus une fois toutes les 10 s.

Les raisons d'échec (`failure`, § 6.9) sont des textes techniques en anglais écrits par la passe
(comme ses journaux) : affichés en mono, `lang="en" dir="ltr"`, tronqués à une ligne avec le
texte complet au survol et dans la liste des échecs.

**Échecs** (`FailuresList`, F3) : « ⚠ 17 unités en échec ou avec des pages en échec »
(`unitsWithFailures`), « Voir les échecs » déplie la liste des 20 plus récentes (`failures`) :
version · langue, badge d'état, « 2 pages en échec sur 1 140 », raison, bouton « Voir le patch ».
Au-delà de 20 : « …et 37 autres, visibles dans la matrice et la vue tableau. » Le bouton
« Relancer 17 unités (≈ 19 000 pages) » (libellé chiffré de `failuresPreset`, F1, § 3.1 :
une unité `w` est refaite entière, deux pages en échec relancent ses 1 140 pages, et le libellé
le laisse voir) émet le préréglage `failures` par la sortie `launch` du moniteur — désactivé
tant que la passe est active (une seule à la fois) avec la raison « disponible quand la passe
sera finie ».

### 4.7 Actions et dialogues de conséquence

`PassActionsBar` (F3) : selon l'état, « Mettre en pause » (fantôme) et « Annuler la passe… »
(danger) en cours ; « Reprendre » (or) et « Annuler la passe… » en pause. Les appels restent ceux
d'aujourd'hui (`injectPassActions`, déplacé dans `monitor/`, même comportement : relecture après
chaque action, refus compris). Leur résultat, succès ou refus (un `409` : la passe a bougé
entre-temps), se dit **à côté des boutons**, dans une ligne `role="status"` toujours présente dont
seul le texte change (`AdminCommand.attempt`), jamais dans la bande en haut de page, hors de vue. Chaque dialogue passe par `ConfirmDialog` (F1) : titre, phrases de
conséquence, bouton de confirmation, bouton qui ne fait rien **et qui a le focus à l'ouverture**
(Entrée ne confirme jamais par mégarde). Les phrases viennent de `consequencesOf(kind, context)`
(F3, pur), avec les chiffres de la dernière progression.

**Mettre en pause** :

```
┌ METTRE LA PASSE N°12 EN PAUSE ? ─────────────────────────────── ✕ ┐
│ • 16.18.1 · fr s'arrête une fois les pages en cours écrites, à    │
│   312 / 1 140.                                                    │
│ • La passe reprendra exactement là, unité et page.                │
│ • Pendant la pause, un patch promu rejoint la passe mais attend   │
│   la reprise.                                                     │
│ • Elle garde la seule place active : aucune autre passe ne peut   │
│   être lancée avant sa reprise ou son annulation.                 │
│                               [Laisser tourner] [Mettre en pause] │
└───────────────────────────────────────────────────────────────────┘
```

(La troisième puce seulement si `report.enqueuesPromotions` ; la première seulement si une unité
tourne.)

**Reprendre** : direct, sans dialogue, quand un administrateur a mis en pause. Quand la passe
s'est arrêtée **d'elle-même** (`pauseReason`), un dialogue « Reprendre malgré l'erreur ? » :
« La passe s'est arrêtée d'elle-même : <raison>. Sans correction, elle se remettra en pause dès la
première page. » — [Laisser en pause] [Reprendre quand même].

**Annuler** (ton danger) :

```
┌ ANNULER DÉFINITIVEMENT LA PASSE N°12 ? ──────────────────────── ✕ ┐
│ • 1 410 unités à venir ne seront pas rendues.                     │
│ • Ce qui est écrit reste dans le stockage : 1 512 300 pages,      │
│   58.98 GB.                                                       │
│ • L'epoch 2 restera partielle (71.4 % couverte) jusqu'à une passe │
│   qui la complète.                                                │
│ • Pour la finir plus tard : « Finir cette passe » dans son        │
│   récapitulatif, ou « Relancer » dans son détail (Historique).    │
│                        [Garder la passe] [Annuler la passe n°12]  │
└───────────────────────────────────────────────────────────────────┘
```

La couverture vient de `report.storage.epochs[epoch].coverage` (§ 6.5). Après une annulation, le
récapitulatif de fin (§ 4.8) propose « Compléter l'epoch 2 ».

**Bande « en pause d'elle-même »** (rouge, en tête du moniteur) : « La passe s'est mise en pause
d'elle-même : <raison>. Corrigez la configuration du stockage ou du SSR (clé, politique, hôte
public), puis reprenez. » + [Reprendre…]. **En pause par un administrateur** : pas de bande, le
badge « en pause » et « depuis 2 h 12 » (`serverNow − pausedAt`) dans la tête.

### 4.8 Fin de passe

Quand la progression voit `completed` ou `cancelled`, ou au chargement si la passe la plus récente
a fini il y a moins de 24 h, `PassRecap` (F3) remplace le moniteur :

```
┌ PASSE N°12 TERMINÉE ─────────────────────────────────────────────────────────── [Masquer] ┐
│ 4 452 unités · 2 131 870 pages écrites · 270 en échec · 5 j 2 h de marche · 83.14 GB         │
│ ███████████████████████████████████████████████████████████████████████▒ (barre finale)    │
│ [Relancer 17 unités (≈ 19 000 pages)]   [Voir le détail]                                   │
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

Annulée : titre « Passe n°12 annulée » ; l'action principale finit **cette** passe, « Finir
cette passe (4 unités, ≈ 4 316 pages) » (préréglage `failures` : ses unités en échec, jamais
atteintes ou coupées, `live.recap.finish`), puis, en bouton fantôme, « Compléter l'epoch 2 »
(préréglage `complete-epoch`, `versions: 'incomplete'`), qui prend tous les manques de l'epoch,
parfois des milliers d'unités ; sans unité laissée, « Compléter l'epoch 2 » redevient principal.
Pour ces boutons, `PassRecap` lit une fois la progression complète de la passe (§ 6.7), terminée
ou annulée, et en tire `failuresPreset` et son libellé chiffré ; sans unité à refaire, le bouton
de relance n'apparaît pas. Les deux boutons émettent par la sortie `launch` ; « Voir le détail » par la
sortie `detail`. « Masquer » mémorise l'identifiant de la passe dans `localStorage`
(`lodb.admin.prerender.recap-dismissed`, sous `try/catch`) et laisse la place à l'`IdleCard` ;
« Voir le détail » ouvre le dialogue de l'historique (§ 5.4).

### 4.9 Lecture et fusion de la progression

`injectPassProgress(passId, state)` (F3) s'appuie sur `injectLivePoll` (F1) :

- cadence : 5 s si `running`, 30 s si `paused`, arrêt sinon (jetons de `PRERENDER_CADENCE`, F1,
  pour que les tests lisent tout de suite) ;
- `load(last)` : `since = last ? last.generatedAt − 30 s : undefined`, sauf si `last` a plus de
  10 min (lecture complète) ; puis `mergeProgress(last, response, receivedAt)` ;
- `mergeProgress` (F3, pur) : `isComplete` → remplace la table des versions ; sinon met à jour
  ou ajoute chaque version reçue (aucune ne disparaît jamais : une unité n'est jamais supprimée) ;
  remplace `pass`, `running`, `next`, `recent`, `failures`, `unitsWithFailures`, `pulse`,
  `locales` (et `scopeVersions` quand la réponse est complète) ; ajoute l'échantillon à
  `PaceSamples` ; calcule les événements ; tient `shownShare` (le pourcentage affiché, jamais en
  recul pour une même passe, § 4.2) et l'état de l'hystérésis de la fin estimée (§ 4.3) ;
- garde l'état fusionné (`MonitorState`) comme valeur du `LivePoll`.

## 5. Couverture, Stockage et coûts, Historique

### 5.1 Les onglets

`<lodb-tabs [label]="'Vues du précalcul'">` (`src/app/ui/tabs/tabs.ts`), trois `<lodb-tab>` de clés
`prerender-coverage`, `prerender-costs`, `prerender-history` (uniques dans la page, `tab.ts`).
Activation automatique aux flèches, comme partout. Le premier est sélectionné à l'ouverture.
Sur téléphone (sous `sm`), « Stockage et coûts » devient « Coûts » (`overview.tabs.costs_short`) :
les trois tiennent dans 328 px, et « Historique » ne disparaît plus hors écran. Partout,
`lodb-tabs` estompe le bord d'une liste qui déborde (`tabFade`, `mask-image`, sa barre de
défilement étant masquée) ; la flèche qui déplace le focus amène son onglet dans la vue.

### 5.2 Couverture

`CoverageTab` (F4, `prerender/coverage/`) lit `GET …/patches?epoch=` (via `injectPatches`, F1).
La tuile « Couverture servie » (une part d'**unités** ; sa sous-ligne le dit, « des unités · 391 /
402 patchs complets », les deux mesures n'étant pas la même) et la carte sans passe, elles, lisent la couverture du rapport
(`report.storage.epochs[…].coverage`, § 6.5) : elles ne dépendent pas de cette lecture.

```
EPOCH LUE  [Servie · 2] [En attente · 3 (64.2 %)]
391 / 402 patchs complets · 6 partiels · 3 absents · 2 en échec — 98.0 % des unités
16.x ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇  15.x ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▒░  14.x ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇  …
[Filtrer…        ] [Tous|Complets|Partiels|Absents|En échec]   [Liste|Matrice]
[Compléter les manques (11 patchs)]
┌───┬──────────┬───────────────────────────────┬─────────┬──────────┬─────────┬──────────────┐
│ ☐ │ VERSION  │ LANGUES                       │ PAGES   │ VOLUME   │ ÉCHECS  │ DERN. ÉCRIT. │
├───┼──────────┴───────────────────────────────┴─────────┴──────────┴─────────┴──────────────┤
│ ◩ │ ▾ 16.x · 2026 — 19 patchs · 18 complets · 1 partiel                                    │
│ ☐ │ 16.19.1  │ ▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇▇ 21/21   │ 24 310  │ 0.65 GB  │ —       │ 20/10 14:02  │
│ ☑ │ 16.18.1  │ ▇▇▇▇▇▇▇▇▇▇▇▇░░░░░░░░░ 12/21   │ 13 880  │ 0.37 GB  │ 2       │ 21/10 08:42  │
└───┴──────────┴───────────────────────────────┴─────────┴──────────┴─────────┴──────────────┘
═══ 1 patch sélectionné   [Pré-rendre la sélection]  [Tout désélectionner] ═══ (barre collante)
```

- **Epoch lue** : boutons à `aria-pressed`, une entrée par epoch non retirée du rapport
  (`servie`, `en écriture`, `en attente`), avec sa couverture ; défaut : la servie.
- **Résumé** : décompte par statut (`patchStatusOf`) et part des unités
  (`coverage.completeUnits / coverage.expectedUnits`) ; une barre empilée par saison
  (`StackedBar`, petite) : complets / partiels (et complets avec échecs) / en échec / absents.
- **Table** : groupée par saison (ligne de saison à case tri-état et pli, comme la modale), les
  saisons sans manque pliées par défaut. Colonnes : case, version, `CellStrip` + « n/21 », pages,
  volume, pages en échec, dernière écriture (`lastEndedAt`). Clic sur la case : `toggle` ;
  Maj+clic : `extend` (même modèle `selectionAfter`, F1, sans priorité). C'est une vraie
  `<table class="hx-table hx-table--flush admin-tbl">`, cases natives : elle sert la consultation,
  la modale sert la sélection au clavier.
- **Filtres** : texte (même règle que la modale, `patchMatches`, F1) et statut (`patchStatusOf`,
  F1).
- **Vue matrice** : `UnitMatrix` en mode `coverage` (21 colonnes, codes `c w p f -`).
- **Actions** : barre collante en bas de l'onglet dès qu'une ligne est cochée : « Pré-rendre la
  sélection » → préréglage `coverage` ; « Compléter les manques (N patchs) » (au-dessus de la
  table, si des patchs ont une cellule `-` ou `p`) → préréglage `complete-epoch`. Les patchs en
  échec définitif (`f`) ou à pages en échec (`w`) se relancent en les cochant, puis « Pré-rendre
  la sélection » : « Ignorer ce qui est déjà complet » n'en écarte alors que les langues
  tenues. Ces deux boutons sont
  désactivés avec la raison du CTA quand une passe est active.
- Le dernier patch (`latestVersion`) est nommé sous le résumé : « Le dernier patch (16.20.1)
  n'est jamais précalculé. »

### 5.3 Stockage et coûts

`CostsTab` (F4, `prerender/costs/`) reprend `storage/storage-section.*` et l'enrichit :

```
┌ EPOCHS DU STOCKAGE ─────────────────────────────────────────────────────── 1,10 € / mois ┐
│ EPOCH    ÉTAT                COUVERTURE     PAGES       VOLUME     COÛT/MOIS  ACTIONS      │
│ Epoch 3  [en attente]        ▇▇▇▇░░ 64.2 %  1 380 000   53.82 GB   0,4322 €  [Préparer la │
│                                                                              bascule]     │
│                                                                              [Compléter]  │
│ Epoch 2  [servie][en écr.]   ▇▇▇▇▇▇ 98.1 %  2 106 000   82.13 GB   0,6595 €  [Compléter]  │
│ Epoch 1  [retirée]           —              2 090 000   81.51 GB   0,6545 €  [Retirer…]   │
│ Bundles  4 builds · 2 310 fichiers, en Multi-AZ           —        0.21 GB    0,0034 €    │
│ TOTAL    epochs retirées exclues                         3 486 000 136.16 GB  1,0951 €    │
│ (notes existantes : prix, sortie, compteurs, epochs retirées)                             │
└───────────────────────────────────────────────────────────────────────────────────────────┘
┌ SI UNE NOUVELLE EPOCH ÉTAIT ÉCRITE ─────────────────────────────────────────────────────┐
│ Elle pèserait ≈ 82.13 GB comme l'epoch servie : + 0,66 € / mois tant que les deux        │
│ coexistent (jusqu'au retrait de l'ancienne, 9 jours au moins après la bascule).          │
└──────────────────────────────────────────────────────────────────────────────────────────┘
(bloc existant <lodb-scaleway-bill project="pages" />, avec une ligne au-dessus :
 « La facture compte aussi la sortie et les requêtes, que l'estimation laisse de côté. »)
```

- Colonne **Couverture** : `PrerenderEpochVolume.coverage` (§ 6.5), barre fine et pourcentage ;
  « — » pour une epoch retirée.
- **Actions** : « Préparer la bascule » sur une epoch `standby` (§ 5.5) ; « Compléter » sur une
  epoch non retirée qui a des manques (préréglage `complete-epoch`), désactivé pendant une passe
  active ; « Retirer… » sur une epoch **retirée** (plus ancienne que la servie), qui ouvre le
  dialogue de retrait (§ 5.5) : c'est le seul endroit où la commande de retrait est copiable,
  parce que c'est le seul moment où elle est sûre.
- Le tableau « Par version de l'epoch servie » (`servedVersions`) quitte cet onglet : l'onglet
  Couverture le remplace (mêmes pages et volumes, plus les langues). Le champ reste dans le
  rapport (`PrerenderStorageView.servedVersions`) : le retirer serait un changement de contrat
  sans gain.
- **Projection** (`epochProjection(storage)`, F4, pur) : volume et coût de l'epoch servie, à
  supposer une nouvelle epoch de même taille ; 9 jours = le délai de suppression d'une epoch
  retirée (texte existant `storage.retired`).
- Toutes les notes existantes (`storage.prices`, `egress`, `counters`, `retired`) sont reprises
  telles quelles sous `costs.*`.

### 5.4 Historique

`PassesTab` (F4, `prerender/passes/`) reprend `pass/pass-history-table.ts` :

| Colonne | Contenu |
|---|---|
| Passe | « #12 », bouton qui ouvre le détail |
| Epoch | nombre |
| État | badge (`prerenderTone`) |
| Périmètre | `scopeSummary` (F1) : « 212 patchs · 21 langues · tous types · 3 prioritaires », « 16.19.1 · fr · objets », « 14.1.1 → 14.24.1 · objets · fr » (passe d'avant les listes), « tous les anciens patchs » |
| Progression | `StackedBar` mini (écrites / sautées / en échec / restantes) + le chiffre de la même barre (`progressSegments`, comme le moniteur) : traitées sur traitées + restantes, « 2 131 870 / 2 146 000 », « 2 713 / ≈ 6 474 » tant que des unités n'ont pas listé leurs pages (une passe annulée avant elles), en unités avant toute liste. Jamais `pagesTotal`, qui ne compte que les unités atteintes |
| Volume | `decimalBytes` |
| Durée active | `duration(activeSeconds)` |
| Par | auteur, ou « promotion ou CLI » (texte existant) |
| Lancée, Fin | `stamp(…, 'minute')` |

Dix lignes, le reste plié (`foldRows`, `FoldToggle`, existants) ; la passe active en tête avec un
lien « Suivre » qui ramène au moniteur. Une ligne s'ouvre au clic, à Entrée sur son bouton.

**Détail d'une passe** (`PassDetailDialog`, F4 ; dialogue `wide` / feuille) :

```
┌ PASSE N°11 · EPOCH 2 ──────────────────────────────────────────────────────────────── ✕ ┐
│ [terminée] lancée le 12/10/2026 08:00 par charles · finie le 17/10/2026 22:14            │
│ PÉRIMÈTRE  212 patchs (16.19.1 → 14.1.1) [Voir la liste] · 21 langues · tous types ·      │
│            3 prioritaires (16.18.1, 16.12.1, 15.2.1) · manques seulement                  │
│ RÉSULTAT   4 452 unités : 4 435 terminées · 17 en échec │ 2 131 870 pages écrites ·       │
│            13 860 sautées · 270 en échec │ 83.14 GB │ 5 j 2 h actives │ 231 pages/min     │
│ MATRICE    (UnitMatrix, mode pass, lecture complète unique de …/progress)                 │
│ ÉCHECS     (FailuresList, sans « relancer » ici)                                          │
│        [Relancer 2 en échec et 15 non rendues (≈ 21 000 pages)] [Relancer ce périmètre]   │
└───────────────────────────────────────────────────────────────────────────────────────────┘
```

- Données : la ligne de l'historique (`PrerenderPassView`) + **une** lecture complète de
  `GET …/passes/{id}/progress` (pas de lecture répétée ; pour la passe active, le dialogue montre
  « Suivre en direct » qui ferme et fait défiler vers le moniteur).
- « Voir la liste » déplie les versions en puces mono.
- Passe d'avant les listes (`scope.oldest`/`newest`) : « Plage d'avant les listes : 14.1.1 →
  14.24.1, convertie en 24 patchs à la relance. »
- **Relancer ce périmètre** : préréglage `relaunch` (`relaunchPreset(pass, patches)`, F4, pur) —
  versions du périmètre (`null` → `'all'` ; plage → les anciens patchs de la plage selon le
  rapport de couverture), langues, types, priorités, epoch de la passe si elle n'est pas retirée,
  sinon l'epoch servie ; `skipComplete: false`.
- **Relancer N unités (≈ P pages)**, « Finir N unités non rendues », « Relancer N en échec et M
  non rendues » (`retryLabel`) : préréglage `failures` — versions dont une
  cellule de la passe n'est pas `c` ; `skipComplete: true` ; libellé chiffré (§ 3.1).
- La liste complète des versions du périmètre (« Voir la liste », « Relancer ce périmètre »)
  vient de `scopeVersions` de la lecture complète de la progression (§ 6.7) : les vues de liste
  ne portent qu'un résumé (§ 6.3).
- Les deux sont désactivés, avec la raison, pendant une passe active.

### 5.5 Le dialogue de bascule, et celui du retrait

`SwitchDialog` (F4, `prerender/costs/switch/switch-dialog.ts`) s'ouvre depuis la bande « prête »
(« Comment basculer ? ») ou l'action « Préparer la bascule » d'une epoch en attente. Données :
l'epoch cible, sa couverture, l'epoch servie, la passe active. Les textes viennent de
`switchSnippets(target, report)` (F4, pur).

```
┌ BASCULER SUR L'EPOCH 3 ────────────────────────────────────────────────────────── ✕ ┐
│ ✓ Prête : 402 patchs × 21 langues, sans manque. 12 unités en échec définitif et      │
│   270 pages en échec passeront par le SSR.  (ou : ⚠ Couverte à 64.2 % : 3 022 unités │
│   passeront par le SSR après la bascule.)                                            │
│ nginx ne change pas d'epoch à chaud : aucun bouton ne la bascule d'ici.              │
│                                                                                      │
│ 1. Dans le secret ENV_PROD (ou ENV_STAGING), posez :                                 │
│    LODB_ARCHIVE_SERVE_EPOCH=3                                         [Copier]       │
│ 2. Redéployez (en prod : fusion test → main). Le panneau affichera alors l'epoch     │
│    servie 3.                                                                         │
│ 3. Ensuite seulement, l'epoch 2 sera retirée : sa ligne de « Stockage et coûts »     │
│    proposera « Retirer… ». Ne posez aucune règle de retrait avant : l'epoch 2 est    │
│    encore celle que nginx sert.                                                      │
│                                                                    [Fermer]          │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

- L'étape 3 est **du texte, sans commande ni bouton Copier**. Le script
  (`tools/prerender/lib/configure.sh`, `refuse_unretirable_epoch`) ne compare l'epoch à retirer
  qu'aux `SERVED_EPOCH` et `WRITE_EPOCH` que l'opérateur lui donne : collée avant le
  redéploiement, une commande `SERVED_EPOCH=3 … RETIRED_EPOCHS=2` poserait `retire-pages-2` sur
  l'epoch que nginx sert encore (500 000 objets supprimés par jour, tout repasserait par le SSR).
- « Copier » : `CopyLink` existant (`src/app/ui/controls/copy-link.ts`), qui copie n'importe quel
  texte et ouvre un champ sélectionné si le presse-papiers refuse.
- Une passe active écrit cette epoch : bande dorée « Une passe écrit encore cette epoch : attendez
  sa fin. » en tête.

**Le dialogue de retrait** (`RetireDialog`, F4, `prerender/costs/switch/retire-dialog.ts`)
s'ouvre par « Retirer… » sur une epoch **déjà retirée** au sens du rapport (plus ancienne que
`report.servedEpoch`, donc plus servie par l'image déployée) :

```
┌ RETIRER L'EPOCH 2 ─────────────────────────────────────────────────────────────── ✕ ┐
│ L'epoch 2 n'est plus servie (l'epoch servie est 3). Sa règle de cycle de vie la      │
│ supprimera, 500 000 objets par jour au plus ; elle coûte jusque-là 0,6545 € / mois.  │
│ Ajoutez ces variables à la commande de tools/prerender/README.md, § 4, avec les      │
│ identifiants de la console qu'elle demande :                                         │
│    ENVIRONMENT=prod BUCKET=lodb-pages-prod SERVED_EPOCH=3 WRITE_EPOCH=3              │
│    RETIRED_EPOCHS=2                                                   [Copier]       │
│ Au staging : sans objet, la règle d'expiration du bucket efface toute page.          │
│                                                                    [Fermer]          │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

- Les valeurs viennent du rapport : `servedEpoch`, l'epoch écrite par la passe active (sinon la
  servie) pour `WRITE_EPOCH`, et `storeBucket` (nouveau champ du rapport, § 6.3 :
  `PageStoreOptions.Bucket`, un nom, jamais un secret). Sans `storeBucket`, la ligne garde
  `BUCKET=<bucket>` et le bouton Copier disparaît. Les identifiants et la région restent ceux
  que le README fait saisir : le dialogue ne prétend pas donner une commande complète, il donne
  les variables exactes, au moment où elles sont sûres.
- Les noms `ENV_PROD`, `ENV_STAGING`, `LODB_ARCHIVE_SERVE_EPOCH`, `RETIRED_EPOCHS`, `SERVED_EPOCH`,
  `WRITE_EPOCH` sont ceux du dépôt (`docs/guides/configuration.md` § 4.8,
  `tools/prerender/README.md` § 4) : ne jamais les renommer (convention de l'équipe : ni les
  environnements ni les secrets ne changent de nom).

## 6. Contrat d'API

Tout reste sous le groupe `/api/admin/prerender` d'`AdminModule.MapAdmin`
(`src/LoDb.Api/Modules/Admin/AdminModule.cs`) : politique `AuthorizationPolicies.Admin`
(administrateur avec second facteur), filtre `NoStoreFilter` (jamais en cache), `401`/`403` en
ProblemDetails. Les lectures n'écrivent rien au journal d'audit ; le lancement continue d'y écrire
`admin.prerender_start`. Les commentaires de code ci-dessous sont en anglais (convention), le
reste en français.

### 6.1 Routes

| Méthode et route | Nom (`WithName`, donc la fonction générée) | Réponses | Statut |
|---|---|---|---|
| `GET /api/admin/prerender` | `readAdminPrerender` | `200 PrerenderReport` | existante, enrichie (§ 6.3, § 6.5) |
| `POST /api/admin/prerender/passes` | `startAdminPrerenderPass` | `201 PrerenderPassStarted`, `400`, `409` | existante, corps changé (§ 6.2) |
| `POST /api/admin/prerender/passes/{id}/pause` · `/resume` · `/cancel` | `pause…` · `resume…` · `cancelAdminPrerenderPass` | `204`, `404`, `409` | inchangées |
| `GET /api/admin/prerender/patches?epoch={n}` | `readAdminPrerenderPatches` | `200 PrerenderPatchesReport`, `400` | **nouvelle** (§ 6.6) |
| `GET /api/admin/prerender/passes/{id:long}/progress?since={iso}` | `readAdminPrerenderPassProgress` | `200 PrerenderPassProgress`, `404` | **nouvelle** (§ 6.7) |
| `GET /api/admin/prerender/passes/{id:long}/versions/{version}` | `readAdminPrerenderPassVersion` | `200 PrerenderVersionUnits`, `400`, `404` | **nouvelle** (§ 6.8) |

Nouveaux fichiers de points d'accès, sur le modèle de `Http/PrerenderReportEndpoint.cs` :
`Http/PrerenderPatchesEndpoint.cs`, `Http/PrerenderProgressEndpoint.cs`,
`Http/PrerenderPassVersionEndpoint.cs`, chacun avec `Map(IEndpointRouteBuilder prerender)`,
appelés dans `AdminModule.MapAdmin` après `PrerenderPassEndpoints.Map(prerender)`. `404` :
`PrerenderProblems.NotFound()` (`prerender-pass-not-found`). `400` : `FieldErrors.ToProblem()`
(`validation-failed`).

### 6.2 Le corps du lancement

`Http/PassRequest.cs` remplace la plage, le type unique et la langue unique par des listes :

```csharp
namespace LoDb.Api.Modules.Admin.Prerender.Http;

/// <summary>
/// A pass to launch: the epoch it writes and its perimeter, as explicit lists. Every field is
/// optional: empty, it fills the served epoch with every page of every older patch, newest
/// first.
/// </summary>
internal sealed record PassRequest
{
    /// <summary>
    /// The epoch written: the served one by default, to fill it; a newer one to fill or to
    /// replace it. An older one is retired and refused.
    /// </summary>
    public int? Epoch { get; init; }

    /// <summary>
    /// The older patches rendered, as Data Dragon spells them; null for every one of them. A
    /// version that is not an older patch is left out and named back. At most 1,000.
    /// </summary>
    public IReadOnlyList<string>? Versions { get; init; }

    /// <summary>The locales rendered, as the URLs spell them (fr, zh-hans…); null for all 21.</summary>
    public IReadOnlyList<string>? Locales { get; init; }

    /// <summary>
    /// The types of page rendered (champions, items, runes, summoners); null for all four.
    /// </summary>
    public IReadOnlyList<string>? PageTypes { get; init; }

    /// <summary>The versions rendered first, in this order: the most asked for. At most 100.</summary>
    public IReadOnlyList<string>? Priority { get; init; }

    /// <summary>
    /// Whether each (version, locale) the epoch already holds for every type asked (the latest
    /// completed unit over each of them failed no page) is left out.
    /// </summary>
    public bool SkipComplete { get; init; }
}
```

Exemple :

```json
{
  "epoch": 2,
  "versions": ["16.18.1", "16.12.1", "15.2.1", "14.24.1"],
  "locales": ["en", "fr", "zh-hans"],
  "pageTypes": null,
  "priority": ["16.18.1"],
  "skipComplete": true
}
```

`{}` reste valide : toute l'epoch servie, tous les anciens patchs, comme aujourd'hui.

**Lecture** (`Http/PassRequestReader.cs`), toutes les erreurs d'un coup (`FieldErrors`,
`src/LoDb.Api/Modules/Accounts/Http/FieldErrors.cs`) ; les codes réutilisent
`FieldErrors.Required` (`required`) et `FieldErrors.Malformed` (`malformed`) :

| Champ | Code | Quand | Constante |
|---|---|---|---|
| `epoch` | `out-of-range` | `< 1` | `OutOfRange` (existante) |
| `epoch` | `retired` | `< ServedEpoch` | `Retired` (existante) |
| `versions` | `required` | liste vide `[]` | `FieldErrors.Required` |
| `versions` | `too-many` | plus de `MaxVersions` (1 000) | `TooMany` (existante) |
| `versions` | `malformed` | une entrée ne passe pas `PatchVersion.TryParse` | `FieldErrors.Malformed` |
| `versions` | `none-pinned` | aucune version demandée n'est un ancien patch (décidé après la planification, § 6.4) | `NonePinned` (nouvelle) |
| `locales` | `required` / `malformed` | liste vide / code inconnu de `UiLocales.TryParse` | — |
| `pageTypes` | `required` / `malformed` | liste vide / inconnu de `PageTypes.TryParse` | — |
| `priority` | `too-many` / `malformed` | plus de 100 / entrée invalide | existants |
| `scope` | `nothing-to-render` | `skipComplete` ne laisse aucune unité (§ 6.4) | `NothingToRender` (nouvelle) |

Champs : `EpochField = "epoch"`, `VersionsField = "versions"`, `LocalesField = "locales"`,
`PageTypesField = "pageTypes"`, `PriorityField = "priority"`, `ScopeField = "scope"`.
`OldestField`, `NewestField`, `PageTypeField`, `LocaleField` et `BeforeOldest` disparaissent.

**Normalisation**, pour que le périmètre stocké reste petit et que « entier » se reconnaisse :
doublons retirés (la première occurrence garde son rang) ; langues rangées dans l'ordre de
`UiLocales.All`, types dans l'ordre de l'énumération `ResourceType` ; une liste qui couvre les 21
langues ou les 4 types devient `null`. Les versions gardent l'ordre envoyé (le front envoie le
plus récent d'abord) et ne sont **pas** normalisées en `null` : l'ensemble des anciens patchs
change à chaque promotion, c'est au client de dire « tous » (`null`).

Limites : 1 000 versions de 12 caractères au plus font ~14 Ko, loin des 128 Kio du corps par
défaut (`Hosting/HostingOptions.cs`, `DefaultMaxRequestBodySize`).

### 6.3 Le périmètre et sa forme stockée

**`Pages/PassScope.cs`** :

```csharp
/// <summary>
/// The perimeter a pass was launched over: explicit lists of older patches, locales and types
/// of page, each left null for all of them, the versions to render first, and whether the
/// units already complete in its epoch were left out.
/// </summary>
/// <remarks>
/// Stored as JSON in <c>prerender_pass.scope</c> (<see cref="PassScopeJson"/>). A pass stored
/// before the lists (October 2026) may hold a range instead: <see cref="Oldest"/> and
/// <see cref="Newest"/> are read, never written.
/// </remarks>
internal sealed record PassScope
{
    /// <summary>Every older patch, every page, every locale, newest first.</summary>
    public static PassScope Everything { get; } = new();

    /// <summary>The older patches rendered; null for every one of them.</summary>
    public IReadOnlyList<PatchVersion>? Versions { get; init; }

    /// <summary>The oldest version of a range stored before the lists; null otherwise.</summary>
    public PatchVersion? Oldest { get; init; }

    /// <summary>The newest version of a range stored before the lists; null otherwise.</summary>
    public PatchVersion? Newest { get; init; }

    /// <summary>The locales rendered, in the site's order; null for all of them.</summary>
    public IReadOnlyList<UiLocale>? Locales { get; init; }

    /// <summary>The types of page rendered, their lists and their entities; null for all.</summary>
    public IReadOnlyList<ResourceType>? PageTypes { get; init; }

    /// <summary>The versions to render first, in this order; the rest follow, newest first.</summary>
    public IReadOnlyList<PatchVersion> Priority { get; init; } = [];

    /// <summary>Whether the units already complete in the pass's epoch were left out.</summary>
    public bool SkipComplete { get; init; }

    /// <summary>Whether it renders every type of page: a promotion may then join it.</summary>
    public bool HasEveryPageType => PageTypes is null;

    /// <summary>Whether nothing restricts it: its epoch is then whole once it completes.</summary>
    public bool IsWhole =>
        Versions is null && Oldest is null && Newest is null && Locales is null && PageTypes is null;

    /// <summary>A scope of exactly one version, as a promotion opens.</summary>
    public static PassScope Of(PatchVersion version) => new() { Versions = [version] };

    public bool Admits(PatchVersion version) =>
        (Versions is null || Versions.Contains(version))
        && (Oldest is null || version >= Oldest)
        && (Newest is null || version <= Newest);

    public bool Admits(UiLocale locale) => Locales is null || Locales.Contains(locale);
}
```

`PageType` et `Locale` (singuliers) disparaissent. `Pages/PageTypes.cs` : `Keep(paths,
IReadOnlyList<ResourceType>? types)` garde les chemins de **l'un** des types (`null` : tous) ;
`UnitRunner.RunAsync` appelle `PageTypes.Keep(list.Paths, unit.Pass.Scope.PageTypes)`.

**`Pages/PassScopeJson.cs`** lit l'ancienne forme et la nouvelle, et écrit la nouvelle **avec**
les anciens champs qui peuvent la dire : l'image d'avant les listes ignore `versions`, `locales`
et `pageTypes`, et lirait sans eux `{}`, tout ancien patch, tout type, toute langue (curseur de
l'unité en cours décalé, couverture comptée complète, epoch annoncée prête). Un retour à cette
image doit rester possible tant que la migration n'est pas contractée
([pièges](../architecture/pieges.md)).

| Champ JSON | Lu | Écrit | Vers |
|---|---|---|---|
| `versions` | ✓ | ✓ (absent si `null`) | `Versions` |
| `locales` | ✓ | ✓ (absent si `null`) | `Locales` (normalisé : 21 → `null`) |
| `pageTypes` | ✓ | ✓ (absent si `null`) | `PageTypes` (4 → `null`) |
| `priority` | ✓ | ✓ (absent si vide) | `Priority` |
| `skipComplete` | ✓ | ✓ (absent si `false`) | `SkipComplete` |
| `oldest`, `newest` | ✓, ignorés à côté de `versions` | les bornes de `versions`, ou la plage d'avant | `Oldest`, `Newest` |
| `pageType` (ancien) | ✓ | quand `pageTypes` n'en compte qu'un | `PageTypes = [type]` |
| `locale` (ancien) | ✓ | quand `locales` n'en compte qu'une | `Locales = [locale]` |

Quand un document porte à la fois `locale` et `locales`, `locales` gagne ; `versions` gagne sur
les bornes. Ce que les anciens champs ne disent pas, l'image précédente le lit plus large :
plusieurs langues ou types sur 21 et 4, une liste trouée (toute la plage entre ses bornes).
Un document illisible lève toujours `FormatException` (inchangé).

Exemples :

| Stocké | Lu comme |
|---|---|
| `{}` | `Everything` |
| `{"oldest":"14.1.1","newest":"14.24.1","pageType":"items","locale":"fr","priority":["14.24.1"]}` (avant les listes) | `Oldest 14.1.1, Newest 14.24.1, PageTypes [Items], Locales [Fr], Priority [14.24.1]` |
| `{"oldest":"16.19.1","newest":"16.19.1"}` (promotion d'avant) | une plage d'une version |
| `{"versions":["16.19.1"],"oldest":"16.19.1","newest":"16.19.1"}` (promotion d'après) | `Versions [16.19.1]` |
| `{"versions":["16.18.1","16.12.1"],"oldest":"16.12.1","newest":"16.18.1","locales":["en","fr"],"priority":["16.18.1"],"skipComplete":true}` | — |

**Vue** (`Http/Views/PrerenderScopeView.cs`, remplace l'actuelle). Elle part dans chaque vue de
liste : le rapport, relu toutes les 60 s, en porte jusqu'à 50 (`PassHistory.MaxPasses`), et la
progression, relue toutes les 5 s, une. Une liste de 380 versions pèse ~5 Ko, 1 000 en pèsent
~14 Ko : dans une vue de liste, la liste des versions est donc **résumée** (compte et bornes), et
ne part en entier que dans la lecture complète de la progression (`scopeVersions`, § 6.7).

```csharp
/// <summary>
/// The perimeter of a pass, spelled as the URLs spell it, sized for lists: the versions are
/// counted, not listed (the full list comes with a complete read of the pass's progress); a
/// list left null is not restricted.
/// </summary>
internal sealed record PrerenderScopeView
{
    /// <summary>How many older patches the pass lists; null when it takes every one.</summary>
    public int? VersionCount { get; init; }

    /// <summary>The newest version of the list; null when it takes every one.</summary>
    public string? NewestListed { get; init; }

    /// <summary>The oldest version of the list; null when it takes every one.</summary>
    public string? OldestListed { get; init; }

    /// <summary>The oldest version of a range, for a pass launched before the lists.</summary>
    public string? Oldest { get; init; }

    /// <summary>The newest version of a range, for a pass launched before the lists.</summary>
    public string? Newest { get; init; }

    /// <summary>The locales rendered, in the site's order; null for all 21.</summary>
    public IReadOnlyList<string>? Locales { get; init; }

    /// <summary>The types of page rendered; null for all four.</summary>
    public IReadOnlyList<string>? PageTypes { get; init; }

    /// <summary>The versions rendered first, in this order (100 at most).</summary>
    public required IReadOnlyList<string> Priority { get; init; }

    /// <summary>Whether the units already held in the epoch were left out.</summary>
    public required bool SkipComplete { get; init; }
}
```

`scopeSummary` (F1) écrit « 212 patchs (16.19.1 → 14.1.1) · 21 langues · tous types ·
3 prioritaires » à partir de ces champs ; les priorités (≤ 100 versions, ~1,2 Ko) restent
entières.

**Ajouts à `Http/Views/PrerenderReport.cs`** :

```csharp
    /// <summary>
    /// The page store's bucket, a name and never a secret, for the retirement's variables;
    /// null while the store is not configured.
    /// </summary>
    public string? StoreBucket { get; init; }
```

**Ajouts à `Http/Views/PrerenderPassView.cs`** (le reste inchangé) :

```csharp
    /// <summary>
    /// Since when it runs, from its launch or its last resume; null unless running. A resume
    /// writes no unit: without it, a pass resumed after a long pause would look stalled.
    /// </summary>
    public DateTimeOffset? RunningSince { get; init; }

    /// <summary>Since when it is paused; null unless paused.</summary>
    public DateTimeOffset? PausedAt { get; init; }

    /// <summary>
    /// Why the pass paused itself (the store or the SSR refusing it), as the engine says it;
    /// null for a pause of an administrator, and once it runs again.
    /// </summary>
    public string? PauseReason { get; init; }

    /// <summary>
    /// Pages still to process: those of the listed units not processed yet, plus each unit not
    /// listed yet at its version's expected pages, else at the mean of the listed ones. Null
    /// before any unit is listed.
    /// </summary>
    public long? PagesLeft { get; init; }
```

**`PagesLeft`, par version.** La passe va du plus récent au plus ancien, prioritaires d'abord
(`UnitPlanner`) : ses premières unités listées sont les plus grosses (≈ 1 140 pages contre 496
en moyenne pour un ancien patch). Extrapoler les unités à venir à la moyenne des listées
(`PassProgress.EstimatedEnd` aujourd'hui) surestime le total d'environ ×2 pendant des jours, sous-
estime le pourcentage et repousse la fin estimée. D'où :

- une colonne **`prerender_unit.pages_expected`** (`int NULL`, migration additive du § 6.9),
  posée à la planification (`PassPlanning`, § 6.4) et par `VersionEnqueuer` pour les unités qu'il
  ajoute : les pages attendues de l'unité, pour son jeu de types, dans cet ordre — le
  `PagesTotal` maximal d'une unité **terminée** de la même version dans une passe antérieure
  **au même jeu de types** (n'importe quelle epoch) ; sinon `null` ;
- `PassFacts` gagne, pour les unités en attente, `ExpectedPages` (somme des `pages_expected`
  non nuls) et `UnexpectedUnits` (nombre d'unités en attente sans estimation), lus par le même
  `GroupBy(PassId, State)` que les comptes d'état (`PassHistory.UnitsAsync`) : aucune requête
  de plus, ni pour le rapport ni pour la progression ;
- `PassProgress.PagesLeft(facts)` (nouvelle, pure) : `listed = total − pending` ; `null` si
  `listed = 0` ; sinon `max(0, PagesTotal − traitées) + ExpectedPages + UnexpectedUnits ×
  PagesTotal / listed`, arrondi au supérieur. `EstimatedEnd` s'appuie désormais dessus.

Une unité listée plus grosse que prévu fait grandir le total : le moniteur ne recule jamais pour
autant (§ 4.2).

**Ajouts à `Http/Views/PrerenderPassStarted.cs`** :

```csharp
    /// <summary>Units left out, already held in the epoch (<c>skipComplete</c>).</summary>
    public required int SkippedUnits { get; init; }

    /// <summary>Versions asked for that are not older patches, left out.</summary>
    public required IReadOnlyList<string> IgnoredVersions { get; init; }
```

### 6.4 Planifier, puis ouvrir

Aujourd'hui `PassLauncher.StartAsync` lit les anciens patchs et planifie lui-même. On sépare la
planification, pour pouvoir refuser une passe vide **avant** d'écrire sa ligne :

```csharp
namespace LoDb.Api.Modules.Admin.Prerender.Passes;

/// <summary>What a pass would render: its units in order, and what its scope left out.</summary>
/// <param name="Units">One per (version, locale), in the order the worker takes them.</param>
/// <param name="Skipped">(version, locale) left out as already complete in the epoch.</param>
/// <param name="Ignored">Versions asked for that are not older patches.</param>
internal sealed record PassPlan(
    IReadOnlyList<PlannedUnit> Units,
    int Skipped,
    IReadOnlyList<string> Ignored);

/// <summary>Plans a pass against the older patches and, if asked, its epoch's coverage.</summary>
internal sealed class PassPlanning(IPrerenderCatalog catalog, EpochCoverage coverage)
{
    public Task<PassPlan> PlanAsync(PassLaunch launch, CancellationToken cancellationToken);
}
```

- `UnitPlanner.Plan(pinned, scope, held)` : `held` est l'ensemble des `(version, code de
  langue)` de l'epoch dont le masque de types tenus inclut les types du périmètre
  (`CoverageMap.HeldFor(types)`, § 6.5 ; vide sans `SkipComplete`) ; l'ordre ne change pas
  (prioritaires d'abord, dans leur ordre, puis le plus récent d'abord ; les langues dans l'ordre
  du site) ; les rangs restent contigus après le retrait des unités ignorées. `Ignored` =
  `scope.Versions` moins les anciens patchs, dans l'ordre reçu.
- `PassPlanning` pose aussi `PlannedUnit.PagesExpected` (§ 6.3) : une requête agrégée par
  version (`MAX(pages_total)` des unités `completed` des passes au même jeu de types, ces passes
  choisies en mémoire d'après leur `scope`, quelques centaines de lignes), une fois par
  lancement. `VersionEnqueuer` fait de même pour la version qu'il ajoute.
- `PassLauncher.StartAsync(PassLaunch launch, PassPlan plan, CancellationToken)` écrit la passe et
  ses unités (`null` si une passe est active, comme aujourd'hui, index
  `ux_prerender_pass_active`). L'ancienne surcharge `StartAsync(launch, token)` disparaît : ses deux
  appelants (le pupitre, la ligne de commande) planifient d'abord.

**Le pupitre** (`Http/PrerenderDesk.StartAsync`), dans cet ordre :

1. `PassRequestReader.Read` → `400` avec tous les champs fautifs ;
2. `!activation.IsActive` → `409 prerender-dormant` ;
3. une passe active (`AnyAsync(IsActive)`) → `409 prerender-pass-active` (avant le coût de la
   planification) ;
4. `planning.PlanAsync(launch)` ;
5. plan vide : `400` `versions: ["none-pinned"]` si `Versions` est posé et entièrement ignoré, ou
   s'il n'existe aucun ancien patch ; sinon (tout est complet) `400` `scope: ["nothing-to-render"]` ;
6. `launcher.StartAsync(launch, plan)` → `null` : `409 prerender-pass-active` (course perdue) ;
7. audit `admin.prerender_start`, méta `epoch`, `scope` (JSON stocké, ~5 Ko au plus pour 400
   versions), `units`, `skipped` ;
8. `201` avec `{ id, epoch, units, skippedUnits, ignoredVersions }`.

### 6.5 La couverture d'une epoch

Nouveau dossier `Prerender/Coverage/` (lot `api`) :

```csharp
/// <summary>
/// What an epoch holds of the older patches, (version, locale) by (version, locale), read from
/// the units of its passes: the store cannot be listed (the writer key does not list).
/// </summary>
internal sealed class EpochCoverage(IDbContextFactory<LoDbDbContext> contexts)
{
    /// <summary>The cells of <paramref name="epoch"/>, keyed by version and locale code.</summary>
    public Task<CoverageMap> ReadAsync(int epoch, CancellationToken cancellationToken);
}

/// <summary>The state of one (version, locale) in an epoch.</summary>
/// <param name="Code">c, w, p or f (<see cref="UnitCells"/>); an absent cell is not in the map.</param>
/// <param name="Held">
/// The types of page held: bit 0 champions, 1 items, 2 runes, 3 summoners (the order of
/// ResourceType), each set when the latest completed unit over that type failed no page.
/// </param>
/// <param name="Pages">The most pages any unit wrote for it (a later unit replaces them).</param>
/// <param name="Bytes">The most bytes any unit wrote for it.</param>
/// <param name="FailedPages">Pages failed by the latest completed unit over each type, summed once.</param>
/// <param name="LastEndedAt">When its latest unit ended, completed or failed; null if none.</param>
internal sealed record CoverageCell(
    char Code, int Held, int Pages, long Bytes, int FailedPages, DateTimeOffset? LastEndedAt);
```

`CoverageMap` expose `CellOf(version, locale)`, `HeldFor(types)` (les (version, langue) dont
`Held` inclut le masque de `types`) et `Summary(pinned)` → `PrerenderEpochCoverage`. Le pliage
est une fonction pure, `CoverageFold.Of(units, typesByPass)`, testée sans base.

**Requêtes** (deux, sur une connexion) :

```csharp
var passes = await db.PrerenderPasses.AsNoTracking()
    .Where(pass => pass.Epoch == epoch)
    .Select(static pass => new { pass.Id, pass.Scope })
    .ToListAsync(token);
// SQL: SELECT id, scope FROM prerender_pass WHERE epoch = @epoch
// A seq scan of a table of a few hundred rows: no index needed.

var units = await db.PrerenderUnits.AsNoTracking()
    .Where(unit => passIds.Contains(unit.PassId)
        && (unit.State != PrerenderUnitState.Pending || unit.PagesWritten > 0))
    .Select(static unit => new CoverageUnit(unit.PassId, unit.Version, unit.Locale, unit.State,
        unit.PagesTotal, unit.PagesWritten, unit.PagesFailed, unit.BytesWritten, unit.EndedAt))
    .ToListAsync(token);
// SQL: … FROM prerender_unit WHERE pass_id = ANY(@ids) AND (state <> 'pending' OR …)
// Served by pk_prerender_unit (pass_id, version, locale): ~8,400 rows for a whole pass.
```

**Pliage**, par (version, langue). Les types d'une unité sont ceux du périmètre de sa passe (lus
par `PassScopeJson` ; `null` = les quatre). L'ordre des unités est celui de `EndedAt`
(`UpdatedAt` pour les lignes d'avant la migration, § 6.9).

1. **Par type** `t` : la plus récente unité **terminée** dont la passe couvre `t` décide ; `t`
   est **tenu** (bit de `Held`) si elle n'a raté aucune page, **à pages en échec** sinon, **non
   couvert** s'il n'y en a aucune. Une unité ne sait pas sur quel type ses pages ont échoué :
   ses échecs marquent tous ses types (prudent : la relance les refait).
2. **Code** : les quatre types tenus → `c` ; les quatre couverts, au moins un à pages en échec →
   `w` ; sinon, si la plus récente unité finie (terminée ou en échec) est en échec et qu'aucun
   type n'est couvert → `f` ; sinon, si une unité a écrit des pages, couvre des types, ou
   tourne → `p` ; sinon la cellule est absente (`-`).
3. `Pages`, `Bytes` = maximum sur les unités (même règle que `StorageCensus.UnitsAsync`, qui
   l'explique : une passe qui réécrit les mêmes pages les remplace).

Ainsi une passe propre limitée aux objets n'efface pas les pages en échec qu'une passe plus
ancienne avait laissées sur les champions : seul le bit des objets change.

`PrerenderEpochCoverage` (nouvelle vue, `Http/Views/PrerenderEpochCoverage.cs`) :

```csharp
/// <summary>How much of the older patches an epoch holds, (version, locale) by (version, locale).</summary>
internal sealed record PrerenderEpochCoverage
{
    /// <summary>The older patches Data Dragon lists now.</summary>
    public required int PinnedVersions { get; init; }

    /// <summary>Their (version, locale): PinnedVersions times the site's 21 locales.</summary>
    public required int ExpectedUnits { get; init; }

    /// <summary>(version, locale) covered over every type of page (c or w).</summary>
    public required int CompleteUnits { get; init; }

    /// <summary>Those of CompleteUnits holding every type (c).</summary>
    public required int CleanUnits { get; init; }

    /// <summary>(version, locale) whose unit failed for good (f): the SSR serves them.</summary>
    public required int FailedUnits { get; init; }

    /// <summary>
    /// (version, locale) with a gap, absent (-) or partial (p): what keeps the epoch from being
    /// ready, ExpectedUnits − CompleteUnits − FailedUnits.
    /// </summary>
    public required int GapUnits { get; init; }

    /// <summary>Pages failed in the c and w cells, which the SSR serves once switched.</summary>
    public required long FailedPages { get; init; }

    /// <summary>Older patches complete (c or w) in all 21 locales.</summary>
    public required int CompleteVersions { get; init; }

    /// <summary>Older patches with a gap in one locale at least.</summary>
    public required int GapVersions { get; init; }
}
```

`PrerenderEpochVolume` gagne `public PrerenderEpochCoverage? Coverage { get; init; }` (« null for a
retired epoch »). `PrerenderJournal.ReportAsync` lit la couverture de chaque epoch non retirée :
la servie, celle qu'on écrit, et **toutes** celles en attente (des restes de passes annulées
peuvent en laisser plusieurs, `EpochStates.Standby`), et la passe à `StorageLedger.Of` par
`StorageFacts.Coverages` (`IReadOnlyDictionary<int, PrerenderEpochCoverage>`).

**Coût dans le rapport** (relu toutes les 60 s pendant une passe). Plier à chaque lecture toutes
les unités de toutes les passes de chaque epoch non retirée (passe entière, relances, promotions :
plusieurs fois 8 400 lignes par epoch) serait du gaspillage : la couverture ne change que quand
une unité **finit**. D'où `CoverageCache` (singleton, `Coverage/CoverageCache.cs`) : par epoch,
le résumé plié et son **tampon** `(unités finies, plus grand ended_at)`, lu par un agrégat sur
l'index partiel `ix_prerender_unit_ended` (§ 6.9, parcours d'index seul) ; le pliage complet ne
se refait que quand le tampon change, soit une fois par unité finie (≈ toutes les 6 min pendant
une passe). Une unité qui démarre change `-` en `p`, deux manques : le résumé n'en dépend pas. Le
point d'accès `…/patches` (§ 6.6), lu rarement, plie sans cache.

**Epoch prête** (`Coverage/EpochReadiness.cs`, qui remplace `PassHistory.ReadyEpochAsync`) : la
plus récente epoch `E > servie`, que la passe active n'écrit pas, dont `GapUnits == 0` et
`ExpectedUnits > 0`. Les cellules `f` sont **admises** : une unité échoue pour de bon (version
inconnue de Data Dragon, `UnitRunner.UnknownAsync` ; catalogue jamais arrivé,
`UnitQueue.AbandonAsync` après 30 min) et `UnitQueue.NextAsync` ne la reprend jamais ; exiger
`c`/`w` partout éteindrait la bande « prête » pour toujours, et « Compléter » relancerait sans fin
ce qui échoue de la même façon. La bande les nomme (`overview.bands.ready_failed_units`, « 12
unités en échec définitif passeront par le SSR »), comme les pages en échec des `w`
(`overview.bands.ready_failed_pages`) ; les relancer reste possible depuis la couverture
(les cocher, puis « Pré-rendre la sélection »). **Changement de règle voulu** : aujourd'hui une epoch n'est prête qu'après **une**
passe « entière » terminée (`PassHistory.IsWhole`) ; une epoch remplie par plusieurs passes
ciblées ne l'était jamais. La couverture répond à ce cas comme à celui d'aujourd'hui (une passe
entière terminée avec quelques unités en échec reste prête).

### 6.6 `GET /api/admin/prerender/patches?epoch={n}`

`epoch` : facultatif, l'epoch servie par défaut ; `< 1` → `400` `epoch: ["out-of-range"]`. Une
epoch retirée se lit (consultation). Service : `Coverage/PatchCoverageReader.cs` (lot `api`, partie B1).

```csharp
/// <summary>The older patches and what one epoch holds of each: the admin's coverage view.</summary>
internal sealed record PrerenderPatchesReport
{
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>The epoch the coverage is read in.</summary>
    public required int Epoch { get; init; }

    public required int ServedEpoch { get; init; }

    /// <summary>The latest version, which the SSR serves and no pass renders; null before any.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>The site's locales, in the order of each patch's cells.</summary>
    public required IReadOnlyList<string> Locales { get; init; }

    /// <summary>Every older patch, newest first.</summary>
    public required IReadOnlyList<PrerenderPatchRow> Patches { get; init; }

    public required PrerenderEpochCoverage Coverage { get; init; }

    public required PrerenderEstimateBasis Estimates { get; init; }
}

/// <summary>One older patch in an epoch.</summary>
internal sealed record PrerenderPatchRow
{
    public required string Version { get; init; }

    /// <summary>One character per locale of Locales: c, w, p, f or - (absent).</summary>
    public required string Cells { get; init; }

    /// <summary>
    /// One hexadecimal character per locale of Locales: the mask of the types held (bit 0
    /// champions, 1 items, 2 runes, 3 summoners); f holds all four, 0 none.
    /// </summary>
    public required string Types { get; init; }

    /// <summary>Locales whose cell is c or w.</summary>
    public required int CompleteLocales { get; init; }

    /// <summary>Pages stored in the epoch for it, all locales.</summary>
    public required long Pages { get; init; }

    public required long Bytes { get; init; }

    /// <summary>Pages failed by the latest completed units, served by the SSR.</summary>
    public required long FailedPages { get; init; }

    /// <summary>
    /// Pages of one locale, as a completed unit over every type listed them, in any pass of any
    /// epoch (the most of them); null when none did.
    /// </summary>
    public int? PagesPerLocale { get; init; }

    /// <summary>When a unit of the epoch last ended for it; null when none did.</summary>
    public DateTimeOffset? LastEndedAt { get; init; }
}

/// <summary>What the admin estimates a pass with, all measured here.</summary>
internal sealed record PrerenderEstimateBasis
{
    /// <summary>
    /// The share of each type in a version's pages, from the catalog of the newest older patch
    /// a unit completed (its catalog is stored); null when none did.
    /// </summary>
    public PrerenderTypeShares? TypeShares { get; init; }

    /// <summary>Mean bytes of a written page over the completed units of the served epoch.</summary>
    public long? BytesPerPage { get; init; }

    /// <summary>
    /// Pages per minute of the latest pass that ran 10 minutes or more; null if none. The
    /// client bounds it by ConfiguredRendersPerMinute: the ceiling may have been lowered since.
    /// </summary>
    public decimal? MeasuredPagesPerMinute { get; init; }

    /// <summary>The pass's ceiling, LoDb:Prerender:RendersPerMinute.</summary>
    public required int ConfiguredRendersPerMinute { get; init; }

    public required decimal PricePerGbMonthOneZone { get; init; }

    /// <summary>ISO 4217 code of the price.</summary>
    public required string Currency { get; init; }
}

/// <summary>Shares from 0 to 1, summing to 1, to four decimals.</summary>
internal sealed record PrerenderTypeShares
{
    public required decimal Champions { get; init; }
    public required decimal Items { get; init; }
    public required decimal Runes { get; init; }
    public required decimal Summoners { get; init; }
}
```

Exemple (tronqué à deux patchs) :

```json
{
  "generatedAt": "2026-10-21T08:40:00+00:00",
  "epoch": 2,
  "servedEpoch": 2,
  "latestVersion": "16.20.1",
  "locales": ["ar","cs","de","el","en","es","fr","hu","id","it","ja","ko","pl","pt","ro","ru","th","tr","vi","zh-hans","zh-hant"],
  "patches": [
    { "version": "16.19.1", "cells": "ccccccccccccccccccccc", "types": "fffffffffffffffffffff",
      "completeLocales": 21,
      "pages": 24310, "bytes": 948090000, "failedPages": 0, "pagesPerLocale": 1158,
      "lastEndedAt": "2026-10-20T14:02:11+00:00" },
    { "version": "16.18.1", "cells": "ccccwcpf-------------", "types": "ffffdf200000000000000",
      "completeLocales": 5,
      "pages": 6140, "bytes": 239460000, "failedPages": 2, "pagesPerLocale": 1140,
      "lastEndedAt": "2026-10-21T08:30:40+00:00" }
  ],
  "coverage": { "pinnedVersions": 402, "expectedUnits": 8442, "completeUnits": 8274,
    "cleanUnits": 8223, "failedUnits": 12, "gapUnits": 156, "failedPages": 270,
    "completeVersions": 391, "gapVersions": 11 },
  "estimates": {
    "typeShares": { "champions": 0.1512, "items": 0.5803, "runes": 0.1894, "summoners": 0.0791 },
    "bytesPerPage": 39000, "measuredPagesPerMinute": 112.6, "configuredRendersPerMinute": 120,
    "pricePerGbMonthOneZone": 0.00803, "currency": "EUR" }
}
```

Lectures (service `Coverage/PatchCoverageReader.cs`, bases dans `Coverage/EstimateBasisReader.cs`) :
couverture de l'epoch (§ 6.5, sans cache) ; `PagesPerLocale` : `MAX(pages_total)` par version
des unités `completed` des passes « tous types » (choisies en mémoire d'après leur `scope`), une
requête agrégée ; `BytesPerPage` : sur les unités terminées de l'epoch servie ; les anciens
patchs et le dernier (`IPrerenderCatalog.PinnedVersionsAsync` et un nouveau
`LatestVersionAsync`, à ajouter à l'interface, à `CatalogPages` et à `FakeCatalog` des tests) ;
`TypeShares` : le catalogue du plus récent ancien patch **dont une unité est terminée** (son
catalogue est donc stocké : `PagesAsync` le lit sans rien mettre en file — lu sur un patch non
stocké, il mettrait une ingestion en file, un effet de bord qu'une lecture GET ne doit pas
avoir), compté par `PageTypes.Keep` ; `null` si aucun ; `MeasuredPagesPerMinute` : les cinq
dernières passes, la première dont le temps actif (`PassProgress.ActiveSeconds`) atteint 600 s,
son `PagesPerMinute`. Taille : ~400 lignes × ~230 o ≈ 95 Ko brut, ~13 Ko gzip ; jamais relue en
boucle.

### 6.7 `GET /api/admin/prerender/passes/{id}/progress?since={iso}`

Service : `Http/Monitor/PassMonitor.cs` (lot `api`, partie B2). Toute passe, active ou finie.

```csharp
/// <summary>
/// The progress of a pass, sized to be read every few seconds: its counters, each version's
/// cells, what runs, what comes next, what just ended or failed, and the engine's own signals.
/// </summary>
internal sealed record PrerenderPassProgress
{
    public required DateTimeOffset GeneratedAt { get; init; }

    public required PrerenderPassView Pass { get; init; }

    /// <summary>The locales the pass has units in, in the site's order: the order of each Cells.</summary>
    public required IReadOnlyList<string> Locales { get; init; }

    /// <summary>
    /// Whether Versions holds every version of the pass; false when it holds only those with a
    /// unit changed since the <c>since</c> asked for.
    /// </summary>
    public required bool IsComplete { get; init; }

    /// <summary>The pass's versions, in the order the worker reaches them.</summary>
    public required IReadOnlyList<PrerenderVersionProgress> Versions { get; init; }

    /// <summary>
    /// The older patches the pass's scope lists, in the order sent, when IsComplete and the
    /// scope lists some; null otherwise (every older patch, or an incremental read). The list
    /// views carry only their count (PrerenderScopeView).
    /// </summary>
    public IReadOnlyList<string>? ScopeVersions { get; init; }

    /// <summary>The units running now; one in practice (left running by a pause, see z).</summary>
    public required IReadOnlyList<PrerenderUnitView> Running { get; init; }

    /// <summary>
    /// The next pending units, in the order the worker takes them, without the versions this
    /// instance sees waiting (Pulse.Waiting); 5 at most.
    /// </summary>
    public required IReadOnlyList<PrerenderUnitKey> Next { get; init; }

    /// <summary>The units that ended last, completed or failed, newest first; 12 at most.</summary>
    public required IReadOnlyList<PrerenderUnitView> Recent { get; init; }

    /// <summary>The units failed or with failed pages, newest first; 20 at most.</summary>
    public required IReadOnlyList<PrerenderUnitView> Failures { get; init; }

    /// <summary>Every unit failed or with failed pages.</summary>
    public required int UnitsWithFailures { get; init; }

    public required PrerenderEnginePulse Pulse { get; init; }
}

/// <summary>One version of a pass: a cell per locale, and its pages so far.</summary>
internal sealed record PrerenderVersionProgress
{
    public required string Version { get; init; }

    /// <summary>The lowest priority of its units: where the worker reaches it.</summary>
    public required int Rank { get; init; }

    /// <summary>
    /// One character per locale of Locales: . pending, r running, z stopped (running in a
    /// paused pass), c completed, w completed with failed pages, f failed, i interrupted
    /// (running in a pass no longer active), _ no unit.
    /// </summary>
    public required string Cells { get; init; }

    public required long PagesTotal { get; init; }
    public required long PagesWritten { get; init; }
    public required long PagesSkipped { get; init; }
    public required long PagesFailed { get; init; }
    public required long BytesWritten { get; init; }

    /// <summary>When one of its units last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>One unit of a pass, where the worker left it.</summary>
internal sealed record PrerenderUnitView
{
    public required string Version { get; init; }
    public required string Locale { get; init; }

    /// <summary>pending, running, completed or failed.</summary>
    public required string State { get; init; }

    /// <summary>Index of the next page to render.</summary>
    public required int Cursor { get; init; }

    public required int PagesTotal { get; init; }
    public required int PagesWritten { get; init; }
    public required int PagesSkipped { get; init; }
    public required int PagesFailed { get; init; }
    public required long BytesWritten { get; init; }

    /// <summary>When the worker first started it; null while pending.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>When it ended, completed or failed; null while it has not.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>Why its last page failed, or why it failed; null when nothing failed.</summary>
    public string? Failure { get; init; }
}

/// <summary>A (version, locale) of a pass.</summary>
internal sealed record PrerenderUnitKey
{
    public required string Version { get; init; }
    public required string Locale { get; init; }
}

/// <summary>
/// What the instance answering sees of the engine: none of it is stored, so another instance
/// sees nothing (<see cref="ObservedAt"/> null).
/// </summary>
internal sealed record PrerenderEnginePulse
{
    /// <summary>When this instance last ran the engine; null when it never did.</summary>
    public DateTimeOffset? ObservedAt { get; init; }

    /// <summary>Whether this instance runs its background tasks (LoDb:Workers:Enabled).</summary>
    public required bool IsWorkerEnabled { get; init; }

    /// <summary>Since when the SSR is out of reach for the pass; null while it answers.</summary>
    public DateTimeOffset? SsrUnreachableSince { get; init; }

    /// <summary>The versions of this pass whose units wait.</summary>
    public required IReadOnlyList<PrerenderWaitView> Waiting { get; init; }
}

/// <summary>A version whose units wait, and until when they may.</summary>
internal sealed record PrerenderWaitView
{
    public required string Version { get; init; }

    /// <summary>catalog-pending, not-pinned or settling.</summary>
    public required string Reason { get; init; }

    public required DateTimeOffset Since { get; init; }

    /// <summary>When its units fail if it still waits: UnitWaits.Longest after Since.</summary>
    public required DateTimeOffset GivesUpAt { get; init; }
}
```

**`since`** (`[FromQuery] DateTimeOffset? since`) :

- absent, ou plus de **10 minutes** avant `GeneratedAt` : `IsComplete = true`, toutes les versions ;
- sinon : `IsComplete = false`, seulement les versions dont une unité a `updated_at > since`
  (recalculées entières : leurs 21 cellules et leurs sommes).

Le client envoie `since = generatedAt précédent − 30 s`. `updated_at` et `GeneratedAt` viennent de
la même horloge (`TimeProvider` de l'API, un seul hôte, ADR 0002). Aucune unité n'est jamais
supprimée (seule la cascade d'une passe supprimée, ce que rien ne fait), donc une version ne
disparaît jamais d'une réponse à l'autre.

Exemple (lecture incrémentale) :

```json
{
  "generatedAt": "2026-10-21T08:42:05+00:00",
  "pass": { "id": 12, "epoch": 2, "state": "running",
    "scope": { "versionCount": null, "newestListed": null, "oldestListed": null,
      "oldest": null, "newest": null, "locales": null, "pageTypes": null,
      "priority": ["16.18.1","16.12.1","15.2.1"], "skipComplete": true },
    "authorId": 1, "authorName": "charles", "createdAt": "2026-10-18T08:00:00+00:00",
    "finishedAt": null, "activeSeconds": 274800, "pagesTotal": 1610320,
    "pagesWritten": 1512300, "pagesSkipped": 19840, "pagesFailed": 270,
    "bytesWritten": 58979700000, "units": { "total": 4452, "pending": 1410, "running": 1,
      "completed": 3024, "failed": 17 },
    "pagesPerMinute": 231.4, "estimatedEndAt": "2026-10-23T14:00:00+00:00",
    "runningSince": "2026-10-21T06:00:00+00:00", "pausedAt": null, "pauseReason": null,
    "pagesLeft": 613590 },
  "locales": ["ar","cs","de","el","en","es","fr","hu","id","it","ja","ko","pl","pt","ro","ru","th","tr","vi","zh-hans","zh-hant"],
  "isComplete": false,
  "scopeVersions": null,
  "versions": [
    { "version": "16.18.1", "rank": 0, "cells": "ccwcccr..............",
      "pagesTotal": 7980, "pagesWritten": 7140, "pagesSkipped": 40, "pagesFailed": 2,
      "bytesWritten": 278460000, "updatedAt": "2026-10-21T08:42:03+00:00" }
  ],
  "running": [
    { "version": "16.18.1", "locale": "fr", "state": "running", "cursor": 312,
      "pagesTotal": 1140, "pagesWritten": 309, "pagesSkipped": 3, "pagesFailed": 0,
      "bytesWritten": 12051000, "startedAt": "2026-10-21T08:38:01+00:00",
      "updatedAt": "2026-10-21T08:42:03+00:00", "endedAt": null, "failure": null }
  ],
  "next": [ { "version": "16.18.1", "locale": "hu" }, { "version": "16.18.1", "locale": "id" } ],
  "recent": [
    { "version": "16.18.1", "locale": "es", "state": "completed", "cursor": 1140,
      "pagesTotal": 1140, "pagesWritten": 1134, "pagesSkipped": 6, "pagesFailed": 0,
      "bytesWritten": 44226000, "startedAt": "2026-10-21T08:31:55+00:00",
      "updatedAt": "2026-10-21T08:38:00+00:00", "endedAt": "2026-10-21T08:38:00+00:00",
      "failure": null }
  ],
  "failures": [
    { "version": "16.18.1", "locale": "de", "state": "completed", "cursor": 1138,
      "pagesTotal": 1138, "pagesWritten": 1130, "pagesSkipped": 6, "pagesFailed": 2,
      "bytesWritten": 44070000, "startedAt": "2026-10-21T08:24:10+00:00",
      "updatedAt": "2026-10-21T08:30:40+00:00", "endedAt": "2026-10-21T08:30:40+00:00",
      "failure": "The SSR answered 500, not a page to store." }
  ],
  "unitsWithFailures": 17,
  "pulse": { "observedAt": "2026-10-21T08:42:03+00:00", "isWorkerEnabled": true,
    "ssrUnreachableSince": null,
    "waiting": [ { "version": "16.3.1", "reason": "catalog-pending",
      "since": "2026-10-21T08:39:00+00:00", "givesUpAt": "2026-10-21T09:09:00+00:00" } ] }
}
```

**Requêtes** (partie B2), une connexion, pour une passe de 8 400 unités :

| # | Ce qu'elle lit | LINQ (esquisse) | Index | Lignes |
|---|---|---|---|---|
| 1 | la passe, son auteur, ses comptes et `ExpectedPages` | `PassHistory.OneAsync(id)` (nouvelle, sur le modèle de `LatestAsync`, même `GroupBy(PassId, State)`) | `pk_prerender_pass`, `ix_prerender_unit_next` | 1 + 4 groupes |
| 2 | en cours | `Where(PassId == id && State == Running)` | `ix_prerender_unit_next` | 1 |
| 3 | suivantes | `Where(PassId == id && State == Pending && !waiting.Contains(Version)).OrderBy(Priority).ThenBy(Version).ThenBy(Locale).Take(5)` (l'ordre et l'exclusion de `UnitQueue.NextAsync` ; `waiting` = versions de `UnitWaits.Snapshot(id)` sur cette instance) | `ix_prerender_unit_next` | 5 |
| 4 | récentes | `Where(PassId == id && EndedAt != null).OrderByDescending(EndedAt).Take(12)` | **`ix_prerender_unit_ended`** (pass_id, ended_at) partiel, parcours inverse | 12 |
| 5 | échecs | `Where(PassId == id && EndedAt != null && (State == Failed ‖ PagesFailed > 0)).OrderByDescending(EndedAt)`, `Take(20)` + `CountAsync` | `ix_prerender_unit_ended` | ≤ unités finies, filtrées par le serveur SQL |
| 6 | versions changées | `Where(PassId == id && changed.Contains(Version))` où `changed = Where(PassId == id && UpdatedAt > since).Select(Version).Distinct()` | `pk_prerender_unit` (préfixe `pass_id`) : 8 400 lignes parcourues, quelques ms | ~21 à 63 |
| 6′ | toutes les versions (complète) | `Where(PassId == id).Select(colonnes de cellule)` | `pk_prerender_unit` | 8 400 |

Les cellules et les sommes par version se calculent en mémoire à partir de la requête 6/6′
(`VersionRows.Of(units, locales, passState)`, pur) : `r` dans une passe en cours, `z` dans une
passe en pause, `i` dans une passe finie. Coût attendu : quelques millisecondes par lecture
(le vérifier par `EXPLAIN ANALYZE` sur 8 400 lignes dans `AdminPrerenderProgressTests`, § 7.4) ;
réponse incrémentale < 10 Ko brut, complète ~70 Ko brut (~10 Ko gzip). Le périmètre de la passe
(`pass.scope`) est **résumé** (§ 6.3, < 2 Ko avec 100 priorités) ; la liste complète des versions
(`scopeVersions`, jusqu'à ~14 Ko brut pour 1 000 versions) ne part que dans une lecture complète,
soit à l'ouverture, toutes les 10 min, et au retour d'une absence.

### 6.8 `GET /api/admin/prerender/passes/{id}/versions/{version}`

```csharp
/// <summary>The units of one version in a pass, a row per locale.</summary>
internal sealed record PrerenderVersionUnits
{
    public required long PassId { get; init; }
    public required string Version { get; init; }

    /// <summary>Its units, in the site's order of the locales; empty when the pass has none.</summary>
    public required IReadOnlyList<PrerenderUnitView> Units { get; init; }
}
```

`version` mal formée → `400` `version: ["malformed"]` ; passe inconnue → `404` ; version sans
unité → `200` avec `units: []`. Requête : `Where(PassId == id && Version == v)`,
`pk_prerender_unit`, 21 lignes.

### 6.9 Diagnostics : ce que la passe doit désormais retenir

Aujourd'hui la raison d'un échec, la pause d'elle-même et les attentes ne vivent que dans le
journal (`Telemetry/PrerenderLog.cs`). Le parcours P3 en a besoin à l'écran.

**Migration additive `PrerenderDiagnostics`** (lot `api`, `src/LoDb.Infrastructure/Persistence/Migrations/`) :

| Table | Colonne | Type | Entité, propriété | Écrite par |
|---|---|---|---|---|
| `prerender_unit` | `started_at` | `timestamptz NULL` | `PrerenderUnit.StartedAt` | `UnitQueue.StartAsync` : `row.StartedAt ?? now` (la première fois seulement, une reprise garde le premier départ) |
| `prerender_unit` | `failure` | `varchar(300) NULL` | `PrerenderUnit.Failure`, `FailureMaxLength = 300` | `UnitQueue.RecordAsync` (raison de la dernière page en échec du pas, `UnitStep.Failure`), `UnitQueue.FailAsync` (unité inconnue de Data Dragon), `UnitQueue.AbandonAsync` (attente expirée) ; tronquée à 300 avec « … » |
| `prerender_pass` | `paused_at` | `timestamptz NULL` | `PrerenderPass.PausedAt` | `PassTimes.Move` : `now` vers `Paused`, `null` sinon |
| `prerender_pass` | `pause_reason` | `varchar(300) NULL` | `PrerenderPass.PauseReason`, `PauseReasonMaxLength = 300` | `PassControl.PauseItselfAsync(id, reason, token)` (nouvelle, appelée par `UnitRunner.StopAsync` sur `IsMisconfigured`) ; `null` à tout autre mouvement |
| `prerender_unit` | `ended_at` | `timestamptz NULL` | `PrerenderUnit.EndedAt` | **une seule fois**, à la fin de l'unité : `UnitQueue.FinishAsync`, `FailAsync`, `AbandonAsync` (`now`) |
| `prerender_unit` | `pages_expected` | `int NULL` | `PrerenderUnit.PagesExpected` | `PassLauncher.StartAsync` (depuis `PlannedUnit.PagesExpected`, § 6.4), `VersionEnqueuer` |
| `prerender_unit` | index partiel `ix_prerender_unit_ended` | `(pass_id, ended_at) WHERE ended_at IS NOT NULL` | `PrerenderUnitConfiguration` (`HasFilter`) | — |

**Pourquoi `ended_at` plutôt qu'un index sur `updated_at`.** `updated_at` est réécrit à chaque pas
de page (`UnitQueue.RecordAsync`) : l'indexer empêcherait les mises à jour HOT de
`prerender_unit` (une colonne indexée change) et ajouterait une entrée d'index à chaque page,
quatre millions de fois par passe. `ended_at` n'est écrit qu'une fois par unité : son index
partiel ne coûte qu'à la fin d'une unité, et sert les récentes, les échecs et le tampon de
`CoverageCache` (§ 6.5). Les versions changées depuis `since` se lisent sans index, par le
préfixe `pass_id` de la clé primaire (8 400 lignes, quelques ms). La migration date les unités
finies avant elle de leur dernier changement (`ended_at = updated_at`, sur la seule colonne
neuve) : sans cela, les échecs d'une passe en cours au déploiement, ou de toute passe ancienne,
manqueraient à la liste, à son compteur et aux récentes, que la matrice montre pourtant. Une
unité finie par l'image précédente après un retour à elle garde `ended_at` nul ; la couverture
ordonne par `updated_at`.

**Aucune contrainte de vérification** sur ces colonnes (pas de `(paused_at IS NOT NULL) =
(state = 'paused')`) : un retour à l'image précédente de la stack (runbook, AGENTS.md
« migrations additives ») mettrait des passes en pause sans `paused_at` et casserait sur la
contrainte. Les lignes existantes restent `NULL` : le moniteur dit alors « en pause » sans
durée.

**Écritures du moteur** (lot `api`, partie B2) :

- `UnitStep` gagne `Failure` (`string?`), posée par `Then(page)` quand la page finit `Failed`
  (`page.Reason`) ; `UnitQueue.RecordAsync` fait `SetProperty(row => row.Failure, row => failure
  ?? row.Failure)`.
- `UnitQueue.FinishAsync(unit, state, token)` reste et pose `ended_at` ;
  `UnitQueue.FailAsync(unit, reason, token)` (nouvelle) pour `UnitRunner.UnknownAsync` ;
  `AbandonAsync(unit, reason, token)` pose la raison ; les deux posent `ended_at`.
- `PassControl.MoveAsync` prend une cible `PassTarget(PrerenderPassState State, string? Reason)`
  (trois paramètres au plus) et écrit `paused_at`, `pause_reason` avec les autres colonnes.
- `UnitWaits.TryBegin(unit, UnitWaitReason reason)` retient la raison et l'heure murale ;
  `UnitWaits.Snapshot(passId)` rend `IReadOnlyList<UnitWait>` (`Version`, `Reason`, `Since`).
  `UnitWaitReason` : `CatalogPending`, `NotPinned`, `Settling` (écrits `catalog-pending`,
  `not-pinned`, `settling`) ; `UnitRunner.WhyWaitAsync` rend la raison avec son message.
- `SsrOutage` gagne `DateTimeOffset? Since` (heure murale de `Begin`, `null` après `End`).
- `EnginePulse` (nouveau singleton, `Writing/EnginePulse.cs`) : `Beat()` appelé à chaque tour de
  `PrerenderEngine.DrainAsync` et à la prise du verrou ; `LastBeatAt`.
- `ConventionalWorkers.IsEnabled(IConfiguration)` devient public (`Workers/ConventionalWorkers.cs`)
  pour `PrerenderEnginePulse.IsWorkerEnabled`.

Ces écritures ne changent ni l'ordre, ni le rythme, ni ce qui est compté : les tests existants de
la passe (`tests/LoDb.Api.Tests/Prerender/*`) restent verts tels quels.

### 6.10 Index et coût

| Index | Existe | Sert |
|---|---|---|
| `pk_prerender_unit` (pass_id, version, locale) | oui | couverture d'une epoch, versions d'une passe (et changées depuis `since`), détail d'une version |
| `ix_prerender_unit_next` (pass_id, state, priority) | oui (`PrerenderUnitConfiguration`) | comptes par état, en cours, suivantes |
| `ix_prerender_unit_ended` (pass_id, ended_at) `WHERE ended_at IS NOT NULL` | **nouveau**, partiel | récentes, échecs, tampon de `CoverageCache` |
| `ix_prerender_pass_created_at` | oui | historique |
| `ux_prerender_pass_active` | oui | une seule passe active |

Pas d'index sur `prerender_unit.updated_at` (réécrit à chaque page, § 6.9), ni sur
`prerender_pass.epoch` (quelques centaines de lignes), ni sur `prerender_unit.version` (lu par
`VersionEnqueuer` une fois par promotion, et par la requête agrégée de `pages_expected` une fois
par lancement).

### 6.11 Ce que gardent la ligne de commande, la promotion et le rapprochement

- **`prerender unit`** (`src/LoDb.Api/Cli/Prerender/`) : même usage, mêmes codes de sortie, même
  sortie. `PrerenderUnitArguments` construit `PassScope.Of(version) with { Locales = [locale],
  PageTypes = type is null ? null : [type] }` ; `PrerenderUnitCommand` planifie
  (`PassPlanning.PlanAsync`, sans `SkipComplete`) puis ouvre (`PassLauncher.StartAsync(launch,
  plan)`) ; une passe sans unité reste créée puis rapportée « No unit: the version is not an older
  patch Data Dragon lists. » comme aujourd'hui (le refus d'un plan vide est propre au pupitre).
- **`VersionEnqueuer`** (promotion et rapprochement, `Promotion/`) : même comportement.
  `PassScope.Of(version)` s'écrit désormais `{"versions":["16.19.1"]}` ; le report quand la passe
  active ne couvre pas tous les types devient `!PassScopeJson.Read(active.Scope).HasEveryPageType` ;
  `Coverage.Covers` teste `HasEveryPageType`. Les unités ajoutées à une passe active restreinte à
  certaines **langues** couvrent toujours les 21 langues manquantes (inchangé : seul le type de
  page est lu par le moteur dans le périmètre).
- **`PrerenderReconciler`**, **`PrerenderWorker`**, **`PrerenderEngine`** : inchangés hors
  `EnginePulse.Beat()`.
- **Audit** : la méta `scope` d'`admin.prerender_start` porte la nouvelle forme ; le journal
  d'audit (`/admin/journal`) l'affiche comme du JSON, sans autre changement.

## 7. Architecture front

### 7.1 Arborescence, élément public de chaque fichier, taille visée

Tout sous `prerender/`. Un élément public par fichier, nommé comme lui ; les types qui
l'accompagnent (entrées, sorties d'une fonction pure) vivent dans le même fichier, comme
`Segment` dans `admin/widgets/segment-bar.ts`. Tailles en lignes, plafond 300 (alerte), 400 max.
Chaque fichier `*.ts` de logique a son `*.spec.ts` à côté (listés en § 7.4), et **aucun dossier
ne compte plus de 10 fichiers, spécifications comprises** (`AGENTS.md`, « Fichiers / dossier
≤ 10 ») : d'où les sous-dossiers par responsabilité. Le nombre entre crochets est celui des
fichiers du dossier.

```
prerender/                                                         [6 + dossiers]
├── prerender-panel.ts / .html                 PrerenderPanel            150 / 120        F4 (réécrits)
├── prerender-panel.states.spec.ts             (états, CTA, bandes)      220              F4
├── prerender-panel.presets.spec.ts            (onglets, préréglages)    200              F4
├── prerender-tone.ts (+ spec)                 prerenderTone             35               F1 (+ pending, failed, stopped)
├── format/decimal-bytes*.ts                   (inchangés)               [3]              —
├── common/                                                                               F1
│   ├── selection/                                                     [8]
│   │   ├── patch-selection.ts                 selectionAfter            190
│   │   ├── selection-undo.ts                  SelectionUndo             55
│   │   ├── patch-matches.ts                   patchMatches              35
│   │   └── version-tokens.ts                  versionTokens             25
│   ├── patches/                                                       [8]
│   │   ├── season-of.ts                       seasonOf                  40
│   │   ├── patch-status-of.ts                 patchStatusOf             35
│   │   ├── is-held.ts                         isHeld                    30
│   │   └── locale-name.ts                     localeName                25
│   └── scope/                                                         [10]
│       ├── launch-preset.ts                   LaunchPreset (type)       35   (pas de spec)
│       ├── prerender-cadence.ts               PRERENDER_CADENCE         35   (pas de spec)
│       ├── inject-patches.ts                  injectPatches             40   (+ spec)
│       ├── scope-summary.ts                   scopeSummary              75
│       ├── failures-preset.ts                 failuresPreset (+ FailuresPreset) 70
│       └── retry-label.ts (+ spec)            retryLabel                25
├── live/                                                                                 F1
│   ├── poll/                                                          [5]
│   │   ├── inject-live-poll.ts                injectLivePoll            170
│   │   ├── poll-delay.ts                      pollDelay                 20
│   │   └── inject-page-visible.ts             injectPageVisible         35   (couvert par inject-live-poll.spec)
│   └── badge/                                                         [7]
│       ├── live-badge.ts                      LiveBadge                 110
│       ├── freshness-of.ts                    freshnessOf               35
│       ├── relative-time.ts                   relativeTime              35
│       └── inject-now.ts                      injectNow                 40   (couvert par live-badge.spec)
├── bars/                                                              [8]                F1
│   ├── stacked-bar.ts                         StackedBar                100
│   ├── stacked-segments.ts                    stackedSegments           45
│   ├── cell-strip.ts                          CellStrip                 50
│   └── strip-gradient.ts                      stripGradient             40
├── matrix/                                                                               F1
│   ├── grid/                                                          [10]
│   │   ├── cell-state.ts                      cellStateOf               65
│   │   ├── matrix-rows.ts                     matrixRows                120
│   │   ├── cell-tooltip.ts                    cellTooltip               65
│   │   └── unit-matrix.ts / .html / .css      UnitMatrix                180 / 110 / 150
│   ├── table/matrix-table.ts (+ spec)         MatrixTable               130  [2]
│   └── units/version-units-dialog.ts / .html (+ spec)  VersionUnitsDialog  140 / 90  [3]
├── dialogs/                                                           [4]                F1
│   ├── confirm-data.ts                        ConfirmData (type)        25
│   ├── confirm-dialog.ts (+ spec)             ConfirmDialog             90
│   └── inject-admin-dialogs.ts                injectAdminDialogs        70
├── launch/                                                                               F2
│   ├── dialog/                                                        [8]
│   │   ├── launch-opener.ts                   LaunchOpener              80   (pas de spec)
│   │   ├── launch-dialog.ts / .html (+ spec)  LaunchDialog              140 / 200
│   │   ├── launch-dialog-store.ts (+ spec)    LaunchDialogStore         200  (signaux : étape, brouillon, couverture, estimation, erreurs, appel, « Vérifier »)
│   │   └── launch-draft.ts (+ spec)           draftOf (+ LaunchDraft)   100
│   ├── picker/                                                        [9]
│   │   ├── patch-picker.ts / .html (+ spec)   PatchPicker               170 / 130
│   │   ├── picker-focus.ts (+ spec)           PickerFocus               130  (focus itinérant, recherche à la frappe)
│   │   ├── picker-rows.ts (+ spec)            pickerRows                90
│   │   └── picker-key.ts (+ spec)             pickerActionOf            130
│   ├── priority/                                                      [4]
│   │   ├── priority-tray.ts                   PriorityTray              90
│   │   ├── paste-box.ts                       PasteBox                  110  (couverts par patch-picker.spec et launch-dialog.spec)
│   │   └── paste-result-of.ts (+ spec)        pasteResultOf             50
│   ├── perimeter/                                                     [5]
│   │   ├── locale-chips.ts                    LocaleChips               110
│   │   ├── type-chips.ts                      TypeChips                 90
│   │   ├── epoch-cards.ts                     EpochCards                120  (couverts par launch-dialog.spec)
│   │   └── epoch-choices.ts (+ spec)          epochChoices              60
│   ├── summary/                                                       [7]
│   │   ├── launch-summary.ts                  LaunchSummary             170
│   │   ├── launch-estimate.ts (+ spec)        launchEstimate            150
│   │   ├── pages-per-locale-of.ts (+ spec)    pagesPerLocaleOf          60
│   │   └── launch-warnings.ts (+ spec)        launchWarnings            90
│   └── call/                                                          [5]
│       ├── request-of-draft.ts (+ spec)       requestOfDraft            50
│       ├── launch-errors-of.ts (+ spec)       launchErrorsOf            70
│       └── inject-launch-call.ts              injectLaunchCall          60   (couvert par launch-dialog.spec)
├── monitor/                                                           [6 + dossiers]     F3
│   ├── live-monitor.ts / .html (+ spec)       LiveMonitor               200 / 230
│   ├── inject-pass-progress.ts                injectPassProgress        90
│   ├── merge-progress.ts (+ spec)             mergeProgress (+ MonitorState) 150
│   ├── progress/                                                      [8]
│   │   ├── progress-segments.ts               progressSegments          55
│   │   ├── pace-samples.ts                    PaceSamples               160
│   │   ├── eta-of.ts                          etaOf                     70
│   │   └── server-now.ts                      serverNow                 20   (tous + spec)
│   ├── now/                                                           [9]
│   │   ├── now-block.ts                       NowBlock                  180  (en cours / « Reprendra par »)
│   │   ├── stall-of.ts (+ spec)               stallOf                   70
│   │   ├── written-at.ts (+ spec)             writtenAtOf               45
│   │   ├── failing-unit-of.ts (+ spec)        failingUnitOf             45
│   │   └── failing-band.ts                    FailingBand               70
│   ├── alarm/                                                         [3]
│   │   ├── alarm-of.ts (+ spec)               alarmOf (+ Alarm)          40
│   │   └── monitor-status.ts                  MonitorStatus             75
│   ├── activity/                                                      [4]
│   │   ├── activity-events.ts (+ spec)        activityEvents            150
│   │   ├── activity-feed.ts                   ActivityFeed              140
│   │   └── failures-list.ts                   FailuresList              130
│   ├── actions/                                                       [4]
│   │   ├── pass-actions-bar.ts                PassActionsBar            150
│   │   ├── consequences-of.ts (+ spec)        consequencesOf            110
│   │   └── inject-pass-actions.ts             injectPassActions         60   (recréé ici)
│   └── recap/pass-recap.ts (+ spec)           PassRecap                 150  [2]
├── overview/                                                                             F4
│   ├── state/                                                         [9]
│   │   ├── inject-prerender-report.ts         injectPrerenderReport     60
│   │   ├── quiet-panel.ts (+ spec)            quietPanel                40   (§ 2.4 : pas de bande sur un échec de fond)
│   │   ├── launch-lock-of.ts (+ spec)         launchLockOf              40
│   │   ├── band-state-of.ts (+ spec)          bandStateOf               70
│   │   └── pass-title-of.ts (+ spec)          passTitleOf               40
│   └── blocks/                                                        [4]
│       ├── launch-cta.ts                      LaunchCta                 90
│       ├── prerender-bands.ts                 PrerenderBands            150
│       ├── idle-card.ts                       IdleCard                  140
│       └── prerender-kpis.ts                  PrerenderKpis             120
├── coverage/                                                          [7]                F4
│   ├── coverage-tab.ts / .html (+ spec)       CoverageTab               210 / 180
│   ├── coverage-groups.ts (+ spec)            coverageGroups            90
│   └── coverage-summary-of.ts (+ spec)        coverageSummaryOf         50
├── costs/                                                             [5 + switch]       F4
│   ├── costs-tab.ts / .html (+ spec)          CostsTab                  140 / 190
│   ├── epoch-projection.ts (+ spec)           epochProjection           40
│   └── switch/                                                        [7]
│       ├── switch-snippets.ts (+ spec)        switchSnippets            60
│       ├── switch-dialog.ts / .html (+ spec)  SwitchDialog              110 / 90
│       └── retire-dialog.ts (+ spec)          RetireDialog              110
└── passes/                                                            [7]                F4
    ├── passes-tab.ts (+ spec)                 PassesTab                 180
    ├── relaunch-preset.ts (+ spec)            relaunchPreset            80
    └── pass-detail-dialog.ts / .html (+ spec) PassDetailDialog          210 / 170
```

**Supprimés par F4** (et seulement alors : l'ancien panneau doit compiler tant que F4 ne l'a pas
recâblé) : `launch/launch-form.{ts,html}`, `launch/pass-request-of{,.spec}.ts`, `pass/` entier
(`current-pass-card`, `pass-history-table`, `scope-summary`, `unit-share`),
`storage/storage-section.{ts,html}`, `poll-while.ts`, `prerender-poll.ts`,
`inject-pass-actions.ts` (racine), `prerender-panel.spec.ts` (remplacé par les deux spécifications
du panneau).

**Hors de `prerender/`.** Propriétaire F1 : `admin/admin-page.css` et
`admin/frame/admin-frame.css` (§ 7.5), `admin/charts/sparkline.ts` et
`admin/charts/scale/sparkline-shape.ts` (entrée `gaps`) avec leurs spécifications existantes
(`admin/charts/charts.spec.ts`, `admin/charts/scale/shapes.spec.ts`). Propriétaire F4 : `admin/layout/page-head.ts` (entrée
facultative `documentTitle`, § 2.5) et `admin/layout/layout.spec.ts` (son cas),
`admin/testing/i18n/provide-admin-catalogues.ts`. Propriétaire du lot `api` (phase contrat) :
`admin/testing/fixtures/prerender-report.ts` (mis au nouveau contrat), `prerender-patches.ts` et
`prerender-progress.ts` (nouveaux), à côté ; les autres lots ne les modifient pas (un besoin de
plus se construit par surcharge, `prerenderReport(overrides)`, dans la spécification qui en a
besoin).

#### Les contrats entre lots (à respecter à la lettre)

Ces éléments publics sont des contrats : les lots qui en dépendent (§ 8.1) compilent contre eux
tels qu'écrits ici (sélecteur, entrées, sorties, signature). Leur auteur ne les change pas ; un
écart inévitable est signalé dans sa remise, avec l'appelant à adapter.

```ts
// F1 — common/scope/prerender-cadence.ts
export interface PrerenderCadence {
  readonly progressRunningMs: number; // 5_000
  readonly progressPausedMs: number;  // 30_000
  readonly reportActiveMs: number;    // 60_000
  readonly reportIdleMs: number;      // 120_000
  readonly reportHiddenMs: number;    // 300_000
  readonly versionMs: number;         // 10_000
}
export const PRERENDER_CADENCE: InjectionToken<PrerenderCadence>; // providedIn root

// F1 — live/poll/inject-live-poll.ts
export interface LivePollOptions<T> {
  /** Reads once; gets the last good value, for a delta. */
  readonly load: (last: T | undefined) => Observable<T>;
  /** Milliseconds from an answer to the next read; null stops. Read reactively. */
  readonly every: () => number | null;
  /** The same while the tab is hidden; absent or null suspends the reads until it shows. */
  readonly hiddenEvery?: () => number | null;
}
export interface LivePoll<T> extends Panel<T> {  // Panel: admin/state/panel.ts
  readonly lastSuccessAt: Signal<number | null>;  // Date.now() at the last success
  readonly failures: Signal<number>;              // consecutive
  readonly nextAttemptAt: Signal<number | null>;
  readonly isHidden: Signal<boolean>;
}
export function injectLivePoll<T>(options: LivePollOptions<T>): LivePoll<T>;

// F1 — live/badge/live-badge.ts
@Component({ selector: 'lodb-live-badge' })
export class LiveBadge {
  readonly poll = input.required<LivePoll<unknown>>();
  readonly every = input.required<number>();
  /** Compact: the dot and « il y a 4 s » only, for the page header (§ 2.4). */
  readonly isCompact = input(false);
  /** Whether a change of state is announced: false while another badge says the same. */
  readonly announces = input(true);
}

// F1 — bars/stacked-bar.ts
export interface BarSegment {
  readonly key: string;
  readonly value: number;
  /** A CSS colour, a token. */
  readonly color: string;
  /** Translated, for the legend and the accessible name. */
  readonly label: string;
}
@Component({ selector: 'lodb-stacked-bar' })
export class StackedBar {
  readonly segments = input.required<readonly BarSegment[]>();
  /** The whole bar's accessible name, translated. */
  readonly label = input.required<string>();
  readonly size = input<'thin' | 'regular' | 'hero'>('regular'); // 4, 8, 14 px
  readonly isMuted = input(false);
}

// F1 — bars/cell-strip.ts
@Component({ selector: 'lodb-cell-strip' })
export class CellStrip {
  readonly cells = input.required<string>();
  readonly label = input.required<string>();
}

// F1 — matrix/grid/unit-matrix.ts
export interface MatrixVersion {
  readonly version: string;
  readonly cells: string;
  readonly rank?: number;
}
export interface RunningCell {
  readonly version: string;
  readonly locale: string;
  /** cursor / pagesTotal, 0 to 1; for an r or a z cell. */
  readonly share: number;
}
@Component({ selector: 'lodb-unit-matrix' })
export class UnitMatrix {
  readonly mode = input.required<'pass' | 'coverage'>();
  readonly locales = input.required<readonly string[]>();
  readonly versions = input.required<readonly MatrixVersion[]>();
  readonly running = input<readonly RunningCell[]>([]);
  /** Units the client knows in detail (running, recent, failures), for the tooltip. */
  readonly details = input<readonly PrerenderUnitView[]>([]);
  /** Coverage mode: one hexadecimal mask per locale, by version, for the tooltip. */
  readonly held = input<ReadonlyMap<string, string>>(new Map());
  readonly selected = input<ReadonlySet<string>>(new Set()); // coverage mode
  readonly opened = output<string>();   // pass mode: a version's detail
  readonly toggled = output<string>();  // coverage mode: a version's selection
}

// F1 — matrix/units/version-units-dialog.ts : opened through injectAdminDialogs().open
export interface VersionUnitsData { readonly passId: number; readonly version: string; readonly isActive: boolean }
export class VersionUnitsDialog { static readonly HEADING_ID = 'prerender-version-units-title'; }

// F1 — dialogs/inject-admin-dialogs.ts
export interface AdminDialogs {
  /** A dialog, or a bottom sheet under 640px; the component's host carries `admin-overlay`. */
  open<C, D, R>(component: ComponentType<C>, options: DialogOptions<D>): DialogRef<R, C>;
  /** Resolves true once confirmed, false once kept or closed. */
  confirm(data: ConfirmData): Promise<boolean>;
}
export function injectAdminDialogs(): AdminDialogs;

// F1 — dialogs/confirm-data.ts
export interface ConfirmData {
  readonly title: string;
  readonly lines: readonly string[];
  readonly confirmLabel: string;
  /** The safe choice, focused on opening. */
  readonly keepLabel: string;
  readonly tone: 'danger' | 'gold';
}

// F1 — common/selection/patch-selection.ts : PatchSelection, SelectionAction, selectionAfter
// exactly as § 3.3.2 (extent included).

// F1 — common/patches/is-held.ts
/** Whether the (version, locale) holds every type asked: its mask includes theirs (§ 3.4). */
export function isHeld(mask: string, types: readonly string[] | null): boolean;

// F1 — common/scope/failures-preset.ts
export interface FailuresPreset {
  readonly preset: LaunchPreset;
  /** Cells kept: what the relaunch renders again. */
  readonly units: number;
  /** ≈ pages of those units. */
  readonly pages: number;
  /** Of them, the units failed or with failed pages (f, w). */
  readonly failed: number;
  /** Of them, the units a cancelled pass never rendered whole (., i, z). */
  readonly gaps: number;
}
export function failuresPreset(
  pass: PrerenderPassView,
  versions: readonly PrerenderVersionProgress[],
  servedEpoch: number, // a retired epoch is relaunched into the served one
): FailuresPreset | null; // null: nothing to render again

// F2 — launch/dialog/launch-opener.ts
@Injectable({ providedIn: 'root' })
export class LaunchOpener {
  /** Opens the launch dialog; the pass it started, or null once closed. Posts the notice. */
  open(preset: LaunchPreset, report: PrerenderReport): Promise<PrerenderPassStarted | null>;
}

// F3 — monitor/live-monitor.ts (unchanged path)
@Component({ selector: 'lodb-live-monitor' })
export class LiveMonitor {
  readonly pass = input.required<PrerenderPassView>();     // report.current
  readonly report = input.required<PrerenderReport>();
  readonly launch = output<LaunchPreset>();                // « Relancer N unités »
  readonly changed = output<void>();                       // after an action: reread the report
  readonly ended = output<number>();                       // the pass id seen completed/cancelled
}

// F3 — monitor/recap/pass-recap.ts
@Component({ selector: 'lodb-pass-recap' })
export class PassRecap {
  readonly pass = input.required<PrerenderPassView>();
  readonly launch = output<LaunchPreset>();
  readonly detail = output<number>();
  readonly dismissed = output<number>();
}
```

Le panneau (F4) compose : `<lodb-live-monitor [pass] [report] (launch)="openLaunch($event)"
(changed)="report.reload()" (ended)="onEnded($event)" />`, `<lodb-pass-recap …>` ou
`<lodb-idle-card …>`, puis tuiles et onglets. Il est le seul à appeler `LaunchOpener.open` et à
ouvrir `PassDetailDialog`.

### 7.2 Fonctions pures à tester seules

| Fonction | Lot | Cas à couvrir (une ligne par classe d'équivalence, plus les bornes, `docs/guides/tests.md`) |
|---|---|---|
| `seasonOf` | F1 | `16.18.1` → 16/2026 ; `3.15.1` → 2013 ; `0.151.2` → pas d'année ; `100.1` → pas d'année |
| `patchStatusOf` | F1 | tout `c` ; `c`+`w` ; tout `-` ; `f` sans `c` ; mélange `p` |
| `patchMatches` | F1 | égalité ; préfixe de saison `15` ; `16.1` ne prend pas `16.10.1` ; `16.1*` le prend ; plusieurs jetons ; requête vide |
| `selectionAfter` | F1 | chaque action du tableau § 3.3.2 ; **pour chaque action qui peut décocher** (`toggle`, `extend` depuis une ancre décochée, `season`, `set` `on: false`, `invert`, `replace`, `clear`), une version prioritaire décochée quitte `priority` ; `extend` sans ancre ; `extend` avec ancre décochée ; Maj+↓ ×5 puis Maj+↑ ×3 rend leur état aux 3 lignes sorties (`extent.base`) ; une autre action remet `extent` à `null` ; plage à travers une saison pliée ; `priority` à 100 refusé ; `paste` remplace / ajoute sans doublon |
| `SelectionUndo` | F1 | empiler, dépiler, plafond de 20 ; une suite d'`extend` d'une même plage n'empile qu'une fois |
| `isHeld` | F1 | masque `f` et tous types ; masque partiel qui inclut / n'inclut pas les types demandés ; `null` = les quatre |
| `versionTokens` | F1 | lignes, virgules, TSV de LogsQL (le compte ignoré), JSON, doublons, `1.5.` en bord |
| `scopeSummary` | F1 | entier ; compte et bornes (`versionCount`, `newestListed`, `oldestListed`) ; une version ; plage d'avant les listes ; langue et type uniques (lus de l'ancien stockage par l'API) ; priorités ; `skipComplete` |
| `relativeTime` | F1 | passé, futur, < 1 s |
| `failuresPreset` | F1 | versions dont une cellule n'est pas `c` ; langues et types de la passe ; epoch, servie si la sienne est retirée ; `skipComplete` ; `units`, `failed`, `gaps` et `pages` comptés ; `null` sans rien à refaire |
| `retryLabel` | F1 | échecs seuls ; non rendues seules (« Finir ») ; les deux |
| `pollDelay` | F1 | 0 échec ; 1, 2, 3 ; plafond 60 s ; base 60 s plafonnée à 300 s |
| `freshnessOf` | F1 | `live`, `late`, `lost`, `hidden`, jamais lu |
| `stackedSegments` | F1 | parts qui somment à 1 ; segment minuscule relevé au minimum ; total nul |
| `stripGradient` | F1 | 21 arrêts, jetons par code, cellule `_` transparente |
| `cellStateOf` | F1 | chaque code des deux modes, `z` compris ; code inconnu → `out_of_scope` |
| `matrixRows` | F1 | regroupement par saison ; agrégat par colonne ; dépliage par défaut (≤ 40 / activité) ; tri par rang |
| `cellTooltip` | F1 | cellule connue en détail ; cellule seule ; mode couverture |
| `draftOf` | F2 | chaque `origin` du tableau § 3.1 ; `'all'` ; `'incomplete'` résolu sur un rapport |
| `requestOfDraft` | F2 | tous les anciens patchs → `versions: null` ; 21 langues → `null` ; 4 types → `null` ; epoch toujours explicite ; `skipComplete` faux pour une epoch nouvelle |
| `pickerRows` | F2 | filtres texte et statut ; saisons pliées sautées au clavier mais comptées dans une plage ; saison vide masquée |
| `pickerActionOf` | F2 | chaque ligne du tableau § 3.3.5 |
| `pasteResultOf` | F2 | connues, dernier patch, inconnues, au-delà de 100 |
| `epochChoices` | F2 | servie seule ; avec une epoch en attente ; numéro de la nouvelle au-dessus d'une retirée plus haute que la servie (impossible) et de toutes les autres |
| `launchEstimate` | F2 | sans `skip` ; avec `skip` conscient des types (une langue qui ne tient que les objets est ignorée pour une passe « objets », refaite pour une passe « tous types ») ; types partiels avec et sans `typeShares` ; rythme mesuré borné par le plafond ; plafond seul ; coût : seules les (v, l) sans page stockée ; epoch nouvelle vs servie |
| `pagesPerLocaleOf` | F2 | mesure propre ; médiane de saison (≥ 3 mesures) ; saison à 2 mesures → voisin le plus proche ; égalité → le plus ancien ; aucune mesure → 500 |
| `launchWarnings` | F2 | chaque code du tableau § 3.5 |
| `launchErrorsOf` | F2 | chaque code de champ → étape et clé ; code inconnu ; `409` actif / dormant ; `0` |
| `mergeProgress` | F3 | complète remplace ; incrémentale fusionne ; version nouvelle ; ordre par rang ; `scopeVersions` gardé d'une lecture complète à l'autre ; `shownShare` ne recule pas quand le total grandit, rattrape ensuite, repart de zéro pour une autre passe |
| `PaceSamples` | F3 | ajout ; même `at` ignoré ; remise à zéro (autre passe, baisse) ; rythme instantané sur 2 min ; < 20 s → `null` ; trou > 2 min coupe la courbe ; plafond 360 |
| `progressSegments` | F3 | estimation (`pending > 0`) ; exacte ; `pagesLeft` nul → unités |
| `etaOf` | F3 | moyen seul ; écart > 15 % → deux lignes ; entre 5 et 15 % → état précédent gardé ; < 5 % → une ligne ; rythme récent inconnu (< 5 min) ; en pause |
| `stallOf` | F3 | pas en cours ; en pause avec une unité `z` → aucun blocage ; démarrage < 90 s ; chaque cause dans l'ordre ; < 180 s ; pages en échec sans page écrite → bloquée ; reprise récente → rien |
| `writtenAtOf` | F3 | première réponse ; une page écrite déplace l'instant ; des pages en échec ne le déplacent pas |
| `failingUnitOf` | F3 | unité saine ; moins de 10 pages ; un cinquième en échec ; passe en pause |
| `serverNow` | F3 | décalage client / serveur |
| `activityEvents` | F3 | première lecture ; unité vue une fois ; version jointe ; total réévalué (> 1 %, avec et sans version jointe ; ≤ 1 % : rien) ; chaque changement d'état ; pause d'elle-même |
| `consequencesOf` | F3 | pause avec et sans unité en cours, avec et sans promotions ; reprise d'elle-même ; annulation avec couverture |
| `launchLockOf` | F4 | chargement ; dormant ; active ; libre |
| `quietPanel` | F4 | premier échec → bande ; échec avec valeur → pas de bande ; `session` → bande même avec valeur |
| `passTitleOf` | F4 | en cours ; en pause ; finie < 24 h ; annulée ; masquée ; aucune ; pourcentage qui ne recule pas |
| `bandStateOf` | F4 | dormant ; prête ; prête avec des `f` (nommés) et des pages en échec ; presque prête (manques, ≥ 90 %) ; prête l'emporte ; aucune |
| `coverageGroups`, `coverageSummaryOf` | F4 | regroupement, filtres, décomptes |
| `epochProjection` | F4 | epoch servie vide ; pleine |
| `switchSnippets` | F4 | variable de bascule ; étape de retrait en texte seul avant la bascule ; variables de retrait d'une epoch retirée (`WRITE_EPOCH` = epoch écrite sinon servie) ; sans `storeBucket` → pas de copie |
| `relaunchPreset` | F4 | `scopeVersions` de la lecture complète ; `versionCount: null` → `'all'` ; plage d'avant les listes convertie ; epoch retirée → servie |

### 7.3 Les clés de traduction

Toutes sous `admin.prerender.` dans `i18n/admin/fr.json` et `en.json` (l'admin s'affiche toujours
en français, `admin/shared/admin-i18n.ts`, mais les deux catalogues gardent les mêmes clés pour
`npm run test:i18n`). Messages ICU comme ailleurs (`{count, plural, one {…} other {…}}`,
`{x, select, a {…} other {…}}`). **Chaque sous-arbre a un propriétaire** (§ 8.3). Les clés
existantes `states.*` et `page_types.*` restent, complétées.

**Pluriels.** Partout où un nombre précède un nom, le message est au pluriel ICU
(`{versions, plural, one {# patch} other {# patchs}}`) : sans quoi on lit « 1 patchs ·
1 terminés », et `test:i18n` ne le détecte pas. Le paramètre d'un `plural` est passé **brut**
(un `number`) : `#` l'écrit à la française (`@messageformat/core`) ; ne jamais lui passer une
chaîne déjà formatée par `figure`. Les tables ci-dessous l'appliquent ; une clé ajoutée en cours
de lot suit la même règle, et chaque spécification de composant vérifie au moins un cas à 1.

**`states`** (F1) — ajouts : `pending` « à venir » | « to come » ; `failed` « en échec » |
« failed » ; `stopped` « arrêtée » | « stopped ».

**`common`** (F1)

| Clé | fr | en |
|---|---|---|
| `cells.pending` | à venir | to come |
| `cells.running` | en cours | running |
| `cells.complete` | terminée | completed |
| `cells.complete_with_failures` | terminée, avec des pages en échec | completed, with failed pages |
| `cells.failed` | en échec | failed |
| `cells.interrupted` | interrompue | interrupted |
| `cells.partial` | partielle | partial |
| `cells.absent` | absente | absent |
| `cells.out_of_scope` | hors périmètre | out of scope |
| `cells.stopped` | arrêtée | stopped |
| `status.complete` | complet | complete |
| `status.complete_with_failures` | complet, pages en échec | complete, failed pages |
| `status.partial` | partiel | partial |
| `status.absent` | absent | absent |
| `status.failed` | en échec | failed |
| `seasons.label` | {major}.x · {year} | {major}.x · {year} |
| `seasons.label_bare` | {major}.x | {major}.x |
| `seasons.patches` | {count, plural, one {# patch} other {# patchs}} | {count, plural, one {# patch} other {# patches}} |
| `strip` | {complete, plural, one {# langue} other {# langues}} complètes sur {total} | {complete} of {total, plural, one {# locale} other {# locales}} complete |
| `matrix.label` | Matrice des unités : {versions, plural, one {# patch} other {# patchs}} × {locales, plural, one {# langue} other {# langues}} | Unit matrix: {versions, plural, one {# patch} other {# patches}} × {locales, plural, one {# locale} other {# locales}} |
| `matrix.views` | Affichage de la matrice | Matrix display |
| `matrix.view_matrix` | Matrice | Matrix |
| `matrix.view_table` | Tableau | Table |
| `matrix.by_rank` | Ordre de passage | Order of the pass |
| `matrix.expand_all` | Tout déplier | Expand all |
| `matrix.collapse_all` | Tout replier | Collapse all |
| `matrix.season_counts` | {versions, plural, one {# patch} other {# patchs}} · {complete, plural, one {# terminé} other {# terminés}} · {running} en cours · {failed} en échec | {versions, plural, one {# patch} other {# patches}} · {complete} completed · {running} running · {failed} failed |
| `matrix.season_summary` | Saison {season} : {versions, plural, one {# patch} other {# patchs}}, {complete, plural, one {# terminé} other {# terminés}}, {running} en cours, {failed} en échec, {pending} à venir. | Season {season}: {versions, plural, one {# patch} other {# patches}}, {complete} completed, {running} running, {failed} failed, {pending} to come. |
| `matrix.version_button` | {version} : {complete} / {total, plural, one {# langue} other {# langues}} terminées · détail | {version}: {complete} / {total, plural, one {# locale} other {# locales}} completed · detail |
| `matrix.cell` | {version} · {locale} ({language}) — {state} | {version} · {locale} ({language}) — {state} |
| `matrix.cell_pages` | {done} / {total, plural, one {# page} other {# pages}} · {size} | {done} / {total, plural, one {# page} other {# pages}} · {size} |
| `matrix.cell_since` | depuis {duration} | for {duration} |
| `matrix.cell_rank` | rang {rank} dans la passe | rank {rank} in the pass |
| `matrix.cell_stopped` | arrêtée à {done} / {total} pages, reprendra là | stopped at {done} / {total} pages, resumes there |
| `matrix.cell_held` | tient : {types} | holds: {types} |
| `matrix.cell_held_none` | ne tient aucun type | holds no type |
| `matrix.columns.version` | Version | Version |
| `matrix.columns.state` | État | State |
| `matrix.columns.locales` | Langues terminées | Locales completed |
| `matrix.columns.running` | En cours | Running |
| `matrix.columns.failed` | En échec | Failed |
| `matrix.columns.pages` | Pages | Pages |
| `matrix.columns.volume` | Volume | Volume |
| `matrix.detail` | Détail | Detail |
| `live.live` | En direct · mis à jour {ago} | Live · updated {ago} |
| `live.late` | Mise à jour en retard : {ago} | Update late: {ago} |
| `live.lost` | Hors ligne depuis {since} ({reason}) · nouvel essai {next} | Offline for {since} ({reason}) · next try {next} |
| `live.hidden` | En pause tant que l'onglet est masqué | Paused while the tab is hidden |
| `live.retry` | Réessayer maintenant | Try again now |
| `time.ago` | il y a {duration} | {duration} ago |
| `time.in` | dans {duration} | in {duration} |
| `time.now` | à l'instant | just now |
| `scope.all` | tous les anciens patchs | every older patch |
| `scope.versions` | {count, plural, one {# patch} other {# patchs}} | {count, plural, one {# patch} other {# patches}} |
| `scope.listed` | {count, plural, one {# patch} other {# patchs}} ({newest} → {oldest}) | {count, plural, one {# patch} other {# patches}} ({newest} → {oldest}) |
| `scope.range` | {oldest} → {newest} | {oldest} → {newest} |
| `scope.from` | depuis {oldest} | from {oldest} |
| `scope.until` | jusqu'à {newest} | up to {newest} |
| `scope.locales_all` | 21 langues | 21 locales |
| `scope.locales` | {count, plural, one {# langue} other {# langues}} | {count, plural, one {# locale} other {# locales}} |
| `scope.types_all` | tous types | every type |
| `scope.priority` | {count, plural, one {# prioritaire} other {# prioritaires}} | {count, plural, one {# first} other {# first}} |
| `scope.skip` | manques seulement | gaps only |
| `units.title` | {version} dans la passe n°{id} | {version} in pass #{id} |
| `units.loading` | Chargement des unités… | Loading the units… |
| `units.error` | Unités indisponibles ({reason}). | Units unavailable ({reason}). |
| `units.empty` | La passe n'a aucune unité pour ce patch. | The pass has no unit for this patch. |
| `units.columns.locale` | Langue | Locale |
| `units.columns.state` | État | State |
| `units.columns.progress` | Avancement | Progress |
| `units.columns.pages` | Écrites · sautées · en échec | Written · skipped · failed |
| `units.columns.volume` | Volume | Volume |
| `units.columns.started` | Début (UTC) | Started (UTC) |
| `units.columns.ended` | Dernier changement (UTC) | Last change (UTC) |
| `units.columns.failure` | Raison | Reason |
| `copy.copy` | Copier | Copy |
| `copy.copied` | Copié | Copied |
| `copy.error` | Copiez à la main : | Copy it by hand: |
| `retry.units` | Relancer {units, plural, one {# unité} other {# unités}} (≈ {pages, plural, one {# page} other {# pages}}) | Relaunch {units, plural, one {# unit} other {# units}} (≈ {pages, plural, one {# page} other {# pages}}) |
| `retry.units_gaps` | Relancer {failed} en échec et {gaps, plural, one {# non rendue} other {# non rendues}} (≈ {pages, plural, one {# page} other {# pages}}) | Relaunch {failed} failed and {gaps} not rendered (≈ {pages, plural, one {# page} other {# pages}}) |
| `retry.later` | disponible quand la passe sera finie | available once the pass ends |
| `close` | Fermer | Close |
| `live.compact_late` | En retard · {ago} | Late · {ago} |
| `live.compact_lost` | Hors ligne · {ago} | Offline · {ago} |
| `retry.gaps` | Finir {gaps, plural, one {# unité non rendue} other {# unités non rendues}} (≈ {pages, plural, one {# page} other {# pages}}) | Finish {gaps, plural, one {# unit not rendered} other {# units not rendered}} (≈ {pages, plural, one {# page} other {# pages}}) |

**`overview`** (F4)

| Clé | fr | en |
|---|---|---|
| `cta.launch` | Lancer une passe | Launch a pass |
| `cta.refresh` | Actualiser | Refresh |
| `cta.locked_loading` | Chargement de l'état du précalcul… | Loading the state of the pre-rendering… |
| `cta.locked_active` | Une passe est en cours (n°{id}) : une seule à la fois. | A pass is running (#{id}): one at a time. |
| `cta.locked_dormant` | Précalcul non configuré sur cet hôte. | Pre-rendering not configured on this host. |
| `cta.follow` | Suivre | Follow |
| `bands.dormant` | Le précalcul n'est pas configuré sur cet hôte : posez LoDb__Prerender__PublicHost, LoDb__Prerender__SsrBaseUrl et les clés du stockage, puis redéployez. Aucune passe ne peut démarrer ; la tâche de fond dort. | Pre-rendering is not configured on this host: set LoDb__Prerender__PublicHost, LoDb__Prerender__SsrBaseUrl and the store's keys, then redeploy. No pass can start; the background task sleeps. |
| `bands.ready` | L'epoch {epoch} est prête : {versions, plural, one {# patch} other {# patchs}} × 21 langues, sans manque. Pour la servir : | Epoch {epoch} is ready: {versions, plural, one {# patch} other {# patches}} × 21 locales, no gap. To serve it: |
| `bands.ready_failed_pages` | {pages, plural, one {# page en échec y restera servie} other {# pages en échec y resteront servies}} par le SSR. | {pages, plural, one {# failed page stays} other {# failed pages stay}} served by the SSR. |
| `bands.ready_failed_units` | {units, plural, one {# unité en échec définitif passera} other {# unités en échec définitif passeront}} par le SSR. | {units, plural, one {# unit failed for good goes} other {# units failed for good go}} through the SSR. |
| `bands.ready_how` | Comment basculer ? | How to switch? |
| `bands.nearly` | L'epoch {epoch} couvre {share} des unités des anciens patchs : {missing, plural, one {# unité manque} other {# unités manquent}} avant de pouvoir la servir. | Epoch {epoch} covers {share} of the older patches' units: {missing, plural, one {# unit is} other {# units are}} missing before it can be served. |
| `bands.nearly_cta` | Compléter l'epoch {epoch} | Complete epoch {epoch} |
| `idle.title` | Aucune passe en cours | No pass running |
| `idle.lede` | Le stockage ne reçoit rien. Les patchs promus {promotions, select, on {ouvrent une passe d'eux-mêmes} other {n'ouvrent pas de passe d'eux-mêmes}}. | The store receives nothing. Promoted patches {promotions, select, on {open a pass by themselves} other {do not open a pass by themselves}}. |
| `idle.last` | Dernière passe : n°{id}, {state} le {date} UTC · {units, plural, one {# unité} other {# unités}} · {pages, plural, one {# page} other {# pages}} | Last pass: #{id}, {state} on {date} UTC · {units, plural, one {# unit} other {# units}} · {pages, plural, one {# page} other {# pages}} |
| `idle.detail` | Détail | Detail |
| `idle.never` | Aucune passe lancée jusqu'ici. | No pass launched yet. |
| `idle.gaps` | L'epoch servie couvre {share} des unités des anciens patchs : {versions, plural, one {# patch a des manques} other {# patchs ont des manques}}. | The served epoch covers {share} of the older patches' units: {versions, plural, one {# patch has gaps} other {# patches have gaps}}. |
| `idle.gaps_cta` | Les compléter | Complete them |
| `idle.whole` | L'epoch servie couvre tous les anciens patchs. | The served epoch covers every older patch. |
| `kpis.served` | Epoch servie | Served epoch |
| `kpis.served_sub` | LODB_ARCHIVE_SERVE_EPOCH, changée par un redéploiement | LODB_ARCHIVE_SERVE_EPOCH, changed by a redeploy |
| `kpis.served_ready` | l'epoch {epoch} est prête | epoch {epoch} is ready |
| `kpis.coverage` | Couverture servie | Served coverage |
| `kpis.coverage_sub` | des unités · {complete} / {total, plural, one {# patch} other {# patchs}} complets | of the units · {complete} / {total, plural, one {# patch} other {# patches}} complete |
| `kpis.coverage_none` | aucun ancien patch | no older patch |
| `kpis.stored` | Stocké | Stored |
| `kpis.stored_sub` | {pages, plural, one {# page} other {# pages}}, epochs retirées exclues | {pages, plural, one {# page} other {# pages}}, retired epochs left out |
| `kpis.cost` | Coût estimé / mois | Estimated cost / month |
| `kpis.cost_sub` | hors sortie ; détail sous « Stockage et coûts » | egress left out; detail under “Storage and costs” |
| `kpis.cost_sub_retired` | + {retired} pour les epochs retirées, jusqu'à leur suppression ; hors sortie | + {retired} for the retired epochs, until deleted; egress left out |
| `tabs.label` | Vues du précalcul | Pre-rendering views |
| `tabs.coverage` | Couverture | Coverage |
| `tabs.costs` | Stockage et coûts | Storage and costs |
| `tabs.history` | Historique | History |
| `doc_title.idle` | Précalcul du catalogue | Catalogue pre-rendering |
| `doc_title.running` | {share} · Précalcul du catalogue | {share} · Catalogue pre-rendering |
| `doc_title.paused` | ⏸ {share} · Précalcul du catalogue | ⏸ {share} · Catalogue pre-rendering |
| `doc_title.completed` | ✓ Passe n°{id} terminée · Précalcul du catalogue | ✓ Pass #{id} completed · Catalogue pre-rendering |
| `doc_title.cancelled` | ✕ Passe n°{id} annulée · Précalcul du catalogue | ✕ Pass #{id} cancelled · Catalogue pre-rendering |
| `tabs.costs_short` | Coûts | Costs |

**`live`** (F3) — le moniteur, ses dialogues, le récapitulatif ; `done.pause`, `done.resume`,
`done.cancel` existants restent à F3.

| Clé | fr | en |
|---|---|---|
| `title` | Passe n°{id} · epoch {epoch} | Pass #{id} · epoch {epoch} |
| `origin` | lancée le {date} UTC par {author} | launched on {date} UTC by {author} |
| `origin_auto` | ouverte le {date} UTC par une promotion ou la ligne de commande | opened on {date} UTC by a promotion or the command line |
| `paused_since` | en pause depuis {duration} | paused for {duration} |
| `progress_label` | {share} : {written, plural, one {# page} other {# pages}} écrites, {skipped} sautées, {failed} en échec, {left} restantes | {share}: {written, plural, one {# page} other {# pages}} written, {skipped} skipped, {failed} failed, {left} left |
| `pages_estimated` | {done} / ≈ {total, plural, one {# page} other {# pages}} | {done} / ≈ {total, plural, one {# page} other {# pages}} |
| `pages_exact` | {done} / {total, plural, one {# page} other {# pages}} | {done} / {total, plural, one {# page} other {# pages}} |
| `estimate_note` | Total estimé : les pages d'une unité ne sont listées qu'à son tour. | Estimated total: a unit's pages are listed only when its turn comes. |
| `first_unit` | En attente de la première unité | Waiting for the first unit |
| `segments.written` | écrites | written |
| `segments.skipped` | sautées | skipped |
| `segments.failed` | en échec | failed |
| `segments.left` | restantes | left |
| `units` | Unités : {completed} / {total} — {running} en cours · {pending} à venir · {failed} en échec | Units: {completed} / {total} — {running} running · {pending} to come · {failed} failed |
| `starting` | En attente de la tâche de fond : elle prend une passe à son prochain tour (30 s au plus). | Waiting for the background task: it takes a pass at its next tick (30 s at most). |
| `pace.label` | Rythme | Pace |
| `pace.value` | {pace, plural, one {# page} other {# pages}}/min | {pace, plural, one {# page} other {# pages}}/min |
| `pace.average` | moyenne {pace, plural, one {# page} other {# pages}}/min | average {pace, plural, one {# page} other {# pages}}/min |
| `pace.measuring` | mesure en cours… | measuring… |
| `pace.stopped` | à l'arrêt | stopped |
| `eta.label` | Fin estimée (UTC) | Estimated end (UTC) |
| `eta.average` | {in} (rythme moyen) | {in} (average pace) |
| `eta.now` | au rythme actuel : {in} | at the current pace: {in} |
| `eta.unknown` | en cours d'estimation… | being estimated… |
| `eta.paused` | suspendue pendant la pause | on hold while paused |
| `active.label` | Temps actif | Running time |
| `active.sub` | pauses exclues | pauses left out |
| `volume.label` | Écrit | Written |
| `volume.sub` | ≈ {size} par page | ≈ {size} a page |
| `now.title` | En ce moment | Right now |
| `now.unit` | {version} · {locale} ({language}) | {version} · {locale} ({language}) |
| `now.pages` | {done} / {total, plural, one {# page} other {# pages}} ({share}) | {done} / {total, plural, one {# page} other {# pages}} ({share}) |
| `now.since` | depuis {duration} · dernière page {ago} | for {duration} · last page {ago} |
| `now.detail` | Détail du patch | Patch detail |
| `now.next` | Ensuite : | Next: |
| `now.none` | Aucune unité en cours. | No unit running. |
| `now.resumes_with` | Reprendra par | Resumes with |
| `now.stopped` | {version} · {locale} ({language}), arrêtée à {done} / {total} pages ({share}) | {version} · {locale} ({language}), stopped at {done} / {total} pages ({share}) |
| `now.waiting_chip` | en attente | waiting |
| `now.wait` | {version} attend : {reason}, depuis {duration} ; ses unités échouent à {at} UTC s'il n'arrive pas. | {version} waits: {reason}, for {duration}; its units fail at {at} UTC if it does not come. |
| `now.reasons.catalog-pending` | son catalogue n'est pas encore stocké | its catalog is not stored yet |
| `now.reasons.not-pinned` | l'API ne le compte pas encore parmi les anciens patchs | the API does not count it among the older patches yet |
| `now.reasons.settling` | il vient de quitter le dernier rang et les caches le servent encore comme dernier patch | it just left the latest rank and the caches still serve it as the latest patch |
| `now.ssr_down` | Le SSR est injoignable depuis {duration} : la passe attend sans compter de page. | The SSR has been out of reach for {duration}: the pass waits without counting a page. |
| `now.remote` | Les signaux de la tâche de fond ne sont pas visibles depuis cette instance. | The background task's signals are not visible from this instance. |
| `now.worker_off` | La tâche de fond est coupée sur cette instance (LoDb:Workers:Enabled=false) : la passe n'avance pas. | The background task is off on this instance (LoDb:Workers:Enabled=false): the pass does not move. |
| `stall.title` | Aucune page écrite depuis {duration} | No page written for {duration} |
| `stall.ssr` | Le SSR est injoignable : la passe reprendra seule à son retour. | The SSR is out of reach: the pass resumes by itself once it is back. |
| `stall.worker_off` | La tâche de fond est coupée sur cette instance. | The background task is off on this instance. |
| `stall.waiting` | Toutes les unités restantes attendent un catalogue. | Every unit left waits for a catalog. |
| `stall.remote` | Aucune instance visible n'écrit la passe : une autre instance tient peut-être le verrou, ou l'API vient de redémarrer (la tâche reprend toutes les 30 s). | No visible instance writes the pass: another may hold the lock, or the API just restarted (the task retries every 30 s). |
| `stall.unknown` | Cause inconnue : voir le journal de l'API (prerender.*). | Unknown cause: see the API's log (prerender.*). |
| `paused_itself` | La passe s'est mise en pause d'elle-même : {reason} | The pass paused itself: {reason} |
| `paused_itself_hint` | Corrigez la configuration du stockage ou du SSR (clé, politique, hôte public), puis reprenez. | Fix the store's or the SSR's settings (key, policy, public host), then resume. |
| `feed.title` | Activité (UTC) | Activity (UTC) |
| `feed.done` | {version} · {locale} terminée · {pages, plural, one {# page} other {# pages}} · en {duration} | {version} · {locale} completed · {pages, plural, one {# page} other {# pages}} · in {duration} |
| `feed.done_failures` | {version} · {locale} terminée, {failed, plural, one {# page en échec} other {# pages en échec}} | {version} · {locale} completed, {failed, plural, one {# failed page} other {# failed pages}} |
| `feed.failed` | {version} · {locale} en échec | {version} · {locale} failed |
| `feed.joined` | {version} a rejoint la passe ({units, plural, one {# unité} other {# unités}}) | {version} joined the pass ({units, plural, one {# unit} other {# units}}) |
| `feed.total_up` | Total réévalué : + {pages, plural, one {# page} other {# pages}} | Total re-estimated: + {pages, plural, one {# page} other {# pages}} |
| `feed.total_up_joined` | Total réévalué : + {pages, plural, one {# page} other {# pages}} · {version} a rejoint la passe | Total re-estimated: + {pages, plural, one {# page} other {# pages}} · {version} joined the pass |
| `feed.paused` | Passe mise en pause | Pass paused |
| `feed.paused_itself` | La passe s'est mise en pause d'elle-même | The pass paused itself |
| `feed.resumed` | Passe reprise | Pass resumed |
| `feed.completed` | Passe terminée | Pass completed |
| `feed.cancelled` | Passe annulée | Pass cancelled |
| `feed.empty` | Rien de terminé depuis l'ouverture de la page. | Nothing ended since the page opened. |
| `feed.more` | Tout afficher ({count}) | Show all ({count}) |
| `feed.less` | Réduire | Show less |
| `failures.summary` | {count, plural, one {# unité en échec ou avec des pages en échec} other {# unités en échec ou avec des pages en échec}} | {count, plural, one {# unit failed or with failed pages} other {# units failed or with failed pages}} |
| `failures.show` | Voir les échecs | Show the failures |
| `failures.hide` | Masquer les échecs | Hide the failures |
| `failures.pages` | {failed, plural, one {# page} other {# pages}} en échec sur {total} | {failed} failed pages of {total} |
| `failures.more` | …et {count, plural, one {# autre} other {# autres}}, visibles dans la matrice et la vue tableau. | …and {count} more, in the matrix and the table view. |
| `failures.detail` | Voir le patch | See the patch |
| `actions.pause` | Mettre en pause | Pause |
| `actions.resume` | Reprendre | Resume |
| `actions.resume_itself` | Reprendre… | Resume… |
| `actions.cancel` | Annuler la passe… | Cancel the pass… |
| `confirm.pause_title` | Mettre la passe n°{id} en pause ? | Pause pass #{id}? |
| `confirm.pause_current` | {version} · {locale} s'arrête une fois les pages en cours écrites, à {done} / {total}. | {version} · {locale} stops once the pages under way are written, at {done} / {total}. |
| `confirm.pause_resume` | La passe reprendra exactement là, unité et page. | The pass will resume exactly there, unit and page. |
| `confirm.pause_promotions` | Pendant la pause, un patch promu rejoint la passe mais attend la reprise. | While paused, a promoted patch joins the pass but waits for it to resume. |
| `confirm.pause_slot` | Elle garde la seule place active : aucune autre passe ne peut être lancée avant sa reprise ou son annulation. | It keeps the only active slot: no other pass can start before it resumes or is cancelled. |
| `confirm.pause_confirm` | Mettre en pause | Pause |
| `confirm.pause_keep` | Laisser tourner | Keep running |
| `confirm.resume_title` | Reprendre malgré l'erreur ? | Resume despite the error? |
| `confirm.resume_reason` | La passe s'est arrêtée d'elle-même : {reason} | The pass stopped by itself: {reason} |
| `confirm.resume_warning` | Sans correction, elle se remettra en pause dès la première page. | Without a fix, it pauses again at its first page. |
| `confirm.resume_confirm` | Reprendre quand même | Resume anyway |
| `confirm.resume_keep` | Laisser en pause | Keep paused |
| `confirm.cancel_title` | Annuler définitivement la passe n°{id} ? | Cancel pass #{id} for good? |
| `confirm.cancel_left` | {units, plural, one {# unité à venir ne sera pas rendue} other {# unités à venir ne seront pas rendues}}. | {units, plural, one {# unit to come will not be rendered} other {# units to come will not be rendered}}. |
| `confirm.cancel_kept` | Ce qui est écrit reste dans le stockage : {pages, plural, one {# page} other {# pages}}, {size}. | What is written stays in the store: {pages, plural, one {# page} other {# pages}}, {size}. |
| `confirm.cancel_partial` | L'epoch {epoch} restera partielle ({share} couverte) jusqu'à une passe qui la complète. | Epoch {epoch} stays partial ({share} covered) until a pass completes it. |
| `confirm.cancel_later` | Pour la finir plus tard : « Finir cette passe » dans son récapitulatif, ou « Relancer » dans son détail (Historique). | To finish it later: « Finish this pass » in its recap, or « Relaunch » in its detail (History). |
| `confirm.cancel_confirm` | Annuler la passe n°{id} | Cancel pass #{id} |
| `confirm.cancel_keep` | Garder la passe | Keep the pass |
| `recap.completed` | Passe n°{id} terminée | Pass #{id} completed |
| `recap.cancelled` | Passe n°{id} annulée | Pass #{id} cancelled |
| `recap.summary` | {units, plural, one {# unité} other {# unités}} · {written, plural, one {# page} other {# pages}} écrites · {failed} en échec · {duration} de marche · {size} | {units, plural, one {# unit} other {# units}} · {written, plural, one {# page} other {# pages}} written · {failed} failed · {duration} running · {size} |
| `recap.complete` | Compléter l'epoch {epoch} | Complete epoch {epoch} |
| `recap.detail` | Voir le détail | See the detail |
| `recap.dismiss` | Masquer | Dismiss |
| `failing` | {version} · {locale} échoue : {failed, plural, one {# page en échec} other {# pages en échec}} sur {processed} ({share}). Dernière raison : | {version} · {locale} is failing: {failed, plural, one {# page failed} other {# pages failed}} out of {processed} ({share}). Last reason: |
| `matrix_title` | Matrice | Matrix |
| `folds.activity` | Activité ({count}) | Activity ({count}) |
| `folds.failures` | Échecs ({count}) | Failures ({count}) |
| `folds.matrix` | Matrice ({count, plural, one {# patch} other {# patchs}}) | Matrix ({count, plural, one {# patch} other {# patches}}) |
| `units_paused` | Unités : {completed} / {total} — {running} en pause · {pending} à venir · {failed} en échec | Units: {completed} / {total} — {running} paused · {pending} to come · {failed} failed |
| `recap.finish` | Finir cette passe ({units, plural, one {# unité} other {# unités}}, ≈ {pages, plural, one {# page} other {# pages}}) | Finish this pass ({units, plural, one {# unit} other {# units}}, ≈ {pages, plural, one {# page} other {# pages}}) |

**`launcher`** (F2)

| Clé | fr | en |
|---|---|---|
| `eyebrow` | Précalcul | Pre-rendering |
| `title` | Lancer une passe | Launch a pass |
| `close` | Fermer | Close |
| `from_pass` | Prérempli depuis la passe n°{id}. | Prefilled from pass #{id}. |
| `steps.label` | Étapes | Steps |
| `steps.patches` | Patchs | Patches |
| `steps.scope` | Périmètre | Perimeter |
| `steps.summary` | Récapitulatif | Summary |
| `steps.position` | Étape {step} sur 3 · {name} | Step {step} of 3 · {name} |
| `nav.next_scope` | Suivant : périmètre | Next: perimeter |
| `nav.next_summary` | Suivant : récapitulatif | Next: summary |
| `nav.back` | Retour | Back |
| `nav.cancel` | Annuler | Cancel |
| `nav.launch` | Lancer la passe ({units, plural, one {# unité} other {# unités}}) | Launch the pass ({units, plural, one {# unit} other {# units}}) |
| `nav.launching` | Lancement… | Launching… |
| `footer` | {versions, plural, =0 {Aucun patch} one {# patch} other {# patchs}} · {locales, plural, one {# langue} other {# langues}} · {types} types → ≈ {units, plural, one {# unité} other {# unités}} · ≈ {pages, plural, one {# page} other {# pages}} · ≈ {duration} | {versions, plural, =0 {No patch} one {# patch} other {# patches}} · {locales, plural, one {# locale} other {# locales}} · {types} types → ≈ {units, plural, one {# unit} other {# units}} · ≈ {pages, plural, one {# page} other {# pages}} · ≈ {duration} |
| `footer_loading` | Chargement des anciens patchs… | Loading the older patches… |
| `patches.title` | Anciens patchs | Older patches |
| `patches.target` | Couverture lue dans l'epoch {epoch} ({kind, select, served {servie} standby {en attente} other {nouvelle}}) | Coverage read in epoch {epoch} ({kind, select, served {served} standby {standby} other {new}}) |
| `patches.change_epoch` | changer d'epoch | change epoch |
| `patches.latest` | {version}, le dernier patch, n'est jamais précalculé : le SSR le sert. | {version}, the latest patch, is never pre-rendered: the SSR serves it. |
| `patches.error` | Anciens patchs indisponibles ({reason}). | Older patches unavailable ({reason}). |
| `patches.retry` | Réessayer | Try again |
| `patches.none` | Aucun ancien patch : rien à précalculer avant la première promotion. | No older patch: nothing to pre-render before the first promotion. |
| `patches.filter` | Filtrer les versions | Filter the versions |
| `patches.filter_placeholder` | 16.1, 15 … | 16.1, 15 … |
| `patches.filter_clear` | Effacer le filtre | Clear the filter |
| `patches.no_match` | Aucun patch ne correspond au filtre. | No patch matches the filter. |
| `patches.status` | Statut | Status |
| `patches.status_all` | Tous | All |
| `patches.status_complete` | Complets | Complete |
| `patches.status_partial` | Partiels | Partial |
| `patches.status_absent` | Absents | Absent |
| `patches.status_failed` | En échec | Failed |
| `patches.range_from` | De | From |
| `patches.range_to` | à | to |
| `patches.range_apply` | Sélectionner la plage | Select the range |
| `patches.tools` | Outils | Tools |
| `patches.all` | Tout | All |
| `patches.none_selected` | Aucun | None |
| `patches.invert` | Inverser | Invert |
| `patches.uncovered` | Non couverts | Not covered |
| `patches.partials` | Partiels | Partial |
| `patches.undo` | Annuler la dernière sélection | Undo the last selection |
| `patches.count` | {selected, plural, =0 {Aucun patch sélectionné} one {# patch sélectionné} other {# patchs sélectionnés}} | {selected, plural, =0 {No patch selected} one {# patch selected} other {# patches selected}} |
| `patches.count_hidden` | dont {hidden, plural, one {# masqué} other {# masqués}} par le filtre | {hidden} hidden by the filter |
| `patches.count_priority` | {count, plural, one {# prioritaire} other {# prioritaires}} | {count, plural, one {# first} other {# first}} |
| `patches.grid` | Anciens patchs, par saison | Older patches, by season |
| `patches.season_row` | {season}, {count, plural, one {# patch} other {# patchs}}, {selected, plural, one {# sélectionné} other {# sélectionnés}} | {season}, {count, plural, one {# patch} other {# patches}}, {selected} selected |
| `patches.season_select` | Sélectionner toute la saison {season} | Select the whole season {season} |
| `patches.columns.version` | Version | Version |
| `patches.columns.coverage` | Couverture | Coverage |
| `patches.columns.pages` | Pages | Pages |
| `patches.columns.status` | Statut | Status |
| `patches.columns.priority` | Priorité | Priority |
| `patches.priority_on` | Prioritaire n°{rank} : retirer la priorité | First #{rank}: remove the priority |
| `patches.priority_off` | Rendre prioritaire | Render first |
| `patches.keys` | Clavier : ↑ ↓ parcourir · Espace cocher · Maj+↑ ↓ étendre · P prioriser · / filtrer | Keyboard: ↑ ↓ move · Space tick · Shift+↑ ↓ extend · P put first · / filter |
| `patches.announce` | {count, plural, one {# patch} other {# patchs}} sélectionnés | {count, plural, one {# patch} other {# patches}} selected |
| `paste.toggle` | Coller une liste de versions (journal de l'edge) | Paste a list of versions (edge log) |
| `paste.label` | Versions, une par ligne ou séparées par des virgules | Versions, one a line or comma-separated |
| `paste.hint` | Le résultat de LogsQL « stats by (version) » convient tel quel : chaque version reconnue est cochée et rendue prioritaire, dans l'ordre collé. | LogsQL's “stats by (version)” output fits as is: each version recognised is ticked and rendered first, in the pasted order. |
| `paste.replace` | Remplacer les priorités | Replace the priorities |
| `paste.append` | Ajouter à la suite | Append |
| `paste.apply` | Appliquer | Apply |
| `paste.known` | {count, plural, one {# version reconnue} other {# versions reconnues}} | {count, plural, one {# version recognised} other {# versions recognised}} |
| `paste.ignored` | Ignorées : {list} | Ignored: {list} |
| `paste.latest` | {version} (dernier patch, servi par le SSR) | {version} (latest patch, served by the SSR) |
| `paste.unknown` | {version} (pas un ancien patch) | {version} (not an older patch) |
| `paste.capped` | Au-delà de 100, les versions sont cochées sans priorité. | Beyond 100, the versions are ticked without priority. |
| `priority.title` | Prioritaires, dans l'ordre | First, in this order |
| `priority.empty` | Aucune : les patchs passent du plus récent au plus ancien. | None: patches go from the newest to the oldest. |
| `priority.up` | Monter {version} | Move {version} up |
| `priority.down` | Descendre {version} | Move {version} down |
| `priority.remove` | Retirer {version} des prioritaires | Remove {version} from the first |
| `priority.max` | 100 prioritaires au plus. | 100 first at most. |
| `locales.title` | Langues | Locales |
| `locales.count` | {selected} / {total} | {selected} / {total} |
| `locales.all` | Toutes | All |
| `locales.none` | Aucune | None |
| `locales.complete` | {complete}/{versions, plural, one {# complet} other {# complets}} | {complete}/{versions} complete |
| `locales.required` | Choisissez au moins une langue. | Choose at least one locale. |
| `types.title` | Types de pages | Page types |
| `types.share` | ≈ {share} des pages | ≈ {share} of the pages |
| `types.required` | Choisissez au moins un type. | Choose at least one type. |
| `types.defer` | Une passe limitée à certains types n'accueille pas les patchs promus pendant qu'elle tourne : ils attendent sa fin. | A pass kept to some types takes no promoted patch while it runs: they wait for its end. |
| `epoch.title` | Epoch écrite | Epoch written |
| `epoch.served_title` | Compléter l'epoch servie · {epoch} | Fill the served epoch · {epoch} |
| `epoch.served_body` | Chaque page est servie dès qu'elle est écrite. Une page déjà stockée est remplacée en place. | Each page is served as soon as it is written. A page already stored is replaced in place. |
| `epoch.standby_title` | Continuer l'epoch {epoch} · en attente | Continue epoch {epoch} · standby |
| `epoch.standby_body` | Couverte à {share}. Rien n'en est servi avant la bascule. | {share} covered. Nothing of it is served before the switch. |
| `epoch.new_title` | Écrire une nouvelle epoch · {epoch} | Write a new epoch · {epoch} |
| `epoch.new_body` | Rien n'est servi avant la bascule : une fois l'epoch complète, posez LODB_ARCHIVE_SERVE_EPOCH={epoch} et redéployez. Pour un correctif du front (CSP, chrome, sécurité) : l'epoch servie garde l'ancien build tant qu'elle est servie. | Nothing is served before the switch: once the epoch is complete, set LODB_ARCHIVE_SERVE_EPOCH={epoch} and redeploy. For a front fix (CSP, chrome, security): the served epoch keeps the old build while it is served. |
| `epoch.new_cost` | Complète, elle coûtera ≈ {cost} / mois en plus de l'epoch {served}, jusqu'au retrait de celle-ci. | Once complete, it costs ≈ {cost} / month on top of epoch {served}, until that one is retired. |
| `skip.label` | Ignorer ce qui est déjà complet dans l'epoch {epoch} | Leave out what epoch {epoch} already holds complete |
| `skip.count` | {units, plural, =0 {rien à ignorer} one {# unité ignorée} other {# unités ignorées}} | {units, plural, =0 {nothing to leave out} one {# unit left out} other {# units left out}} |
| `skip.hint` | Une langue est ignorée si elle tient déjà, sans page en échec, chacun des types choisis. | A locale is left out when it already holds each type chosen, with no failed page. |
| `summary.title` | Récapitulatif | Summary |
| `summary.scope` | Epoch {epoch} ({kind, select, served {servie} standby {en attente} other {nouvelle}}) · {perimeter} | Epoch {epoch} ({kind, select, served {served} standby {standby} other {new}}) · {perimeter} |
| `summary.units` | Unités | Units |
| `summary.units_sub` | {versions, plural, one {# patch} other {# patchs}} × {locales, plural, one {# langue} other {# langues}}{skipped, plural, =0 {} one {, # déjà complète, ignorée} other {, # déjà complètes, ignorées}} | {versions, plural, one {# patch} other {# patches}} × {locales, plural, one {# locale} other {# locales}}{skipped, plural, =0 {} other {, # already complete, left out}} |
| `summary.pages` | Pages | Pages |
| `summary.pages_sub` | ≈ {perUnit} par unité | ≈ {perUnit} a unit |
| `summary.volume` | Volume | Volume |
| `summary.volume_sub` | ≈ {perPage} par page | ≈ {perPage} a page |
| `summary.duration` | Durée | Duration |
| `summary.duration_measured` | au dernier rythme mesuré ({pace, plural, one {# page} other {# pages}}/min) | at the last pace measured ({pace, plural, one {# page} other {# pages}}/min) |
| `summary.duration_ceiling` | au plafond configuré ({pace} rendus/min) : aucune passe de plus de 10 min pour mesurer un rythme | at the configured ceiling ({pace} renders/min): no pass over 10 min to measure a pace |
| `summary.duration_capped` | au plafond configuré ({pace} rendus/min), plus bas que le dernier rythme mesuré | at the configured ceiling ({pace} renders/min), below the last pace measured |
| `summary.cost` | Coût ajouté | Added cost |
| `summary.cost_value` | + {cost} | + {cost} |
| `summary.cost_sub` | par mois, au prix One Zone ({price} / Go / mois) | a month, at the One Zone price ({price} / GB / month) |
| `summary.order` | Ordre : {first} d'abord, puis du plus récent au plus ancien. | Order: {first} first, then newest to oldest. |
| `summary.order_default` | Ordre : du plus récent au plus ancien. | Order: newest to oldest. |
| `summary.quality_measured` | Pages par unité mesurées sur des passes précédentes. | Pages per unit measured on earlier passes. |
| `summary.quality_mixed` | Pages par unité mesurées pour {known, plural, one {# patch} other {# patchs}} sur {total} ; {guessed, plural, one {# extrapolé} other {# extrapolés}} de leur saison ou du patch voisin. | Pages per unit measured for {known} of {total, plural, one {# patch} other {# patches}}; {guessed} extrapolated from their season or the nearest patch. |
| `summary.how_guess` | {version} : {source, select, season {médiane de la saison {season}} other {comme {neighbour}}}, {pages, plural, one {# page} other {# pages}} | {version}: {source, select, season {median of season {season}} other {like {neighbour}}}, {pages, plural, one {# page} other {# pages}} |
| `summary.quality_default` | Aucune mesure : estimation par défaut (≈ {pages, plural, one {# page} other {# pages}} par unité). | No measure: default estimate (≈ {pages, plural, one {# page} other {# pages}} a unit). |
| `summary.how` | Comment c'est estimé | How it is estimated |
| `summary.how_units` | {units, plural, one {# unité} other {# unités}} = {versions, plural, one {# patch} other {# patchs}} × {locales, plural, one {# langue} other {# langues}} − {skipped} déjà complètes | {units, plural, one {# unit} other {# units}} = {versions, plural, one {# patch} other {# patches}} × {locales, plural, one {# locale} other {# locales}} − {skipped} already complete |
| `summary.how_pages` | ≈ {pages, plural, one {# page} other {# pages}} = {units, plural, one {# unité} other {# unités}} × ≈ {perUnit, plural, one {# page} other {# pages}} × {share} des types | ≈ {pages, plural, one {# page} other {# pages}} = {units, plural, one {# unit} other {# units}} × ≈ {perUnit, plural, one {# page} other {# pages}} × {share} of the types |
| `summary.how_volume` | ≈ {size} = {pages, plural, one {# page} other {# pages}} × ≈ {perPage} | ≈ {size} = {pages, plural, one {# page} other {# pages}} × ≈ {perPage} |
| `summary.how_duration` | ≈ {duration} = {pages, plural, one {# page} other {# pages}} ÷ {pace, plural, one {# page} other {# pages}}/min | ≈ {duration} = {pages, plural, one {# page} other {# pages}} ÷ {pace, plural, one {# page} other {# pages}}/min |
| `summary.how_cost` | + {cost} / mois = {size} nouveaux ÷ 10⁹ × {price} | + {cost} / month = {size} new ÷ 10⁹ × {price} |
| `warnings.rerender` | {units, plural, one {# unité déjà complète sera refaite} other {# unités déjà complètes seront refaites}} : ne désactivez « Ignorer… » que pour remplacer des pages (nouveau build, correctif). | {units, plural, one {# unit already complete is rendered again} other {# units already complete are rendered again}}: turn “Leave out…” off only to replace pages (new build, fix). |
| `warnings.long` | Passe longue : ≈ {duration}. Les patchs promus pendant ce temps la rejoignent en tête. | Long pass: ≈ {duration}. Patches promoted meanwhile join it at its head. |
| `warnings.types` | Une passe limitée à certains types n'accueille pas les patchs promus pendant qu'elle tourne : ils attendent sa fin. | A pass kept to some types takes no promoted patch while it runs: they wait for its end. |
| `warnings.new_epoch` | Rien de cette passe n'est servi avant la bascule sur l'epoch {epoch}. | Nothing of this pass is served before the switch to epoch {epoch}. |
| `warnings.standby_exists` | L'epoch {epoch} contient déjà des pages non servies : continuez-la plutôt que d'en ouvrir une nouvelle. | Epoch {epoch} already holds pages not served: continue it rather than open a new one. |
| `warnings.default_estimate` | Aucune mesure : l'estimation est grossière. | No measure: the estimate is rough. |
| `warnings.priority_capped` | Au-delà de 100, les versions sont cochées sans priorité. | Beyond 100, the versions are ticked without priority. |
| `warnings.nothing` | Rien à rendre : tout ce qui est sélectionné est déjà complet dans l'epoch {epoch}. | Nothing to render: everything selected is already complete in epoch {epoch}. |
| `errors.versions.malformed` | Une version de la liste n'est pas une version de Data Dragon. | A version of the list is not a Data Dragon version. |
| `errors.versions.too-many` | 1 000 patchs au plus par passe. | 1,000 patches at most a pass. |
| `errors.versions.required` | Choisissez au moins un patch. | Choose at least one patch. |
| `errors.versions.none-pinned` | Aucun des patchs choisis n'est un ancien patch : la liste a pu changer depuis l'ouverture (promotion). Rechargez. | None of the patches chosen is an older patch: the list may have changed since (promotion). Reload. |
| `errors.locales.malformed` | Une langue de la liste n'existe pas sur le site. | A locale of the list does not exist on the site. |
| `errors.locales.required` | Choisissez au moins une langue. | Choose at least one locale. |
| `errors.pageTypes.malformed` | Un type de page de la liste n'existe pas. | A page type of the list does not exist. |
| `errors.pageTypes.required` | Choisissez au moins un type. | Choose at least one type. |
| `errors.priority.malformed` | Une version prioritaire n'est pas une version de Data Dragon. | A first version is not a Data Dragon version. |
| `errors.priority.too-many` | 100 prioritaires au plus. | 100 first at most. |
| `errors.epoch.out-of-range` | L'epoch doit valoir 1 ou plus. | The epoch must be 1 or more. |
| `errors.epoch.retired` | Cette epoch est plus ancienne que l'epoch servie : elle est retirée. | This epoch is older than the served one: it is retired. |
| `errors.scope.nothing-to-render` | Tout est déjà complet dans cette epoch. | Everything is already complete in this epoch. |
| `errors.unknown` | La saisie est refusée ({codes}). | The input is refused ({codes}). |
| `conflict.active` | Une passe vient d'être lancée (par un autre administrateur, une promotion ou la ligne de commande) : une seule à la fois. | A pass was just launched (by another administrator, a promotion or the command line): one at a time. |
| `conflict.follow` | Fermer et suivre la passe | Close and follow the pass |
| `conflict.dormant` | Le précalcul n'est pas configuré sur cet hôte. | Pre-rendering is not configured on this host. |
| `network` | L'API n'a pas répondu : la passe a pu être créée. | The API did not answer: the pass may have been created. |
| `network_check` | Vérifier | Check |
| `session` | Session expirée : reconnectez-vous, puis relancez. | Session expired: sign in again, then relaunch. |
| `done` | Passe n°{id} lancée sur l'epoch {epoch} : {units, plural, one {# unité} other {# unités}}{skipped, plural, =0 {} one {, # déjà complète ignorée} other {, # déjà complètes ignorées}}. | Pass #{id} launched on epoch {epoch}: {units, plural, one {# unit} other {# units}}{skipped, plural, =0 {} one {, # already complete left out} other {, # already complete left out}}. |
| `done_ignored` | {count, plural, one {# version ignorée, absente} other {# versions ignorées, absentes}} des anciens patchs : {versions}. | {count, plural, one {# version left out, not} other {# versions left out, not}} among the older patches: {versions}. |
| `season_count` | {count, plural, one {# patch} other {# patchs}} · {selected, plural, =0 {aucun sélectionné} one {# sélectionné} other {# sélectionnés}} | {count, plural, one {# patch} other {# patches}} · {selected, plural, =0 {none selected} other {# selected}} |
| `patches.columns.select` | Cocher | Tick |
| `patches.mouse` | Souris : clic pour cocher · Maj+clic pour cocher une plage · ☆ pour prioriser | Mouse: click to tick · Shift+click to tick a range · ☆ to put first |
| `patches.range_hint` | Maj+clic sur un autre patch pour cocher toute la plage depuis {version}. | Shift+click another patch to tick the whole range from {version}. |
| `footer_empty` | Cochez au moins un patch pour continuer. | Tick a patch at least to go on. |
| `patches.show` | Afficher : | Show: |
| `patches.tick` | Cocher : | Tick: |
| `patches.undo_short` | Défaire | Undo |

**`coverage`** (F4)

| Clé | fr | en |
|---|---|---|
| `epoch_label` | Epoch lue | Epoch read |
| `epoch_option` | {state} · {epoch} ({share}) | {state} · {epoch} ({share}) |
| `summary` | {complete} / {total, plural, one {# patch} other {# patchs}} complets · {partial, plural, one {# partiel} other {# partiels}} · {absent, plural, one {# absent} other {# absents}} · {failed} en échec — {share} des unités | {complete} / {total, plural, one {# patch} other {# patches}} complete · {partial} partial · {absent} absent · {failed} failed — {share} of the units |
| `latest` | Le dernier patch ({version}) n'est jamais précalculé. | The latest patch ({version}) is never pre-rendered. |
| `filter` | Filtrer les versions | Filter the versions |
| `status_all` · `status_complete` · `status_partial` · `status_absent` · `status_failed` | Tous · Complets · Partiels · Absents · En échec | All · Complete · Partial · Absent · Failed |
| `view_list` | Liste | List |
| `view_matrix` | Matrice | Matrix |
| `columns.version` · `columns.locales` · `columns.pages` · `columns.volume` · `columns.failed` · `columns.last` | Version · Langues · Pages · Volume · Échecs · Dernière écriture | Version · Locales · Pages · Volume · Failures · Last written |
| `season_select` | Sélectionner la saison {season} | Select season {season} |
| `row_select` | Sélectionner {version} | Select {version} |
| `selection` | {count, plural, one {# patch sélectionné} other {# patchs sélectionnés}} | {count, plural, one {# patch selected} other {# patches selected}} |
| `launch_selection` | Pré-rendre la sélection | Pre-render the selection |
| `clear_selection` | Tout désélectionner | Clear the selection |
| `gaps` | Compléter les manques ({count, plural, one {# patch} other {# patchs}}) | Complete the gaps ({count, plural, one {# patch} other {# patches}}) |
| `no_gaps` | Rien ne manque dans cette epoch. | Nothing is missing in this epoch. |
| `error` | Couverture indisponible ({reason}). | Coverage unavailable ({reason}). |
| `retry` | Réessayer | Try again |
| `empty` | Aucun ancien patch. | No older patch. |
| `season_counts` | {versions, plural, one {# patch} other {# patchs}} · {complete, plural, one {# complet} other {# complets}}{missing, plural, =0 {} one { · # à compléter} other { · # à compléter}} | {versions, plural, one {# patch} other {# patches}} · {complete} complete{missing, plural, =0 {} other { · # to complete}} |

**`costs`** (F4) — reprend mot pour mot les textes de `storage.*` sous de nouvelles clés :
`title`, `chip`, `epoch`, `builds`, `builds_sub`, `total`, `total_sub`, `empty`, `prices`,
`egress`, `counters`, `retired`, `columns.{epoch,state,pages,volume,cost}` ; et ajoute :

| Clé | fr | en |
|---|---|---|
| `columns.coverage` | Couverture | Coverage |
| `columns.actions` | Actions | Actions |
| `switch` | Préparer la bascule | Prepare the switch |
| `complete` | Compléter | Complete |
| `retire` | Retirer… | Retire… |
| `projection_title` | Si une nouvelle epoch était écrite | If a new epoch were written |
| `projection` | Elle pèserait ≈ {size} comme l'epoch servie : + {cost} / mois tant que les deux coexistent (jusqu'au retrait de l'ancienne, 9 jours au moins après la bascule). | It would weigh ≈ {size} like the served epoch: + {cost} / month while both coexist (until the old one is retired, 9 days at least after the switch). |
| `bill_note` | La facture compte aussi la sortie et les requêtes, que l'estimation laisse de côté. | The bill also counts the egress and the requests, which the estimate leaves out. |

**`passes`** (F4)

| Clé | fr | en |
|---|---|---|
| `columns.pass` · `epoch` · `state` · `scope` · `progress` · `volume` · `active` · `author` · `started` · `finished` | Passe · Epoch · État · Périmètre · Progression · Volume · Durée active · Par · Lancée (UTC) · Fin (UTC) | Pass · Epoch · State · Perimeter · Progress · Volume · Running time · By · Launched (UTC) · Ended (UTC) |
| `automatic` | promotion ou CLI | promotion or CLI |
| `empty` | Aucune passe lancée. | No pass launched. |
| `pages` | {done} / {total} | {done} / {total} |
| `follow` | Suivre | Follow |
| `detail.title` | Passe n°{id} · epoch {epoch} | Pass #{id} · epoch {epoch} |
| `detail.ended` | finie le {date} UTC | ended on {date} UTC |
| `detail.scope` | Périmètre | Perimeter |
| `detail.show_versions` | Voir la liste | Show the list |
| `detail.hide_versions` | Masquer la liste | Hide the list |
| `detail.legacy_range` | Plage d'avant les listes : {oldest} → {newest}, convertie en {count, plural, one {# patch} other {# patchs}} à la relance. | Range from before the lists: {oldest} → {newest}, turned into {count, plural, one {# patch} other {# patches}} on relaunch. |
| `detail.outcome` | Résultat | Outcome |
| `detail.units` | {total, plural, one {# unité} other {# unités}} : {completed} terminées · {failed} en échec · {pending} non rendues | {total, plural, one {# unit} other {# units}}: {completed} completed · {failed} failed · {pending} not rendered |
| `detail.pages` | {written, plural, one {# page} other {# pages}} écrites · {skipped} sautées · {failed} en échec | {written, plural, one {# page} other {# pages}} written · {skipped} skipped · {failed} failed |
| `detail.pace` | {pace, plural, one {# page} other {# pages}}/min en moyenne | {pace, plural, one {# page} other {# pages}}/min on average |
| `detail.matrix` | Matrice | Matrix |
| `detail.failures` | Échecs | Failures |
| `detail.relaunch` | Relancer ce périmètre | Relaunch this perimeter |
| `detail.follow` | Suivre en direct | Follow live |
| `detail.locked` | Une passe est en cours : une seule à la fois. | A pass is running: one at a time. |
| `detail.loading` | Chargement de la passe… | Loading the pass… |
| `detail.error` | Passe indisponible ({reason}). | Pass unavailable ({reason}). |
| `pages_estimated` | {done} / ≈ {total} | {done} / ≈ {total} |
| `units` | {done} / {total, plural, one {# unité} other {# unités}} | {done} / {total, plural, one {# unit} other {# units}} |

**`switch`** (F4)

| Clé | fr | en |
|---|---|---|
| `title` | Basculer sur l'epoch {epoch} | Switch to epoch {epoch} |
| `ready` | Prête : {versions, plural, one {# patch} other {# patchs}} × 21 langues, sans manque. | Ready: {versions, plural, one {# patch} other {# patches}} × 21 locales, no gap. |
| `ready_ssr` | {units, plural, =0 {} one {# unité en échec définitif} other {# unités en échec définitif}}{both, select, yes { et } other {}}{pages, plural, =0 {} one {# page en échec} other {# pages en échec}} passeront par le SSR. | {units, plural, =0 {} one {# unit failed for good} other {# units failed for good}}{both, select, yes { and } other {}}{pages, plural, =0 {} one {# failed page} other {# failed pages}} will go through the SSR. |
| `partial` | Couverte à {share} : {missing, plural, one {# unité passera} other {# unités passeront}} par le SSR après la bascule. | {share} covered: {missing, plural, one {# unit goes} other {# units go}} through the SSR after the switch. |
| `writing` | Une passe écrit encore cette epoch : attendez sa fin. | A pass still writes this epoch: wait for it to end. |
| `no_button` | nginx ne change pas d'epoch à chaud : aucun bouton ne la bascule d'ici. | nginx does not switch epochs on the fly: no button switches it from here. |
| `step_variable` | Dans le secret ENV_PROD (ou ENV_STAGING), posez : | In the ENV_PROD (or ENV_STAGING) secret, set: |
| `step_deploy` | Redéployez (en prod : fusion test → main). Le panneau affichera alors l'epoch servie {epoch}. | Redeploy (in production: merge test → main). The panel then shows served epoch {epoch}. |
| `step_retire` | Ensuite seulement, l'epoch {served} sera retirée : sa ligne de « Stockage et coûts » proposera « Retirer… ». Ne posez aucune règle de retrait avant : l'epoch {served} est encore celle que nginx sert. | Only then is epoch {served} retired: its row under “Storage and costs” offers “Retire…”. Set no retirement rule before: epoch {served} is still the one nginx serves. |
| `retire_title` | Retirer l'epoch {epoch} | Retire epoch {epoch} |
| `retire_body` | L'epoch {epoch} n'est plus servie (l'epoch servie est {served}). Sa règle de cycle de vie la supprimera, 500 000 objets par jour au plus ; elle coûte jusque-là {cost} / mois. | Epoch {epoch} is no longer served (the served epoch is {served}). Its lifecycle rule deletes it, 500,000 objects a day at most; until then it costs {cost} / month. |
| `retire_vars` | Ajoutez ces variables à la commande de tools/prerender/README.md, § 4, avec les identifiants de la console qu'elle demande : | Add these variables to the command of tools/prerender/README.md, § 4, with the console credentials it asks for: |
| `retire_no_bucket` | Le nom du bucket n'est pas configuré sur cet hôte : remplacez <bucket>. | The bucket's name is not configured on this host: replace <bucket>. |
| `retire_staging` | Au staging : sans objet, la règle d'expiration du bucket efface toute page. | On staging: not needed, the bucket's expiry rule deletes every page. |
| `close` | Fermer | Close |

**Clés retirées par F4 à la fin** : `admin.prerender.dormant`, `ready`, `kpi.*`, `current.*`,
`launch.*` (dont `launch.versions` et `launch.versions_hint`, posées par le lot `api` (phase contrat) pour
l'ancien formulaire), `scope.*`, `storage.*`, `history.*`, `done.start` ; `admin.rules.current_pass`,
`admin.rules.launch`. Restent : `title`, `lede`, `states.*`, `page_types.*`, `done.pause`,
`done.resume`, `done.cancel`, et `admin.errors.prerender_*` (utilisés par `AdminCommand` sur les
`409` des actions). L'extrait de catalogue des tests (`admin/testing/i18n/provide-admin-
catalogues.ts`) est mis à jour par F4 dans le même commit.

### 7.4 Les tests

**Vitest** (`*.spec.ts` à côté du code ; les spécifications affichent les **clés**, comme
`prerender-panel.spec.ts` aujourd'hui, et se lisent sans catalogue). Outils existants :
`configureAdminTestBed`, `openPanel`, `reply`, `sent`, `press`, `notice`
(`admin/testing/`), fixtures du lot `api` (phase contrat). `PRERENDER_CADENCE` et `ADMIN_TIMEOUT` remplacés
par des valeurs courtes dans les tests (comme `PRERENDER_POLL` aujourd'hui).

| Spécification | Lot | Ce qu'elle prouve |
|---|---|---|
| fonctions pures du § 7.2 | tous | leurs cas |
| `live/poll/inject-live-poll.spec.ts` | F1 | une lecture à la fois ; cadence ; arrêt quand `every` rend `null` ; suspension onglet masqué (`visibilitychange` simulé) et relecture au retour ; `hiddenEvery` ralentit au lieu de suspendre ; recul sur `502` ; valeur gardée ; arrêt sur `401` |
| `live/badge/live-badge.spec.ts` | F1 | les quatre états, la forme compacte et « Réessayer » ; le texte avance chaque seconde (`injectNow`) |
| `bars/stacked-bar.spec.ts`, `bars/cell-strip.spec.ts` | F1 | largeurs, nom accessible |
| `matrix/grid/unit-matrix.spec.ts` | F1 | saisons et dépliage ; cellule `r` remplie et qui pulse, cellule `z` remplie sans halo ; clic → `opened` ; vue tableau |
| `matrix/table/matrix-table.spec.ts` | F1 | un `tbody` par saison, `th scope="rowgroup"`, bouton « Détail » |
| `matrix/units/version-units-dialog.spec.ts` | F1 | une lecture ; 21 lignes ; raison en mono |
| `dialogs/confirm-dialog.spec.ts` | F1 | focus sur le choix sûr ; Échap rend `false` |
| `launch/picker/patch-picker.spec.ts`, `picker-focus.spec.ts` | F2 | clic, Maj+clic, saison tri-état, P, flèches, Maj+flèches (plage qui grandit puis rétrécit), `/`, Échap dans le filtre, recherche à la frappe, annonce du compteur |
| `launch/dialog/launch-dialog.spec.ts`, `launch-dialog-store.spec.ts` | F2 | pas-à-pas ; validation qui bloque ; corps envoyé (`sent`) ; `201` ferme et rend la passe ; `400` ramène à l'étape et affiche les clés ; `409` bande ; délai → « Vérifier » (identifiant de passe plus grand que celui de l'ouverture → fermeture ; sinon réactivation) ; changement d'epoch recharge la couverture |
| `monitor/live-monitor.spec.ts` | F3 | rendu en cours / pause par un administrateur (« Reprendra par », cellule `z`, aucune bande de blocage) / pause d'elle-même ; lecture incrémentale avec `since` ; `502` puis retour (badge `lost` → `live`) ; total qui grandit : pourcentage tenu et ligne « Total réévalué » ; fin → sortie `ended` ; dialogue de pause appelle `pause` ; annulation confirme puis appelle `cancel` |
| `monitor/recap/pass-recap.spec.ts` | F3 | terminée / annulée ; libellé chiffré de la relance ; sorties ; masquage mémorisé |
| `prerender-panel.states.spec.ts` | F4 | chaque ligne du tableau § 2.4 (dont l'échec de fond sans bande et la session avec bande) ; CTA et sa raison ; bandes (prête avec des `f`) ; titre du document |
| `prerender-panel.presets.spec.ts` | F4 | onglets ; le préréglage passé à l'ouvreur par chaque entrée (ouvreur remplacé par un faux) |
| `coverage/coverage-tab.spec.ts`, `costs/costs-tab.spec.ts`, `costs/switch/switch-dialog.spec.ts`, `costs/switch/retire-dialog.spec.ts`, `passes/passes-tab.spec.ts`, `passes/pass-detail-dialog.spec.ts` | F4 | rendu, sélection, préréglages émis ; bascule sans commande de retrait ; retrait copiable seulement sur une epoch retirée ; liste du périmètre lue de `scopeVersions` |
| `admin/layout/layout.spec.ts` (étendu) | F4 | `documentTitle` titre le document sans changer le `<h1>` |

**xUnit** (`tests/LoDb.Api.Tests/`, Postgres réel par Testcontainers, `FakeCatalog`, worker
coupé ; `tests/LoDb.Infrastructure.Tests/` pour la migration) :

| Test | Lot | Ce qu'il prouve |
|---|---|---|
| `Prerender/PassOrderTests` (étendu) | `api` | listes explicites ; priorités ; `skipComplete` conscient des types (une langue qui tient les objets est ignorée par une passe « objets », refaite par une passe « tous types ») ; rangs contigus ; `pages_expected` posé d'après une passe antérieure au même jeu de types, nul sinon ; ancienne forme stockée lue (plage, `pageType`, `locale`), nouvelle écrite ; listes pleines normalisées en `null` |
| `Prerender/CoverageFoldTests` (nouveau) | `api` | codes `c w p f` et absence ; masque `Held` par type ; une passe propre limitée aux objets n'efface pas l'échec laissé sur les champions ; unité plus récente (par `EndedAt`, puis `UpdatedAt`) qui l'emporte ; résumé : `GapUnits`, `FailedUnits`, `FailedPages` |
| `Admin/Prerender/AdminPrerenderTests` (étendu) | `api` | chaque code de champ ; `none-pinned` ; `nothing-to-render` ; `201` avec `skippedUnits` et `ignoredVersions` ; audit ; `409` actif avant planification |
| `Admin/Prerender/AdminPrerenderPatchesTests` (nouveau) | `api` | cellules et masques par langue ; epoch non servie ; `pagesPerLocale` lu de n'importe quelle passe « tous types » ; `typeShares` nul tant qu'aucune unité n'est terminée (et aucune ingestion mise en file : `FakeCatalog` compte les appels) ; `400` sur `epoch=0` ; accès refusé sans second facteur |
| `Admin/Prerender/AdminPrerenderReportTests` (étendu) | `api` | epoch prête par la couverture (deux passes ciblées) ; **prête malgré une unité inconnue de Data Dragon** (`f`) et malgré des `w` ; pas prête avec un `p` ou un `-` ; `coverage` par epoch, deux epochs en attente, `null` pour une retirée ; `pagesLeft` par version (unités à venir d'un patch ancien estimées à sa mesure, pas à la moyenne des récentes) ; périmètre résumé (`versionCount`, bornes) ; `storeBucket` |
| `Prerender/PromotionTests` (étendu) | `api` | report quand la passe active est restreinte en types ; périmètre stocké `{"versions":[…]}` ; `pages_expected` des unités ajoutées |
| `Prerender/PrerenderUnitCommandTests` | `api` | inchangés, verts |
| `Admin/Prerender/AdminPrerenderProgressTests` (nouveau) | `api` | lecture complète avec `scopeVersions` ; `since` ne rend que les versions changées, sans `scopeVersions` ; `since` trop ancien → complète ; codes de cellule, `z` d'une passe en pause, `i` d'une passe annulée ; `running`, `next` dans l'ordre de `UnitQueue.NextAsync` et sans les versions en attente, `recent` par `ended_at`, `failures` et leur compte ; `404` ; `pulse` (worker coupé → `isWorkerEnabled: false`, `observedAt: null`) ; `EXPLAIN` des requêtes 4 à 6 sur 8 400 unités : aucun parcours séquentiel de `prerender_unit` |
| `Admin/Prerender/AdminPrerenderPassVersionTests` (nouveau) | `api` | 21 unités dans l'ordre ; `400` ; `404` |
| `Prerender/PassLifecycleTests` (étendu) | `api` | `pausedAt` posé puis effacé ; `pauseReason` sur une erreur de configuration ; l'unité reste `running` pendant la pause et reprend d'abord ; `startedAt` gardé à la reprise ; `endedAt` posé une fois ; `failure` d'une page ratée ; raison d'un abandon et d'une version inconnue |
| `Persistence/Prerender/PrerenderTablesTests` (étendu) | `api` | colonnes nulles par défaut ; index partiel `ix_prerender_unit_ended` présent ; aucun index sur `updated_at` |

**E2E Playwright : aucune nouvelle spécification.** `docs/guides/tests.md` réserve l'E2E aux
parcours critiques des joueurs et à ce que seule la pile réelle montre ; l'accès à
l'administration y est déjà (`tests/LoDb.E2E/specs/admin/access.spec.ts`). Tout ce que cette
refonte ajoute se voit plus bas : le contrat et le SQL dans `LoDb.Api.Tests` (Postgres réel), le
rendu, le clavier, le direct et les dialogues dans Vitest. À la place, une **vérification
manuelle au staging** avant fusion (§ 8.5).

### 7.5 Pièges de mise en œuvre

- **Les dialogues vivent hors de la page.** Le CDK monte les dialogues dans
  `.cdk-overlay-container`, sous `<body>` : les règles de l'admin, écrites sous
  `lodb-admin-page` (`admin/admin-page.css` : `.admin-tbl`, `.admin-input`, `.readout-*`,
  `.kpi-grid`) et sous `lodb-admin-frame` (`admin/frame/admin-frame.css` : taille des boutons et
  des puces), ne les atteignent pas. F1 élargit chaque sélecteur à
  `:is(lodb-admin-page, .admin-overlay)` et `:is(lodb-admin-frame, .admin-overlay)`, et chaque
  composant de dialogue de l'admin porte `host: { class: 'admin-overlay …' }`. Les jetons, eux,
  suivent : l'admin épingle `data-theme="hextech"` sur `<html>`
  (`src/app/core/layout/shell/shell.spec.ts`). À ajouter à `docs/architecture/pieges.md` (texte
  exact au § 8.4, écrit par le lot `api`, propriétaire du fichier).
- **`lodb-tabs` rend tous les panneaux** (`tab.ts`) : aucun onglet ne charge « à son
  ouverture ».
- **Sans zone** : la lecture périodique repose sur `setTimeout` et des signaux (comme
  `poll-while.ts`), jamais sur `setInterval` sans nettoyage ; tout minuteur est effacé dans
  `onCleanup` ou `DestroyRef`.
- **Navigateur seulement** : l'admin ne se rend pas côté serveur (`poll-while.ts` le rappelle) ;
  `document`, `localStorage`, `Intl.DisplayNames` s'utilisent via `inject(DOCUMENT)` et sous
  `try/catch` pour le stockage.
- **Mouvement** : pulsation du point « en direct » et halo des cellules en cours coupés sous
  `prefers-reduced-motion: reduce` (`src/styles/foundation/motion.css` : « no information is ever
  carried by movement alone »).
- **RTL** : propriétés logiques partout (`inset-inline-start`, `margin-inline`, `padding-block`) ;
  la barre et la matrice suivent la direction.
- **Champs** : 16 px au moins (le filtre, la zone de collage, les sélecteurs De / à).
- **Chiffres** : `figure` (`admin/format/figure.ts`) pour les nombres (espace des milliers, point
  décimal ; une part non nulle ne s'écrit jamais « 0 % », ni une part incomplète « 100 % » :
  « < 0.1 % couverte » après 2 643 pages, jamais « 0 % couverte » ; « > 99.9 % » en haut),
  `decimalBytes` pour les volumes (Go de 10⁹), `monthlyCost` pour les euros, `stamp` pour les
  instants (UTC), `duration` pour les durées. Un instant en UTC le dit à l'écran, jamais
  dans un seul `title` : à Paris, « 01:51 » se lirait comme une heure passée. Le libellé le porte
  (« Fin estimée (UTC) », « Activité (UTC) », les colonnes « Lancée (UTC) », « Début (UTC) »,
  « Dernière écriture (UTC) »), sinon la phrase (« lancée le 21/10/2026 01:36 UTC par … »,
  « finie le … UTC », « échouent à 09:12 UTC »).
- **Couleurs** : jetons seulement, jamais une teinte en dur ; `--color-good`, `--color-bad`,
  `--color-gold`, `--color-hex`, `--color-hex-bright`, `--color-track`, `--color-text-dim`.

### 7.6 L'entrée de changelog : aucune

Décision : **pas d'entrée** dans `docs/changelog/`. La règle du dépôt (`AGENTS.md`, « Changelog ») ne
retient que ce que le **joueur** perçoit (fonctionnalité, bug visible, performance ou UX
perceptible, devops visible) ; cette refonte ne change rien pour lui : ni page, ni disponibilité,
ni vitesse. Pour l'admin du précalcul, le dépôt l'a déjà tranché : ni le panneau
(`de579965 feat(front/admin): ajouter le panneau de precalcul du catalogue`) ni la facture
Scaleway (`4d6d0c15`) n'ont d'entrée ; l'effet joueur du chantier est porté par
`docs/changelog/2026/2026-10-20-anciens-patchs-sans-attente.md`.

Le précédent contraire, `docs/changelog/2026/2026-10-12-historique-des-sauvegardes.md`
(`scope: full-stack`, « L'administration gagne une page « Sauvegardes » »), ne s'applique pas :
cette entrée annonce un changement **devops visible**, la sauvegarde nocturne des comptes et des
builds des joueurs hors du serveur, dont la page d'administration n'est que la surveillance. Ici,
rien de tel : ni la disponibilité ni la sécurité ne changent. Le seul changement de règle,
l'epoch prête jugée par sa couverture (§ 6.5), ne change pas ce que sert nginx : la bascule
reste une variable posée à la main. La documentation, elle, suit le code (§ 8.4).

## 8. Découpage en lots parallèles

### 8.1 Vue d'ensemble

Cinq lots, des propriétés de fichiers **disjointes** : un fichier n'a qu'un propriétaire. Les
branches partent de `feat/refonte-ecran-pre-rendu` (la branche de ce document) et y reviennent.
Les sigles du texte (B1, B2, F1 à F4) désignent ces lots :

| Lot (clé) | Sigle dans le texte | Branche | Contenu | Dépend de |
|---|---|---|---|---|
| **`api`** — API du précalcul | phase contrat (ex-« lot 0 »), B1, B2 | `feat/precalcul-api` | contrat (périmètre en listes, toutes les vues, trois points d'accès), client généré, fixtures du front, ancien panneau adapté ; puis périmètre, planification, couverture, epoch prête ; puis migration `PrerenderDiagnostics`, écritures du moteur, pouls, progression | — |
| **`front-socle`** — socle front | F1 | `feat/precalcul-socle-front` | `common/`, `live/`, `bars/`, `matrix/`, `dialogs/`, styles des dialogues, sparkline à trous, graine des traductions | `api` |
| **`front-modale`** — modale de lancement | F2 | `feat/precalcul-modale` | `launch/{dialog,picker,priority,perimeter,summary,call}/` | `api`, `front-socle` |
| **`front-moniteur`** — moniteur en direct | F3 | `feat/precalcul-moniteur` | `monitor/` et ses sous-dossiers | `api`, `front-socle` |
| **`front-page`** — page et onglets | F4 | `feat/precalcul-page` | panneau, `overview/`, `coverage/`, `costs/`, `passes/`, câblage final, suppressions de l'héritage, nettoyage des clés, titre du document | `api`, `front-socle`, `front-modale`, `front-moniteur` |

**Pourquoi un seul lot côté API.** Le contrat (lot 0 de la première version) et les deux lots
back (B1, B2) se partageaient des fichiers : `UnitRunner.cs` (une ligne du contrat, puis les
diagnostics), `PassViews.cs`, `PassHistory.cs`, `PrerenderJournal.cs`, les deux fichiers
d'enregistrement (`PrerenderRegistration.cs`, `PrerenderHttpRegistration.cs`, où B1 et B2
auraient ajouté `EstimateBasisReader`, `ProgressQueries`, `PulseReader` sur des lignes voisines),
le harnais `tests/LoDb.Api.Tests/Prerender/Support/PrerenderHarness.cs`, et les coquilles que le
lot 0 créait pour qu'un autre les remplisse. Un lot unique, mené en trois phases, supprime ces
collisions ; le front, qui ne dépend que du contrat et des fixtures, ne l'attend que jusqu'à sa
fin. **Pourquoi `front-socle` à part** : ses éléments (sélection, lecture en direct, barres,
matrice, dialogues) servent à trois lots (F2, F3, F4).

**Le chemin critique** : `api` → `front-socle` → (`front-modale` ‖ `front-moniteur`) →
`front-page`. `front-modale` et `front-moniteur` ne s'importent jamais l'un l'autre : le
moniteur émet un `LaunchPreset` (F1), seul le panneau ouvre la modale (§ 3.1).

### 8.2 Fichiers par lot

Chemins abrégés comme au début du document. **C** = créé, **M** = modifié, **S** = supprimé.

#### `api` — API du précalcul (phases contrat, B1, B2)

**Phase contrat** (un commit, build et tests verts de bout en bout) :

| Fichier | | Ce qui change |
|---|---|---|
| `Prerender/Pages/PassScope.cs`, `PassScopeJson.cs`, `PageTypes.cs` | M | forme du § 6.3 (lecture des deux formes, écriture de la nouvelle) ; `Keep(paths, IReadOnlyList<ResourceType>?)` |
| `Prerender/Http/PassRequest.cs`, `PassRequestReader.cs` | M | § 6.2 entier (codes, limites, normalisation) |
| `Prerender/Http/Views/*.cs` | M/C | toutes les vues des § 6.3 à 6.8, `UnitCells.cs` (codes `. r z c w f i _` et `c w p f -`) |
| `Prerender/Http/PrerenderPatchesEndpoint.cs`, `PrerenderProgressEndpoint.cs`, `PrerenderPassVersionEndpoint.cs`, `src/LoDb.Api/Modules/Admin/AdminModule.cs` | C/M | points d'accès complets (`WithName`, `Produces…`), trois `Map` |
| `src/LoDb.Api/openapi/LoDb.Api_app.json`, `src/LoDb.Web/src/app/core/api/generated/**` | M | `npm --prefix src/LoDb.Web run api:generate` |
| `admin/testing/fixtures/prerender-report.ts` (M), `prerender-patches.ts`, `prerender-progress.ts` (C) | M/C | les exemples JSON des § 6.6 et § 6.7, en fonctions à surcharges (comme `prerenderReport(overrides)`) |
| **Ancien panneau** : `prerender/launch/launch-form.{ts,html}`, `launch/pass-request-of{,.spec}.ts`, `pass/scope-summary{,.spec}.ts`, `prerender-panel.spec.ts` | M | pour qu'il compile et que ses tests restent verts contre le nouveau contrat : les deux champs « plus ancienne / plus récente » deviennent une zone « Versions » (collée, une par ligne, `versionTokens`-like local), le type et la langue uniques partent en listes d'un élément (`pageTypes: [type]`, `locales: [locale]`), le corps vérifié par la spécification devient `{ epoch: 3, versions: ['14.1.1'], locales: ['fr'], pageTypes: ['items'], priority: [...], skipComplete: false }` ; `scope-summary` lit le périmètre résumé (§ 6.3). `front-page` supprimera ces fichiers |
| `i18n/admin/{fr,en}.json` | M | sous-arbre hérité `admin.prerender.launch` seulement : `versions`, `versions_hint` |

**Phases B1 (périmètre et couverture) et B2 (suivi)** :

| Fichier | | |
|---|---|---|
| `Prerender/Passes/*` (`PassPlan` C, `PassPlanning` C, `UnitPlanner`, `PassLauncher`, `PlannedUnit`, `PassControl`, `PassTimes`, `PassTarget` C) | M/C | § 6.4, § 6.9 |
| `Prerender/Http/PrerenderDesk.cs`, `PrerenderJournal.cs`, `Http/Storage/StorageFacts.cs`, `StorageLedger.cs` | M | ordre du § 6.4, couverture par epoch, `storeBucket` |
| `Prerender/Http/History/*` (`PassViews`, `PassProgress`, `PassHistory`, `PassFacts`) | M | périmètre résumé, `PagesLeft` par version, `OneAsync`, `PausedAt`, `PauseReason` |
| `Prerender/Coverage/*` (`EpochCoverage`, `CoverageMap`, `CoverageCell`, `CoverageUnit`, `CoverageFold`, `CoverageCache`, `EpochReadiness`, `PatchCoverageReader`, `EstimateBasisReader`) | C | § 6.5, § 6.6 (dix fichiers au plus : un dossier `Coverage/Basis/` si besoin) |
| `Prerender/Http/Monitor/*` (`PassMonitor`, `ProgressQueries`, `VersionRows`, `PulseReader`) | C | § 6.7, § 6.8 |
| `Prerender/Pages/IPrerenderCatalog.cs`, `CatalogPages.cs` | M | `LatestVersionAsync` |
| `Prerender/Units/*` (`UnitQueue`, `UnitRunner`, `UnitStep`, `UnitWaits`, `UnitWait` C, `UnitWaitReason` C) | M/C | § 6.9 (dont la ligne `Scope.PageTypes` du contrat) |
| `Prerender/Rendering/SsrOutage.cs`, `Prerender/Writing/EnginePulse.cs` (C), `PrerenderEngine.cs` | M/C | pouls |
| `Prerender/Promotion/VersionEnqueuer.cs` | M | `HasEveryPageType`, `pages_expected` |
| `Prerender/PrerenderRegistration.cs`, `Prerender/Http/PrerenderHttpRegistration.cs` | M | tous les nouveaux services |
| `src/LoDb.Api/Cli/Prerender/PrerenderUnitArguments.cs`, `PrerenderUnitCommand.cs` | M | § 6.11 |
| `src/LoDb.Api/Workers/ConventionalWorkers.cs` | M | `IsEnabled` public |
| `Persistence/Prerender/PrerenderUnit.cs`, `PrerenderPass.cs`, `PrerenderUnitConfiguration.cs`, `PrerenderPassConfiguration.cs` | M | § 6.9 |
| `src/LoDb.Infrastructure/Persistence/Migrations/<horodatage>_PrerenderDiagnostics.cs` (+ `.Designer.cs`), `LoDbDbContextModelSnapshot.cs` | C/M | additive |
| `tests/LoDb.Api.Tests/Prerender/**` (dont `Support/*`), `tests/LoDb.Api.Tests/Admin/Prerender/AdminPrerender*.cs`, `tests/LoDb.Infrastructure.Tests/Persistence/Prerender/PrerenderTablesTests.cs` | M/C | § 7.4 |
| `docs/produit/pre-rendu-catalogue-cdn.md`, `docs/guides/configuration.md`, `docs/architecture/pieges.md` | M | § 8.4 |

**Le contrat ne bouge plus après la phase contrat.** Un champ manquant découvert en B1 ou B2 est
ajouté par ce lot dans un commit de contrat à part (client régénéré, fixtures à jour) **avant**
qu'il ne rende la main : les lots front partent du contrat final.

Commits : `feat(back/admin): lister versions, langues et types d'une passe` (contrat, client
généré, fixtures et ancien panneau dans le même commit, `AGENTS.md` « Artefacts générés ») ;
`feat(back/admin): lire la couverture des anciens patchs par epoch` ;
`feat(back/admin): juger une epoch prete par sa couverture` ;
`feat(back/db): retenir les raisons d'echec et de pause des passes` ;
`feat(back/admin): suivre une passe par lecture incrementale` ;
`docs: decrire le pupitre du precalcul refondu`.

#### `front-socle` — socle front (F1)

| Fichier | | |
|---|---|---|
| `prerender/common/**`, `live/**`, `bars/**`, `matrix/**`, `dialogs/**` | C | § 7.1 |
| `prerender/prerender-tone.ts` (+ spec) | M | `pending`, `failed`, `stopped` |
| `admin/admin-page.css`, `admin/frame/admin-frame.css` | M | `.admin-overlay` (§ 7.5) |
| `admin/charts/sparkline.ts`, `admin/charts/scale/sparkline-shape.ts`, `admin/charts/charts.spec.ts`, `admin/charts/scale/shapes.spec.ts` | M | entrée `gaps` |
| `i18n/admin/fr.json`, `en.json` | M | **premier commit** : la graine de tout l'arbre du § 7.3 ; ensuite `states`, `page_types`, `common` seulement |

Commits : `feat(i18n): poser les textes du nouvel ecran de precalcul`,
`feat(front/admin): ajouter le socle du suivi en direct du precalcul`.

#### `front-modale` — modale de lancement (F2)

`prerender/launch/{dialog,picker,priority,perimeter,summary,call}/**` : tous les fichiers du § 7.1
et leurs spécifications (pas `launch/launch-form.*` ni `launch/pass-request-of.*`, hérités, que
`front-page` supprime). Traductions : sous-arbre `launcher`. Commit :
`feat(front/admin): lancer une passe depuis une modale en trois etapes`.

#### `front-moniteur` — moniteur en direct (F3)

`prerender/monitor/**` : tous les fichiers du § 7.1 et leurs spécifications
(`monitor/actions/inject-pass-actions.ts` y est **recréé** ; l'ancien, à la racine, reste jusqu'à
`front-page`). Traductions : sous-arbres `live` et `done`. Commit :
`feat(front/admin): suivre la passe de precalcul en direct`.

#### `front-page` — page et onglets (F4)

| Fichier | | |
|---|---|---|
| `prerender/prerender-panel.{ts,html}` | M | réécrits |
| `prerender/prerender-panel.states.spec.ts`, `prerender-panel.presets.spec.ts` | C | § 7.4 |
| `prerender/overview/**`, `coverage/**`, `costs/**`, `passes/**` | C | § 7.1 |
| `admin/layout/page-head.ts`, `admin/layout/layout.spec.ts` | M | entrée `documentTitle` (§ 2.5) |
| `admin/testing/i18n/provide-admin-catalogues.ts` | M | extrait mis à jour |
| `i18n/admin/fr.json`, `en.json` | M | sous-arbres `title`, `lede`, `overview`, `coverage`, `costs`, `passes`, `switch` ; puis le nettoyage final (§ 7.3) |

**Suppressions de l'héritage** (après la fusion de tous les autres lots ; ces fichiers ne sont la
propriété de personne d'autre à ce moment-là, le lot `api` n'y touchant plus après sa phase
contrat) : `prerender/launch/launch-form.*`, `launch/pass-request-of*`, `pass/**`, `storage/**`,
`poll-while.ts`, `prerender-poll.ts`, `inject-pass-actions.ts` (racine), `prerender-panel.spec.ts`,
et les clés héritées du § 7.3.

Commit : `feat(front/admin): refondre la page du precalcul du catalogue` (suppressions et
nettoyage des clés dans le même commit).

### 8.3 Les traductions sans collision

`fr.json` et `en.json` sont un seul fichier par langue pour tout l'admin : quatre lots front qui
y écrivent se marcheraient dessus. La règle :

1. Le lot `api` n'édite que le sous-arbre hérité `admin.prerender.launch` (deux clés), avant tous
   les autres.
2. **`front-socle`, en tout premier commit**, écrit l'arbre complet du § 7.3 dans les deux
   fichiers, sans toucher aux clés existantes : `states.pending`, `states.failed`,
   `states.stopped` dans `states`, puis, après le sous-arbre `done`, les nouveaux sous-arbres dans
   cet ordre : `common`, `overview`, `live`, `launcher`, `coverage`, `costs`, `passes`, `switch`.
   Les lots suivants partent de ce commit.
3. Ensuite, **chaque lot n'édite que ses sous-arbres** (`front-socle` : `states`, `page_types`,
   `common` ; `front-modale` : `launcher` ; `front-moniteur` : `live`, `done` ; `front-page` :
   `title`, `lede`, `overview`, `coverage`, `costs`, `passes`, `switch`). Une clé manquante
   s'ajoute **à la fin de son sous-arbre**. Deux lots ne modifient donc jamais des lignes
   voisines : git fusionne sans conflit.
4. Le nettoyage des anciennes clés (§ 7.3, « Clés retirées ») se fait **en dernier**, par
   `front-page`.
5. Les spécifications Vitest affichent les clés, pas les textes : aucun lot n'a besoin du catalogue
   d'un autre pour ses tests. `npm --prefix src/LoDb.Web run test:i18n` vérifie la parité des deux
   fichiers et la compilation ICU.

### 8.4 La documentation qui suit le code

Toute la documentation revient au lot `api` (un fichier, un propriétaire) ; les textes du front y
sont donnés ici mot pour mot.

| Document | Ce qu'on y écrit |
|---|---|
| `docs/produit/pre-rendu-catalogue-cdn.md`, lot 2, « Administration » | le lancement prend des listes (versions, langues, types) et `skipComplete` au lieu d'une plage, d'un type et d'une langue ; les trois points d'accès de lecture ; l'epoch prête jugée par sa couverture ; un lien vers ce document pour l'interface |
| `docs/guides/configuration.md`, § 4.8 « Changer l'epoch servie » | « Quand l'administration la montre prête » : prête = plus aucun manque (chaque (ancien patch, langue) terminé sur tous les types, ou en échec définitif, que le SSR servira) ; le dialogue de bascule donne la variable ; le retrait se prépare depuis la ligne de l'epoch retirée, une fois la bascule déployée |
| `docs/architecture/pieges.md`, « Catalogue pré-rendu et CDN » | trois puces, ci-dessous |
| `docs/architecture/pieges.md`, « Front, SSR et SEO » | une puce, ci-dessous |

Puces exactes pour `pieges.md` :

- « **Diagnostics de la passe sans contrainte.** `prerender_pass.paused_at`, `pause_reason`,
  `prerender_unit.started_at`, `ended_at`, `failure` et `pages_expected` (migration
  `PrerenderDiagnostics`) n'ont aucune contrainte de cohérence avec `state` : l'image précédente
  de la stack met des passes en pause sans les remplir, et un retour arrière doit rester possible.
  Une passe mise en pause avant la migration n'a pas de `paused_at` : le panneau dit « en pause »
  sans durée. »
- « **Pas d'index sur `prerender_unit.updated_at`.** Il est réécrit à chaque page : l'indexer
  couperait les mises à jour HOT de la table, quatre millions de fois par passe. Ce qui doit être
  lu par ordre de fin passe par `ended_at`, écrit une fois, et son index partiel. »
- « **Epoch prête = plus aucun manque.** `readyEpoch` ne regarde plus si une passe « entière » a
  fini, mais si chaque (ancien patch, langue) de l'epoch a une unité terminée sur les quatre types
  de pages, ou une unité en échec définitif (`f`), quelles que soient les passes. Ces échecs et
  les pages en échec des unités terminées restent servis par le SSR après la bascule : la bande
  les compte. »
- « **Les dialogues de l'admin vivent hors de sa page.** Le CDK les monte sous `<body>` :
  les règles de `admin-page.css` et `admin-frame.css` ne les atteignent que par `.admin-overlay`,
  que chaque composant de dialogue de l'admin porte sur son hôte. Un nouveau sélecteur de ces
  feuilles s'écrit `:is(lodb-admin-page, .admin-overlay) …`. »

### 8.5 Ordre, fusions et vérifications

```
api ─────────────┐ (contrat d'abord, puis B1 et B2)
                 └─ front-socle ──┬─ front-modale ───┐
                                  └─ front-moniteur ─┴─ front-page
Fusions dans feat/refonte-ecran-pre-rendu : api → front-socle → front-modale → front-moniteur
→ front-page.
```

- **Chaque lot avant de rendre** passe ses vérifications ciblées (la liste exacte est dans sa
  fiche de lot) : pour `api`, `dotnet build LoDb.slnx -c Release` (zéro avertissement), les tests
  filtrés sur le précalcul (Docker démarré), `api:check` ; pour un lot front, Vitest sur ses
  dossiers (`ng test --include`), ESLint et Prettier sur ses chemins, `typecheck`, `test:i18n`.
- **À l'intégration, après `front-page`**, les garde-fous complets d'`AGENTS.md` : build, `dotnet
  test LoDb.slnx`, `lint`, `typecheck`, `test`, `build:web`, `build:shell`, `api:check`.
- **Pile locale** : un lot qui lance la pile prend un emplacement (`lodb-dev-e1`, `-e2`) et le
  supprime (`down -v`) avant de rendre (`AGENTS.md`). La pile locale a un stockage simulé
  (`compose.override.yaml`, moto) : une passe y tourne vraiment.
- **Vérification manuelle, pile locale puis staging, après `front-page`** (le budget du staging est
  de 30 rendus par minute, `compose.staging.yaml`) :
  1. lancer depuis la modale une passe de 3 patchs × 2 langues, dont un prioritaire collé depuis
     une sortie LogsQL ; lire le récapitulatif et « Comment c'est estimé » ;
  2. suivre le moniteur : pourcentage, unité en cours qui avance, fil d'activité, matrice, titre
     de l'onglet (passer sur un autre onglet et revenir) ;
  3. redémarrer l'API (`docker compose restart api`) : badges « hors ligne », puis retour « en
     direct », sans perte de la passe, **sans bande rouge** en tête de page ;
  4. mettre en pause (« Reprendra par », cellule arrêtée), reprendre, annuler, en lisant chaque
     dialogue ;
  5. onglet Couverture : la sélection → « Pré-rendre la sélection » ; « Compléter les manques » ;
  6. historique : détail, « Relancer N en échec et M non rendues » ;
  7. tout refaire au clavier seul dans la modale (dont Maj+↓ puis Maj+↑), et à 360 px (outils du
     navigateur) ;
  8. VoiceOver sur la liste des patchs : niveaux, sélection, compteur annoncé.
- Aucune entrée de changelog (§ 7.6).
