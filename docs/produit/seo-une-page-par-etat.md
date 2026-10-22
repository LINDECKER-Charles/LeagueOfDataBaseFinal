# Une page indexée par état : tout l'historique dans Google, sans copie

> **État** (2026-10-22) : **en vigueur dans le code** (lots L0 à L7, § 6, sur
> `feat/seo-une-page-par-etat`). La prod applique la règle du 14 octobre (une page indexée
> par entité) jusqu'à la bascule du § 4, son déploiement. Ce que l'implémentation a tranché
> autrement que le texte ci-dessous est au § 11, qui l'emporte. Règle en vigueur et
> avancement : [`seo-indexabilite.md`](seo-indexabilite.md).

Chantier décidé le 22 octobre 2026. Le site est une base de données : chaque contenu
distinct qu'elle tient doit avoir une page indexée, une seule. Une fiche d'ancien patch n'est
plus indexée sous l'URL du dernier patch, ni répétée à chaque patch : elle l'est **une fois
par état**, c'est-à-dire par suite de patchs où son contenu ne change pas. Chaque état montre
ce que son patch a changé, et chaque patch a sa page de changements.

Cette règle remplace celle du 14 octobre 2026 (une page indexée par entité, celle du dernier
patch) et ne dépend plus d'aucune contrainte de charge. Décision :
[ADR 0005, amendement du 2026-10-22](../reecriture/adr/0005-web-ssr-urls-et-seo.md),
[ADR 0011, amendement du 2026-10-22](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md)
et [ADR 0003, amendement du 2026-10-22](../reecriture/adr/0003-ingestion-proactive-et-taches-de-fond.md)
(écouteurs de promotion ordonnés, tâches de l'index). Diagnostic précédent :
[`seo-anciens-patchs.md`](seo-anciens-patchs.md).

## 1. Constat

### 1.1 Trois règles d'indexation depuis juillet

| Période | Index `/sitemap.xml` | URL annoncées | Pages d'anciens patchs | Raison |
|---|---|---|---|---|
| 18 juillet → 4 octobre (ancien site) | une sitemap par version, ~400 fichiers ; une langue par hôte, la locale vivant en session | ~200 000 par langue | propre canonique, indexable, sans hreflang | ouvrir la longue traîne (« Aatrox patch 16.13 ») |
| 4 → 11 octobre (nouvelle pile) | ~8 400 fichiers (402 versions × 21 langues) | ~4,2 millions | propre canonique, indexable, hreflang | la même règle, en 21 langues |
| 11 → 14 octobre | 273 fichiers (13 versions × 21) | ~160 000 (Search Console) | inchangé | **la charge** : pages rendues à la demande dans un budget borné |
| depuis le 14 octobre | 21 `latest.xml` | ~22 800 (1 085 × 21) | canonique vers l'URL courte ; entité retirée indexée à sa dernière version | **le SEO** : Google voyait des doublons |

### 1.2 Ce que la charge cachait : 85 % de copies

Mesure du 22 octobre 2026 sur les données Data Dragon en_US. Chaque fiche d'une version est
comparée à celle de la version listée juste avant, en ignorant les champs d'image (`image`,
`sprite`).

| Ressource | Versions comparées | Pages | Pages différentes de la version précédente |
|---|---|---|---|
| Objets | 167 (10.1.1 → 16.21.1) | 69 256 | **13,5 %** |
| Champions | 45 (15.1.1 → 16.21.1) | 7 773 | **17,7 %** (12,9 % sans les skins) |
| Sorts d'invocateur | 167 | 2 853 | **6,8 %** |

- Environ **85 % des 4,19 millions de pages d'anciens patchs** sont la copie exacte de la page
  de la version d'avant : seuls le numéro de patch et le titre changent. Google voyait donc la
  même page jusqu'à 30 fois de suite. C'est ce qui l'a fait décrocher : le problème venait des
  copies, pas de la quantité de pages.
- Les contenus distincts représentent ~14 % des pages : **~600 000 pages indexables**
  (≈ 28 000 états × 21 langues), contre ~22 800 aujourd'hui. Estimation : les runes et les
  saisons avant 10.1 ne sont pas mesurées.
- Un état dure en moyenne 7,7 versions pour un objet, 5,4 pour un champion, 11,7 pour un sort.
- Les pages d'anciens patchs encore dans l'**état en cours** de leur entité (celui du dernier
  patch) sont peu nombreuses : 5 % pour les objets sur 167 versions, 15 % pour les champions
  sur 45. Un patch clôt les états d'environ 1 150 pages en_US, soit **~24 000 pages** pour les
  21 langues.

**Ces chiffres sont des majorants.** La mesure porte sur le JSON brut de Data Dragon.
L'empreinte (§ 3.1) ne voit que ce que la fiche affiche, après la mise en forme de
l'ingestion : elle trouve au plus autant de changements. Les comptes réels viendront des
lignes de synthèse de la construction de l'index (§ 3.2) et s'écriront au § Résultats.

### 1.3 Rappel de la chute d'août

- 18 juillet : une sitemap par version. Fin juillet, Google explore jusqu'à 24 000 pages par
  jour.
- 1ᵉʳ au 15 août : 85 093 impressions. 16 août : 589. Rien n'a été déployé du 9 au 21 août ;
  la spam update d'août n'a commencé que le 18, et il n'y a pas eu de core update en août.
- Les pages sont toutes restées dans les sitemaps jusqu'au 11 octobre, pendant que les
  impressions tombaient à 0–14 par jour.
- La règle du 14 octobre retire les copies, mais aussi tout l'historique : seule la dernière
  version de chaque entité reste indexable. C'est ce que ce chantier corrige.

### 1.4 La charge n'est plus la contrainte

Les pages d'anciens patchs sont pré-calculées dans le stockage
([ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md)). La file de la demande
(PR #98) pré-calcule en priorité toute page qui a reçu une 503, et le rythme de la passe
monte à 900 pages par minute (PR #97). La règle d'indexation se décide donc sur le seul
terrain du SEO.

## 2. Principe et vocabulaire

**Chaque contenu distinct de la base a une page indexée, une seule ; une copie désigne
l'original.**

| Terme | Sens |
|---|---|
| **Entité** | Un champion, un objet, un arbre de runes ou un sort d'invocateur, désigné par sa **clé** (§ ci-dessous) |
| **Empreinte** | Condensé de ce que la fiche affiche de l'entité dans une version (§ 3.1) |
| **État** | Suite de versions listées consécutives où l'entité existe avec la même empreinte |
| **Ouvreur** | La première version d'un état : celle dont le patch a apporté le changement |
| **État ouvert** | L'état qui contient le dernier patch ; tous les autres sont **clos** |
| **Retour** | Un état qui s'ouvre après une absence de l'entité |
| **Couverture** | Les versions que l'index connaît (§ 3.2) |
| **Génération** | Une construction complète de l'index, pour un schéma d'empreinte (§ 3.2) |
| **Page figée** | Page d'ancien patch d'un état clos : elle ne changera plus (pour un schéma donné) |
| **Page révisable** | Page d'ancien patch de l'état ouvert : elle changera quand l'état se clôt |
| **Page de patch** | `/{locale}/[{version}/]changes` : ce qu'une version a changé (§ 3.5) |

- **Existence.** Une entité existe dans une version quand elle y a une fiche : tous les
  champions (`Champions.Entries`), tous les arbres de runes (`Runes.Entries`), les objets
  listés (`ListedItems`) et les sorts listés (`ListedSummoners`) du catalogue en_US de la
  version. Chaque id compte une fois, la première entrée l'emporte (comme
  `CatalogResource.Find`). Un objet qui devient débris est absent ; l'inverse est un retour.
  L'index, les sitemaps et la passe partagent ce prédicat (`VersionPages.ListedEntityPaths`,
  déplacé dans `LoDb.Ingestion`).
- **Clé.** La clé d'une entité est son id replié par `ToLowerInvariant`, pour les quatre
  ressources : Data Dragon a changé la casse d'ids de champions (`FiddleSticks`, puis
  `Fiddlesticks`). Le chemin stocké garde la casse de la version ouvreuse ; toute lecture
  replie l'id de la même façon.
- **Absence.** Une entité absente d'une version interrompt son état. Si elle revient, un
  nouvel état commence, même avec la même empreinte.
- **Jeu absent.** Une ressource dont le jeu de données est définitivement absent dans une
  version (stocké vide, `ContentLanguage` nul : les runes avant 7.22.1) ne compte pas pour
  cette version : ses états n'y sont ni étendus ni interrompus. Les runes n'ont donc aucun état
  avant 7.22.1.
- **Id réutilisé** (3001 : Abyssal Scepter, Abyssal Mask, puis Evenshroud). Pas de règle à
  part : l'empreinte change, donc l'état. L'historique et la différence traversent la
  réutilisation ; le premier état de la nouvelle entité montre le changement de nom. C'est
  voulu.

## 3. Décisions

### 3.1 L'empreinte

- **Entrée** : les entrées du domaine (`ChampionDetail`, `Item`, `RuneTree`,
  `SummonerSpell`) du `CatalogSnapshot` en_US stocké de la version, vues par une
  **projection affichée** : ce que la fiche montre de l'entité elle-même. Jamais le JSON brut
  de Data Dragon, qui n'est pas stocké.
- **Projection** par ressource. La liste définitive est l'énumération `ChangeField` du lot L0
  (§ 3.4) ; un champ que la fiche ne montre pas n'y entre pas.

  | Ressource | Projeté | Ignoré |
  |---|---|---|
  | Objet | nom, `plaintext`, description, prix total, de base et de revente, achetable, recette `from` (dans l'ordre), statistiques affichées (`ItemStats.Of`), cartes où il s'obtient (`ItemEdition.ClaimableMapIds`, ensemble trié), étiquettes (ensemble trié), profondeur, consommable, champion requis, allié requis | `into` (tiré des autres objets), `Counterpart`, `Edition`, `Image`, drapeaux que la fiche ne montre pas |
  | Champion | nom, titre, rôles (dans l'ordre), ressource (`partype`), notes (`Info`), lignes de statistiques affichées (`ChampionStats`), lore (le `blurb` quand le lore manque, comme la fiche), conseils alliés et ennemis, passif (nom, description), chaque sort par emplacement Q/W/E/R (nom, description, recharge, coût, portée masquée par `RangeSentinels.MaskAbilityRange`, rang maximal, charges), skins : ensemble des (`Skin.Id`, `Skin.Name`) | `Skin.Number`, `Skin.Chromas` (CommunityDragon), images |
  | Arbre de runes | nom ; par emplacement, les ids de ses runes ; par rune, nom, description courte et longue | icônes |
  | Sort d'invocateur | nom, description, recharge, coût, type de coût, portée (masquée comme à l'affichage), niveau d'invocateur, charges (`Charges`, pas `MaxAmmo` brut), modes (ensemble trié) | `Counterpart`, `Edition`, `Image`, `Key` |

  La table `ChampionStats` descend de l'API dans `LoDb.Domain` (lot L1) : la fiche et
  l'empreinte lisent la même liste.
- **Textes** : une seule forme, partagée par l'empreinte et la différence (§ 3.4) :
  `DdragonText.PlainText`, c'est-à-dire `DdragonText.Clean`, puis les balises retirées
  (`<br>` devient un saut de ligne), puis les blancs réduits. Un changement de balisage seul
  ne crée donc pas d'état. C'est accepté.
- **Forme** : SHA-256 en hexadécimal minuscule d'une sérialisation canonique de la
  projection : champs dans l'ordre de l'énumération, clés triées en ordinal, nombres en
  `ToString("R", InvariantCulture)`, textes en forme `PlainText`.
- **Schéma** : `FingerprintScheme.Current = 1`, constante `int` de `LoDb.Domain`, conservée
  avec chaque génération de l'index. Changer la définition de l'empreinte, c'est monter ce
  numéro, reconstruire l'index et pré-calculer de nouveau les pages dont l'état change (§ 3.7).
  Une page figée ne l'est que pour un schéma.
- **Mise en forme de l'ingestion** : un changement d'un mapper qui modifie les entrées
  stockées change l'entrée de l'empreinte, sans changer de schéma. Il impose de réingérer
  l'en_US de toutes les versions (ou de neutraliser ce changement dans la projection), puis de
  reconstruire l'index (`--rebuild`). Nouveau piège dans `pieges.md` (lot L1).
- Le catalogue en_US répond pour les 21 langues, comme aujourd'hui pour les canoniques. Une
  correction de traduction seule ne crée donc pas d'état. C'est accepté.
- Une icône redessinée sous le même nom de fichier ne crée pas d'état non plus (v2 possible :
  le hash du blob, déjà adressé par contenu).
- **Limite connue** : dans quelques versions anciennes sans `championFull`, la fiche d'un
  champion dont le fichier répond 403 se réduit à son résumé (sorts, passif, lore et skins
  vides). L'empreinte y voit tout disparaître puis revenir : deux états de plus. Accepté.

### 3.2 L'index des états

- **Deux tables** (migration additive du lot L0) :
  - `catalog_state_generation`, une ligne par construction : id, schéma, première et dernière
    version couvertes, `built_at`, `is_current` (une seule génération courante par schéma),
    les comptes de la ligne de synthèse ;
  - `catalog_entity_state`, une ligne par état : génération, ressource, clé d'entité, version
    ouvreuse, dernière version (nulle tant que l'état est ouvert : la lecture prend la fin de
    la couverture), `closed_at` (nul si et seulement si l'état est ouvert), empreinte, chemin
    (valeur complète de `CanonicalPath` dans la version ouvreuse, par exemple
    `items/3371-molten-edge`), version ouvreuse de l'état précédent, `is_return`, clés des
    champs changés (`text[]`, § 3.4), et des clés numériques de tri des versions si le SQL
    trie.

  Pas de colonne « clos par » : les états clos par la promotion de V sont les états clos dont
  la dernière version est comprise entre l'ancienne fin de la couverture (incluse) et V
  (exclue). Environ 30 000 lignes par génération.
- **Couverture** : la plus longue suite de versions listées consécutives dont les jeux en_US
  sont stockés, qui se termine au dernier patch promu, dans l'ordre de `PatchVersion`. Une
  version en absence définitive chez Data Dragon compte comme non listée. Une version plus
  ancienne que le début de la couverture est hors de l'index (règle résiduelle, § 3.3) ; une
  version listée après le dernier patch y entre à sa promotion. La construction journalise
  le début de la couverture dans sa ligne de synthèse. En prod, chaque version listée est
  stockée : la couverture est complète, et le § 4 le vérifie avant la bascule.
- **Commande** `catalog states build` (`ICliCommand` sous `Cli/Catalog/`), options
  `--rebuild` et `--ingest-missing` :

  | Situation | Ce qu'elle fait |
  |---|---|
  | La génération courante du schéma compilé couvre déjà [début … dernier patch] | rien |
  | Seule la fin de la couverture a avancé | rattrapage vers l'avant, dans la génération courante |
  | Le début a bougé, le schéma diffère, ou `--rebuild` | reconstruction complète dans une **nouvelle** génération, puis bascule de `is_current` en une transaction et suppression de l'ancienne |

  - Une reconstruction garde le `closed_at` des états dont la ressource, la clé, l'ouvreur, la
    dernière version et l'empreinte n'ont pas changé ; les autres prennent la date de la
    reconstruction.
  - Elle lit un catalogue en_US à la fois (jeux de données lus directement, pas par la LRU),
    une transaction par version, idempotente, reprenable, une ligne de synthèse par lot.
  - Elle ne télécharge rien, sauf avec `--ingest-missing` (en_US seulement).
  - L'API ne lit que la génération courante du schéma qu'elle compile : une reconstruction
    écrit à côté et ne gêne jamais l'image en service.
- **Extension** : une seule routine sert à la commande, à la promotion et à la
  réconciliation. L'index s'étend de la fin de la couverture jusqu'à V, version listée par
  version listée (une version que la promotion a sautée est couverte quand même). Chaque
  version est comparée à la version listée juste avant :
  - un état dont l'empreinte ne change pas s'étend ;
  - un état qui change ou disparaît se clôt (`closed_at`) ;
  - un nouvel état s'ouvre pour une entité changée, nouvelle ou de retour.

  À la promotion et en réconciliation, un jeu en_US intermédiaire qui manque est ingéré
  (en_US seulement ; la promotion tourne là où la sortie vers Data Dragon est permise). Si
  l'ingestion échoue, l'étape échoue et la réconciliation reprend.
- **Verrou** : toute écriture prend le verrou consultatif `catalog:states`. Pris, l'écrivain
  réessaie quelques secondes, puis laisse la main à la réconciliation. Une transaction par
  version.
- **Réconciliation** : une tâche périodique (`PeriodicJob`, ADR 0003) sous `Workers/Catalog/`,
  toutes les 10 minutes, sur une instance à la fois. Elle étend l'index jusqu'au dernier patch
  promu. Elle ne garantit pas de passage au démarrage.
- **Copie en mémoire**, une par processus, qui se recharge d'elle-même : un singleton charge
  paresseusement la génération courante (un seul chargement à la fois), relit au plus toutes
  les `VersionsLifetime` un repère peu coûteux (id de génération et fin de couverture), et
  recharge quand il a changé ; une écriture locale l'invalide aussitôt. Elle ne dépend jamais
  du processus qui a promu (la commande `ingest` peut promouvoir) et marche sans les workers
  (tests). Copie pas encore chargée et Postgres injoignable : 503 avec `Retry-After`, une
  ligne de journal et un compteur.
- **Lectures**, interface figée au lot L0 avec un double pour les tests unitaires, utilisée
  par L3, L4 et L6 :
  - les versions couvertes, dans l'ordre ;
  - l'état à (ressource, clé, version) ; les états d'une entité ;
  - les ouvreurs des états clos ouverts à V ;
  - si V a changé quelque chose (un état s'ouvre à V ou une entité y est retirée), et les
    retraits à V ;
  - les états clos par la promotion de V ;
  - les données de `lastmod` par version (§ 3.6).

### 3.3 La règle des canoniques

Le **dernier patch** qui décide d'une canonique est celui du lecteur de catalogues
(`ICatalogReader.GetLatestAsync`, comme `CatalogContext`), jamais celui de l'index. Une page à
cette version est toujours sa propre canonique (URL courte), quel que soit l'index.

| Page | Canonique | `robots` | hreflang, JSON-LD | Sitemap |
|---|---|---|---|---|
| Fiche du dernier patch (URL courte) | elle-même | `index, follow` | oui | `latest.xml` |
| Ancien patch couvert, **ouvreur d'un état clos** | elle-même | `index, follow` | oui (21 langues, même version) | `{version}.xml` de l'ouvreur |
| Ancien patch couvert, état clos, autre version | l'ouvreur de l'état, même langue | `index, follow` | non | non |
| Ancien patch couvert, **état ouvert** | l'URL courte | `index, follow` | non | non |
| Ancien patch après la fin de la couverture (index en retard sur le lecteur) | l'URL courte, comme l'état ouvert ; classe `revisable` | `index, follow` | non | non |
| Version hors couverture : plus ancienne que son début, listée après le dernier patch, ou avant toute promotion | **règle résiduelle** : l'URL courte au chemin du dernier patch si son catalogue en_US épinglé tient l'entité (`PageOf`), sinon la page elle-même | `index, follow` | seulement si elle est sa propre canonique | non |
| Liste d'un ancien patch | la liste nue du dernier patch (inchangé) | `index, follow` | non | non |
| Page de patch du dernier patch `/{locale}/changes` | elle-même | `index, follow` ; `noindex, follow` si le patch n'a rien changé | oui si indexable | `latest.xml` si indexable |
| Page de patch d'une version passée (§ 3.5) | elle-même | idem | oui si indexable | `{version}.xml` si indexable |

- **`canonical` est toujours présent et non nul** sur une fiche (contrat L0 : requis). Le
  front retire `UNINDEXED`, la branche `null` de `canonicalOf` et les `?? null` des quatre
  constructeurs de `<head>`.
- La règle par état et la règle résiduelle vivent dans le **même service** de l'API, seule
  source de la canonique des fiches. Avant toute promotion, il n'y a pas de dernier patch : la
  page est sa propre canonique.
- **Une entité retirée** n'est plus un cas particulier. Son dernier état est un état clos
  comme un autre, indexé à son ouvreur, et ses états antérieurs aussi. Le `noindex, follow`
  de ses fiches antérieures disparaît.
- L'API garde la forme de `canonical` (`{ version, path }`, `version` nulle pour l'URL
  courte).

### 3.4 Ce que montre une fiche

Le lot L0 fixe les records exacts du contrat ; leur contenu est décidé ici.

| Champ | Présent | Contenu |
|---|---|---|
| `canonical` | toujours | § 3.3 |
| `state` | nul seulement hors de la couverture | version ouvreuse, dernière version (nulle tant qu'il est ouvert), `isOpen`, `isOpener`, `kind` (`new`, `changed`, `returned`), clés des champs changés, lien canonique de l'état |
| `history` | toujours (vide hors de la couverture) | les états de l'entité jusqu'à celui de la page inclus, du plus récent au plus ancien ; tous sur l'URL courte. Pour chacun : plage (ouvreur, dernière version ou nulle), `kind`, champs changés (libellés seuls), lien canonique |
| `next` | état clos seulement | `{ kind: changed \| removed, version, link? }` : pour `changed`, l'URL **versionnée** de l'ouvreur suivant |
| `presentInLatest` | page épinglée | l'entité existe au dernier patch au moment du rendu |
| `changes` | ouvreur, et URL courte quand l'état ouvert commence au dernier patch | `kind`, version comparée, changements par champ |

1. **« Ce qui a changé dans ce patch »** :
   - on compare au catalogue de la **dernière version de l'état précédent**, un catalogue de
     plus que celui de la page, retour compris (jamais trois) ;
   - seuls les champs que l'index marque changés (clés en_US) sont rendus, avec les valeurs
     de la langue de la page. Un champ dont les deux valeurs localisées sont égales s'affiche
     quand même, avec sa seule valeur « après ». Le bloc, l'historique et la page de patch
     disent donc toujours la même chose ;
   - pour une valeur courte (nombre, coût, portée, codes) : les deux valeurs. Elles voyagent
     en chaînes invariantes, l'OpenAPI refusant les unions `number | string` ; le front
     formate les nombres selon la locale. Les codes (modes, cartes, étiquettes, ressource…)
     voyagent en codes, traduits par les tables du front quand elles existent, montrés tels
     quels sinon ;
   - pour un texte : la différence sur la forme `PlainText`, découpée en mots (blancs et
     ponctuation), ou en éléments de texte (`StringInfo`, sans paquet) pour ja_JP, zh_CN,
     zh_TW et th_TH. Les segments `{ kind: same | removed | added, text }` sont rendus en
     texte (`<del>`, `<ins>`), jamais en `innerHTML` ;
   - pour une entité nouvelle : « Nouveau dans ce patch » ; pour un retour : « De retour »,
     comparé à l'état d'avant l'absence.

   **Clés des champs** : un ensemble fermé de gabarits (énumération `ChangeField`), avec un
   sujet facultatif (emplacement de sort, statistique, id de rune, id de skin). En base, un
   `text[]` de clés pointées (`spell.q.cooldown`, `stat.hp`, `rune.8005.longDesc`, `skins`).
   Dans l'API, `{ field, subject?: { kind, key, name } }`, le nom du sujet dans la langue de la
   page. Un libellé i18n par gabarit ; les statistiques reprennent les libellés `stat.*`
   existants, une clé par statistique. Les libellés vivent dans le catalogue racine
   (`public/i18n/{locale}.json`), lu par les blocs et par les deux chaînes de `<head>`.

   **Moteur** : pur, dans `LoDb.Domain` : `(avant?, après, langue, clés changées) →
   changements`. Il ne sait rien des versions : « nouveau » veut dire `avant` nul. Le retour
   et le choix de l'état comparé viennent de l'index.

   **Catalogue de comparaison pas encore chargé** : sur l'URL courte, la fiche sort sans le
   bloc (classe `latest`, 30 minutes), ce qui est acceptable pour le dernier patch. Un ouvreur
   épinglé ne sort jamais sans son bloc : 503 avec `Retry-After`, jamais stockée.
2. **« Inchangé depuis le patch X »**, sur toute page couverte qui n'est pas un ouvreur, URL courte
   comprise quand l'état ouvert a commencé avant le dernier patch. Le lien mène à l'URL
   **versionnée** de l'ouvreur, sauf si l'ouvreur est le dernier patch : il mène alors à
   l'URL courte. Pas de bloc de différences.
3. **« Historique »** remplace « Dans les patchs précédents » (6 versions) :
   - chaque état de l'entité jusqu'à celui de la page inclus, du plus récent au plus ancien ;
     l'historique complet sur l'URL courte ;
   - pour chacun : sa plage et les champs qu'il a changés (libellés seuls), avec un lien vers
     sa canonique ;
   - les versions s'écrivent entières, telles que Data Dragon les liste : « patchs 11.5.1 à
     11.9.1 », « patch 11.5.1 » pour un état d'une seule version. La plage de l'état ouvert
     ne nomme pas le dernier patch : « depuis le patch 11.5.1 ». Une page révisable ne dépend
     ainsi d'aucune donnée que la promotion change, sauf la clôture de son propre état ;
   - sur une page figée, en plus, l'état suivant : « Changé au patch 11.10.1 », lié par son
     URL versionnée (une 301 tant que cette version est le dernier patch : accepté), ou
     « Retiré au patch 11.10.1 », sans lien ;
   - « Dans le dernier patch » s'affiche sur toute page épinglée, figée comprise, quand
     `presentInLatest` est vrai. Une entité retirée plus tard laisse ce lien en 404 sur une
     page figée : accepté.

   **Une page figée ne lie que des URL versionnées**, sauf « Dans le dernier patch » : son
   contenu ne dépend que de son état et de ceux d'avant, et ne change plus (§ 3.7).
4. **Titres et descriptions** :
   - l'ouvreur d'un état clos de plusieurs versions prend le suffixe de plage « (patchs
     11.5.1 à 11.9.1) », nouvelle clé `seo.state_range_suffix`, qui **remplace**
     `seo.versioned_suffix`. Un état d'une seule version garde `seo.versioned_suffix`
     (« (patch 11.5.1) »), qui dit la même chose. Une page n'a jamais deux suffixes ;
   - sa description commence par le résumé de ses changements, `seo.state.summary`
     (« Changé au patch {{ version }} : {{ fields }}. »), trois libellés au plus puis « … »,
     avec une variante pour une entité nouvelle et une pour un retour ;
   - les autres pages gardent leur titre (`seo.versioned_suffix` sur une page épinglée).
5. **JSON-LD** : il suit la canonique, comme aujourd'hui (seulement sur une page qui est sa
   propre canonique).

### 3.5 La page d'un patch

- **Routes** : `/{locale}/{version}/changes`, et `/{locale}/changes` pour le dernier patch.
  - `/{locale}/{dernier}/changes` répond une 301 vers `/{locale}/changes` ; `?version=`
    connue, une 301 dans le chemin.
  - Elle ne lit que `version` et `lang` (variante régionale, canonique sur la langue par
    défaut) ; le pré-routeur retire le reste par une 301. Comme une liste, elle reprend la
    version mémorisée.
  - Front : un nouveau prédicat `isVersionedRoute` (ressource ou `changes`) pour
    `switchContext`, `rememberedTarget` et les liens du chrome ; `isCatalogueRoute` garde son
    sens « ressource » (paramètres possédés, préchauffage).
- **Existence** : une page de patch existe pour chaque version couverte, **sauf la première**,
  qui n'a pas de version précédente. 404 pour celle-là, pour une version hors de la
  couverture et pour une version listée après le dernier patch. `CatalogMeta` donne la plus
  ancienne version qui a une page de patch (ou nulle) : `/patches` ne lie que celles qui
  existent.
- **Contenu** : ce que V a changé par rapport à la version listée avant elle, groupé par
  ressource :
  - les entités dont un état s'ouvre à V : changées (avec leurs champs), nouvelles, et de
    retour (un groupe à part) ;
  - les entités retirées à V.

  Noms et icônes viennent du catalogue de V, dans la langue de la page ; ceux des entités
  retirées, du catalogue de la version précédente (deux catalogues quand V retire quelque
  chose). Chaque entrée porte un lien de la forme de `CanonicalLink` : la fiche à V (l'URL
  courte quand V est le dernier patch), ou, pour une entité retirée, l'ouvreur de son dernier
  état.
- **API** : `GET /api/catalog/{version}/{lang}/changes`.
- **`hasChanges`**, calculé en un seul endroit depuis l'index : un état s'ouvre à V ou une
  entité y est retirée. Sinon, `noindex, follow`, hors des sitemaps et d'IndexNow.
- **`<head>`** : sa propre canonique (la page épinglée passe `{ version, path: 'changes' }`
  explicitement, sinon `canonicalOf` la rabattrait sur le dernier patch) ; hreflang et
  JSON-LD seulement si elle est indexable. JSON-LD : `BreadcrumbList` (accueil › historique
  des patchs › patch) et `ItemList` des entrées. Un `kind` générique `noindex` remplace le
  réemploi de `donation-return`.
- **Cache** : la page de patch d'un ancien patch est figée (`archived`) ; celle du dernier
  patch, `latest`. La seule page de patch qu'une promotion change est celle de la version qui
  quitte le dernier patch : elle entre dans le stockage avec l'unité de cette version, pour la
  première fois.
- **`/{locale}/patches`** lie chaque version à sa page de patch d'abord, quand elle existe,
  puis à ses quatre listes comme aujourd'hui.

### 3.6 Les sitemaps et `llms.txt`

- **`/sitemap.xml`** : les 21 `latest.xml`, puis les 21 `{version}.xml` de chaque version
  **couverte** plus ancienne que le dernier patch dont le fichier **n'est pas vide** (page de
  patch indexable, ou au moins un ouvreur d'état clos). « ~8 440 entrées » devient un
  plafond, sous la limite de 50 000. Plus de fenêtre : `LoDb__Seo__SitemapVersionWindow`
  disparaît.
- **Calcul** : `{version}.xml` et l'index se lisent dans la copie en mémoire de l'index des
  états **seule**, sans catalogue ni budget robot (le chemin est stocké, et le slug est le
  même dans les 21 langues). 503 seulement tant que la copie n'est pas chargée.
- **`{version}.xml`** : la page de patch de la version (si `hasChanges`), puis les ouvreurs
  des états clos ouverts par cette version. Un état encore ouvert y entre à sa clôture.
- **Réponses** de `/sitemaps/{locale}/{version}.xml` :
  - le dernier patch : 301 vers `latest.xml` (inchangé) ;
  - une version plus récente que le dernier patch, ou hors de la couverture : 404 ;
  - une version couverte au fichier vide : 200, `urlset` vide, non annoncée par l'index ;
  - les 301 des formes de l'ancien site restent.
- **`latest.xml`** : inchangé, plus `/{locale}/changes` quand le dernier patch a changé
  quelque chose, daté par la dernière promotion comme les URL courtes.
- **`lastmod`** :
  - celui d'un ouvreur est son `closed_at`, le moment où sa page devient indexable. Pour
    l'historique reconstruit, c'est la date de la reconstruction, gardée d'une
    reconstruction à l'autre (§ 3.2) ;
  - celui d'une page de patch est le plus tardif de deux instants : le `built_at` de la
    génération, et la promotion de la première version promue après elle ;
  - celui d'une entrée de l'index est le plus récent de son fichier ;
  - aucune date de sortie de patch n'est devinée : Data Dragon n'en donne pas.
- **Cache** : `max-age=3600` comme aujourd'hui, puisque les fichiers grandissent à chaque
  promotion. Le débit de `/sitemaps/` reste borné comme aujourd'hui (§ 5 surveille les 429).
- **`llms.txt`** : une ligne « Game data » pour `/en/changes` ; celle de `/en/patches` devient
  « Every game patch, each linking its page of changes and its four lists as they were ».
  Les commentaires périmés de `SitePages.PatchHistory` et de `RobotsTxt` se corrigent dans le
  même lot.

### 3.7 Cache et stockage : pages figées et pages révisables

- **Classes** :

  | Page | Classe | `Cache-Control` |
  |---|---|---|
  | Figée (état clos) | `archived` | inchangé (un an) |
  | Révisable (état ouvert, ou état inconnu sur une version épinglée) | **`revisable`** | `public, max-age=3600, s-maxage=3600, stale-while-revalidate=3600` |

  La classe `revisable` est courte pour qu'une page d'état clos, recalculée, remplace la copie
  périmée en deux heures environ ; une relecture du stockage coûte peu. Elle représente 5 à
  15 % des pages épinglées (§ 1.2).
- **Le SSR choisit la classe selon l'état** rendu par l'API : épinglée et état ouvert,
  `revisable` ; épinglée et état clos, `archived` ; épinglée et état nul ou inconnu,
  `revisable`, jamais `archived` ; sinon `latest`. Seul le résolveur des fiches passe l'état ;
  les listes, l'accueil et la page de patch gardent la règle par version (`cacheClassOf`).
- **Réponses JSON de l'API** : en-têtes inchangés (`Frozen`, un jour, pour un ancien patch).
  Le décalage d'un jour au plus dans le navigateur sur une réponse révisable est accepté :
  nginx ne met pas l'API en cache.
- **Le stockage garde les deux classes** :
  - `RenderVerdicts` et `PrerenderHeaders` admettent `archived` et `revisable` comme
    stockables ; une marque inconnue reste `Skipped`. `cache-class-mark.ts` marque les deux ;
    `PageObjects` garde déjà la `Cache-Control` du rendu.
  - nginx, pour les **pages** : le cache chaud suit la `Cache-Control` de l'objet
    (`s-maxage`, `stale-while-revalidate`) ; `proxy_cache_valid 200 365d` ne vaut plus qu'en
    repli, pour un objet sans `Cache-Control`. Les **bundles** gardent l'année forcée : les
    deux lignes sortent de `snippets/pages-store/request.conf` vers `pages.conf` et
    `bundles.conf`. L'en-tête envoyé au client se règle par une `map` sur le build et la
    `Cache-Control` : la valeur `revisable` pour un objet révisable, `archived` par défaut.
    Aujourd'hui, nginx impose `archived` à toute réponse du stockage
    (`$lodb_pinned_cache_control`) et la garde un an.
  - La `map` stricte des chemins du stockage admet `changes`.
- **Pages des états clos, recalculées page par page.** Une unité (version, langue) couvre
  toute une version : elle ne sait pas refaire des fiches précises. Une file par page,
  `prerender_revision` (chemin, epoch, état, essais, `enqueued_at`, `written_at`, raison ;
  clé primaire (chemin, epoch) ; schéma du lot L0) :
  - **Producteur** : après l'extension de l'index (écouteur de l'étape `Prerender` et
    rapprochement du précalcul, de façon idempotente), les chemins de toutes les versions de
    chaque état nouvellement clos × 21 langues, sauf la version qui quitte le dernier patch
    (son unité la couvre). Ils entrent pour l'epoch **servie** et pour l'epoch qu'écrit la
    passe active si elle diffère. Une marque (le `closed_at` de la dernière clôture traitée,
    petite table ou colonne, schéma du lot L0) rend le producteur idempotent entre processus.
  - **Worker** : il reprend `DemandPageWriter` (build copié d'abord, mêmes verdicts, mêmes
    clés) et partage les places de rendu comme la file de la demande.
  - Une page n'est rendue qu'une fois que l'API qui sert lit un index qui couvre sa clôture
    (repère de l'index lu par l'API, ou le délai de stabilisation existant).
  - Elle **ne dépend pas** de `AutoEnqueueOnPromotion`, qui ne commande plus que l'unité de la
    version qui part : elle tourne dès que le stockage est configuré, staging compris.
  - Volume : ~24 000 pages par promotion au plus (§ 1.2), ~27 minutes à 900 pages par minute.
  - Le pupitre montre la file (en attente, écrites, en échec) et, par version, les pages
    révisables et figées attendues, calculées depuis l'index. Aucun compteur par classe n'est
    ajouté aux unités.
- **La page de patch dans la passe** : `changes` n'est pas un type de page de la passe. Toute
  unité dont le périmètre couvre tous les types la rend ; tout filtre de type l'écarte ;
  `TypeMask` reste à quatre bits. L'ordre de `VersionPages.Of` est figé : les listes, puis
  `changes`, puis les entités. Comme cet ordre change sous le curseur des unités, toute passe
  active est annulée avant le déploiement. La file de la demande admet `changes`
  (`PAGE_TYPES` de `refused-page-path.ts`, `DemandPath`, `ServedUnits`).
- **Ouvreurs d'abord** : un nouveau champ de `PassScope`, `pages: "openers"`. Chaque unité y
  rend, dans l'ordre de `VersionPages`, les ouvreurs de **tout** état (ouvert ou clos) à sa
  version, plus `changes` : un ensemble stable une fois la version indexée, donc un curseur qui
  reste valable. Ce champ rend `IsWhole` et `HasEveryPageType` faux, pour qu'une promotion ne
  rejoigne jamais une telle passe. `PassScopeJson` reste lisible par l'image précédente (sinon,
  annuler la passe avant un retour arrière). Une passe entière `{}` suit. Le formulaire de
  lancement du pupitre propose ce périmètre.
- **Annuler une passe** existe déjà (`POST /api/admin/prerender/passes/{id}/cancel`, bouton
  du pupitre, audité) : rien à construire.
- **Nouveau schéma d'empreinte** : reconstruire l'index, puis pré-calculer de nouveau les pages
  dont l'état change. Peu nombreuses : dans l'epoch servie, puis monter `LODB_ARCHIVE_EPOCH`
  (purge du cache chaud). Nombreuses : une passe sur l'epoch N+1, puis basculer
  `LODB_ARCHIVE_SERVE_EPOCH`.

### 3.8 À la promotion d'un patch, dans cet ordre

L'ordre est porté par les écouteurs eux-mêmes : `IPromotionListener` gagne
`PromotionStage Stage { get; }` (énumération de `LoDb.Ingestion/Pipeline/Versions` :
`StateIndex = 0`, `Prerender = 1`, `Announce = 2`), et `VersionIngestion.NotifyAsync` les
trie par un tri stable (`OrderBy(Stage)`). L'ordre d'enregistrement ne compte plus
(aujourd'hui, `AddSeo` passe avant `AddAdmin` : IndexNow avant le pré-calcul) ; `Program.cs`
ne change pas.

1. **`StateIndex`** : l'index s'étend jusqu'à V (§ 3.2).
2. **`Prerender`** : l'unité de la version qui quitte le dernier patch, comme aujourd'hui
   (sous `AutoEnqueueOnPromotion`, avec l'attente de `CatalogPages.Settling`), page de patch
   comprise ; puis le producteur de la file des états clos (§ 3.7), sans condition.
3. **`Announce`** : IndexNow, par lots de 10 000 URL au plus :
   - toutes les URL courtes, comme aujourd'hui (`SitePages.PatchBound` et toutes les entités
     listées) ;
   - les ouvreurs des états que cette promotion a clos, dans les 21 langues ;
   - `/{locale}/changes` et `/{locale}/{version précédente}/changes`, quand ils ont changé
     quelque chose.

   Sans clé mais avec `CanonicalOrigin`, une seule ligne `seo.indexnow.planned` donne les
   comptes par catégorie, sans envoi. Accepté : Bing peut lire l'ancienne copie révisable
   d'un ouvreur jusqu'à son nouveau rendu (§ 3.7, deux heures environ après la file).

- Les étapes 2 et 3 lisent **dans l'index** ce que la promotion a changé : cela marche aussi
  pour la réconciliation. Si l'index ne couvre pas V, elles journalisent et s'arrêtent, sans
  rien calculer sur un index périmé.
- Un écouteur qui échoue est journalisé sans annuler la promotion, comme aujourd'hui. La
  réconciliation répare l'index, le rapprochement du précalcul alimente ensuite la file des
  états clos ; IndexNow n'est pas rejoué (les sitemaps datés prennent le relais).
- **Aucun envoi IndexNow à la bascule** (§ 4) : les sitemaps annoncent l'historique.

### 3.9 Les redirections héritées

L'amendement du 2026-10-16 suit la nouvelle règle : une ancienne URL qui nomme une version
mène directement à la canonique de sa cible.

- L'id se résout comme aujourd'hui : dans le catalogue du dernier patch, sinon dans le
  catalogue stocké de la version nommée ; le nom d'un arbre de runes se traduit en id par le
  catalogue du dernier patch.
- L'index tranche ensuite à la version nommée : état clos, son ouvreur ; état ouvert, l'URL
  courte.
- Pas d'état à cette version : l'URL courte si le dernier patch porte l'id, sinon la 302 vers
  la liste reste.
- Version listée après le dernier patch : la règle du 2026-10-16, inchangée.
- Sans version nommée, une entité retirée mène à l'ouvreur de son dernier état, au lieu d'une
  404.
- Une liste mène toujours à la liste nue du dernier patch. Le reste ne change pas (503 tant que
  le catalogue du dernier patch n'est pas lu, choix de la locale, 301 gardée une heure).

### 3.10 Ce qui disparaît

- `LoDb__Seo__SitemapVersionWindow` : l'option, sa validation, la fenêtre de
  `SitemapLocations.Index`, la clé transmise par `compose.deploy.yaml`, `SitemapWindowTests`,
  et une éventuelle ligne des `.env` (par `coffre`).
- `CanonicalReferences` et `CanonicalReader`, leur enregistrement (`CatalogRegistration.cs`)
  et leurs tests, qui ne lisaient que les catalogues du dernier patch et de la version
  suivante, faute d'un index entité → versions. Leur branche « après le dernier patch » passe
  dans la règle résiduelle (§ 3.3).
- Le `noindex, follow` des fiches antérieures d'une entité retirée : `UNINDEXED`, la branche
  `null` de `canonicalOf` et les `?? null` des quatre constructeurs de `<head>`.
- Le bloc « Dans les patchs précédents » : tout le dossier actuel
  `features/catalogue/shared/codex/history/` (`previous-patches.ts`,
  `previous-versions-of.ts`, `previous-patches.spec.ts`, `PREVIOUS_PATCH_COUNT`). La clé
  racine `detail.history.title` reste, mais son texte devient « Historique » dans les 21
  langues.
- Les `urlset` vides annoncés par l'index (un fichier vide reste servi, § 3.6).
- L'`archived` imposé par nginx à toute réponse du stockage et l'année imposée au cache chaud
  des pages (repli seulement, § 3.7).

### 3.11 Ce qui ne change pas

- La grammaire d'URL, sauf le segment `changes` ajouté.
- La 301 de l'URL versionnée du dernier patch.
- La canonique des listes et les variantes régionales (`?lang=`).
- Le schéma des clés du stockage (`pages/{epoch}/{locale}/{version}/{ressource}[/…]`) : la
  page de patch y entre comme un chemin de plus (`…/{version}/changes`), que `VersionPages`
  ajoute aux pages d'une unité.
- La file de la demande, sauf qu'elle admet `changes` ; le rythme de la passe ; `TypeMask`.
- Les en-têtes de cache des réponses JSON de l'API, le cache des sitemaps et leur débit.
- Le plan Cloudflare, `robots.txt`, et la canonique des versions listées après le dernier
  patch (règle résiduelle, la même que le 2026-10-14).

## 4. Mise en production : une seule bascule

La nouvelle règle remplace l'ancienne dans le même changement : ni interrupteur, ni période où
les deux règles coexistent. Le déploiement qui la porte est la bascule. Toute valeur des
`.env` passe par `coffre`, puis par le secret `ENV_STAGING` ou `ENV_PROD` ; elle vaut au
déploiement qui suit.

### 4.1 L'index se construit avant que l'API serve

- **Service `states`** défini dans `compose.yaml`, sur le modèle de `migrate` :
  `build: *api-build`, `command: ["catalog","states","build"]`, `<<: *hardening`, `tmpfs`,
  réseaux `default` et `data`, `ddragon:/srv/storage:ro`, connexion `lodb_app`,
  `restart: "no"`, après `migrate` (`service_completed_successfully`). `api` dépend de lui
  (`service_completed_successfully`) : la pile locale et les E2E de la CI ont donc un index.
- `compose.deploy.yaml` le surcharge : image, `build: !reset null`, secret
  `LODB_APP_DB_PASSWORD`, limites (512m, 0.5 CPU, 256 pids, `cpu_shares` au-dessus de 128 ; à
  mesurer). `served-overlays.test.mjs` (`SERVICES`) et les contrôles de secrets le connaissent.
- **`_deploy.yml`** lance `docker compose run --rm states` juste après
  `docker compose run --rm migrate`, **avant** `maintenance_on`. L'ancienne pile sert pendant
  ce temps (tables additives) ; un échec arrête le job pendant qu'elle sert encore. Dans
  `up`, le service ne fait plus rien.
- La première fois, la commande lit les catalogues en_US de toutes les versions couvertes
  (durée mesurée au staging) ; ensuite, elle rattrape ou ne fait rien. Au déploiement, elle ne
  télécharge jamais : pas de `--ingest-missing`, `ddragon` monté en lecture seule.

### 4.2 Staging

Une fusion dans `dev` déploie le staging d'elle-même : c'est la bascule du staging. Avant la
fusion :

- vérifier la couverture et ingérer l'en_US des versions listées qui manquent (boucle
  `ingest --version <v> --languages en_US`, `--latest` étant plafonné à 50) ;
- `ENV_STAGING` : `LODB_ARCHIVE_SERVE_EPOCH` sur une epoch neuve (les pages stockées du
  staging, gardées 14 jours, portent l'ancienne règle).

Après le déploiement :

- une passe manuelle « ouvreurs d'abord » sur un petit périmètre, dans cette epoch ;
- vérification des `<head>` de chaque ligne du § 3.3, des sitemaps, de
  `tools/routing/check-urls.sh` et des E2E ;
- à la prochaine promotion réelle du staging (sans clé IndexNow), la ligne
  `seo.indexnow.planned`.

### 4.3 Prod, dans cet ordre

1. **Couverture** : chaque version listée a ses jeux en_US ; sinon, la même boucle
   d'ingestion en_US.
2. **Annuler la passe active de l'epoch 1** au pupitre (Annuler,
   `POST /api/admin/prerender/passes/{id}/cancel`) : ses pages portent l'ancienne règle, et
   l'ordre de `VersionPages` change au déploiement (§ 3.7).
3. **Relevé de départ** (§ 5).
4. **`ENV_PROD`** : `LODB_ARCHIVE_SERVE_EPOCH=2`, rien d'autre. Changer l'epoch servie purge
   déjà le cache chaud (sa clé la porte) : `LODB_ARCHIVE_EPOCH` ne bouge pas. Aucune variable
   ne règle d'epoch d'écriture : c'est celle de chaque passe, choisie à son lancement.
   `AutoEnqueueOnPromotion` reste faux jusqu'à la fin de la passe complète, comme aujourd'hui ;
   la file des états clos n'en dépend pas.
5. **Déploiement** (`test` → `main`) : `states` construit l'index avant la page de
   maintenance, puis l'API sert la nouvelle règle.
6. **Au pupitre, sur l'epoch 2** : une passe `{"pages":"openers"}`, puis une passe entière
   `{}`.
7. **Aucun envoi IndexNow** : les sitemaps annoncent l'historique (`lastmod` = date de la
   reconstruction) ; les premiers ouvreurs partent à la promotion suivante.
8. **Après le déploiement**, un commit de documentation date la bascule et passe la règle à
   « en vigueur » (§ 7).

**Durée et exposition.** L'epoch 2 est vide à la bascule. À 900 pages par minute, il faut
~11 h pour les ~600 000 ouvreurs et ~3 jours pour le reste. D'ici là, une page absente du
stockage part à la voie épinglée du SSR, au budget borné (503 avec `Retry-After` au-delà),
puis à la file de la demande, qui la pré-calcule en priorité. C'est accepté par la décision
d'une seule bascule : relire l'epoch 1 avant le SSR ferait coexister les deux règles.

**Suivi des 503** : la part de 503 servies à Googlebot (`asn:"15169"`) sur les pages
épinglées se suit dans le journal de l'edge **pendant** la passe, pas seulement au relevé de
départ. Seuil d'action : plus de 10 % sur une heure. Au-delà, baisser le budget des rendus
épinglés des visiteurs, pour rendre ce temps à la passe et à la file de la demande. Si la part
dépasse déjà ce seuil au relevé de départ, la bascule est reportée.

### 4.4 Retour arrière

1. Annuler la passe active de l'epoch 2 au pupitre : l'image précédente lirait son périmètre
   `openers` plus large qu'il n'est.
2. `ENV_PROD` : `LODB_ARCHIVE_SERVE_EPOCH=1`, et `LODB_ARCHIVE_EPOCH` monté à une valeur
   explicite (la valeur actuelle, lue par `coffre`, plus un), pour ne pas reprendre les copies
   du cache chaud gardées avant la bascule.
3. `git revert -m 1` de la fusion, par une PR vers `dev`, puis le staging, puis `test` →
   `main` : le chemin normal, qui prend des heures.
4. Les versions promues depuis la bascule n'ont été écrites que dans l'epoch 2 : elles
   retombent sur le SSR et la file de la demande. Accepté. Les tables de l'index restent
   (additives) ; l'image précédente les ignore.

### 4.5 Retrait de l'epoch 1

Deux semaines après la bascule, relancer la configuration du stockage
([`tools/prerender/README.md`](../../tools/prerender/README.md), § 4) :
`ENVIRONMENT=prod BUCKET=lodb-pages-prod SERVED_EPOCH=2 WRITE_EPOCH=2 RETIRED_EPOCHS=1 …
bash tools/prerender/scaleway-bucket.sh`. Elle pose la règle `retire-pages-1` ; l'epoch met au
moins 9 jours à disparaître.

## 5. Mesure

**Relevé de départ**, le jour de la bascule, dans la Search Console et le journal de l'edge :

- les pages indexées ;
- les impressions et les clics par type d'URL : courtes, versionnées (regex
  `/\d+\.\d+\.\d+/`), pages de patch (`/changes`) ;
- « Explorée, actuellement non indexée », « Autre page avec balise canonique correcte »,
  « Google n'a pas choisi la même URL canonique que l'utilisateur » ;
- les statistiques d'exploration ;
- la part de 503 servies à Googlebot (`asn:"15169"`), suivie ensuite pendant la passe
  (§ 4.3) ;
- la part de 429 servies à Googlebot sur `/sitemaps/`.

**Points de contrôle** : 2, 4 et 8 semaines après la bascule.

**Leviers de réglage** :

- Des ouvreurs restent massivement « explorés, non indexés », ou Google choisit une autre
  canonique entre deux états voisins : on rend l'empreinte moins fine (sans les skins
  d'abord, puis sans les champs mineurs). Ça veut dire monter le schéma, reconstruire l'index
  et pré-calculer de nouveau les pages changées (§ 3.7).
- « Autre page avec balise canonique correcte » monte : c'est attendu (les versions d'un état
  qui ne sont pas l'ouvreur).
- L'exploration se perd dans les anciennes versions pendant que les URL courtes reculent : on
  scinde les sitemaps de version, ou on classe l'index par ancienneté.
- Les 429 sur `/sitemaps/` montent : on relève `LODB_SITEMAPS_RATE`, sans toucher au code.

Les résultats s'écrivent ici (§ Résultats, à créer) et dans l'amendement de l'ADR 0005, à
commencer par les comptes réels de la première construction (états par ressource, début de
la couverture), qui remplacent les majorants du § 1.2.

## 6. Lots

Chaque lot livre dans le même commit son code, ses tests, et les documents et notes de
changelog du comportement qu'il change (§ 7), selon `AGENTS.md`. Un test existant que le lot
casse, E2E compris, se corrige dans ce lot. Les lots arrivent sur la même branche, en une PR
(§ 10).

Le contrat du lot L0 a réparti les fichiers entre les lots pour qu'ils travaillent en
parallèle : le tableau dit ce que chacun a livré, l'intégration ce qu'elle a ajouté en les
fusionnant (L1 à L6 dans cet ordre).

| Lot | Contenu livré | Dépend de |
|---|---|---|
| **L0 Contrat** | **Tout le schéma du chantier**, en une migration additive (`20261022103012_CatalogStateIndex`) : `catalog_state_generation`, `catalog_state_version` (les versions couvertes d'une génération, leur ordre et le point de reprise d'une construction), `catalog_entity_state`, `prerender_revision` et `prerender_revision_watermark` (la marque de son producteur). **Contrat OpenAPI** : sur les fiches, `canonical` requis, `state`, `history`, `next`, `presentInLatest`, `changes` ; `GET …/changes` mappé, réponse en bouchon jusqu'à L3 ; `CatalogMeta.oldestChangesVersion` ; énumération `ChangeField` (46 gabarits fermés, sujet facultatif) et ses clés pointées (`FieldKey`). Records sous `Modules/Catalog/States/` et `Changes/`, client régénéré (`npm run api:generate`). **Interfaces** : `IEntityStateIndex` et `EntityStateSnapshot` (lectures du § 3.2) avec un double de test (`InMemoryEntityStateIndex`, `TestStates`) ; `PromotionStage` sur `IPromotionListener`, tri dans `NotifyAsync`, étapes déclarées par les deux écouteurs existants, test d'ordre ; prédicat d'existence partagé (`ListedEntities`, déplacé dans `LoDb.Ingestion`, que `VersionPages` appelle). **Classe `revisable`** côté front (`CacheClass`, `CACHE_CONTROL`) et `cache_of` de `check-urls.sh` | — |
| **L1 Domaine** | Projections et empreintes par ressource, `DdragonText.PlainText`, SHA-256 et `FingerprintScheme`, `ChampionStats` descendu dans `LoDb.Domain` (`ChampionStatRows`), moteur de différences (découpage par langue), purs, dans `LoDb.Domain` ; tests du § 8 (domaine, paires réelles) | L0 |
| **L2 Index** | Couverture, générations, commande `catalog states build`, routine d'extension, écouteur `StateIndex`, verrou, tâche de réconciliation, politique de rechargement de la copie en mémoire, service `states` (`compose.yaml`, `compose.deploy.yaml` dont le retrait de la ligne `LoDb__Seo__SitemapVersionWindow` pour L4, `_deploy.yml` et son commentaire d'ordre, tests de surcouche et de secrets), métriques et lignes de synthèse | L0, L1 |
| **L3 API** | Service des canoniques `StateView` (règle par état et règle résiduelle, `LoDb.Ingestion/Catalog/States/Canonicals/`) ; `StateGateway` remplace `CanonicalGateway` ; champs des fiches, 503 `catalog-pending` d'un ouvreur épinglé sans catalogue de comparaison ; point d'accès des changements ; plus ancienne page de patch dans `CatalogMeta` ; redirections héritées. Documents : invariant SSR d'`AGENTS.md`, paragraphe « Historique des patchs » et 302 héritée de `pieges.md`, notes « Remplacé » du changelog du 2026-10-14 (une fiche par entité) et du 2026-10-16 | L0, L1 ; L2 pour les vraies données (le double avant) |
| **L4 Fichiers SEO** | Index des sitemaps sans fenêtre (fichiers non vides), `{version}.xml` depuis l'index seul, réponses HTTP, `latest.xml`, `lastmod`, IndexNow (contenu, lots de 10 000, lignes `seo.indexnow.planned` et `.unindexed`), `llms.txt` et commentaires ; retrait de l'option `LoDb__Seo__SitemapVersionWindow`, de sa validation et de `SitemapWindowTests` ; notes « Remplacé en partie » du changelog du 2026-10-11 et du 2026-10-12 (pages datées) | L3 |
| **L5 Front** | `<head>` selon le § 3.3 et le § 3.4 (canonique requise, `UNINDEXED` retiré, suffixe de plage, résumé, `kind` `noindex`), blocs « Ce qui a changé », « Inchangé depuis », « Historique » (remplace `codex/history/`), page de patch, route et `isVersionedRoute`, paramètres possédés et pré-routeur, liens de `/patches`, clés i18n des 21 langues (et leur test, `i18n/test/translated-texts.test.mjs`), **classe selon l'état dans le résolveur des fiches** (`entryCacheClassOf`), lignes de `check-urls.sh` (pages de patch, fiches épinglées choisies par l'état) ; notes « Remplacé » du changelog du 2026-10-14 (lien vers le patch actuel) et du 2026-10-21 | L0 ; L3 pour les vraies données |
| **L6 Cache et pré-calcul** | Verdicts et `cache-class-mark.ts`, nginx (cache chaud des pages selon l'objet, `map` de l'en-tête client, `changes` dans la `map` stricte, tests et README de `tools/nginx`, commentaire de `LODB_ARCHIVE_EPOCH` du `Dockerfile`), **`VersionPages`** (`changes`, ordre figé), file de la demande (`changes`), file `prerender_revision` (producteur, worker, marque, pupitre), périmètre `openers` (pupitre compris) ; note « Remplacé » du changelog du 2026-10-20 | L0, L2 |
| **Intégration** | Retrait de `CanonicalReferences`, `CanonicalReader`, de leur enregistrement et de leurs tests ; `EntityStateSnapshot.PromotedBefore`, lu par IndexNow ; le genre `baseline` (domaine, API, front) ; `SitePages.Changes`, seul littéral du segment ; le poids de `states` au staging (`compose.staging.yaml`) ; le commentaire de `LODB_ARCHIVE_EPOCH` de `compose.yaml` ; les remarques de `FieldDiff` ; un champ sans valeur rendu par son seul libellé ; les clés i18n du pupitre ; `PrerenderRevisionWorker` dans `ConventionalWorkersTests` | L1 à L6 |
| **L7 E2E et documentation** | E2E du § 8 ; tous les documents du § 7 que les lots n'ont pas écrits, d'après le code intégré ; entrée de changelog côté joueur ; états « en vigueur dans le code » | L1 à L6 |

## 7. Documentation à mettre à jour avec le code

Chaque document change dans le lot qui change le comportement qu'il décrit. Les notes de
changelog suivent le format de [`docs/changelog/README.md`](../changelog/README.md)
(« Remplacé le AAAA-MM-JJ par … ») ; aucune release n'est publiée avant.

Le contrat du lot L0 n'a laissé aux lots L1 à L6 que les documents du § 6 (invariant
d'`AGENTS.md`, deux paragraphes de `pieges.md`, notes du changelog, `tools/nginx/README.md`) :
le reste de ce tableau a été écrit par le lot L7, d'après le code intégré. Les notes
« Remplacé » citent toutes l'entrée côté joueur
[`2026-10-22-une-page-par-etat-dans-google`](../changelog/2026/2026-10-22-une-page-par-etat-dans-google.md),
qui remplace aussi en partie `2026-10-12-patch-annonce-a-sa-sortie`. Les guides gagnent
deux sections : `configuration.md` § 4.12 (index des états) et § 4.13 (pages révisables et file
des états clos).

| Lot | Documents |
|---|---|
| L1 | `pieges.md` : nouveau piège, un changement de mapper change l'entrée de l'empreinte (§ 3.1) |
| L2 | `configuration.md` § 4.3 (le service `states`) ; `pieges.md` : ordre du déploiement (`states` avant la page de maintenance, écritures en `lodb_app`), ordre des écouteurs ; `architecture.md:88-91` (écouteurs de promotion par étapes) ; `logging.md` et `metriques.md` (événements et métriques de l'index) ; commentaire d'ordre de `_deploy.yml` (lignes 23-25) et `tools/deploy/README.md` |
| L3 | `AGENTS.md`, invariant SSR : la canonique des fiches suit une seule règle, calculée par l'API depuis l'index des états et transmise au SSR ; `pieges.md:87-98` (paragraphe « Historique des patchs », réécrit en entier avec L4 et L5 : ici la canonique et les 301) et `:224-225` (302 héritée) ; `architecture.md:151-154` ; colonne « Code » de `seo-indexabilite.md` ; ADR 0005, notes « Remplacé » dans les amendements du 2026-10-14 (règle unique, entité retirée) et du 2026-10-16 (entité retirée) ; note d'en-tête de `seo-anciens-patchs.md` ; changelog : `2026-10-14-une-fiche-par-entite-dans-google`, `2026-10-16-anciennes-adresses-vers-le-dernier-patch` |
| L4 | `configuration.md:402-403` (fenêtre retirée, contenu d'IndexNow, ligne sans clé) ; `pieges.md:87-98` (index des sitemaps) ; `seo-indexabilite.md:34` ; ADR 0005, amendement du 2026-10-11 (fenêtre, « Rien ne disparaît ») ; `logging.md` (`seo.indexnow.planned`) ; changelog : `2026-10-11-historique-des-patchs`, `2026-10-12-pages-datees-par-patch` |
| L5 | `pieges.md:87-98` (bloc de la fiche) ; ADR 0005, « Dans les patchs précédents » dans les amendements du 2026-10-11 (ligne 225) et du 2026-10-14 (ligne 312) ; changelog : `2026-10-14-lien-vers-le-patch-actuel`, `2026-10-21-lien-dernier-patch-sans-numero` |
| L6 | `pieges.md:444-451` (un an pour toute page d'ancien patch), `:546-556` (epochs), `:608-629` (nginx impose `archived`), et les nouveaux pièges (pages révisables, ordre figé de `VersionPages`, passe annulée avant un déploiement ou un retour arrière) ; `configuration.md:263` (`LODB_ARCHIVE_EPOCH`), `:412` et `:581` (`AutoEnqueueOnPromotion` ne commande plus que l'unité qui part), `:693-707` (bascule d'epoch) ; `pre-rendu-catalogue-cdn.md` (en-tête, lignes 92, 126-130, 308, 377, 942-951, 1108, 1150, 1167 : seule une `200` `archived` stockée, `archived` imposé par nginx, rejet du `noindex` « la règle des canoniques suffit », « les canoniques et les sitemaps ne changent pas ») ; ADR 0011, lignes 95-98, 121, 138 et la note de § nginx ; `logging.md` et `metriques.md` (file des états clos) ; changelog : `2026-10-20-anciens-patchs-sans-attente` |
| L7 | Nouvelle entrée de changelog côté joueur (`type` feat, `scope` full-stack) ; états « en vigueur dans le code » : en-têtes et amendements du 2026-10-22 des ADR 0003, 0005 et 0011, « Règle décidée » de `seo-indexabilite.md`, en-tête de cette spécification, `docs/README.md` |
| Après le déploiement en prod | Un commit de documentation : date de la bascule et « en vigueur » dans la chronologie et la règle en vigueur de `seo-indexabilite.md`, les en-têtes des ADR 0003, 0005 et 0011, `architecture.md:151-154` et cette spécification |

## 8. Tests

Selon [`tests.md`](../guides/tests.md) : le nominal et les cas limites introduits, rien de
plus.

- **Domaine** (`LoDb.Domain.Tests`, échantillons construits à la main) : l'empreinte ne bouge
  pas pour une image seule, une chroma seule, un ordre de dictionnaire ou un balisage seul ;
  elle change avec une statistique, un texte ou un skin ; entité nouvelle (`avant` nul) ;
  découpage CJK ; forme `PlainText`.
- **Paires réelles** (`LoDb.Ingestion.Tests`, par `CatalogFixtures.ReadAsync`) : 1001
  7.21.1 → 7.22.1 (texte) ; 3078 8.7.1 → 16.18.1 (refonte) ; 3001 8.7.1 → 16.18.1 (id
  réutilisé) ; Fiddlesticks (casse de l'id changée entre 3.13.24 et 7.21.1, sorts 7.22.1 →
  8.7.1) ; tout 16.18.1 → 16.19.1 inchangé, Ahri à chroma seule comprise. Ordre des écouteurs : enregistrés dans le
  désordre, appelés par étape (`PromotionListenerTests`).
- **Index** (Testcontainers) : des jeux réduits, dérivés de vrais, écrits directement dans
  `IDatasetStore` sous des versions consécutives propres au test, sans toucher à
  l'enregistrement rejoué. Cas : état inchangé, changement, retrait, retour, id réutilisé,
  changement de casse, jeu absent, version sautée par la promotion. La reconstruction et la
  promotion donnent le même index, `closed_at` exclu ; rattrapage, opération vide et
  reconstruction dans une nouvelle génération (`closed_at` gardé) ; début de la couverture ;
  repère de la copie en mémoire.
- **API** : une ligne de test par ligne de la table du § 3.3, index en retard, règle
  résiduelle et « avant toute promotion » compris ; le point d'accès des changements (404 de
  la première version couverte et hors couverture, entités retirées tirées du catalogue
  précédent, `hasChanges`) ; la 503 d'un ouvreur épinglé sans catalogue de comparaison ; les
  replis des redirections héritées.
- **Sitemaps et IndexNow** : l'index n'annonce que les fichiers non vides ; le contenu d'un
  fichier de version ; les réponses HTTP ; `lastmod` ; 503 tant que la copie n'est pas
  chargée ; les listes IndexNow et la ligne `seo.indexnow.planned`.
- **Pré-calcul** (xUnit) : une `200` marquée `revisable` est stockable, une marque inconnue
  reste `Skipped` ; le producteur de la file des états clos est idempotent (marque, deux
  epochs) ; le périmètre `openers`.
- **Front** (Vitest) : le `<head>` pour chaque ligne du § 3.3 ; un seul suffixe de titre, sur
  une plage et sur une version ; le résumé de la description ; une page figée ne lie que des
  URL versionnées, sauf « Dans le dernier patch » ; le rendu d'une page révisable est le même
  avant et après une promotion qui ne touche pas l'entité ; la route de la page de patch
  (301, 404, classe) et le pré-routeur ; les liens de `/patches`.
- **nginx** (`node --test`) : les deux classes appliquées, `archived` par défaut ; un objet
  révisable quitte le cache chaud après expiration (`EXPIRED` ou `MISS`) ; les bundles gardent
  l'année ; la `map` stricte admet `changes`.
- **Outils** : `served-overlays.test.mjs` et les contrôles de secrets (service `states`),
  `check-urls.sh`.
- **E2E** (Playwright), le minimum que les couches basses ne voient pas, en choisissant les
  entités par le `state` que rend l'API (les données réelles de Data Dragon bougent) : le
  `<head>` rendu par le SSR d'un ouvreur, d'une page d'état clos qui n'est pas l'ouvreur, et
  d'une page révisable ; une page de patch ; `/sitemap.xml`. `public/seo.spec.ts` et
  `prerender/stored-page.spec.ts` se réécrivent de la même façon.

## 9. Hors périmètre

- Le CDN (plan de l'ADR 0011 inchangé), la publicité et les citations par les LLM.
- Le contenu éditorial des fiches au-delà des différences.
- La détection des icônes redessinées (§ 3.1, v2).
- Les dates de sortie des patchs.
- L'épinglage des catalogues de la version précédant le dernier patch (§ 3.4 : la fiche courte
  sort sans bloc si le catalogue de comparaison n'est pas chargé).
- Une commande `prerender cancel` : l'annulation se fait au pupitre.

## 10. Livraison

Selon `AGENTS.md` : conventions, garde-fous, un commit par changement cohérent, aucune ligne
d'attribution. Pas de push direct sur `dev`, `test` ni `main`.

1. **Branche `feat/seo-une-page-par-etat`**, déjà créée : elle porte cette spécification, puis
   les lots L0 à L7 dans l'ordre du § 6. Avant la PR, sur la pile locale : l'index construit
   par le service `states` sur les fixtures, une petite passe, des `<head>` et des sitemaps
   conformes au § 3.3.
2. **Une pull request vers `dev`**, avec une CI verte. `ENV_STAGING` change avant la fusion
   (§ 4.2) : la fusion déploie le staging, et c'est sa bascule.
3. **Prod** selon le § 4.3.

## 11. Ce que l'implémentation a tranché

Décisions prises par les lots et l'intégration là où ce texte ne disait rien, ou autrement que
lui. Elles l'emportent sur les sections citées ; les ADR en reprennent l'essentiel (notes
« précisé à l'implémentation »). Pièges qui en découlent : [`pieges.md`](../architecture/pieges.md),
§ « Index des états d'entité » et « Catalogue pré-rendu ».

### 11.1 Empreinte et différences (§ 3.1, § 3.4)

- **`PlainText`** fait un saut de ligne de `<br>`, `<hr>` et des deux bouts de `li`, `p`, `ul`,
  `ol`, `maintext`, `stats`, `rules`, `flavortext` (pas seulement `<br>`) ; il ne retire que
  les vraies balises (`<` suivi d'une lettre) et les commentaires, garde un `<` isolé comme le
  fait la page, décode les entités et replie les sauts répétés. Un `<br>` de plus ou de moins
  n'ouvre donc pas d'état.
- **Sérialisation** : les champs projetés, dédoublonnés (la première clé l'emporte), triés par
  l'ordre de `ChangeField` puis par sujet en ordinal, écrits préfixés de leur longueur, en
  UTF-8, puis SHA-256 en hexadécimal minuscule. `EntityDiff.ChangedKeys` n'est pas vide si et
  seulement si les empreintes diffèrent ; il rend les clés de l'« après » dans leur ordre,
  puis celles du seul « avant ».
- **Portée d'un sort d'invocateur** : 25 000 ou plus s'écrit `global`.
- **Différence d'un texte** : mots, suites de blancs et signes de ponctuation, chacun fait
  d'éléments de texte entiers ; un élément de texte par jeton en ja_JP, zh_CN, zh_TW et th_TH.
  Au-delà d'un million de cases d'alignement (une refonte d'un long lore CJK), le milieu changé
  s'écrit en un seul segment retiré puis un seul ajouté.
- **`FieldDiff`** : pour un champ texte, `Before` et `After` sont vides et la différence est
  dans `Segments` ; pour les autres, `Segments` est vide.
- **Chromas** : les skins projetés sont ceux que l'ingestion a gardés, après le filtre des
  chromas par le catalogue de CommunityDragon. Limite acceptée le 2026-10-22, sans
  réingestion : un patch ingéré avant que CommunityDragon connaisse une nouvelle chroma ouvre
  un état pour elle seule. Correctif à venir : `parentSkin` dans le mapper, en_US réingéré,
  `--rebuild`.

### 11.2 Index des états (§ 3.2, § 4.1)

- **Une table de plus**, `catalog_state_version` (génération, version, rang, `indexed_at`) :
  les versions couvertes, leur ordre, que le SQL n'a pas, et le point de reprise d'une
  construction. Pas de colonnes de tri des versions sur les états.
- **« Listée »** veut dire la liste de Data Dragon plus le dernier patch promu ; une version
  dont les quatre jeux en_US sont définitivement absents est sautée, comme non listée.
- **Seule la commande reconstruit quand le début de la couverture recule.** La promotion et la
  réconciliation ne construisent que si aucune génération n'est courante, et sinon étendent.
  Une version listée après la fin de la couverture sans son en_US laisse la génération courante
  en retard (`states.coverage.interrupted`) : jamais une génération plus courte à sa place.
  Une génération en attente au même début reprend ; les autres sont supprimées ;
  `--rebuild` les supprime toutes.
- **La commande lit la liste des versions de Data Dragon** (dès qu'une version est promue) :
  « elle ne télécharge rien » vaut pour les jeux. Liste illisible sans `--ingest-missing` :
  code 0 et `states.build.deferred` si une génération est courante ; code 1 et
  `states.build.failed` s'il n'y en a aucune, pour que le premier déploiement et un nouveau
  schéma arrêtent le job au lieu de servir un index vide.
- **Verrou** : essayé toutes les 0,5 s ; une minute pour la commande, puis code 1 ; 5 s pour la
  promotion et la réconciliation, qui rendent la main sans échec (issue `locked`).
- **Déploiement** : `docker compose run --rm --no-deps states` (sans relancer `migrate`).
- **Retrait juste après un jeu absent** : un état clos juste après une version où sa ressource
  manquait garde cette version pour dernière.
- **Copie en mémoire** : tout échec de rechargement, pas seulement de la base, garde la copie
  tenue ; sans copie, 503 `state-index-unavailable` et nouvel essai à la lecture suivante.
- **Événements** `states.*` (trois segments, ADR 0010) et métriques `lodb_states_*`
  ([`metriques.md`](../guides/metriques.md)).
- **Sans aucune génération** (pile neuve), la première promotion construit tout l'index dans
  le processus de l'API.

### 11.3 Canoniques, fiches, page de patch, anciennes URL (§ 3.3 à § 3.5, § 3.9)

- **Genre `baseline`** (ajouté à `EntityChangeKind`, intégration) : le premier état d'une
  entité ouvert au début de la couverture n'a été comparé à rien. L'API le dit `baseline`
  dans `state` et `history`, avec `changes: null` ; la fiche n'affiche ni résumé, ni pastille,
  ni bloc, et sa description et son titre sont ceux d'un ouvreur sans résumé. L'index le
  stocke comme un état nouveau ; `PatchChangeKind` ne change pas (la version de début n'a pas
  de page de patch).
- **État ouvert dont le dernier patch n'a plus l'entité** (index en retard sur un retrait) :
  la canonique est l'ouvreur de l'état, pas une URL courte en 404.
- **Ouvreur dont l'index n'a pas l'état précédent** : `changes` garde son genre, sans version
  comparée ni champs, sans 503.
- **Le bloc liste exactement les clés de l'index, dans leur ordre**, même un champ qu'aucun
  des deux côtés n'a dans la langue de la page (sans valeur ; le front n'en montre que le
  libellé). Sur un ouvreur, `state.changedFields` reprend `changes.fields`, sujets nommés
  pareil, depuis le catalogue comparé quand la page ne les a plus.
- **Catalogue comparé en en_US de repli** (Data Dragon n'avait pas encore la langue de la page
  à cette version) : les nombres, codes, ids et chaînes par rang des sorts gardent leur valeur
  avant ; un texte ou un nom ne montre que sa valeur après (`ComparedProjection`).
- **URL courte** : sans bloc aussi quand Data Dragon échoue sur le catalogue comparé, pas
  seulement quand il n'est pas chargé. Un ouvreur épinglé répond 503 `catalog-pending`.
- **Page de patch** : une entité que le catalogue n'a pas est nommée par son id, sans image ;
  une réponse dont des icônes sont en attente est gardée 5 s (`public, max-age=5`, avec
  `Retry-After`) ; validation dans l'ordre 400 (version), 400 (langue), 503, 404.
- **`/api/meta`** ne répond jamais 503 pour l'index : `oldestChangesVersion` nul et une ligne
  `catalog.meta.degraded`.
- **Anciennes URL** : une voie de runes absente du dernier patch garde la règle du 2026-10-16
  (sa page épinglée avec une version, 404 sans) : seul le catalogue du dernier patch traduit sa
  clé en id.
- **Front** : le résumé de la description ne vaut que pour l'ouvreur épinglé d'un état clos,
  jamais pour l'URL courte ; les textes de la page de patch vivent dans le scope `patches` ;
  les blocs restent en bas de la fiche, à la place de l'ancien ; l'historique nomme les sujets
  de sorts, de runes et de skins par les noms de la page ; les codes sans table du front
  (modes, type de coût, ressource) s'affichent tels quels ; une page `noindex` n'a plus de
  JSON-LD du site. Sur une page de patch, la version mémorisée ou choisie ne s'applique
  qu'entre `oldestChangesVersion` et le dernier patch (`has-changes-page.ts`, la règle de
  `/patches`) ; sinon la page reste sur le dernier patch et la pastille le dit.
- **`check-urls.sh`** suit ce que dit l'API : une page de patch peut répondre 404 sur une
  petite couverture, et les lignes des pages figée et révisable s'écrivent « skip » quand
  l'historique de l'objet n'en a pas.

### 11.4 Sitemaps, IndexNow, `llms.txt` (§ 3.6, § 3.8)

- **IndexNow part du patch promu avant** (`EntityStateSnapshot.PromotedBefore`, intégration),
  pas de la version couverte juste avant : il annonce `/{locale}/{v}/changes` de chaque version
  couverte depuis ce patch (compris) jusqu'à la version promue (exclue) qui a changé quelque
  chose, et les ouvreurs des états dont la dernière version est dans cet intervalle. Une
  version listée mais jamais promue est ainsi annoncée par la promotion suivante.
- **`latest.xml`** répond aussi 503 tant que la copie de l'index n'est pas chargée.
- **Index en retard sur le lecteur** : `latest.xml` sans `/{locale}/changes`, la fin de la
  couverture annoncée comme une version passée, une version entre elle et le dernier patch en
  404.
- **`SitePages.Changes`** est le seul littéral du segment `changes` côté API ; il n'entre pas
  dans `PatchBound`. La ligne `/en/changes` de `llms.txt` est toujours présente.
- **Index illisible pendant une promotion** : IndexNow échoue en `ingest.listener.failed` ;
  une version que l'index ne couvre pas donne `seo.indexnow.unindexed`.
- Pas de commande de simulation d'IndexNow : la ligne `seo.indexnow.planned` en tient lieu.

### 11.5 Pré-calcul et cache (§ 3.7, § 3.8)

- **La file des états clos prend aussi la version qui quitte le dernier patch** (acceptée) :
  sa page a pu être stockée révisable par la file de la demande, une passe ou une unité
  tardive pendant que l'index tenait l'état ouvert. Une page par état clos et par langue de
  plus, comptée dans les ~24 000 du § 1.2.
- **Rien du pré-calcul ne part sur un index qui ne couvre pas le patch promu** : ni l'unité
  de la version qui part, ni la file (`prerender.promotion.index_behind`, aussi sans index du
  tout) ; `prerender.reconcile` les ajoute une fois l'index rattrapé.
- **Attendre l'index** : par l'échéance des lignes (`due_at` = maintenant +
  `2 × VersionsLifetime` + 1 min, 21 min), pas par le repère de l'index lu par l'API.
- **Marque** : sur une génération que la marque ne nomme pas (premier passage,
  reconstruction), le producteur part du plus tardif de `built_at` et du dernier `closed_at`
  des états clos avant la version qui part : il ne met en file que les clôtures de la dernière
  promotion (`prerender.revision.generation`). Une copie de l'index plus ancienne que la
  génération de la marque ne fait rien. Une ligne déjà en file pour la même page et la même
  epoch repasse en attente.
- **Epochs** : l'epoch servie et celle de la passe active ; une epoch remplie par une passe
  terminée, ni servie ni écrite, ne reçoit pas les clôtures d'ici sa bascule.
- **Rendu de la file** : chaque version sous la graphie de son catalogue (casse de l'id,
  slug), pas celle de l'ouvreur ; une page qui revient `revisable` compte comme un essai raté
  (nouvel essai 5 min après, échec définitif au troisième) ; les places de rendu sont celles
  de la file de la demande (`RenderClaimant.Queue`).
- **`changes` est toujours dans `VersionPages.Of`**, même pour une version sans page de patch
  (404, sautée) : la liste ne dépend pas de l'index. Une passe « ouvreurs » sur une version
  hors de l'index ne rend que `changes`.
- **Pupitre** : les comptes par version (ouvreurs, figées, révisables) sont ceux des fiches
  par langue ; listes et page de patch, figées aussi, n'y sont pas. `prerender unit` n'a pas
  d'option pour le périmètre des ouvreurs.
- **En-tête client de nginx** : sans build stocké, celui de l'amont ; avec, `revisable` pour
  un objet qui porte exactement sa valeur, `archived` sinon.
