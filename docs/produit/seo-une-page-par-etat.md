# Une page indexée par état : tout l'historique dans Google, sans copie

Chantier décidé le 22 octobre 2026. Le site est une base de données : chaque contenu
distinct qu'elle tient doit avoir une page indexée, une seule. Une fiche d'ancien patch n'est
plus indexée sous l'URL du dernier patch, ni répétée à chaque patch : elle l'est **une fois
par état**, c'est-à-dire par suite de patchs où son contenu ne change pas. Chaque état montre
ce que son patch a changé, et chaque patch a sa page de changements.

Cette règle remplace celle du 14 octobre 2026 (une page indexée par entité, celle du dernier
patch) et ne dépend plus d'aucune contrainte de charge. Décision :
[ADR 0005, amendement du 2026-10-22](../reecriture/adr/0005-web-ssr-urls-et-seo.md) et
[ADR 0011, amendement du 2026-10-22](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md).
Diagnostic précédent : [`seo-anciens-patchs.md`](seo-anciens-patchs.md).

## 1. Constat

### 1.1 Trois règles d'indexation depuis juillet

| Période | Index `/sitemap.xml` | URL annoncées | Pages d'anciens patchs | Raison |
|---|---|---|---|---|
| 18 juillet → 11 octobre | ~8 400 fichiers (402 versions × 21 langues) | ~4,2 millions | propre canonique, indexable, hreflang | ouvrir la longue traîne (« Aatrox patch 16.13 ») |
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
  21 langues. C'est l'ordre de grandeur de l'unité que le pré-calcul ajoute déjà à chaque
  promotion.

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
| **Empreinte** | Condensé du contenu propre d'une entité dans une version (§ 3.1) |
| **État** | Suite de versions listées consécutives où l'entité existe avec la même empreinte |
| **Ouvreur** | La première version d'un état : celle dont le patch a apporté le changement |
| **État ouvert** | L'état qui contient le dernier patch ; tous les autres sont **clos** |
| **Page figée** | Page d'ancien patch d'un état clos : elle ne changera plus jamais |
| **Page révisable** | Page d'ancien patch de l'état ouvert : elle changera quand l'entité changera |

Une entité absente d'une version interrompt son état. Si elle revient, un nouvel état
commence, même avec la même empreinte.

## 3. Décisions

### 3.1 L'empreinte

- Fonction pure de `LoDb.Domain`, une par ressource (champion, objet, arbre de runes, sort
  d'invocateur). Elle porte sur l'entrée de l'entité dans le catalogue en_US de la version,
  c'est-à-dire ce que sa fiche affiche d'elle-même :
  - noms, titres, étiquettes et textes nettoyés (descriptions, info-bulles, passif) ;
  - statistiques, coûts, temps de recharge, portées et modes ;
  - recette de l'objet (`from`, `into`) ;
  - skins d'un champion ;
  - runes d'un arbre.
- Elle ignore :
  - les champs d'image (`image`, `sprite`, coordonnées de planche) et la version ;
  - ce que la fiche tire d'autres entités : voisins dans la liste, prix des améliorations.
- Elle est versionnée par un numéro de schéma (`FingerprintScheme`), conservé avec chaque
  état. Changer sa définition, c'est monter ce numéro, reconstruire l'index (§ 3.2) et
  pré-calculer de nouveau les pages dont l'état change.
- Le catalogue en_US répond pour les 21 langues, comme aujourd'hui pour les canoniques. Une
  correction de traduction seule ne crée donc pas d'état. C'est accepté.
- Une icône redessinée sous le même nom de fichier ne crée pas d'état non plus (v2 possible :
  le hash du blob, déjà adressé par contenu).

### 3.2 L'index des états

- **Table Postgres** (migration additive), une ligne par état :
  - ressource, id de l'entité ;
  - version ouvreuse, dernière version de l'état (le dernier patch tant qu'il est ouvert),
    indicateur d'état ouvert ;
  - empreinte et schéma ;
  - chemin canonique (slug) de l'entité dans la version ouvreuse ;
  - état précédent, et s'il est séparé du présent par une absence (« retour ») ;
  - clés des champs changés par rapport à l'état précédent (en anglais, sans valeurs : les
    valeurs dépendent de la langue) ;
  - `closed_at`, l'instant où l'état a été clos.

  Environ 30 000 lignes en tout : l'index se lit depuis une copie en mémoire, rechargée à
  chaque promotion.
- **Versions couvertes** : celles que Data Dragon liste et que le site sert, jusqu'au dernier
  patch inclus, dans l'ordre de `PatchVersion`. Une version listée après le dernier patch
  (ingérée avant sa promotion) n'y entre qu'à sa promotion.
- **Construction initiale** : une commande `ICliCommand` (sous `Cli/<Zone>/`) reconstruit
  l'index depuis la plus ancienne version. Elle lit un catalogue en_US à la fois, peut
  reprendre où elle s'est arrêtée, peut être relancée sans effet de bord, et écrit une ligne
  de synthèse par lot. Les jeux de données sont déjà ingérés : elle ne télécharge rien.
- **À chaque promotion**, avant tout autre écouteur (§ 3.8), le nouveau dernier patch est
  comparé à la version précédente :
  - un état qui ne change pas s'étend ;
  - un état qui change ou disparaît se clôt (`closed_at`) ;
  - un nouvel état s'ouvre.
- **Réconciliation** au démarrage du worker : une version promue absente de l'index y entre,
  comme `PrerenderReconciler` répare une notification perdue.
- **Lecture** : (ressource, id, version) → l'état qui contient cette version.

### 3.3 La règle des canoniques

| Page | Canonique | `robots` | hreflang, JSON-LD | Sitemap |
|---|---|---|---|---|
| Fiche du dernier patch (URL courte) | elle-même | `index, follow` | oui | `latest.xml` |
| Ancien patch, **ouvreur d'un état clos** | elle-même | `index, follow` | oui (21 langues, même version) | `{version}.xml` de l'ouvreur |
| Ancien patch, état clos, autre version | l'ouvreur de l'état, même langue | `index, follow` | non | non |
| Ancien patch, **état ouvert** | l'URL courte | `index, follow` | non | non |
| Version listée après le dernier patch | inchangée (règle du 2026-10-14) | inchangé | inchangé | non |
| Liste d'un ancien patch | la liste nue du dernier patch (inchangé) | `index, follow` | non | non |
| Page de patch `/{locale}/{version}/changes` (§ 3.5) | elle-même | `index, follow` ; `noindex, follow` si la version n'a rien changé | oui | `{version}.xml` |

- **L'id fait foi.** Un id réutilisé par une autre entité (3001 : Abyssal Mask, puis
  Evenshroud) change d'empreinte, donc d'état : chaque entité a ses pages, sans règle à part.
- **Une entité retirée** n'est plus un cas particulier. Son dernier état est un état clos
  comme un autre, indexé à son ouvreur, et ses états antérieurs aussi. Le `noindex, follow`
  de ses fiches antérieures disparaît.
- **L'API garde la forme de `canonical`** (`{ version, path }`, `version` nulle pour l'URL
  courte), calculée en un seul endroit depuis l'index. `null` (hors de l'index) ne reste
  possible que pour les cas que la table laisse inchangés.

### 3.4 Ce que montre une fiche

1. **« Ce qui a changé dans ce patch »**, sur l'ouvreur d'un état, et sur l'URL courte quand
   l'état ouvert commence au dernier patch :
   - la différence avec l'état précédent, champ par champ, avant → après, dans la langue de
     la page ;
   - pour une valeur courte (nombre, coût, portée, liste de modes) : les deux valeurs ;
   - pour un texte (description, info-bulle) : la différence mot à mot (retiré, ajouté) ;
   - pour une entité nouvelle : « Nouveau dans ce patch » ; pour un retour après une
     absence : « De retour », comparé à l'état d'avant l'absence.

   Un moteur de différences pur dans `LoDb.Domain` compare les deux entrées de catalogue.
   L'API rend le résultat structuré (`changes`) et le front l'affiche, avec des libellés
   traduits. Une unité de pré-calcul lit deux catalogues, celui de sa version et celui de la
   version précédente, dans la langue de la page ; un retour après une absence en lit un de
   plus.
2. **Une page qui n'est pas un ouvreur** affiche « Inchangé depuis le patch X », avec un lien
   vers l'ouvreur, sa canonique. Elle n'a pas de bloc de différences.
3. **« Historique »** remplace « Dans les patchs précédents » (6 versions) :
   - chaque état de l'entité jusqu'à celui de la page inclus, du plus récent au plus ancien ;
   - pour chacun : sa plage (« patchs 11.5 à 11.9 ») et les champs qu'il a changés
     (libellés seuls), avec un lien vers sa canonique ;
   - sur une page figée, en plus, l'état suivant (« Changé au patch 11.10 »), lié par son URL
     versionnée ;
   - sur l'URL courte, l'historique complet ;
   - le lien « Dans le dernier patch » reste.

   **Une page figée ne lie que des URL versionnées** : son contenu ne dépend alors que de son
   état et de ceux d'avant, et ne change plus jamais (§ 3.7). Seul « Dans le dernier patch »
   pointe l'URL courte, qui suit d'elle-même le dernier patch.
4. **Titres et descriptions** :
   - l'ouvreur d'un état clos prend le suffixe « (patchs 11.5.1 à 11.9.1) », ou
     « (patch 11.5.1) » pour un état d'une seule version (nouvelle clé dans les 21 langues) ;
   - sa description commence par le résumé de ses changements ;
   - les autres pages gardent leur titre.
5. **JSON-LD** : il suit la canonique, comme aujourd'hui (seulement sur une page qui est sa
   propre canonique).

### 3.5 La page d'un patch

- **`/{locale}/{version}/changes`**, et **`/{locale}/changes`** pour le dernier patch.
  `/{locale}/{dernier}/changes` répond une 301 vers `/{locale}/changes`, comme le reste de la
  grammaire. Le segment s'ajoute aux routes du catalogue, sous la version épinglée ou non.
- **Contenu** : ce que la version a changé par rapport à la version listée avant elle.
  - Les entités dont un état s'ouvre à cette version, groupées par ressource, avec les champs
    changés.
  - Les entités nouvelles et les entités retirées.
  - Chaque entrée mène à la fiche de l'entité dans cette version (`/{locale}/{version}/…`),
    ou à l'URL courte pour le dernier patch.
- **API** : `GET /api/catalog/{version}/{lang}/changes`. Elle tire les champs changés de
  l'index, et les noms et icônes du catalogue de la version : un seul catalogue.
- **`/{locale}/patches`** (l'historique des patchs) lie chaque version à sa page de patch
  d'abord, puis à ses quatre listes comme aujourd'hui.
- **Cache** : la page d'un ancien patch ne dépend que de sa version et de la précédente, donc
  elle est figée. Celle du dernier patch lie les URL courtes ; elle est pré-calculée de
  nouveau quand sa version cesse d'être la dernière, avec l'unité de cette version.

### 3.6 Les sitemaps

- **`/sitemap.xml`** : les 21 `latest.xml`, puis les 21 `{version}.xml` de **chaque** version
  listée plus ancienne que le dernier patch. Ça fait environ 8 440 entrées, sous la limite de
  50 000. Plus de fenêtre : `LoDb__Seo__SitemapVersionWindow` disparaît.
- **`latest.xml`** : inchangé, plus `/{locale}/changes`.
- **`{version}.xml`** : la page de patch de la version (si elle a changé quelque chose),
  puis les ouvreurs des états clos ouverts par cette version. Les états encore ouverts y
  entrent à leur clôture.
- **`lastmod`** :
  - celui d'un ouvreur est son `closed_at`, le moment où sa page devient indexable. Pour
    l'historique reconstruit, c'est la date de la reconstruction ;
  - celui d'une page de patch est la promotion qui a rendu sa version ancienne, ou la date de
    la reconstruction ;
  - celui d'une entrée de l'index est le plus récent de son fichier ;
  - aucune date de sortie de patch n'est devinée : Data Dragon n'en donne pas.
- **Cache** : `max-age=3600` comme aujourd'hui, puisque les fichiers grandissent à chaque
  promotion.

### 3.7 Cache et stockage : pages figées et pages révisables

- **Page figée** : classe `archived` (un an) comme aujourd'hui.
- **Page révisable** : nouvelle classe **`revisable`**,
  `public, max-age=3600, s-maxage=86400, stale-while-revalidate=86400`. Sa canonique, son
  titre et son historique changent quand l'état ouvert se clôt. Elle représente 5 à 15 % des
  pages épinglées (§ 1.2).
- **Le SSR choisit la classe selon l'état** rendu par l'API, et plus seulement selon la
  version (`cacheClassOf`).
- **Le stockage garde les deux classes.**
  - La passe écrit aussi les pages révisables, avec leur `Cache-Control`.
  - nginx applique la classe de l'objet quand c'est l'une des deux connues, et `archived` pour
    un objet sans classe. Aujourd'hui, il impose `archived` à toute réponse du stockage
    (`$lodb_pinned_cache_control`, `docker/nginx/sites/site.conf`).
  - `tools/nginx/test/archived-cache-control.test.mjs` couvre les deux classes.
- **À chaque promotion** (§ 3.8), après la mise à jour de l'index, le pré-calcul refait dans
  l'epoch servie :
  - les pages des états que la promotion a clos : toutes leurs versions × 21 langues, soit
    ~24 000 pages par patch ;
  - les pages de patch concernées ;
  - et, comme aujourd'hui, l'unité de la version qui quitte le dernier patch.

  Les copies de nginx et du CDN de ces pages révisables expirent en un jour au plus.
- **L'ordre de la passe** : les pages indexables d'abord, sur toutes les versions (ouvreurs et
  pages de patch), puis toutes les autres. Ces pages-là sont celles que les sitemaps
  annoncent et que Google demandera.

### 3.8 À la promotion d'un patch, dans cet ordre

1. **L'index** : ce qui s'étend, ce qui se clôt, ce qui s'ouvre (§ 3.2).
2. **Le pré-calcul** : l'unité de la version qui quitte le dernier patch, les pages des états
   clos et les pages de patch (§ 3.7).
3. **IndexNow**, dans les 21 langues, par lots de 10 000 URL au plus :
   - les URL courtes changées, comme aujourd'hui ;
   - les ouvreurs devenus indexables ;
   - `/{locale}/changes` et `/{locale}/{version précédente}/changes`.

L'ordre est explicite entre les écouteurs (`IPromotionListener`). Un écouteur qui échoue est
journalisé sans annuler la promotion, comme aujourd'hui, et la réconciliation répare l'index.

### 3.9 Les redirections héritées

L'amendement du 2026-10-16 suit la nouvelle règle : une ancienne URL qui nomme une version
mène directement à la canonique de sa cible. Pour une entité, c'est l'ouvreur de son état, ou
l'URL courte si l'état est ouvert. Pour une liste, toujours la liste nue du dernier patch.

### 3.10 Ce qui disparaît

- `LoDb__Seo__SitemapVersionWindow`, sa validation et la fenêtre de `SitemapLocations.Index`.
- `CanonicalReferences` et `CanonicalReader`, qui ne lisaient que les catalogues du dernier
  patch et de la version suivante, faute d'un index entité → versions.
- Le `noindex, follow` des fiches antérieures d'une entité retirée (`UNINDEXED` pour le
  catalogue, `build-head-tags.ts`).
- Le bloc « Dans les patchs précédents » (`previous-patches.ts`, `PREVIOUS_PATCH_COUNT`).
- Les `urlset` vides des sitemaps de version.

### 3.11 Ce qui ne change pas

- La grammaire d'URL, sauf le segment `changes` ajouté.
- La 301 de l'URL versionnée du dernier patch.
- La canonique des listes et les variantes régionales (`?lang=`).
- La file de la demande, le rythme de la passe et les clés du stockage.
- Le plan Cloudflare, `robots.txt`, et les versions listées après le dernier patch.

## 4. Mise en production : une seule bascule

La nouvelle règle remplace l'ancienne dans le même changement : ni interrupteur, ni période où
les deux règles coexistent. Le déploiement qui la porte est la bascule.

1. **L'index se construit avant que l'API serve.** Un service à usage unique de
   `compose.deploy.yaml` exécute la commande de reconstruction (§ 3.2), après `migrate` et
   avant `api`. L'ancienne pile sert pendant ce temps : la table est additive. La première
   fois, la commande lit les catalogues en_US de toutes les versions ; ensuite, elle ne fait
   rien tant que l'index est à jour.
2. **Staging** :
   - le déploiement bascule ;
   - une passe manuelle sur un petit périmètre ;
   - vérification des `<head>`, des sitemaps et des listes IndexNow (journal, sans envoi), et
     E2E.
3. **Prod**, dans cet ordre :
   - la passe en cours sur l'epoch 1 s'arrête : ses pages portent l'ancienne règle ;
   - relevé de départ (§ 5) ;
   - `ENV_PROD` : epoch d'écriture et epoch servie = 2, `LODB_ARCHIVE_EPOCH` + 1 (purge du
     cache chaud). Ces valeurs valent au déploiement qui suit ;
   - déploiement (`test` → `main`) : l'index se construit, puis l'API sert la nouvelle règle ;
   - passe « indexables d'abord » sur l'epoch 2, puis toutes les autres pages. D'ici là, une
     page absente du stockage part au SSR, puis à la file de la demande, qui la pré-calcule en
     priorité.
4. **Retour arrière** : revert de la fusion, puis redéploiement avec epoch servie = 1 et
   `LODB_ARCHIVE_EPOCH` + 1. L'epoch 1 est donc gardée deux semaines après la bascule, puis
   retirée par le script du stockage (`retire-pages-1`).

## 5. Mesure

**Relevé de départ**, le jour de la bascule, dans la Search Console et le journal de l'edge :

- les pages indexées ;
- les impressions et les clics par type d'URL : courtes, versionnées (regex
  `/\d+\.\d+\.\d+/`), pages de patch (`/changes`) ;
- « Explorée, actuellement non indexée », « Autre page avec balise canonique correcte »,
  « Google n'a pas choisi la même URL canonique que l'utilisateur » ;
- les statistiques d'exploration ;
- la part de 503 servies à Googlebot (`asn:"15169"`).

**Points de contrôle** : 2, 4 et 8 semaines après la bascule.

**Leviers de réglage** :

- Des ouvreurs restent massivement « explorés, non indexés », ou Google choisit une autre
  canonique entre deux états voisins : on rend l'empreinte moins fine (sans les skins
  d'abord, puis sans les champs mineurs). Ça veut dire monter le schéma, reconstruire l'index
  et pré-calculer de nouveau les pages changées.
- « Autre page avec balise canonique correcte » monte : c'est attendu (les versions d'un état
  qui ne sont pas l'ouvreur).
- L'exploration se perd dans les anciennes versions pendant que les URL courtes reculent : on
  scinde les sitemaps de version, ou on classe l'index par ancienneté.

Les résultats s'écrivent ici (§ Résultats, à créer) et dans l'amendement de l'ADR 0005.

## 6. Lots

Chaque lot livre son code, ses tests et ses documents dans le même commit, selon
`AGENTS.md`.

| Lot | Contenu | Dépend de |
|---|---|---|
| **L0 Contrat** | Schéma de la table, contrat OpenAPI (`state`, `history`, `changes` sur les fiches ; `GET …/changes`), énumération des champs du moteur de différences, classe `revisable`. Client régénéré (`npm run api:generate`) | — |
| **L1 Domaine** | Empreintes et moteur de différences par ressource, purs, dans `LoDb.Domain`. Tests sur des paires réelles : refonte d'un objet, id réutilisé (3001), changement de sort d'un champion, sort inchangé | L0 |
| **L2 Index** | Migration additive, construction, commande de reconstruction, étape de promotion (premier écouteur), réconciliation, service de construction au déploiement (`compose.deploy.yaml`, après `migrate`), copie en mémoire, métriques et une ligne de synthèse par lot | L0, L1 |
| **L3 API** | Canonique par état, qui remplace `CanonicalReferences` et `CanonicalReader` ; champs des fiches, point d'accès des changements, redirections héritées | L2 |
| **L4 Fichiers SEO** | Index des sitemaps sans fenêtre (retrait de `LoDb__Seo__SitemapVersionWindow`), sitemaps de version, `lastmod`, IndexNow (ouvreurs et pages de patch, lots de 10 000), `llms.txt` (pages de patch) | L3 |
| **L5 Front** | `<head>` selon § 3.3 et § 3.4 (titres, descriptions, canonique, hreflang, JSON-LD), blocs « Ce qui a changé », « Inchangé depuis », « Historique » (remplace `previous-patches.ts`), page de patch et route, liens de `/patches`, classe selon l'état, clés i18n des 21 langues | L3 |
| **L6 Cache et pré-calcul** | Classe `revisable` (`CACHE_CONTROL`, `cache-class-mark.ts`, `map` nginx, tests `tools/nginx`), segment `changes` dans la `map` stricte des chemins du stockage et dans les pages d'une unité (`VersionPages`), écriture des deux classes, passe « indexables d'abord », pré-calcul des états clos et des pages de patch à la promotion, abandon d'une passe en cours si l'admin ne le permet pas encore, vues de l'admin (pages par classe) | L3 |
| **L7 Docs, changelog, E2E** | Documents du § 7, entrée de changelog (historique et pages de patch, côté joueur), E2E | L4, L5, L6 |

## 7. Documentation à mettre à jour avec le code

- `docs/architecture/pieges.md` : le paragraphe sur l'index des sitemaps ; les pages
  révisables ; l'ordre des écouteurs de promotion.
- `docs/guides/configuration.md` : retrait de `LoDb__Seo__SitemapVersionWindow` ; le service
  de construction de l'index au déploiement.
- `docs/produit/seo-indexabilite.md` : « Politique canonique ».
- `tools/routing/check-urls.sh` : le segment `changes`.
- L'ADR 0005 et l'ADR 0011 si l'implémentation tranche autrement un point de leurs
  amendements du 2026-10-22.

## 8. Tests

Selon [`tests.md`](../guides/tests.md) : le nominal et les cas limites introduits, rien de
plus.

- **Domaine** (xUnit) :
  - l'empreinte est stable quand seule une image change, et change avec une stat, un texte ou
    un skin ;
  - différences de nombres et de textes ; entité nouvelle ; retour après une absence.
- **Index** (Testcontainers, catalogues rejoués depuis `tests/fixtures/`) : quatre ou cinq
  versions consécutives couvrant un état inchangé, un changement, un retrait, un retour et
  un id réutilisé. La reconstruction et la promotion donnent le même index. Les fixtures
  restent petites : des catalogues dérivés de vrais, réduits à quelques entités.
- **API** : une ligne de test par ligne de la table du § 3.3, le point d'accès des
  changements, les 301 héritées.
- **Sitemaps** : l'index annonce toutes les versions passées ; le contenu d'un fichier de
  version ; `lastmod`.
- **Front** (Vitest) : le `<head>` pour chaque ligne de la table ; une page figée ne lie que
  des URL versionnées, sauf « Dans le dernier patch » ; titres sur une plage et sur une
  version.
- **nginx** (`node --test`) : la classe de l'objet est appliquée, `archived` par défaut.
- **E2E** (Playwright), le minimum que les couches basses ne voient pas : le `<head>` rendu
  par le SSR d'un ouvreur, d'une page d'état clos qui n'est pas l'ouvreur, et d'une page
  révisable ; une page de patch ; `/sitemap.xml`.

## 9. Hors périmètre

- Le CDN (plan de l'ADR 0011 inchangé), la publicité et les citations par les LLM.
- Le contenu éditorial des fiches au-delà des différences.
- La détection des icônes redessinées (§ 3.1, v2).
- Les dates de sortie des patchs.

## 10. Livraison

Selon `AGENTS.md` : conventions, garde-fous, un commit par changement cohérent, aucune ligne
d'attribution. Pas de push direct sur `dev`, `test` ni `main`.

1. **Branche `feat/seo-une-page-par-etat`**, partie de `docs/seo-une-page-par-etat` (ou de
   `dev` une fois cette branche fusionnée) : lots L0 à L7.
   Une pull request vers `dev`, avec une CI verte. Avant la PR, sur la pile locale :
   reconstruction de l'index sur les fixtures, une petite passe, des `<head>` et
   des sitemaps conformes au § 3.3.
2. **Staging**, puis **prod**, selon le § 4.
