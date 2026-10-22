# ADR 0005 — Web : SSR Angular, URLs localisées, SEO

- **Statut** : acceptée — 2026-09-24
- **Remplace** : rendu Twig + îlots Vue, locale portée par la session et un cookie
- **En vigueur dans le code** (2026-10-22) : la grammaire d'URL, le hreflang et la CSP du texte
  et de leurs amendements ; pour les canoniques et les sitemaps, **l'amendement du
  2026-10-14**, complété le 2026-10-16 (anciennes URL) ; depuis le 2026-10-20, les pages
  stockées gardent la canonique de leur rendu (limite connue, ADR 0011). L'amendement du 2026-10-22 est
  décidé, pas encore implémenté : il prend effet au déploiement du changement qui le porte,
  d'un seul coup et sans interrupteur. Le CDN de l'amendement du 2026-10-20 n'est pas construit. Règle en vigueur et
  avancement : [`seo-indexabilite.md`](../../produit/seo-indexabilite.md).
- **Amendements** :
  - le 2026-10-10, les pages des anciens patchs ont leur propre cache, dont la clé ne contient
    plus la révision de déploiement (voir § « Amendement du 2026-10-10 ») ;
  - le 2026-10-11, index des sitemaps borné, anciens patchs découverts par des liens (voir
    § « Amendement du 2026-10-11 ») ;
  - le 2026-10-11 encore, la CSP ne hache plus que les scripts inline du build, signale ses
    violations et ajoute les Trusted Types en Report-Only (voir § « Amendement du 2026-10-11
    — CSP ») ;
  - le 2026-10-14, une page indexée par entité : la fiche d'un ancien patch est canonique au
    dernier patch, et les sitemaps ne listent plus que des URL canoniques (voir § « Amendement
    du 2026-10-14 ») ;
  - le 2026-10-16, une ancienne URL du site qui nomme une version mène à l'URL courte quand
    l'entité existe au dernier patch, et à la liste nue pour une liste (voir § « Amendement
    du 2026-10-16 ») ;
  - le 2026-10-20, les pages des anciens patchs sont pré-rendues dans un stockage objet ;
    `lodb_archive` devient un cache chaud devant lui, la classe `archived` est gardée un an,
    et un CDN se place devant le site (voir § « Amendement du 2026-10-20 ») ;
  - le 2026-10-22, une page indexée par état d'entité : chaque suite de patchs où une fiche ne
    change pas a une page indexée, son premier patch ; chaque patch a sa page de changements,
    et l'index des sitemaps annonce de nouveau les versions passées (voir § « Amendement du
    2026-10-22 »).
    Il remplace l'amendement du 2026-10-14 pour les canoniques et les sitemaps, **à sa
    bascule** : décidé, pas encore implémenté.

## Contexte

Aujourd'hui, la locale d'interface vient de la session et d'un cookie, pas de l'URL.
Plusieurs limites en découlent :

- Symfony force `Cache-Control: private` : aucun cache HTML partagé n'est possible.
- **Pas de hreflang possible.** Les crawlers ne voient qu'une seule langue.
- Le service worker ne peut pas faire varier son cache sur le cookie.
- La sélection de la version et de la langue a produit une longue série de bugs de
  commutateur (redirections 302, chemin et query en concurrence, session lue à la
  place de l'URL).
- Les listes rendent tout le dataset (~700 objets) côté HTML, et certains îlots
  (galerie de skins) sont invisibles pour les crawlers.

Côté Angular, le SSR exige un runtime JavaScript. Aucune solution maintenue ne
l'héberge dans .NET : `SpaServices` et `NodeServices` ont été retirés en .NET 5.

## Décision

### Rendu : SSR Angular sur Node, par route

Un conteneur **`web-ssr`** (Node 24, `@angular/ssr`, `AngularNodeAppEngine`), derrière
nginx. Le mode de rendu est choisi route par route :

| Routes | Mode | Cache |
|---|---|---|
| Accueil, listes, détails, tendances, profil public `/u/…`, partage `/b/…` | `RenderMode.Server`, **anonyme** | public, partagé |
| À propos, FAQ, données, mentions légales, changelog | `RenderMode.Prerender` (21 locales) | statique |
| Profil, éditeur de builds, portail API, admin | `RenderMode.Client` + `noindex` | `private` |

- **Le serveur SSR ne lit aucun cookie et ne détient aucun secret.** Il rend la version
  anonyme et appelle `LoDb.Api` sur le réseau interne. Les éléments personnels (menu
  du compte, favoris) s'hydratent côté client après chargement.
- **Thème sans flash malgré le cache partagé** : un script inline dans `<head>` pose
  `data-theme` depuis le cookie avant le premier rendu. Le HTML ne dépend donc pas du
  thème.
- **Cache nginx (`proxy_cache`)** devant `web-ssr` :
  - pages de versions anciennes : `s-maxage` long ;
  - dernier patch : court, avec `stale-while-revalidate` ;
  - la **révision de déploiement** fait partie de la clé de cache, donc aucune purge
    manuelle (pages du dernier patch ; les anciens patchs : amendement du 2026-10-10).
- **Hydratation incrémentale** (`@defer` + hydratation au viewport ou à l'interaction)
  pour les blocs lourds : galerie de skins, filtres, vidéos de sorts.
- **Listes** : le SSR rend une première page paginée, lisible par les crawlers.
  Ensuite, le client charge le dataset complet pour un filtrage instantané, avec l'état
  des filtres dans l'URL comme aujourd'hui.
- **Durcissement du SSR** : l'année 2026 a apporté SSRF, open redirects, XSS de
  sérialisation et DoS sur `@angular/ssr`. Le serveur SSR est donc traité comme une
  surface exposée :
  - `allowedHosts` configuré ;
  - CSP stricte générée au rendu (hash des scripts inline via `autoCsp`, ou nonce) ;
    les en-têtes constants (HSTS, `nosniff`…) restent à l'edge ;
  - aucun secret ;
  - utilisateur non root, limite mémoire ;
  - mises à jour Dependabot appliquées en priorité.

### URLs : la locale en préfixe

```
/{locale}/                                      accueil
/{locale}/champions                             liste, dernier patch
/{locale}/champions/{id}                        détail, dernier patch (URL canonique courte)
/{locale}/{version}/champions/{id}              détail, patch passé (canonique : amendements du 2026-10-14 et du 2026-10-22)
/{locale}/[{version}/]changes                   ce qu'un patch a changé (amendement du 2026-10-22, à venir)
/{locale}/items/{id}-{slug}   /{locale}/runes/{id}-{slug}   /{locale}/summoners/{id}
/{locale}/u/{username}                          profil public
/b/{token}                                      partage de build (hors locale, noindex)
```

- **21 locales** : `ar cs de el en es fr hu id it ja ko pl pt ro ru th tr vi zh-hans
  zh-hant`.
  - Chaque locale a une **langue Data Dragon par défaut** (`fr` → `fr_FR`, `pt` →
    `pt_BR`, `zh-hant` → `zh_TW`…).
  - Une variante régionale (`en_GB`, `es_MX`…) reste possible comme préférence
    (`?lang=`), avec une canonique qui pointe vers la locale par défaut.
- **L'identifiant fait foi, pas le nom** (jumeaux Classic). Le slug est décoratif : s'il
  est faux ou absent, on fait une 301 vers l'URL canonique.
- **Même règle qu'aujourd'hui pour le dernier patch** : son URL versionnée renvoie une
  301 vers l'URL courte. Une entité absente sur une version épinglée donne une 302 vers
  la liste de cette version ; sans version épinglée, une vraie 404.
- `/` renvoie une 302 selon `Accept-Language` (`x-default`). `/build/` reste réservé
  aux assets front.
- **Priorité** : chemin > query > cookie, pour chaque axe (locale, version). Le
  commutateur est une **fonction pure** qui réécrit le chemin : fini la « redirect
  dance ».
- Anciennes URLs → **301** vers la nouvelle forme en `en` : c'est la langue qu'ont vue
  les crawlers, qui n'envoient pas de cookie. La table de correspondance est dans le
  [plan de migration](../plan-migration.md).

### SEO porté et étendu

- Canonique = schéma + hôte + chemin, sans query. `www` et `.fr` restent redirigés à
  l'edge (nginx), pas dans l'app. *Exception depuis le 2026-10-12 : au-delà de la première
  page, une liste est sa propre canonique avec `?page=N` (`catalogue-list-seo.ts`).*
- **hreflang** entre les 21 locales + `x-default` (nouveau).
- **Sitemaps par locale**. Aujourd'hui (amendement du 2026-10-14), l'index n'annonce que les
  `latest.xml` des 21 langues ; à la bascule de l'amendement du 2026-10-22, celui d'une
  version passée liste sa page de changements et les fiches indexées chez elle, et l'index
  annonce tous ceux qui ne sont pas vides.
- JSON-LD conservé à l'identique : `@graph` du site, BreadcrumbList, VideoGame,
  PropertyValue pour les stats (jamais Product ou Offer), ItemList, ProfilePage, FAQPage,
  Dataset… Encodage sûr contre la XSS, champs vides retirés.
- `noindex` en staging par en-tête à l'edge (pas de `Disallow: /`), vraies 404, titres
  suffixés du patch, `robots.txt` et `llms.txt` conservés.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| SPA sans SSR | Perd le SEO, qui est le premier canal d'acquisition |
| Prérendu de tout le catalogue | 397 versions × 21 locales × ~1 000 pages : impossible. Et un nouveau patch imposerait un rebuild |
| SSR hébergé dans .NET | Aucune solution maintenue |
| Locale en sous-domaine ou par cookie | Sous-domaine : certificats et edge compliqués sans gain. Cookie : c'est le problème actuel |
| URL = langue Data Dragon (28 variantes) | Cinq copies anglaises quasi identiques : du contenu dupliqué pour peu de valeur |

## Conséquences

- **+** Premier cache HTML partagé du projet : le TTFB des pages chaudes ne dépend plus
  du backend.
- **+** hreflang et sitemaps par langue : les 21 langues deviennent indexables.
- **−** Un conteneur Node à exploiter et à patcher souvent.
- **−** Toutes les URLs changent : table de 301 exhaustive et suivi dans la Search
  Console pendant la bascule.

## Amendement du 2026-10-10 — cache des anciens patchs et des bundles

La révision dans la clé vidait le cache à chaque déploiement, pages des anciens patchs
comprises, alors qu'elles ne changent jamais : leur `s-maxage` d'une semaine n'était jamais
atteint, et chaque passage d'un robot repayait un rendu et un chargement de catalogue. Les
bundles partaient avec l'image : un onglet ouvert avant un déploiement ne trouvait plus ses
chunks.

| Zone nginx | Périmètre | Clé | Taille, inactivité |
|---|---|---|---|
| `lodb_pages` | dernier patch, et toute réponse hors version épinglée | révision + URL | 2 Go, 24 h |
| `lodb_archive` | `^/{locale}/[0-9]` : version épinglée dans le chemin | **epoch** + URL, sans révision | 6 Go, 7 j, gardée à la 2ᵉ demande |
| `lodb_assets` | `/build/` | nom du fichier | 1 Go, 30 j |

*Remplacé le 2026-10-20 pour `lodb_archive` (20 Go, 90 j, gardée à la 1ʳᵉ demande, devant le
stockage pré-rendu), la classe `archived` (un an au lieu d'une semaine, ce qui retire le
compromis des deux semaines ci-dessous) et la rétention des bundles (400 jours,
`LODB_FRONT_BUILDS_RETENTION_DAYS`) : § « Amendement du 2026-10-20 » et
[ADR 0011](0011-catalogue-pre-rendu-et-cdn.md).*

- **Epoch** (`LODB_ARCHIVE_EPOCH`) : la seule purge des anciens patchs. Elle se monte à la
  main quand le chrome, la CSP ou le contrat `/api` changent de façon visible, et après tout
  correctif de sécurité du front, avec le retrait du chunk vulnérable
  ([procédure](../../guides/configuration.md)). Compromis assumé : entre deux montées, le
  chrome d'une page d'ancien patch peut dater de deux semaines (une semaine de `s-maxage`,
  puis une dernière copie périmée servie pendant son rafraîchissement, avant 7 jours
  d'inactivité).
- **Gardée à la 2ᵉ demande** (`LODB_ARCHIVE_CACHE_MIN_USES`, `2`) : un balayage de tous les
  patchs, qui ne repasse pas, n'écrit pas ses rendus sur le disque et n'évince jamais le
  dernier patch, rangé à part. Chaque URL distincte y tient pourtant une clé jusqu'à son
  inactivité, quelle que soit la réponse (`301` d'une query nettoyée, `503` de délestage) :
  c'est la zone des clés (`LODB_ARCHIVE_CACHE_KEYS_SIZE`, ≈ 8 000 clés par Mo) qui borne le
  nombre d'URL qu'un balayage parcourt avant que les anciens patchs les moins récemment
  demandés sortent du cache. À `1`, seules les réponses gardées tiennent une clé.
- **Bundles conservés** : chaque build publie ses bundles hachés dans le volume
  `front-builds` tant qu'il est en service (date rafraîchie toutes les heures), servi sous
  `/build/` après ceux de l'image ; le déploiement retire ceux qu'aucun build n'a livrés
  depuis 30 jours, plus que la vie d'une page en cache. Un chunk
  introuvable malgré tout recharge la page une fois. Node sert, nginx met en cache
  (ADR 0004 inchangée).
- **HTML compressé une fois**, au rendu (gzip 6) : nginx demande gzip à l'amont, garde cette
  copie pour tous les clients et la décompresse pour un client sans gzip.
- **Dernier patch** : `s-maxage=1800, stale-while-revalidate=86400` (au lieu de 300 s et
  3 600 s). Un nouveau patch apparaît sur une page en cache au plus 30 minutes après sa
  promotion ; un déploiement change toujours la clé.

## Amendement du 2026-10-11 — anciens patchs : des liens plutôt que l'index des sitemaps

- **Index borné** : `/sitemap.xml` n'annonce plus que les sitemaps du dernier patch
  (`latest.xml` des 21 locales) et ceux des **12** versions listées ensuite par Data Dragon
  (`LoDb__Seo__SitemapVersionWindow`, `12` par défaut, `0` pour le dernier patch seul, une
  valeur négative refusée au démarrage). Il annonçait auparavant chaque version listée dans
  chaque locale : des milliers de sitemaps, des millions d'URL épinglées. *Remplacé le
  2026-10-14 : la fenêtre vaut `0` par défaut (`SeoOptions.DefaultSitemapVersionWindow`) ;
  l'amendement du 2026-10-22 la retire à sa bascule.*
- **Rien ne disparaît** : le sitemap d'une version hors de la fenêtre reste servi à son URL,
  immuable, et ses pages restent canoniques et indexables. Ni `noindex` ni `Disallow`. Le
  sitemap principal de chaque locale ne change pas, sinon qu'il liste la nouvelle page
  ci-dessous. *Remplacé le 2026-10-14 : les pages d'un ancien patch ne sont plus canoniques,
  la fenêtre passe à `0`.*
- **Découverte par liens** : le sélecteur de version est un formulaire, que les robots ne
  soumettent pas. Deux chemins de liens rendent chaque ancien patch atteignable :
  - `/{locale}/patches`, « Historique des patchs », lié depuis le pied de page de chaque
    page : chaque version que Data Dragon liste (`versions` de `/api/meta`, pas seulement
    les versions prêtes) avec ses quatre listes, rendue par le serveur avec le cache du
    dernier patch ;
  - sur chaque fiche, « Dans les patchs précédents » : la même entrée dans les six versions
    listées avant celle de la page.

  Aucune des deux ne lit de catalogue pour se rendre : des liens seulement, tirés de
  `/api/meta`.
- **Pourquoi** : annoncer tout l'espace des versions épinglées invite chaque robot à le
  parcourir d'un coup, alors que ces pages sont construites à la demande, dans le budget
  borné des rendus épinglés, et non servies depuis un cache. Par les liens, un robot découvre
  les anciens patchs au fil de son exploration, à partir de pages déjà en cache.
- **Mesure** (propriétaire) : dans la Search Console, le rapport Sitemaps par fichier (URL
  soumises et indexées par version) et le rapport Performances filtré sur les URL
  versionnées, avant le déploiement puis trois mois après ; la fenêtre se règle sur la
  demande observée.

## Amendement du 2026-10-11 — CSP

- **Scripts admis** : la politique d'une page ne hache plus tout script inline de son HTML
  final, mais ceux que le build a écrits dans ses gabarits (`index.server.html`,
  `index.csr.html` : thème, contrat d'événements d'Angular, chargeur des feuilles différées),
  plus l'appel de rejeu d'événements, de forme fixe, placé juste après le contrat. Un script
  inline qui atteindrait le HTML au rendu (contenu stocké, erreur de gabarit) n'a pas de
  hash : le navigateur ne l'exécute pas, et le SSR le compte. Une politique dérivée du
  document qu'elle protège admettait tout ce qu'il contenait.
- **Rapports** : `report-to` (`Reporting-Endpoints`) et `report-uri` vers
  `POST /api/csp-reports` ; l'API en garde des comptes par directive, jamais la page ni le
  client.
- **Trusted Types** en `Content-Security-Policy-Report-Only` : appliqués une fois les rapports
  vides en production.
- `'unsafe-eval'` est retiré depuis la précompilation des messages ICU (ADR 0006, amendement
  du 2026-10-11) ; `style-src 'unsafe-inline'` reste imposé par Angular.

## Amendement du 2026-10-14 — une page indexée par entité

*En vigueur dans le code. Remplacé pour la règle des canoniques et les sitemaps par
l'amendement du 2026-10-22 (une page indexée par état d'entité), décidé mais pas encore
implémenté : cette règle reste celle du site jusqu'au déploiement du changement qui porte
la règle par état.*

Le site a perdu presque toute sa visibilité dans Google depuis le 16 août 2026
([diagnostic](../../produit/seo-anciens-patchs.md)) : 2 570 pages indexées contre ~20 000 fin
juillet, 31 201 « explorées, actuellement non indexées ». Chaque fiche d'ancien patch était sa
propre canonique (« patch passé (auto-canonique) ») : une entité existait en autant d'URL
canoniques que de patchs épinglés, fois 21 langues, presque identiques, que les sitemaps
annonçaient (~160 000 URL pour 13 versions) et que les liens internes servaient. Google en
garde une poignée, et les fiches du dernier patch, celles qui comptent, s'y noient.

**Objectif** : une page indexée par entité et par langue, celle du dernier patch ; les anciens
patchs restent consultables sans lui faire concurrence.

| Option pour la fiche d'un ancien patch | Pour | Contre |
|---|---|---|
| A — canonique vers l'URL courte de la même entité, même langue | Le signal se concentre sur le dernier patch ; la page reste explorable, ses liens suivis | Un indice : Google peut l'ignorer entre deux pages qui diffèrent (un patch très ancien) |
| B — `noindex, follow` | Explicite, certain | Ne transmet rien à la fiche du dernier patch ; une page `noindex` est de moins en moins explorée, ses liens avec elle |
| Statu quo, fenêtre de sitemaps | Rien à changer | C'est la cause |

**Décision.**

- **Entité présente au dernier patch** (même id : l'id fait foi, comme dans les URL) :
  option A. Canonique et `og:url` sur l'URL courte, au chemin que lui donne le dernier patch
  (le slug d'un objet renommé suit) ; `index, follow`. Ni hreflang, dont les alternates d'une
  page canonisée ailleurs n'auraient pas de retour, ni données structurées, dont les valeurs
  d'un ancien patch contrediraient sous les mêmes `@id` celles de la canonique.
- **Liste d'un ancien patch**, quelle que soit sa page : canonique sur la liste nue du dernier
  patch, qui existe toujours.
- **Entité absente du dernier patch** (retirée) : son ancienne fiche est un contenu unique,
  mais en autant de copies que de versions qui la portaient. Elle reste indexable dans **la
  dernière version qui la portait**, celle dont la version listée ensuite ne la porte plus :
  auto-canonique, hreflang entre ses 21 langues, données structurées. Ses fiches des versions
  antérieures sont `noindex, follow`, sans canonique. Aucun index entité → versions n'existe :
  désigner cette dernière version ne lit qu'un catalogue de plus, celui de la version suivante,
  quand l'entité manque au dernier patch. Une entité retirée, réintroduite puis retirée de
  nouveau garde une page par période : rare, accepté.
- **Une seule règle, côté API** (`CanonicalReferences`, `LoDb.Ingestion/Catalog/Canonicals`) :
  chaque fiche porte `canonical` (`{ version, path }`, `version` nulle pour l'URL courte ;
  `null` hors de l'index), que le front écrit dans le `<head>` par `Seo.apply`
  (`core/seo/head/build-head-tags.ts`) ; les sitemaps lisent la même règle. Présence et chemins
  ne dépendent pas de la langue : les catalogues en_US répondent pour les 21. Celui du dernier
  patch est épinglé en mémoire ; celui de la version suivante se charge comme celui de la page,
  dans le budget du travail à froid (503 avec `Retry-After` sinon, comme une page froide).
- **Sitemaps** : seulement des URL canoniques. `LoDb__Seo__SitemapVersionWindow` passe à `0`
  par défaut : l'index n'annonce que les `latest.xml` des 21 langues. Le sitemap d'une version
  passée reste servi, jamais en 404 pour une version listée : il ne liste plus que les fiches
  canoniques chez lui (les entités dont elle est la dernière version), un `urlset` vide sinon.
  Une URL que Google a déjà apprise répond ainsi un document valide plutôt qu'une erreur du
  rapport Sitemaps, et une fenêtre non nulle garde un sens : annoncer les entités que les
  derniers patchs ont retirées. Ils ne sont plus `immutable` : ce qu'ils listent suit le
  dernier patch (`max-age=3600`).
- **Liens internes** : `/{locale}/patches` et « Dans les patchs précédents » restent, pour les
  visiteurs ; rien n'est ajouté qui élargisse l'exploration.
- **Caches** : la réponse d'une fiche d'ancien patch (un jour) et sa page (`lodb_archive`,
  jusqu'à deux semaines) gardent leur canonique jusqu'à expiration ; une entité que le patch
  suivant retire ou renomme pointe ce temps vers une URL courte en 404 ou en 301. Rare, borné,
  accepté. Le `<head>` des anciens patchs change : l'epoch par défaut monte à `3`.
  *Plus borné depuis le 2026-10-20 : une page gardée un an (`archived`) ou stockée par le
  précalcul garde la canonique de son rendu jusqu'à l'epoch suivante (ADR 0011, note de
  § nginx). L'amendement du 2026-10-22 traite ce cas (pages révisables).*

**Mesure** (propriétaire), dans la Search Console, 4 à 8 semaines après le déploiement en
production, comparée au relevé du 14 octobre 2026 :

- le nombre de pages indexées (2 570) et les impressions des URL courtes remontent ;
- « Explorée, actuellement non indexée » (31 201) baisse ; « Autre page avec balise canonique
  correcte » (10 886) monte, attendu avec l'option A, comme « Exclue par la balise `noindex` »
  (70), avec les fiches antérieures des entités retirées ;
- si « Google n'a pas choisi la même URL canonique que l'utilisateur » (75) gonfle, Google
  ignore la canonique entre des pages trop différentes : passer ces fiches à l'option B.

## Amendement du 2026-10-16 — anciennes URL versionnées vers l'URL courte

Les 301 de l'ancien site (`/16.4.1/champion/Poppy`, `/11.7.1/object/3020?lang=en_US`)
gardaient la version qu'elles nommaient : elles menaient à la fiche épinglée, que l'amendement
du 2026-10-14 rend canonique à l'URL courte. Google suivait donc une 301 vers une page qui en
désigne une autre, rendue à la demande dans le budget borné des rendus épinglés (6 par minute
et par client) : 225 des 256 réponses 5xx servies à Googlebot entre le 10 et le 16 octobre
2026 venaient de ces pages. Le slug de la version nommée ajoutait parfois un second saut
(`/10.13.1/object/3001` → `items/3001-evenshroud` du dernier patch, puis `3001-abyssal-mask`
de 10.13.1).

**Décision.** Une 301 héritée mène directement à la canonique de sa cible :

- **Entité présente au dernier patch** (même id) : l'URL courte, au chemin et au slug du dernier
  patch, quelle que soit la version de l'ancienne URL (chemin ou `?version=`).
- **Liste**, épinglée ou non : la liste nue du dernier patch (`/en/16.4.1/champions` est
  canonique à `/en/champions`, `catalogue-list-seo.ts`).
- **Entité absente du dernier patch** (retirée) : inchangé. Fiche épinglée de la version
  nommée, au slug de son catalogue s'il est stocké ; sinon 302 vers la liste de cette version.
- Tant que le catalogue du dernier patch n'est pas lu, une ancienne URL versionnée ne mène
  nulle part (503 avec `Retry-After`, comme une page froide) : elle ne sait pas encore si
  l'entité existe toujours.
- La locale (`?lang=`) se choisit comme avant ; une 301 reste gardée une heure.

## Amendement du 2026-10-20 — catalogue pré-rendu et CDN

Le budget des rendus épinglés reste saturé (142 467 réponses 503 par jour au 2026-10-20) et
`lodb_archive` ne sert que 2,5 % de `HIT` : les robots ne redemandent presque jamais une
page dans la semaine. L'[ADR 0011](0011-catalogue-pre-rendu-et-cdn.md) calcule une fois
chaque page d'ancien patch dans un stockage objet. Pour cette ADR :

- **Alternative « Prérendu de tout le catalogue »** : toujours écartée au build. Le précalcul
  tourne à côté du site, dans une voie d'admission `prerender` du SSR, au fil des patchs ;
  le mode de rendu des routes ne change pas.
- **`lodb_archive` devient un cache chaud** devant le stockage (amende la ligne de la table
  du 2026-10-10) :

  | Zone nginx | Périmètre | Clé | Taille, inactivité |
  |---|---|---|---|
  | `lodb_archive` | `^/{locale}/[0-9]` | `LODB_ARCHIVE_EPOCH` **.** epoch servie + URL | 20 Go, 90 j, gardée à la 1ʳᵉ demande |

  Une page absente du cache est cherchée dans le stockage (epoch servie,
  `LODB_ARCHIVE_SERVE_EPOCH`), puis rendue par le SSR en repli. Changer d'epoch servie
  change la clé. « Gardée à la 2ᵉ demande » ne protégeait que d'un balayage qui rendait ; un
  raté coûte désormais une lecture du stockage.
- **Classe `archived`** : `s-maxage` d'**un an** au lieu d'une semaine. Une page d'ancien
  patch ne change qu'avec une epoch ; le compromis des deux semaines de chrome périmé
  disparaît, la montée de `LODB_ARCHIVE_EPOCH` reste la purge d'urgence du cache nginx.
- **Bundles** : un bundle que ni l'image ni `front-builds` ne connaissent (404 du SSR) est
  cherché sous `build/` dans le stockage, où le précalcul a copié tout le jeu haché de chaque
  build dont une page est stockée.
- **En-têtes** : la CSP générée au rendu voyage avec la page, en métadonnées de l'objet,
  et nginx la repose ; les en-têtes constants restent à l'edge. `X-LoDb-Source:
  cache|store|ssr` dit d'où vient une page épinglée. Le SSR ne lit toujours aucun cookie et
  ne détient aucun secret ; l'en-tête du précalcul est effacé par nginx côté public.
- **CDN** (prévu, pas encore construit au 2026-10-22 : lot 4 de la
  [spécification](../../produit/pre-rendu-catalogue-cdn.md)) : Cloudflare se placera devant
  `league-of-data-base.com`, `www`, `test` et le `.fr`, et gardera les pages aux durées de
  l'origine (`s-maxage`). `/` n'y sera jamais gardé : sa 302 dépend d'`Accept-Language` et du
  cookie, que Cloudflare ne distingue pas. La grammaire d'URL, les canoniques et les sitemaps
  ne changent pas.

## Amendement du 2026-10-22 — une page indexée par état

*Décidé, pas encore implémenté (lots L0 à L7 de la
[spécification](../../produit/seo-une-page-par-etat.md)). Jusqu'au déploiement qui le porte,
le code applique l'amendement du 2026-10-14.*

La règle du 2026-10-14 n'indexe qu'une page par entité et par langue, celle du dernier patch :
~22 800 pages. L'historique des patchs, qui est le contenu propre de la base, reste hors de
Google. Celle du 2026-07-18 indexait chaque patch : ~200 000 pages par langue sur l'ancien
site, qui n'avait qu'une langue par hôte, et ~4,2 millions sur la nouvelle pile en 21 langues
(du 4 au 11 octobre). Dans les deux cas, **~85 % sont la copie exacte de la page de la version
d'avant** (mesure du 22 octobre 2026 sur Data Dragon : 13,5 % des pages d'objets diffèrent de
la version précédente, 17,7 % des pages de champions, 6,8 % des sorts ; mesure sur le JSON
brut, donc des majorants). La chute d'août,
survenue sur l'ancien site, est venue de ces copies, pas du volume. La charge
qui justifiait la fenêtre du 2026-10-11 est désormais tenue par le stockage pré-rendu et la
file de la demande ([ADR 0011](0011-catalogue-pre-rendu-et-cdn.md)). Constat, mesures et
détail : [spécification](../../produit/seo-une-page-par-etat.md).

| Option | Pour | Contre |
|---|---|---|
| Une page par entité (2026-10-14) | Le signal se concentre sur le dernier patch | L'historique, qui fait la base, n'est pas indexable : ~22 800 pages pour ~28 000 contenus distincts × 21 langues |
| Une page par patch (2026-07-18) | Simple ; tout est indexable | 85 % de copies quasi identiques : la cause de la désindexation d'août |
| **Une page par état** | Chaque contenu distinct a une page indexée, aucune copie n'en a ; ~600 000 pages | Un index entité → états à tenir ; des pages d'anciens patchs qui changent encore (révisables) |

**Décision : une page indexée par état.**

- **État** : la suite des versions listées consécutives où une entité existe (a une fiche)
  avec la même **empreinte** : SHA-256 de ce que sa fiche affiche d'elle-même dans le
  catalogue en_US stocké (projection des entrées du domaine, textes sans balises, images
  ignorées), versionné par un numéro de schéma. Son **ouvreur** est sa première version.
  L'état qui contient le dernier patch est **ouvert**, les autres sont **clos**. La clé d'une
  entité est son id replié en minuscules (Data Dragon a changé la casse d'ids de champions).
- **Index des états** en Postgres, par générations : construit par une commande, puis
  étendu à chaque promotion par le **premier** écouteur, dans un ordre que les écouteurs
  déclarent eux-mêmes (ADR 0003, amendement du 2026-10-22), et rattrapé par une tâche
  périodique toutes les 10 minutes. Il couvre la plus longue suite de versions listées dont
  l'en_US est stocké et qui finit au dernier patch ; chaque processus en tient une copie en
  mémoire qui se recharge d'elle-même.
- **Canoniques** (le dernier patch est celui du lecteur de catalogues, jamais celui de
  l'index) :
  - l'URL courte pour le dernier patch et pour les pages d'anciens patchs de l'état ouvert,
    ou d'une version que l'index ne couvre pas encore ;
  - pour l'ouvreur d'un état clos, lui-même, avec hreflang et JSON-LD ;
  - pour les autres versions d'un état clos, l'ouvreur ;
  - hors de la couverture (version plus ancienne, listée après le dernier patch, ou avant
    toute promotion) : la règle résiduelle, celle du 2026-10-14 pour une version non promue
    (l'URL courte si le dernier patch porte l'entité, sinon la page elle-même).

  `canonical` est toujours présent sur une fiche. Les listes et les variantes régionales ne
  changent pas. Une entité retirée n'est plus un cas à part : le `noindex, follow` de ses
  fiches antérieures disparaît. Un id réutilisé change d'empreinte, donc d'état.
- **Contenu** :
  - l'ouvreur montre « Ce qui a changé dans ce patch » : les champs que l'index marque
    changés, avec les valeurs de la langue de la page ;
  - les autres versions montrent « Inchangé depuis le patch X » ;
  - « Historique » remplace « Dans les patchs précédents » ; la plage de l'état ouvert ne
    nomme pas le dernier patch (« depuis le patch X ») ;
  - l'ouvreur d'un état clos porte la plage de l'état dans son titre, à la place du suffixe
    de version.
- **Page de patch** `/{locale}/{version}/changes`, et `/{locale}/changes` pour le dernier
  patch : ce que la version a changé. Elle existe pour chaque version couverte sauf la
  première (404). `noindex, follow` si la version n'a rien changé. `/{locale}/patches` y mène.
- **Sitemaps** : l'index annonce les `latest.xml`, puis les 21 `{version}.xml` de chaque
  version couverte passée dont le fichier n'est pas vide (~8 440 entrées au plus). Un
  fichier de version liste sa page de patch et les ouvreurs de ses états clos ; il se calcule
  depuis l'index seul, sans catalogue. `lastmod` donne le moment où une page est devenue
  indexable ; aucune date de sortie de patch n'est devinée.
  `LoDb__Seo__SitemapVersionWindow` disparaît.
- **Pages figées et révisables** : une page d'un état clos ne lie que des URL versionnées
  (sauf « Dans le dernier patch ») et ne change plus (classe `archived`). Une page d'ancien
  patch de l'état ouvert prend la classe `revisable` (une heure) et se pré-calcule de
  nouveau, page par page, quand une promotion clôt son état (ADR 0011, amendement du
  2026-10-22).
- **À la promotion, dans cet ordre** :
  1. l'index ;
  2. le pré-calcul (l'unité de la version qui part, avec sa page de patch ; la file des
     pages des états clos) ;
  3. IndexNow (toutes les URL courtes comme avant, les ouvreurs que la promotion a clos, les
     pages de patch qui ont changé quelque chose).

  Les étapes 2 et 3 lisent dans l'index ce qui a changé.
- **Redirections héritées** (amendement du 2026-10-16) : une ancienne URL qui nomme une
  version mène à la canonique de sa cible selon cette règle, l'ouvreur de son état ou l'URL
  courte ; sans état à cette version, l'URL courte si le dernier patch porte l'id, sinon la
  302 vers la liste. Sans version, une entité retirée mène à l'ouvreur de son dernier état.
- **Bascule d'un seul coup** : ni interrupteur, ni coexistence des deux règles. Le
  changement qui porte cette règle retire `CanonicalReferences` et `CanonicalReader`, et son
  déploiement est la bascule. L'index se construit avant que l'API serve : le service
  `states` est lancé par le déploiement après `migrate` et avant la page de maintenance.
  Aucun envoi IndexNow à la bascule : les sitemaps annoncent l'historique. Retour arrière :
  un revert, avec l'epoch 1 du stockage gardée deux semaines (ADR 0011, amendement du
  2026-10-22).

**Mesure** (propriétaire), dans la Search Console, 2, 4 et 8 semaines après la bascule,
comparée au relevé du jour de la bascule :

- les pages indexées, les impressions des URL versionnées et des pages de patch montent ;
- « Explorée, actuellement non indexée » baisse ;
- « Autre page avec balise canonique correcte » monte, ce qui est attendu (les versions d'un
  état qui ne sont pas l'ouvreur) ;
- si les ouvreurs restent « explorés, non indexés », ou si « Google n'a pas choisi la même
  URL canonique que l'utilisateur » gonfle entre deux états voisins, l'empreinte devient
  moins fine (sans les skins, puis sans les champs mineurs) : nouveau schéma, index
  reconstruit, pages changées pré-calculées de nouveau.
