# SEO et indexation : cap, règle en vigueur, suivi

Ce document dit la règle que le code applique pour être indexé, celle que la prod sert
jusqu'à la bascule, et comment on mesure. Chaque décision vit dans son ADR, amendements
datés compris (le plus récent l'emporte) : [ADR 0005](../reecriture/adr/0005-web-ssr-urls-et-seo.md) pour
les URL, les canoniques et les sitemaps, [ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md)
pour les pages des anciens patchs. L'état des lieux du site Symfony (juillet 2026) est archivé :
[`legacy/docs/produit/seo-indexabilite.md`](../../legacy/docs/produit/seo-indexabilite.md).

## Cap

- **Objectif** : le trafic organique, donc un site correctement indexé. Le SEO est le premier
  canal d'acquisition (ADR 0005).
- **LoDb est une base de données** : chaque contenu distinct qu'elle tient (chaque état d'une
  entité, chaque patch) doit être trouvable et avoir **une** page indexée ; une copie désigne
  l'original. On ne met pas le dernier patch en avant au détriment de l'historique.
- **Arbitrage** : entre deux options, celle qui sert l'indexation et le trafic l'emporte. On
  ne bâtit pas sur un contournement en attendant une fenêtre de mesure : on pose la règle
  propre, on relève une base de départ, puis on ajuste sur la mesure.

## Règle en vigueur dans le code : une page indexée par état

Celle de l'amendement du 2026-10-22 de l'ADR 0005 (une page indexée par état d'entité), avec
ceux du même jour des ADR 0003 et 0011 : [spécification](seo-une-page-par-etat.md), dont le
§ 11 dit ce que l'implémentation a tranché. **En vigueur dans le code** ; la prod la sert à la
bascule de son déploiement (ci-dessous) et applique jusque-là la règle du 2026-10-14.

Un **état** est une suite de patchs consécutifs où la fiche d'une entité ne change pas
(empreinte de ce qu'elle affiche dans le catalogue en_US, images ignorées). Son premier patch,
l'**ouvreur**, est la page indexée de l'état ; l'état qui contient le dernier patch est
**ouvert**, les autres sont **clos**. Le dernier patch qui décide est celui du lecteur de
catalogues, jamais celui de l'index.

| Sujet | Règle | Code |
|---|---|---|
| Grammaire d'URL | `/{locale}/…` (21 locales) ; l'URL courte sert le dernier patch, `/{locale}/{version}/…` un ancien patch ; la version du dernier patch dans le chemin fait une 301 vers l'URL courte ; l'id fait foi, un slug faux ou absent fait une 301 ; `/` fait une 302 selon `Accept-Language` ; segment `changes` pour la page de patch | `tools/routing/check-urls.sh` |
| Fiche du dernier patch | Sa propre canonique, `index, follow`, hreflang des 21 langues + `x-default`, JSON-LD | `core/seo/head/build-head-tags.ts`, `core/seo/urls/alternates-of.ts` |
| Fiche d'ancien patch, **ouvreur d'un état clos** | Sa propre canonique, `index, follow`, hreflang des 21 langues à la même version, JSON-LD ; titre suffixé de la plage de l'état (`seo.state_range_suffix`), description qui commence par ses changements (`seo.state.*`) ; page figée (`archived`) | `StateView` (`src/LoDb.Ingestion/Catalog/States/Canonicals/`), `features/catalogue/shared/codex/state/head/` |
| Fiche d'ancien patch, autre version d'un état clos | Canonique sur l'ouvreur, même langue ; `index, follow` ; ni hreflang ni JSON-LD ; page figée | idem |
| Fiche d'ancien patch de l'**état ouvert**, ou d'une version que l'index ne couvre pas encore | Canonique sur l'URL courte ; `index, follow` ; ni hreflang ni JSON-LD ; page révisable (`revisable`, une heure), recalculée quand une promotion clôt l'état | idem, `core/routing/version/entry-cache-class-of.ts` |
| Fiche hors de la couverture de l'index (plus ancienne que son début, listée après le dernier patch, avant toute promotion) | Règle résiduelle : l'URL courte si le dernier patch tient l'entité, sinon la page elle-même | `StateView` |
| Entité retirée | Plus de cas à part : son dernier état est indexé à son ouvreur, comme ses états antérieurs ; aucune fiche `noindex` | idem |
| Listes | Canonique sur la liste nue du dernier patch ; au-delà de la première page, `?page=N` est sa propre canonique | `features/catalogue/shared/seo/catalogue-list-seo.ts` |
| Page de patch | `/{locale}/changes` (dernier patch), `/{locale}/{version}/changes` (ancien patch) : ce que la version a changé ; sa propre canonique ; `noindex, follow` si le patch n'a rien changé ; 404 pour la première version couverte, hors de l'index et après le dernier patch ; `/{locale}/patches` y mène | `src/LoDb.Api/Modules/Catalog/Changes/`, `features/catalogue/changes/` |
| Anciennes URL (ancien site) | 301 directe vers la canonique de l'état à la version nommée (l'ouvreur d'un état clos, l'URL courte de l'état ouvert) ; sans état à cette version, l'URL courte si le dernier patch porte l'id, sinon la fiche épinglée, sinon une 302 vers la liste ; sans version, un champion, un objet ou un sort retiré mène à l'ouvreur de son dernier état ; une voie de runes absente du dernier patch garde la règle du 2026-10-16 ; une liste mène à la liste nue | `src/LoDb.Api/Modules/Legacy/` |
| Sitemaps | L'index annonce les 21 `latest.xml`, puis les 21 `{version}.xml` de chaque version couverte passée dont le fichier n'est pas vide (~8 440 entrées au plus) ; `{version}.xml` liste la page de patch (si elle a changé quelque chose), puis les ouvreurs des états clos ouverts à cette version, lus dans l'index seul ; `latest.xml` ajoute `/{locale}/changes` ; `lastmod` : la clôture de l'état pour un ouvreur, la promotion qui a fait passer la version (ou la construction de l'index) pour une page de patch ; `max-age=3600` ; 404 hors de la couverture, 503 tant que l'index n'est pas chargé | `src/LoDb.Api/Modules/Seo/Sitemaps/` |
| IndexNow | À chaque promotion : toutes les URL courtes, puis les pages de patch qui ont changé quelque chose depuis le patch promu avant, puis les ouvreurs des états clos depuis ; rien si l'index ne couvre pas la version ; coupé sans clé (`seo.indexnow.planned`) | `src/LoDb.Api/Modules/Seo/IndexNow/` |
| `robots.txt`, `llms.txt` | Générés par l'API ; `llms.txt` cite `/en/changes` | `src/LoDb.Api/Modules/Seo/Files/` |
| Staging | `LODB_NOINDEX=1` : `X-Robots-Tag: noindex, nofollow` et 403 aux robots d'IA, jamais `Disallow: /` | `docker/nginx/snippets/security-headers.conf`, `docker/nginx/nginx.conf` |
| Pages des anciens patchs | Servies par le cache `lodb_archive` (autant que dit la `Cache-Control` de l'objet : un an figée, une heure révisable), le stockage pré-rendu (derrière `LODB_PAGES_STORE_ENABLED`), puis le SSR dans un budget borné (503 avec `Retry-After` au-delà) ; chaque page refusée entre dans la file de la demande, chaque page d'un état que la promotion clôt dans la file des états clos | ADR 0011, `docker/nginx/sites/site.conf`, `src/LoDb.Api/Modules/Admin/Prerender/Revision/` |

Comportements voulus à ne pas « corriger » : [`pieges.md`](../architecture/pieges.md)
(« Historique des patchs », « Index des états d'entité », pages révisables).

### Limites connues

- **La bascule sert une epoch vide** : à 900 pages par minute, ~11 h pour les ~600 000
  ouvreurs, ~3 jours pour le reste ; d'ici là, une page absente du stockage passe par le budget
  épinglé du SSR (503 au-delà) et la file de la demande. La part de 503 servies à Googlebot se
  suit pendant la passe (spécification, § 4.3).
- **Pages révisables** : la copie d'avant d'une page dont l'état vient de se clore peut être
  servie environ deux heures après son nouveau rendu ; Bing peut la lire via IndexNow. Accepté.
- **Ce que l'empreinte ne voit pas** : une correction de traduction seule, un changement de
  balisage seul, une icône redessinée sous le même nom n'ouvrent pas d'état. À l'inverse, une
  chroma que CommunityDragon ne connaît pas encore au moment de l'ingestion ouvre un état
  pour elle seule, et un champion réduit à son résumé (versions sans `championFull`) en ouvre
  deux.
- **Début de la couverture** : le premier état de chaque entité y est `baseline` (comparé à
  rien) : ni résumé ni bloc, et cette version n'a pas de page de patch.
- **« Dans le dernier patch »** sur une page figée mène à l'URL courte, en 404 si l'entité est
  retirée plus tard ; « Changé au patch X » mène à une 301 tant que X est le dernier patch.
- **Comptes** : les ~600 000 pages indexables et les 85 % de copies (spécification, § 1.2)
  sont des majorants mesurés sur le JSON brut ; les comptes réels viendront de la première
  construction de l'index en prod (ligne `states.update.completed`).

### Bascule en prod

D'un seul coup : ni interrupteur ni coexistence des deux règles ; le déploiement du changement
qui porte la règle est la bascule (spécification, § 4). L'index des états se construit avant
que l'API serve (service `states`, avant la page de maintenance), l'epoch 2 du stockage
devient l'epoch servie et les passes la remplissent, ouvreurs d'abord. Aucun envoi IndexNow à
la bascule : les sitemaps annoncent l'historique, puis IndexNow annonce à chaque promotion les
ouvreurs qu'elle clôt et les pages de patch. Retour arrière : un revert, l'epoch 1 gardée deux
semaines. Un commit de documentation datera la bascule et passera ce document à « en vigueur ».

## Règle servie en prod jusqu'à la bascule (2026-10-14)

Celle de l'amendement du 2026-10-14 de l'ADR 0005, complété le 2026-10-16 (anciennes URL) ;
les pages d'anciens patchs sont servies depuis le stockage pré-rendu depuis le 2026-10-20.

- **Fiche d'un ancien patch, entité présente au dernier patch** : canonique et `og:url` sur
  l'URL courte (chemin et slug du dernier patch), `index, follow`, ni hreflang ni JSON-LD.
- **Entité retirée** : indexable dans la dernière version qui la portait ; ses fiches
  antérieures en `noindex, follow`, sans canonique.
- **Anciennes URL** : l'URL courte si l'entité existe au dernier patch, la liste nue pour une
  liste, la fiche épinglée de la version nommée pour une entité retirée.
- **Sitemaps** : l'index n'annonce que les `latest.xml` des 21 langues
  (`LoDb__Seo__SitemapVersionWindow` à `0`) ; le sitemap d'une version passée ne liste que les
  fiches canoniques chez lui.
- **IndexNow** : les URL courtes du patch promu, dans les 21 langues.
- **Limites** : l'historique est hors de l'index (~22 800 pages indexables, 1 085 × 21
  langues, pour ~28 000 contenus distincts × 21 langues) ; une page d'ancien patch gardée un
  an ou stockée garde la canonique de son rendu, en 404 ou en 301 quand l'entité a été
  retirée ou renommée depuis (ADR 0011, note de § nginx).

## Mesure

Le propriétaire relève la Search Console le jour de la bascule, puis 2, 4 et 8 semaines
après :

- pages indexées, impressions des URL versionnées et des pages de patch ;
- « Explorée, actuellement non indexée », qui doit baisser ;
- « Autre page avec balise canonique correcte », qui monte (attendu) ;
- « Google n'a pas choisi la même URL canonique que l'utilisateur » : s'il gonfle entre deux
  états voisins, l'empreinte devient moins fine (spécification, § 5).

Côté site : les 503 servies aux robots et la source des pages épinglées (`X-LoDb-Source`,
journal de l'edge), l'audience Umami ([`analytics.md`](../architecture/analytics.md)). Les
résultats s'écrivent dans la spécification et dans l'amendement de l'ADR 0005.

## Chronologie des règles

| Date | Règle | Où | En vigueur |
|---|---|---|---|
| 2026-07-18 | Ancien site : une URL par patch, propre canonique ; une sitemap par version ; une langue par hôte | archive `legacy/docs/produit/` | Non |
| 2026-09-24 | Nouvelle pile : locale dans l'URL, hreflang des 21 langues ; anciens patchs à leur propre canonique, index des sitemaps sur toutes les versions | ADR 0005, texte | Grammaire et hreflang oui ; canoniques et sitemaps non |
| 2026-10-04 | Bascule de la prod sur la nouvelle pile | [`bascule.md`](../reecriture/bascule.md) | — |
| 2026-10-10 | Cache des anciens patchs par epoch (7 jours), bundles gardés 30 jours | ADR 0005, am. 10-10 | Non : remplacé le 2026-10-20 |
| 2026-10-11 | Index des sitemaps borné à 12 versions ; anciens patchs découverts par des liens | ADR 0005, am. 10-11 | Liens oui ; fenêtre non (`0` depuis le 2026-10-14, retirée du code le 2026-10-22) |
| 2026-10-12 | `?page=N` canonique, `lastmod` daté par patch, IndexNow, 301 de la barre finale | changelog du 2026-10-12 | Oui |
| 2026-10-14 | Une page indexée par entité : canonique d'un ancien patch sur l'URL courte, entité retirée indexée à sa dernière version | ADR 0005, am. 10-14 | En prod jusqu'à la bascule ; remplacée dans le code |
| 2026-10-16 | Anciennes URL versionnées en 301 vers l'URL courte | ADR 0005, am. 10-16 | En prod jusqu'à la bascule ; remplacée dans le code, sauf pour une voie de runes retirée |
| 2026-10-20 | Pages des anciens patchs pré-rendues dans un stockage objet, `archived` gardée un an ; CDN prévu | ADR 0011, ADR 0005 am. 10-20 | Stockage oui ; CDN non construit |
| 2026-10-21/22 | SSR dédié et régulateur de la passe, file de la demande, vitesse réglable | ADR 0011, amendements | Oui |
| **2026-10-22** | **Une page indexée par état** : un ouvreur indexé par état d'entité, une page par patch, sitemaps des versions passées, pages révisables | ADR 0005, 0003 et 0011, am. 10-22 ; [spécification](seo-une-page-par-etat.md) | **En vigueur dans le code** ; en prod à la bascule |

Diagnostic de la désindexation d'août et demande du 2026-10-14 :
[`seo-anciens-patchs.md`](seo-anciens-patchs.md).
