# SEO et indexation : cap, règle en vigueur, suivi

Ce document dit la règle en vigueur pour être indexé, servie en prod depuis le 2026-10-22,
et comment on mesure. Chaque décision vit dans son ADR, amendements
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

## Règle en vigueur : une page indexée par état

Celle de l'amendement du 2026-10-22 de l'ADR 0005 (une page indexée par état d'entité), avec
ceux du même jour des ADR 0003 et 0011 : [spécification](seo-une-page-par-etat.md), dont le
§ 11 dit ce que l'implémentation a tranché. **En vigueur en prod depuis le 2026-10-22 à
16:55 UTC** (bascule ci-dessous) ; elle y remplace la règle du 2026-10-14 (chronologie).

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
| Sitemaps | L'index annonce les 21 `latest.xml`, puis les 21 `{version}.xml` de chaque version couverte passée dont le fichier n'est pas vide (7 518 entrées au 2026-10-22, dont 21 × 357 fichiers de version) ; `{version}.xml` liste la page de patch (si elle a changé quelque chose), puis les ouvreurs des états clos ouverts à cette version, lus dans l'index seul ; `latest.xml` ajoute `/{locale}/changes` ; `lastmod` : la clôture de l'état pour un ouvreur, la promotion qui a fait passer la version (ou la construction de l'index) pour une page de patch ; `max-age=3600` ; 404 hors de la couverture, 503 tant que l'index n'est pas chargé | `src/LoDb.Api/Modules/Seo/Sitemaps/` |
| IndexNow | À chaque promotion : toutes les URL courtes, puis les pages de patch qui ont changé quelque chose depuis le patch promu avant, puis les ouvreurs des états clos depuis ; rien si l'index ne couvre pas la version ; coupé sans clé (`seo.indexnow.planned`) | `src/LoDb.Api/Modules/Seo/IndexNow/` |
| `robots.txt`, `llms.txt` | Générés par l'API ; `llms.txt` cite `/en/changes` | `src/LoDb.Api/Modules/Seo/Files/` |
| Staging | `LODB_NOINDEX=1` : `X-Robots-Tag: noindex, nofollow` et 403 aux robots d'IA, jamais `Disallow: /` | `docker/nginx/snippets/security-headers.conf`, `docker/nginx/nginx.conf` |
| Pages des anciens patchs | Servies par le cache `lodb_archive` (autant que dit la `Cache-Control` de l'objet : un an figée, une heure révisable), le stockage pré-rendu (derrière `LODB_PAGES_STORE_ENABLED`, à `1` en prod depuis le 2026-10-23), puis le SSR dans un budget borné (503 avec `Retry-After` au-delà) ; chaque page refusée entre dans la file de la demande, chaque page d'un état que la promotion clôt dans la file des états clos | ADR 0011, `docker/nginx/sites/site.conf`, `src/LoDb.Api/Modules/Admin/Prerender/Revision/` |

Comportements voulus à ne pas « corriger » : [`pieges.md`](../architecture/pieges.md)
(« Historique des patchs », « Index des états d'entité », pages révisables).

### Limites connues

- **L'epoch servie n'est pas remplie** : nginx lit le stockage en prod depuis le 2026-10-23
  (`LODB_PAGES_STORE_ENABLED=1`), mais l'epoch 2, servie depuis la bascule, attend ses passes.
  Elle ne tient encore que les pages que la file de la demande y a écrites (~29 500 au
  branchement). Une page d'ancien patch absente du cache `lodb_archive` et du stockage passe
  donc par le budget épinglé du SSR (503 au-delà) et la file de la demande. Le levier : la passe
  « ouvreurs d'abord » (~409 000 ouvreurs des états clos, environ 7 h 30 à 900 pages par
  minute), puis la passe entière. La part de 503 servies à Googlebot se suit pendant les
  passes (spécification, § 4.3 et § 12).
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

### Bascule en prod (2026-10-22)

D'un seul coup, sans interrupteur ni coexistence des deux règles (spécification, § 4) : la
bascule est le déploiement de la PR #102 (`test` → `main`, fusion `9b94ddec`), fini à
**16:55 UTC**. Le
service `states` a construit l'index avant la page de maintenance : génération 1, de 0.151.2 à
16.21.1 (404 versions), **20 540 états** (19 472 clos, 1 068 ouverts), soit ~431 000 fiches
indexables dans les 21 langues, sous le majorant de ~600 000 (spécification, § 1.2).
`ENV_PROD` sert l'epoch 2 (`LODB_ARCHIVE_SERVE_EPOCH=2`) ; `LODB_PAGES_STORE_ENABLED` reste à
`0`. Aucun envoi IndexNow : les sitemaps annoncent l'historique (7 518 fichiers), puis IndexNow
annonce à chaque promotion les ouvreurs qu'elle clôt et les pages de patch. Le staging a
basculé à la fusion de la PR #101 dans `dev`, le même jour.

Reste : les passes de l'epoch 2 au pupitre (`/admin/prerender`, « ouvreurs d'abord » puis
entière), puis la lecture du stockage par nginx ; le retrait de l'epoch 1 à partir du
2026-11-05. Retour arrière : un revert, l'epoch 1 gardée deux semaines (spécification, § 4.4).
Relevé de départ, comptes et vérifications : spécification, § 12.

## Mesure

Le propriétaire relève la Search Console le jour de la bascule (relevé de départ :
spécification, § 12), puis 2, 4 et 8 semaines après, les 2026-11-05, 2026-11-19 et
2026-12-17 :

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
| 2026-10-14 | Une page indexée par entité : canonique d'un ancien patch sur l'URL courte, entité retirée indexée à sa dernière version | ADR 0005, am. 10-14 | En prod jusqu'au 2026-10-22 (bascule par état) |
| 2026-10-16 | Anciennes URL versionnées en 301 vers l'URL courte | ADR 0005, am. 10-16 | En prod jusqu'au 2026-10-22, sauf pour une voie de runes retirée, où elle reste |
| 2026-10-20 | Pages des anciens patchs pré-rendues dans un stockage objet, `archived` gardée un an ; CDN prévu | ADR 0011, ADR 0005 am. 10-20 | Stockage écrit, lu par nginx en prod depuis le 2026-10-23 (`LODB_PAGES_STORE_ENABLED=1`) ; CDN non construit |
| 2026-10-21/22 | SSR dédié et régulateur de la passe, file de la demande, vitesse réglable | ADR 0011, amendements | Oui |
| **2026-10-22** | **Une page indexée par état** : un ouvreur indexé par état d'entité, une page par patch, sitemaps des versions passées, pages révisables | ADR 0005, 0003 et 0011, am. 10-22 ; [spécification](seo-une-page-par-etat.md) | **Oui** : en prod depuis le 2026-10-22 |
| 2026-10-22, 16:55 UTC | Bascule de la prod sur la règle par état (PR #102, `9b94ddec`), epoch 2 servie | [spécification](seo-une-page-par-etat.md), § 12 | — |

Diagnostic de la désindexation d'août et demande du 2026-10-14 :
[`seo-anciens-patchs.md`](seo-anciens-patchs.md).
