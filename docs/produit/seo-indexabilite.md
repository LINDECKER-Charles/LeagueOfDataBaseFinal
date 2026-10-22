# SEO et indexation : cap, règle en vigueur, suivi

Ce document dit ce que le site applique aujourd'hui pour être indexé, la règle décidée qui la
remplacera, et comment on mesure. Chaque décision vit dans son ADR, amendements datés compris
(le plus récent l'emporte) : [ADR 0005](../reecriture/adr/0005-web-ssr-urls-et-seo.md) pour
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

## Règle en vigueur dans le code

Celle de l'amendement du 2026-10-14 de l'ADR 0005, complété le 2026-10-16 (anciennes URL) ; les
pages d'anciens patchs sont servies depuis le stockage pré-rendu depuis le 2026-10-20.

| Sujet | Règle | Code |
|---|---|---|
| Grammaire d'URL | `/{locale}/…` (21 locales) ; l'URL courte sert le dernier patch, `/{locale}/{version}/…` un ancien patch ; la version du dernier patch dans le chemin fait une 301 vers l'URL courte ; l'id fait foi, un slug faux ou absent fait une 301 ; `/` fait une 302 selon `Accept-Language` | `tools/routing/check-urls.sh` |
| Fiche du dernier patch | Sa propre canonique, `index, follow`, hreflang des 21 langues + `x-default`, JSON-LD | `core/seo/head/build-head-tags.ts`, `core/seo/urls/alternates-of.ts` |
| Fiche d'un ancien patch, entité présente au dernier patch | Canonique et `og:url` sur l'URL courte (chemin et slug du dernier patch), `index, follow`, ni hreflang ni JSON-LD | `CanonicalReferences` (`src/LoDb.Ingestion/Catalog/Canonicals/`) |
| Entité retirée | Indexable dans la dernière version qui la portait (sa propre canonique, hreflang, JSON-LD) ; ses fiches antérieures en `noindex, follow`, sans canonique | idem |
| Listes | Canonique sur la liste nue du dernier patch ; au-delà de la première page, `?page=N` est sa propre canonique | `features/catalogue/shared/seo/catalogue-list-seo.ts` |
| Anciennes URL (ancien site) | 301 directe vers la canonique de la cible : l'URL courte si l'entité existe au dernier patch, la liste nue pour une liste, la fiche épinglée de la version nommée pour une entité retirée (indexable seulement si c'est la dernière version qui la portait) | `src/LoDb.Api/Modules/Legacy/` |
| Sitemaps | L'index n'annonce que les `latest.xml` des 21 langues (`LoDb__Seo__SitemapVersionWindow`, `0` par défaut) ; le sitemap d'une version passée reste servi et ne liste que les fiches canoniques chez lui ; `lastmod` = la promotion qui a changé la page ; `max-age=3600` | `src/LoDb.Api/Modules/Seo/Sitemaps/`, `SeoOptions.cs` |
| IndexNow | À chaque promotion, les URL courtes que le patch a changées, dans les 21 langues ; coupé sans clé | `src/LoDb.Api/Modules/Seo/IndexNow/IndexNowSubmitter.cs` |
| `robots.txt`, `llms.txt` | Générés par l'API | `src/LoDb.Api/Modules/Seo/Files/` |
| Staging | `LODB_NOINDEX=1` : `X-Robots-Tag: noindex, nofollow` et 403 aux robots d'IA, jamais `Disallow: /` | `docker/nginx/snippets/security-headers.conf`, `docker/nginx/nginx.conf` |
| Pages des anciens patchs | Servies par le cache `lodb_archive`, le stockage pré-rendu (lu derrière `LODB_PAGES_STORE_ENABLED`), puis le SSR dans un budget borné (503 avec `Retry-After` au-delà) ; chaque page refusée entre dans la file de la demande | ADR 0011, `docker/nginx/sites/site.conf` |

Comportements voulus à ne pas « corriger » : [`pieges.md`](../architecture/pieges.md).

### Limites connues

- **L'historique est hors de l'index** : ~22 800 pages indexables (1 085 × 21 langues) pour
  ~28 000 contenus distincts × 21 langues.
- **Les copies** : ~85 % des 4,19 millions de pages d'anciens patchs reprennent exactement la
  page de la version d'avant (mesure du 2026-10-22,
  [spécification](seo-une-page-par-etat.md), § 1.2).
- **Canoniques figées** : une page d'ancien patch gardée un an ou stockée par le précalcul
  garde la canonique de son rendu. Une entité retirée ou renommée depuis y pointe vers une
  URL courte en 404 ou en 301, jusqu'à l'epoch suivante (ADR 0011, note de § nginx).

## Règle décidée : une page indexée par état

Décidée le 2026-10-22, **pas encore implémentée** : [spécification](seo-une-page-par-etat.md)
(lots L0 à L7), ADR 0005 et ADR 0011, amendements du 2026-10-22.

- Un **état** est une suite de patchs consécutifs où la fiche d'une entité ne change pas
  (empreinte de son contenu en_US, images ignorées). Son premier patch, l'**ouvreur**, est la
  page indexée de l'état ; les autres patchs de l'état ont l'ouvreur pour canonique, et ceux
  de l'état en cours l'URL courte.
- Chaque patch a sa page de changements (`/{locale}/[{version}/]changes`), et l'index des
  sitemaps annonce de nouveau toutes les versions.
- ~600 000 pages indexables, chacune distincte ; plus de cas à part pour une entité retirée.
- **Bascule d'un seul coup** : ni interrupteur ni coexistence des deux règles ; le déploiement
  du changement qui porte la règle est la bascule. L'index des états se construit avant que
  l'API serve, l'epoch 2 du stockage devient l'epoch servie et la passe la remplit, pages
  indexables d'abord ; IndexNow annonce les ouvreurs et les pages de patch. Retour arrière :
  un revert, l'epoch 1 gardée deux semaines.

Tant que ce changement n'est pas déployé, la règle en vigueur est celle du tableau ci-dessus.
À la bascule, ce document change de règle en vigueur et la chronologie la note.

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
| 2026-10-11 | Index des sitemaps borné à 12 versions ; anciens patchs découverts par des liens | ADR 0005, am. 10-11 | Liens oui ; fenêtre non (`0` depuis le 2026-10-14) |
| 2026-10-12 | `?page=N` canonique, `lastmod` daté par patch, IndexNow, 301 de la barre finale | changelog du 2026-10-12 | Oui |
| **2026-10-14** | **Une page indexée par entité** : canonique d'un ancien patch sur l'URL courte, entité retirée indexée à sa dernière version | ADR 0005, am. 10-14 | **Oui (code)** |
| 2026-10-16 | Anciennes URL versionnées en 301 vers l'URL courte | ADR 0005, am. 10-16 | Oui |
| 2026-10-20 | Pages des anciens patchs pré-rendues dans un stockage objet, `archived` gardée un an ; CDN prévu | ADR 0011, ADR 0005 am. 10-20 | Stockage oui ; CDN non construit |
| 2026-10-21/22 | SSR dédié et régulateur de la passe, file de la demande, vitesse réglable | ADR 0011, amendements | Oui |
| 2026-10-22 | **Une page indexée par état** | ADR 0005 am. 10-22, [spécification](seo-une-page-par-etat.md) | Décidée, pas implémentée |

Diagnostic de la désindexation d'août et demande du 2026-10-14 :
[`seo-anciens-patchs.md`](seo-anciens-patchs.md).
