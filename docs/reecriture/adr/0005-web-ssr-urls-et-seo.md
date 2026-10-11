# ADR 0005 — Web : SSR Angular, URLs localisées, SEO

- **Statut** : acceptée — 2026-09-24
- **Remplace** : rendu Twig + îlots Vue, locale portée par la session et un cookie
- **Amendement** : le 2026-10-10, les pages des anciens patchs ont leur propre cache, dont la
  clé ne contient plus la révision de déploiement (voir § « Amendement du 2026-10-10 »).

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
/{locale}/{version}/champions/{id}              détail, patch passé (auto-canonique)
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
  l'edge (nginx), pas dans l'app.
- **hreflang** entre les 21 locales + `x-default` (nouveau).
- **Sitemaps par locale** ; les sitemaps des versions passées restent immuables.
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

- **Epoch** (`LODB_ARCHIVE_EPOCH`) : la seule purge des anciens patchs. Elle se monte à la
  main quand le chrome, la CSP ou le contrat `/api` changent de façon visible, et après tout
  correctif de sécurité du front, avec le retrait du chunk vulnérable
  ([procédure](../../guides/configuration.md)). Compromis assumé : entre deux montées, le
  chrome d'une page d'ancien patch peut dater d'une semaine.
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
