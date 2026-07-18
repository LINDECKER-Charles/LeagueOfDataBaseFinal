# Indexabilité — état des lieux et feuille de route

Liste priorisée des points d'amélioration SEO. Les cases cochées ont été livrées
par le lot « SEO fondations » (2026-07-17, cf. changelog). Le reste est classé
par rapport impact/effort, trade-offs explicités.

## P0 — corrigé dans ce lot

- [x] **Vraies 404 sur les fiches inconnues** — `/champion/Inconnu` renvoyait un
  302 vers la home (soft-404 pénalisé). Désormais : `ResourceNotFoundException`
  levée par les managers → `NotFoundHttpException` (404) via
  `AbstractResourceController::detailFailure()`. Les erreurs transitoires
  (upstream 5xx) conservent le redirect + flash. Pages d'erreur Hextech
  (`templates/bundles/TwigBundle/Exception/`, rendues en prod).
- [x] **Sitemap XML** — `SitemapController` sur `/sitemap.xml` : home, 4 listes,
  toutes les fiches (slugs via `listIndex()`, dernière version, réf. `en_US`),
  légal, don. `Cache-Control: public, max-age=3600`. Déclaré dans `robots.txt`
  et via `<link rel="sitemap">`.
- [x] **robots.txt** — Disallow des zones privées (login, register, profile,
  builds, api, tunnel de don) + directive `Sitemap` absolue.
- [x] **JSON-LD** — `WebSite` + `Organization` globaux ; `BreadcrumbList` +
  `VideoGame` (`character` Person pour les champions, `gameItem` Thing pour
  objets/runes/sorts) sur les détails ; `ItemList` (20 entrées) sur les listes.
  Encodage anti-XSS centralisé dans `JsonLdBuilder`.
- [x] **Canonical** — `<link rel="canonical">` = scheme+host+path **sans query** :
  les variantes `?version&lang` ne créent plus de duplication d'index. `og:url`
  aligné sur la canonical.

## P1 — corrigé dans ce lot

- [x] **Titles/meta uniformisés, 21 locales** — domaine de traduction dédié
  `seo` (`translations/seo.<loc>.yaml`). Pattern « {Nom}, {type} — League Of
  Data Base » via `seo_title()` ; pagination et numéro de patch retirés des
  titles (le patch reste dans les descriptions). Home, 4 listes, 4 détails,
  donate, meta des pages légales, pages d'erreur.
- [x] **og:site_name / og:locale / og:image absolue** — og:image était relative
  (ignorée par les crawlers sociaux) ; désormais absolutisée (`seo_absolute`),
  og:locale mappée depuis la locale UI (`OgLocale`).

## P2 — restant, par priorité décroissante

### 1. Search Console + Bing Webmaster (effort : minutes, impact : indispensable)

Aucune propriété configurée. À faire dès la mise en prod du lot : vérifier le
domaine (DNS TXT), soumettre `https://league-of-data-base.com/sitemap.xml`,
surveiller la couverture (soft-404 résiduels, pages exclues). C'est aussi le
seul moyen de mesurer l'effet des lots SEO.

### 2. Cohérence de l'host canonique — [x] tranché : `.com` canonique

`.com` = host canonique. Le `.fr` est **retiré** : redirect **301 par-URL
`.fr` → `.com`** au niveau **nginx** (`docker/nginx/default.conf`), remplaçant
l'ancien interstitiel `200` (`RetiredDomainSubscriber`, supprimé). Le TLD `.fr`
ne pilote plus que la locale par défaut côté PHP, désormais inatteignable pour du
trafic réel. Suivi désindexation : propriétés vérifiées en **DNS TXT** + outil
**« Changement d'adresse »** Search Console, 301 conservé ≥ 1 an.

- Reste : `legal.site_url` pointe encore sur `www.league-of-data-base.com` alors
  que `CADDY_DOMAINS` ne déclare que les apex (pas de cert `www`) — le `www` est
  301'd vers l'apex, mais la source de vérité devrait être l'apex `.com`.

### 3. hreflang — bloqué par la locale en session (effort : majeur, impact : fort)

**Impossible proprement aujourd'hui** : la langue de l'UI vit en session
(`LocaleSubscriber`), l'URL ne la porte pas. `hreflang` exige une URL par
langue. Publier des hreflang vers `?lang=fr_FR` serait mensonger (la locale UI
ne suit pas le paramètre `lang`, qui ne pilote que les données).

- Résolution cible : locale dans le chemin (`/fr/champion/Aatrox`), refonte du
  routing + `LocaleSubscriber` + liens internes + sitemap par langue.
- Impacts en cascade : les réponses sont aujourd'hui `Cache-Control: private`
  (session) ; une locale dans l'URL est le prérequis pour un cache partagé
  (`s-maxage` + ETag au niveau edge/CDN) — gain SEO (TTFB) et infra.
- D'ici là : le site est indexé dans **une** langue par host (TLD → défaut
  fr/en), assumé.

### 4. Fallback SSR des contenus en îlots Vue (effort : moyen, impact : moyen)

La galerie de skins (SkinGallery) et d'autres îlots rendent côté client : leurs
contenus (noms de skins, chromas) sont invisibles au premier passage crawler
malgré le rendu JS de Google (différé/aléatoire). Ajouter un fallback `<noscript>`
ou un rendu serveur minimal (liste `<ul>` des skins avec URLs splash) dans les
templates détail. Le showcase des sorts a déjà un fallback no-JS — s'aligner.

### 5. Breadcrumbs UI visibles (effort : faible, impact : faible/moyen)

Le `BreadcrumbList` JSON-LD est livré, mais sans fil d'Ariane visible dans la
page. Google privilégie la cohérence balisage/visuel. Ajouter un breadcrumb
discret (Accueil › Champions › Aatrox) sur les 4 détails — composant simple,
améliore aussi la navigation mobile.

### 6. Métadonnées de fraîcheur (effort : faible, impact : faible)

- `<lastmod>` dans le sitemap : nécessiterait de dater l'ingestion par version
  (aujourd'hui non tracé) — utile quand un patch sort.
- `article:modified_time` / datePublished sur les fiches : idem, dépend d'un
  suivi de version→date (l'API versions de DDragon ne le fournit pas ; table de
  correspondance patch→date à maintenir ou dériver du premier fetch).

### 7. Performance crawl (effort : moyen, impact : moyen)

- Les listes rendent **tout** le dataset (render-all volontaire, cf.
  architecture) : ~700 objets = HTML lourd pour Googlebot. Acceptable
  aujourd'hui ; si la couverture de crawl se dégrade, envisager une pagination
  SSR dédiée aux crawlers (`?page=` canonicalisée) — trade-off avec le choix
  render-all + filtre client.
- `Cache-Control: private` sur toutes les pages HTML (session) : voir point 3,
  le cache partagé dépend de la sortie de la locale de la session.

### 8. Images de partage par entité (effort : moyen, impact : moyen)

og:image est aujourd'hui l'aperçu générique de section (`/preview/*.png`) sauf
runes (icône réelle). Utiliser le splash du champion / l'icône de l'objet en
og:image par fiche (URLs DDragon directes pour les splash — déjà la politique
d'hotlink assumée) rendrait chaque partage unique. Attention aux dimensions
minimales Twitter (144×144) pour les icônes 64px → préférer splash/loading art.

### 9. Contenu 404 utile (effort : faible, impact : faible)

La 404 propose le retour accueil ; y ajouter une recherche ou les liens des 4
sections augmenterait la rétention des visiteurs arrivant sur un slug mort
(ancien patch). Nécessite le view-model `client` dans le contexte d'erreur →
soit un `ErrorController` custom, soit des liens statiques (suffisant).

### 10. Divers hygiène

- `/u/{username}` : profils publics indexables — vérifier qu'un profil vide
  n'est pas généré en masse (thin content) ; sinon `noindex` conditionnel.
- `/working-progress` : garde son meta noindex, volontairement crawlable.
- Trailing data : `app/public/upload/` (reliquat pré-MinIO) est servi
  statiquement — ne pas laisser Google le découvrir via des liens morts
  (aucun lien connu aujourd'hui ; à purger un jour).

## Politique canonique (référence)

> Révisée le 2026-07-18 : passage aux **URLs de patch indexables** (cf. changelog
> `2026-07-18-seo-urls-versionnees-sitemaps`).

- **Dernier patch** — URL courte `/champion/Aatrox`, canonique, non versionnée :
  la page mise en avant. `?version&lang` reste une variante de rendu de cette page
  (query strippée du canonical, jamais indexée séparément).
- **Patchs historiques** — URL versionnée `/{version}/champion/Aatrox`,
  **self-canonical** et indexable : une page par (entité, patch), titre suffixé du
  patch pour la différenciation. Le dernier patch en `/{version}/…` fait un **301**
  vers l'URL courte — jamais de doublon.
- **Sitemap-index** — `/sitemap.xml` → `/sitemaps/latest.xml` (principal, dernier
  patch, URLs courtes) + une `/sitemaps/{version}.xml` par patch historique
  (immutable). XML non traduit (standard).
- Précédence de version : segment de chemin `/{version}/…` > query `?version=` >
  session ; langue découplée (query `?lang=` > session).
- La locale UI ne participe toujours pas à l'URL tant que le point 3 (hreflang)
  n'est pas traité.
