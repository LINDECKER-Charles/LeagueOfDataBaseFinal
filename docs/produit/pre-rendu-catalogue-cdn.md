# Catalogue pré-rendu : stockage objet et CDN

Chantier décidé le 20 octobre 2026. Toutes les pages des anciens patchs sont calculées une
fois, rangées dans un stockage objet (Scaleway, Paris), et le site ne fait plus que les
servir. Un CDN (Cloudflare) les rapproche des visiteurs du monde entier. Le rendu SSR ne
reste nécessaire que pour le dernier patch et pour les rares pages absentes du stockage.

Ce chantier remplace la spécification du cache recyclé de 68 Go, interrompue et jamais
publiée. Il en reprend le constat, la voie d'admission dédiée et l'ordre de passage par
(version, langue) (§ 4, lot 2).

Revue du même jour : la spécification intègre ce que les documentations de Scaleway et de
Cloudflare et le code ont confirmé ou démenti. La décision est consignée dans
l'[ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md). Les options retenues
aux questions encore ouvertes (§ 2) sont provisoires.

## 1. Constat

**Trafic** (journal de l'edge et métriques, 24 h avant le 2026-10-20 à 10:30 UTC, relevé de
la spécification précédente) :

- 343 795 requêtes, six fois plus qu'une semaine plus tôt. 87 % visent une version épinglée
  dans le chemin (`^/{locale}/[0-9]`).
- Cache `lodb_archive` : 2,5 % de `HIT`. Les robots demandent 1,5 fois chaque adresse en
  7 jours : une page gardée à sa 2ᵉ demande et pendant une semaine n'est presque jamais
  resservie.
- Le budget des rendus épinglés (60 par minute) est saturé en permanence : 139 165 rendus
  refusés et **142 467 réponses 503** par jour. Les robots déclarés les subissent : 65 % de
  503 pour Bingbot, 53 % pour ClaudeBot, 47 % pour GPTBot, 39 % pour Applebot.

**Coût d'un rendu** : environ 0,1 s du moteur SSR (un seul thread Node) et autant de l'API,
qui charge le catalogue de chaque (version, langue) qu'elle ne garde plus
(`src/server/admission/read-render-limits.ts`). Le délai mesuré depuis l'extérieur est de
0,5 à 1,5 s par page non gardée.

**Taille du catalogue** (relevé du 20 octobre 2026) :

| Élément | Valeur | Source |
|---|---|---|
| Anciens patchs | 402 : les 403 versions valides de `versions.json`, moins la dernière | `PatchVersions.Normalize` (les `lolpatch_*` sont exclus) |
| Langues | 21 | `UiLocales` |
| Pages par (version, langue) | 496 en moyenne : 4 listes et les entités listées, de 323 (0.152) à 1 132 (16.20) | 51 versions de Data Dragon, filtres de `ItemDebris` et `SummonerDebris` |
| **Pages au total** | **4,19 millions**, et 4,5 millions avec les pages `?page=N` des listes | |
| Taille d'une page | 39 Ko en gzip : 37 à 38 Ko pour une fiche, quels que soient la langue et le patch, 40 à 62 Ko pour une liste | 41 554 fichiers du volume `archive-cache` de prod |
| **Volume total** | **~155 Go**, ~165 Go sur disque | Le VPS a 136 Go libres sur 193 Go |

Les jeux de données des 403 versions × 21 langues sont déjà ingérés (12 Go dans le volume
`ddragon`) : le précalcul ne télécharge rien.

**Composition d'une fiche** (37,6 Ko en gzip) : CSS critique en ligne 10,4 Ko, catalogue
i18n dans `ng-state` 8,7 Ko, liste des versions et i18n SEO 2,7 Ko, données de l'entité
1,2 Ko, HTML 11 Ko. Plus de la moitié de la page est identique d'une page à l'autre (lot 6).

**Latence** :

| Trajet | Mesure |
|---|---|
| VPS → Scaleway `fr-par`, ping | 2,5 ms |
| VPS → `fr-par`, requête sur une connexion ouverte | 3 à 5 ms |
| VPS → `fr-par`, nouvelle connexion TLS | 30 à 40 ms |
| VPS → `nl-ams` / `pl-waw` | 2 à 4 fois plus |

Un visiteur lointain paie environ 3 allers-retours vers Paris avant le premier octet (TCP,
TLS, requête), même sur une page gardée : de l'ordre de 0,25 s depuis New York et de 0,7 à
0,85 s depuis Tokyo ou Sydney. Googlebot explore surtout depuis les États-Unis.

## 2. Décisions et options écartées

| Option | Décision |
|---|---|
| Cache recyclé de 68 Go sur le VPS (spécification précédente) | **Remplacée.** Il garde au mieux 40 % du catalogue : un balayage au hasard rate encore plus d'une fois sur deux, et chaque raté repasse par le budget saturé. |
| Précalcul complet sur le disque du VPS | **Écartée.** 165 Go pour 136 Go libres. |
| **Précalcul complet dans un stockage objet Scaleway** | **Retenue.** Région `fr-par`, classe One Zone posée à chaque écriture : Scaleway n'a pas de classe par défaut par stockage, et un objet écrit sans classe est Multi-AZ, deux fois plus cher. Environ 1,25 €/mois pour 155 Go (prix du 1ᵉʳ juin 2026), le double tant que deux epochs coexistent. Les requêtes et l'entrée sont incluses ; 75 Go de sortie sont gratuits par mois, puis 0,01 €/Go : ~2,75 €/mois si chaque demande d'ancien patch atteignait le stockage. Une zone perdue ne perd rien, puisque toutes les pages peuvent être recalculées. |
| Cloudflare R2 + Worker (servir le stockage depuis le CDN) | **Écartée pour l'instant.** Environ 5 $ par mois de Workers en plus, et ~15 $ d'écritures à l'amorçage. Elle reviendrait si le VPS devait ne plus voir du tout les anciens patchs. |
| **CDN devant le site** | **Retenue.** Cloudflare Free : bande passante illimitée, purge par préfixe ou complète sur tous les plans depuis avril 2025. Il supprime la distance ; le stockage supprime le rendu. Les deux sont nécessaires : un CDN seul garde mal 4 millions d'adresses peu demandées, et chacun de ses ratés doit rester peu coûteux à l'origine. |
| `noindex` ou `Disallow` des anciens patchs | **Écartée**, comme dans la spécification précédente : la règle des canoniques (amendement du 2026-10-14 de l'ADR 0005) suffit. |
| Pré-rendre les pages `?page=N` des listes | **Remise à plus tard.** 8 % de pages en plus, 0,3 % des requêtes gardées. Une adresse avec une query reste rendue par le SSR et gardée par nginx. |
| Alléger les pages avant le précalcul | **Remise à plus tard** (lot 6) : le stockage n'est plus la contrainte. À faire avant une seconde passe complète. |
| Staging rempli comme la prod | **Écartée.** Le staging reste presque vide : aucune passe automatique, des passes manuelles sur un petit périmètre, des objets qui expirent après 14 jours. Ses limites (`web-ssr` à 0,25 CPU) ne disent rien du débit de la prod. |

**Questions ouvertes, options retenues** (provisoires, révisables) :

| Question | Option retenue |
|---|---|
| Certificats derrière le CDN | Let's Encrypt en HTTP-01 à travers Cloudflare, mode « Full (strict) », « Always Use HTTPS » coupé (Caddy redirige déjà). Prouvé sur le staging. Le défi DNS (image Caddy à façon, jeton de zone) seulement en cas d'échec ; pas de certificat d'origine Cloudflare, que les navigateurs refusent : il casse le retour en « DNS only ». |
| Hôtes derrière le CDN | `league-of-data-base.com`, `www`, `test`, `league-of-data-base.fr`, `www.fr`. Restent en « DNS only » et hors de la fermeture de l'origine : `api.*` (apps, `/v1`), `api.test.*` (aucun certificat d'edge à deux niveaux sur le plan gratuit), `stats.*` (relevé d'Umami). |
| Bascule de l'epoch servie | La variable `LODB_ARCHIVE_SERVE_EPOCH` et un redéploiement. Elle alimente aussi l'API (`LoDb__Prerender__ServedEpoch`), pour que le panneau montre l'epoch servie. Aucun point d'accès de bascule : nginx lit la variable au démarrage. |
| Audience interne derrière le CDN | La balise compte la première vue avec une origine `arrival` qui porte le référent externe ; le miroir est retiré dans le même déploiement, au moins 7 jours avant la bascule du proxy (lot 4c). |
| Chiffres de Cloudflare | `lablabs/cloudflare-exporter` (agrégats GraphQL, plan gratuit) vers VictoriaMetrics, dans `infra-vps`, déployé avant le premier nuage orange. |

## 3. Architecture cible

```
visiteur ─► Cloudflare, nœud proche ── en cache ─────────────────► ~20 ms
               │ absente
               ▼
            Caddy (edge) ─► nginx
               dernier patch : lodb_pages ─► SSR                  (inchangé)
               ancien patch  : lodb_archive, cache chaud local ── en cache ─► +1 ms
                                 │ absente
                                 ▼
                               stockage objet fr-par ── trouvée ─► +5 à 40 ms
                                 │ absente, erreur, délai dépassé
                                 ▼
                               SSR, voie épinglée actuelle        (repli)
```

Règles :

- **Ce qui est stocké** : la réponse `200` de classe `archived` du SSR, telle qu'il
  l'envoie, c'est-à-dire le corps compressé en gzip une fois (`src/server/compression`).
  Sur la voie `prerender` seulement, le SSR la marque `X-LoDb-Cache-Class: archived` quand
  sa `Cache-Control` est celle de la classe : l'API ne réécrit pas la constante en C#. Jamais
  une 503, une 5xx, une 404 ni une 301.
- **Les en-têtes propres à la page voyagent avec elle** :
  - en en-têtes de l'objet, tels que le SSR les envoie : `Content-Type`,
    `Content-Encoding: gzip`, `Cache-Control` ;
  - en métadonnées (`x-amz-meta-…`) :
    - `content-security-policy`, dont les empreintes de scripts sont celles de la page
      (`server/security/csp/document-policy-headers.ts`) ;
    - `content-security-policy-report-only` ;
    - `reporting-endpoints` ;
    - `link` : le `preconnect` vers l'hôte des images, qu'une page d'ancien patch porte
      (`features/catalogue/shared/network/inject-art-preconnect.ts`) ;
    - `lodb-build` : le build qui a rendu la page, tiré de l'en-tête `X-LoDb-Build` de la
      réponse du SSR (§ 3, les bundles).

  Les métadonnées tiennent sous 2 Ko, la limite de Scaleway : l'API refuse d'écrire une page
  qui la dépasse, et les listes d'empreintes de la CSP sont mesurées. `Vary` et
  `Content-Length` ne sont jamais stockés. nginx pose les autres en-têtes, comme aujourd'hui
  (`snippets/security-headers.conf`). Que Scaleway rende tout cela intact à une lecture
  anonyme n'est pas documenté : le script du lot 1 le prouve avant que le lot 3 parte en
  prod.
- **Clés** : `pages/{epoch}/{locale}/{version}/{ressource}[/{id}-{slug}]`, soit
  `pages/{epoch}` suivi du chemin canonique de la page, en style chemin
  (`/{stockage}/pages/…`). Les slugs sont en ASCII (`Domain/Paths/Slugs.cs`). Les bundles
  vont sous `build/{nom}` et `build/media/{nom}`. L'epoch `0` est réservée aux sondes ; la
  première passe de prod écrit l'epoch `1`, servie pendant qu'elle se remplit.
- **Deux epochs du stockage** :
  - l'epoch d'écriture, celle de la passe en cours ;
  - l'epoch servie, celle que lit nginx (`LODB_ARCHIVE_SERVE_EPOCH`).

  À ne pas confondre avec `LODB_ARCHIVE_EPOCH`, le compteur de la clé du cache nginx
  (défaut `3`), qui reste. La clé de `lodb_archive` devient
  `"$lodb_archive_epoch.$lodb_archive_serve_epoch|…"` : changer d'epoch servie vide aussi
  le cache chaud.

  Une nouvelle epoch se remplit pendant que l'ancienne reste servie, et nginx bascule quand
  elle est complète. Seul un correctif de sécurité du front bascule tout de suite, avec le
  SSR en repli pour les pages pas encore écrites. Ce repli renvoie alors **toutes** les pages
  épinglées vers le budget saturé (60 rendus par minute) pendant les 10 à 16 jours de la
  passe : les 503 reviennent. On le limite par l'ordre de la demande et un budget de passe
  relevé le temps qu'elle dure.
- **Cycle de vie** : une règle unique ne peut pas supprimer les epochs abandonnées. Son
  délai compte depuis la création de chaque objet, et les pages servies ne sont jamais
  réécrites : une règle sur `pages/` effacerait aussi l'epoch servie.
  - **Prod** : une règle par epoch **retirée**, posée au retrait par le script du lot 1,
    jamais par l'API : `retire-pages-<N>`, préfixe `pages/<N>/` (la barre finale évite que
    `pages/1/` prenne `pages/10/`), expiration à 1 jour. Le script refuse de retirer l'epoch
    servie ou celle en écriture. Scaleway traite au plus 500 000 objets par jour : une epoch
    de 4,2 millions d'objets met **au moins 9 jours** à disparaître, pour ~1,25 €/mois tant
    qu'elle dure. Plus vite : `ListObjectsV2` puis `DeleteObjects` depuis le poste de
    l'opérateur (1 000 clés par appel, requêtes gratuites).
  - **Staging** : une règle permanente, expiration de `pages/` à 14 jours.
  - **Partout** : versionnage et Object Lock coupés (sinon chaque nouvel essai d'une clé
    garderait une copie), envois multiparties abandonnés après 1 jour. L'epoch `0` est
    retirée une fois pour toutes : les sondes s'effacent seules.
- **Les bundles** : une page ne nomme que les bundles d'entrée de son build ; les chunks
  chargés plus tard (routes, `@defer`) ne sont nommés que dans le JavaScript. Le précalcul
  copie donc **tout le jeu haché** du build, une fois, avant la première page de ce build
  (`prerender_build`). Le SSR en donne la liste sur le réseau de la pile, à la passe seule
  (`GET /_lodb/build-files`, tiré de `server/assets/read-build-files.ts`).

  Sur la voie `prerender` seulement, chaque page porte `X-LoDb-Build: <empreinte>`, la même
  empreinte (sha256 des noms triés) que `/_lodb/build-files` : la passe sait ainsi quel
  build a rendu chaque page, même quand le SSR est redéployé ou redémarré au milieu d'une
  passe de 10 à 16 jours. L'API n'écrit une page que si son empreinte est déjà dans
  `prerender_build`. Sinon, elle tire `/_lodb/build-files`, copie le jeu, l'inscrit, puis
  écrit la page ; si la liste porte une autre empreinte que la page (le SSR a encore
  changé entre-temps), la page est rendue de nouveau, jamais écrite sous un build non
  copié. Sans cela, une page rendue par un build jamais copié paraîtrait saine jusqu'à ce
  que `front-builds` l'élague, puis ses chunks différés répondraient 404 et l'hydratation
  casserait sans bruit.

  L'API copie chaque fichier par `GET /build/<nom>` puis `PUT build/<nom>` avec
  `If-None-Match: *`, en classe Standard (un bundle perdu ne se recalcule plus une fois
  `front-builds` élagué) et `Cache-Control: public, max-age=31536000, immutable`. nginx y
  cherche un bundle que le SSR ne connaît plus. Une page stockée reste donc utilisable
  aussi longtemps qu'on la sert, quelle que soit la rétention du volume `front-builds`.
- **Accès** :
  - un projet Scaleway par environnement (`lodb-pages-prod`, `lodb-pages-staging`, déjà
    créés), chacun avec son stockage privé du même nom. Les droits IAM de Scaleway portent
    sur un projet entier, jamais sur un seul stockage : une clé du staging ne peut ainsi
    jamais écrire en prod. Le stockage des sauvegardes reste dans le projet par défaut, hors
    de portée ;
  - une bucket policy (version `2023-04-17`, des `Allow` seulement) cite exactement :
    - l'opérateur (`user_id`), `s3:*` sur le stockage et ses objets ;
    - l'application d'écriture de l'environnement (`application_id`), `s3:PutObject` sur
      `pages/*` et `build/*` ;
    - tout le monde (`*`), `s3:GetObject` sur `pages/*` et `build/*`, à la condition
      `aws:SourceIp` = `187.77.173.149/32`, l'adresse IPv4 de sortie du VPS (nginx résout en
      `ipv6=off`, aucun réseau de la pile n'a d'IPv6).

    Un principal absent de la politique perd ses accès : le script refuse une politique
    sans l'opérateur, la pose en dernier et la relit ;
  - l'écriture passe par une application IAM par environnement
    (`lodb-pages-writer-staging`, `lodb-pages-writer-prod`), créée dans la console comme
    celle des sauvegardes (`infra-vps`, `sauvegardes/`), avec le seul jeu de permissions
    `ObjectStorageObjectsWrite` sur son projet. Elle ne liste, ne lit et ne supprime rien ;
  - aucune application d'administration : le script tourne depuis le poste de l'opérateur,
    propriétaire de l'organisation, avec une clé personnelle temporaire, supprimée après
    usage ;
  - une lecture anonyme conditionnée par l'adresse à côté de l'IAM n'est pas documentée par
    Scaleway : la sonde du lot 1 la prouve. Si elle échoue, le repli est une lecture publique
    de `pages/*` et `build/*` sans condition ; le contenu est public, le risque est une sortie
    abusée (0,01 €/Go) et des URL du stockage indexées ;
  - si le VPS change d'adresse, chaque lecture répond 403 et nginx retombe sans bruit sur le
    SSR saturé, pendant que la passe continue d'écrire (l'écriture ne dépend pas de
    l'adresse) : l'en-tête `X-LoDb-Source` (lot 3) le rend visible, et la politique est à
    mettre à jour avec l'adresse.

## 4. Ce qui est demandé

### Lot 0 — Décision écrite

Une **ADR 0011** « Catalogue pré-rendu et CDN » reprend les § 2 et 3. Les ADR qu'elle
révise renvoient vers elle par un amendement daté :

- 0002 : un nouvel hôte sortant, le stockage objet, derrière son propre client nommé et son
  allow-list, sans élargir celle de `ddragon` ;
- 0003 : la tâche de précalcul et l'ajout des unités d'un patch à sa promotion ;
- 0004 : nginx sert aussi des pages et des bundles depuis le stockage objet, plus seulement
  `/cdn/blobs/` ; ce stockage est adressé par chemin, à part d'`IBlobStore` ; MinIO pour les
  tests et le développement seulement ;
- 0005 : la zone `lodb_archive` devient un cache chaud devant le stockage, la classe
  `archived` est gardée un an, et le CDN se place devant le site.

### Lot 1 — Le stockage

- Un script idempotent `tools/prerender/scaleway-bucket.sh`, sur le modèle de
  `infra-vps/sauvegardes/scaleway-bucket.sh`, lancé depuis le poste de l'opérateur avec sa
  clé personnelle temporaire (sous la forme `<ACCESS_KEY>@<PROJECT_ID>`, qui désigne le
  projet). Il :
  - crée le stockage en `fr-par` s'il manque (il peut déjà exister, créé dans la console :
    privé, versionnage et Object Lock coupés), et échoue si le versionnage est actif ;
  - pose les règles de cycle de vie du § 3 (expiration du staging, ou une règle par epoch
    retirée en prod) ;
  - pose la bucket policy du § 3 **en dernier**, puis la relit.

  Entrées : `BUCKET`, `ENVIRONMENT` (`staging` ou `prod`), `REGION` (`fr-par` par défaut),
  `PROJECT_ID`, `OPERATOR_USER_ID`, `WRITER_APPLICATION_ID`, `VPS_IPV4` et, en prod,
  `SERVED_EPOCH`, `WRITE_EPOCH`, `RETIRED_EPOCHS` ; les clés viennent de l'environnement,
  jamais de la ligne de commande. Une entrée manquante ou mal formée est refusée avant tout
  appel. Le script ne pose aucune classe (elle se choisit à chaque écriture) et ne crée
  aucune application IAM, ce que l'API S3 ne sait pas faire : `tools/prerender/README.md`
  décrit les étapes dans la console.
- Sa sous-commande `verifier`, lancée sur le VPS, écrit une sonde `pages/0/_sonde` avec la
  clé d'écriture (gzip, One Zone, les métadonnées du § 3), puis la lit sans clé, **en IPv4
  seulement** (`curl -4`, ou depuis un conteneur du réseau de la pile) : l'hôte joint aussi
  Scaleway en IPv6, d'où la politique répondrait 403 alors que les conteneurs, en IPv4,
  passent. Elle doit montrer, depuis le VPS, une 200 avec les en-têtes, les métadonnées et
  le corps gzip intacts (une clé absente y répond `404 NoSuchKey`, pas 403 : Scaleway le
  dit à une lecture que la politique admet, sans `s3:ListBucket`, mesuré le 21 octobre 2026).
  L'opérateur vérifie ensuite la 403 par un `GET` de la même clé depuis son poste (la
  commande est dans `tools/prerender/README.md`). Le lot 3 ne part pas en prod sans elles.
- Les variables, validées au démarrage :
  - API (options `LoDb`) : `LoDb__Prerender__Endpoint` (`https://s3.fr-par.scw.cloud`),
    `__Region`, `__Bucket`, `__AccessKey` et `__SecretKey` (secrets), `__PublicHost`
    (`league-of-data-base.com`, `test.league-of-data-base.com` au staging), `__SsrBaseUrl`
    (`http://lodb-web-ssr-prerender:4000`, le SSR de la passe), `__ServedEpoch`
    (`${LODB_ARCHIVE_SERVE_EPOCH}`), `__AutoEnqueueOnPromotion` (faux au staging ; faux en prod
    jusqu'à la fin de la passe complète, puis vrai), `__AllowInsecureEndpoint` (développement).
    Vides, elles laissent la tâche en sommeil : `Add*` ne fait aucune I/O ;
  - nginx : `LODB_PAGES_STORE_ENABLED` (`0` par défaut : en local et en CI, rien ne change),
    `LODB_PAGES_STORE_SCHEME`, `_HOST`, `_PORT`, `_BUCKET`, et l'epoch servie
    (`LODB_ARCHIVE_SERVE_EPOCH`) ;
  - SSR : le budget de la voie `prerender` (lot 2).

  Elles entrent dans `.env.*.example`, dans les secrets `ENV_STAGING` / `ENV_PROD` et dans
  `configuration.md`. Les deux clés passent d'abord dans `SECRET_KEYS`
  (`tools/deploy/split-env.sh`) et dans les `secrets` de l'API (`compose.deploy.yaml`), avec
  des clés sans valeur sous son `environment` : sinon elles finiraient dans `.env`, visibles
  par `docker inspect`.
- En développement et en test, un service `pages-store` (MinIO épinglé par empreinte, alias
  `lodb-pages-store`) dans `compose.override.yaml`.

### Lot 2 — La passe de précalcul

**Liste des pages.** Pour chaque (version, langue), les 4 listes, puis toutes les entités
listées du catalogue (débris et sorts de remplacement exclus, jumeaux Classic compris). Les
sitemaps d'une version ne gardent que ses fiches canoniques chez elle
(`SitemapLocations.Version`), alors que le site sert toutes les entités : la liste vient de
`EntityPaths` et `SitePages.Lists`, tirés dans une seule fonction interne
(`Modules/Seo/Pages/VersionPages.cs`) que les sitemaps et la passe partagent. Le précalcul
ne peut ainsi pas s'écarter des liens du site. Une version épinglée n'a ni accueil ni page
`patches` (`tools/routing/check-urls.sh` en attend une 404).

**Ordre.** Par (version, langue) : l'API charge chaque catalogue une fois pour toutes ses
pages, environ 8 450 chargements pour toute la passe, au lieu d'un par page. L'alerte
`LodbCatalogueEnThrash` doit rester muette. Les versions les plus demandées passent
d'abord : l'API ne lit pas VictoriaLogs, le formulaire de lancement prend donc une liste
ordonnée de versions, que l'opérateur tire du journal de l'edge (LogsQL,
`stats by (version)`). Sans liste, les plus récentes d'abord.

**Chemin d'un rendu.** La tâche de l'API appelle le SSR sur le réseau de la pile, en
direct, jamais à travers nginx, qui effacerait l'en-tête, appliquerait `limit_req`,
remplirait `lodb_archive` et déclencherait le miroir. Depuis le 2026-10-21, ce SSR est un
conteneur à elle, `http://lodb-web-ssr-prerender:4000`, et non plus celui des visiteurs
(« Capacité adaptative de la passe », fin du lot) :

- elle envoie l'**hôte public** de son environnement (`X-Forwarded-Host`,
  `test.league-of-data-base.com` au staging) et `X-Forwarded-Proto: https`, deux des
  en-têtes de proxy que le SSR croit (`LODB_TRUST_PROXY_HEADERS`) ; l'hôte doit figurer dans
  `LODB_ALLOWED_HOSTS`. La canonique et les `hreflang` viennent de l'origine de la requête
  (`core/seo/canonical-origin.ts`) : sans cela, les pages stockées nommeraient l'hôte
  interne ;
- elle demande `Accept-Encoding: gzip`, sans quoi le SSR rend du HTML en clair, et son
  client garde `AutomaticDecompression = None` ;
- `X-LoDb-Prerender: 1` marque le précalcul. nginx l'efface toujours côté public
  (`snippets/proxy-headers.conf`), comme `X-LoDb-Edge` : un client ne peut pas se faire
  passer pour la passe ;
- une **voie d'admission `prerender`** dans `src/server/admission/` :
  - l'en-tête l'emporte sur la voie que donne l'URL (`renderLaneOf`), qui la classerait en
    `pinned` ;
  - aucune limite par client : l'API est un seul client, que les 6 rendus épinglés par
    minute et par client freineraient ;
  - un budget propre : `LODB_PRERENDER_RENDERS_PER_MINUTE` (120, 30 au staging), en rafale
    de `LODB_PRERENDER_RENDER_BURST` (10) ;
  - une place seulement si `maxInFlight` en laisse au trafic réel :
    `LODB_PRERENDER_MAX_IN_FLIGHT` (2) et une réserve aux visiteurs,
    `LODB_PRERENDER_VISITOR_RESERVE` (4), toutes deux sous `LODB_RENDER_MAX_IN_FLIGHT`
    (8 par défaut) ; refus si la voie a atteint son maximum, ou si le total en vol plus un
    dépasse le maximum moins la réserve. Mêmes motifs de refus qu'aujourd'hui ;
  - la lecture des variables (`read-render-limits`) suit le modèle des parts (`SHARES`,
    `fitShare`) : baisser `LODB_RENDER_MAX_IN_FLIGHT`, le levier d'un incident, ne doit
    jamais arrêter le SSR, qui rend toutes les pages. Laissées à leur défaut, les deux
    variables de la voie suivent un total abaissé (chacune ramenée au total moins un),
    quitte à ce que la voie n'admette plus rien. Le SSR ne refuse de démarrer que sur une
    valeur **posée** qui ne tient pas strictement sous le total : une réserve égale au
    total ou au-dessus ne laisserait jamais de place, puisque le total en vol plus un
    dépasserait toujours le maximum moins la réserve. Sous le total, la voie admet au moins
    un rendu quand rien n'est en vol. Au staging (`LODB_RENDER_MAX_IN_FLIGHT` à 3,
    `compose.staging.yaml`), à côté du budget de 30 par minute :
    `LODB_PRERENDER_MAX_IN_FLIGHT=1` et `LODB_PRERENDER_VISITOR_RESERVE=1` ;
  - un comptage à part dans `lodb_ssr_renders_total{lane="prerender"}` ;
  - aucune ligne `http.request.served` par page, qui en pousserait 4 millions dans
    VictoriaLogs, sauf ses erreurs serveur (5xx) ; la requête est reconnue à l'en-tête de
    la passe, comme pour le reste de la voie (liste du build et bundles compris).

  `readPositiveInteger` refuse `0` : la passe se met en pause par l'API, pas par le budget.
  La voie `refresh` de la spécification précédente en est le modèle, mais son code n'a
  jamais été publié. Trois idées en sont reprises, à réécrire ici :
  - le test de l'en-tête, une fonction qui rend `request.get('x-lodb-prerender') === '1'` ;
  - un type `VisitorLane = Exclude<RenderLane, 'prerender'>`, qui sépare les voies des
    visiteurs de celle de la passe et tient les budgets par client hors de cette dernière ;
  - la lecture des variables sur le modèle de `readPositiveInteger`.

**Écriture.** Un `PUT` par page, avec les en-têtes et les métadonnées du § 3, en classe
`ONEZONE_IA`. Un `PUT` est atomique pour un objet. L'API n'écrit qu'une `200` marquée
`X-LoDb-Cache-Class: archived`, compressée en gzip et dont le build (`X-LoDb-Build`) est
déjà copié (§ 3) ; une 301 ou une 404 est comptée `skipped`, pas en échec. Une 503 fait
reculer la tâche selon son `Retry-After`. Une page en échec est retentée plus tard dans la
passe, puis comptée en échec. Le stockage est joint par un client nommé à lui
(`page-store`), avec sa propre allow-list, sans redirection : jamais en élargissant
`LoDb:Egress:AllowedHosts`. La signature SigV4 ne couvre que le `PUT` : la clé
d'écriture ne lit rien, une lecture signée répondrait toujours 403. L'API ne vérifie
l'existence d'un objet que par l'écriture conditionnelle (`If-None-Match: *` sur `build/`,
412 = déjà là), ou par une lecture anonyme depuis l'adresse du VPS. Une 401, une 403 ou une
404 (`NoSuchBucket`) sur un `PUT` est une **erreur de configuration** (clé tournée,
politique sans l'application d'écriture, stockage absent), typée et fatale : la passe se met
en pause avec une ligne de journal, et la page n'est comptée ni `written` ni `skipped`.
« Absence définitive » ne vaut que pour une lecture. Une 5xx, une 429 (Scaleway freine
ainsi) ou un délai dépassé est transitoire. Toute autre 4xx sur le `PUT` d'une page (400 :
`InvalidArgument`, `MetadataTooLarge`, `EntityTooLarge`) compte la page en échec, sans
nouvel essai ni pause ; une 409 (`ConditionalRequestConflict`) sur le `PUT` conditionnel
d'un bundle est retentée comme une erreur transitoire.

Un changement d'adresse du VPS ne se voit **pas** à l'écriture : l'application d'écriture
est autorisée par son `application_id`, sans condition d'adresse ; seule la lecture anonyme
dépend de l'adresse. La passe continue donc d'écrire, et le changement ne se voit qu'aux
lectures : nginx reçoit 403 et sert la page par le SSR, avec `X-LoDb-Source: ssr` (§ 3,
accès).

**Débit, à mesurer en prod avant de fixer les budgets.** Les limites du staging
(`web-ssr` à 0,25 CPU, `compose.staging.yaml`) ne disent rien de la prod : le rythme se
mesure en prod, sur une première petite unité à budget bas. Un seul thread Node coûte
~0,1 s par rendu, soit au plus ~10 rendus par seconde s'il ne faisait que ça :

| Où | Rythme visé | Passe complète (4,2 M de pages) |
|---|---|---|
| Voie `prerender` du SSR actuel, dans ce que le trafic laisse | 3 à 5 par seconde | 10 à 16 jours |
| Un second conteneur SSR réservé à la passe (profil Compose), sur le VPS | ~10 par seconde | ~5 jours |
| Une instance louée à l'heure en `fr-par` (écritures internes à la région, sans sortie facturée) | plusieurs dizaines par seconde | 1 à 2 jours |

Le choix se fait sur la mesure : rendus par seconde, charge de l'API avec l'ordre par
(version, langue), et charge laissée aux autres projets du VPS. Au-delà de ~10 écritures par
seconde (instance louée), un ticket au support de Scaleway d'abord : aucune limite n'est
publiée, et le freinage répond 429. Le stockage se remplit au fil de la passe, et nginx sert
ce qui y est déjà (lot 3).

Mesuré le 2026-10-21 et tranché : le second conteneur, de faible priorité, et un rythme qui
suit le CPU libre du VPS, 150 à 220 pages par minute en moyenne plutôt que 10 par seconde. Le
tableau ci-dessus reste l'estimation de départ ; la décision et ses chiffres sont dans
« Capacité adaptative de la passe », à la fin de ce lot.

**Persistance et reprise.**

- Une table des passes (`prerender_pass`) : epoch, état, périmètre, auteur, dates,
  compteurs.
- Une table des unités (`prerender_unit`), une par (version, langue) : état, priorité,
  pages prévues, écrites, sautées et en échec, octets écrits, curseur.
- Une table des builds copiés (`prerender_build`) : empreinte du build, date.

  Les migrations sont additives. Une passe reprend là où elle s'est arrêtée, à travers les
  déploiements. Il n'y a qu'une passe active à la fois (index unique partiel) : en lancer
  une seconde répond `409`.

**Tâche de fond** (ADR 0003) :

- `BackgroundService`, `PeriodicTimer` et verrou consultatif ;
- coupée par `LoDb:Workers:Enabled=false`, en sommeil tant que ses options sont vides ;
- **une ligne de synthèse par unité**, jamais une par page ;
- un compteur `lodb_prerender_pages_total{outcome}` (`written`, `skipped`, `refused`,
  `failed`).

**Nouveau patch.** À la promotion d'une version, celle qui cesse d'être la dernière devient
une version épinglée. Ses 21 unités (une par langue), environ 24 000 pages, rejoignent la
passe active, ou en ouvrent une sur l'epoch servie. L'avis de promotion
(`IPromotionListener`) ne part qu'une fois, sur une seule instance, sans nouvel essai : un
tour de rapprochement de la tâche (versions du catalogue face aux unités) rattrape un avis
perdu. Les deux obéissent à `LoDb__Prerender__AutoEnqueueOnPromotion`, faux au staging et en
prod tant que la passe complète (lancée à la main) n'est pas terminée, puis vrai en prod.
Vrai trop tôt, le rapprochement ouvrirait de lui-même, sans promotion, une passe sur l'epoch
servie pour le patch précédent dans la demi-heure qui suit le déploiement, et la passe de
l'opérateur répondrait `409`. En attendant, ses pages passent par le SSR et le cache local.

**Administration.** Un panneau « Précalcul du catalogue », sur le modèle de `backups/` :

- l'état du stockage : pages et volume par epoch, tirés des compteurs des unités (lister
  coûterait ~4 200 appels par epoch, et la clé d'écriture ne liste pas). Au staging, ces
  compteurs disent ce qui a été écrit, pas ce qui reste après l'expiration à 14 jours : une
  unité marquée faite n'est pas réécrite, et ses pages retombent sans bruit sur le SSR ; il
  faut une nouvelle passe pour les remettre ;
- ce que le stockage contient et ce qu'il coûte, pour le suivre dans le temps : par epoch
  (servie, en écriture, écrite en attente de bascule, retirée), ses pages, ses octets, son état
  et son coût mensuel estimé au prix One Zone ; les builds copiés (nombre, octets, coût au prix
  Multi-AZ) ; les totaux en tête ; et, repliable, le détail par version de l'epoch servie. Le
  coût vaut les octets divisés par 10⁹, multipliés par un prix par Go et par mois réglable
  (`LoDb__Prerender__PricePerGbMonthOneZone`, 0,00803 € ;
  `LoDb__Prerender__PricePerGbMonthMultiAz`, 0,01606 € ; tarifs du 1ᵉʳ juin 2026), arrondi à
  quatre décimales, en euros. Une ligne sous les totaux nomme les prix et ce que l'estimation
  laisse de côté : la sortie (75 Go gratuits par mois, puis 0,01 €/Go), suivie dans Grafana.
  Une epoch retirée coûte encore tant que le cycle de vie ne l'a pas effacée (au moins 9 jours
  pour 4 millions d'objets), ce que l'API ne voit pas : sa ligne garde son coût, mais les
  totaux ne la comptent plus ;
- à côté de l'estimation, qui reste à sa place, la facture réelle de Scaleway pour l'Object
  Storage de tout le projet des pages, lue avec une clé de lecture de la facturation : le mois
  en cours, le précédent, l'historique sur six mois et les lignes du mois, hors TVA. Elle
  compte ce que l'estimation laisse de côté (la sortie, les requêtes) et se charge à part, sans
  jamais retarder le panneau ([`configuration.md`](../guides/configuration.md) § 4.9) ;
- la passe en cours, avec sa progression, son rythme et sa fin estimée ;
- le lancement d'une passe, avec son périmètre en listes explicites : les anciens patchs
  (`versions`, 1 000 au plus), les langues (`locales`) et les types de page (`pageTypes`),
  chacun laissé vide pour « tous », une liste de versions prioritaires et `skipComplete`, qui
  laisse de côté chaque (version, langue) que l'epoch tient déjà sur tous les types demandés ;
  une passe qui n'aurait rien à rendre est refusée avant d'être ouverte. Un périmètre stocké
  avant les listes (une plage, un type, une langue) se lit toujours ;
- pause, reprise, annulation ;
- l'epoch servie, lue dans la configuration ; quand une epoch plus récente est prête, la
  consigne « poser `LODB_ARCHIVE_SERVE_EPOCH=N` et redéployer » (nginx ne change pas d'epoch à
  chaud). Une epoch est prête quand sa couverture n'a plus de manque : chaque (ancien patch,
  langue) a une unité terminée sur les quatre types de pages, ou une unité en échec définitif
  que le SSR servira, quelles que soient les passes qui l'ont remplie ;
- l'historique.

Les points d'accès vivent sous `/api/admin/prerender` (`GET`, `POST …/passes`,
`POST …/passes/{id}/pause|resume|cancel`), sous la politique administrateur. Trois lectures
servent le pupitre : `GET …/patches?epoch={n}` (chaque ancien patch et ce qu'une epoch en
tient, langue par langue et type par type, avec les bases des estimations),
`GET …/passes/{id}/progress?since={iso}` (le suivi en direct d'une passe, entier ou limité aux
versions changées depuis la lecture précédente) et `GET …/passes/{id}/versions/{version}` (les
unités d'une version, une par langue). Chaque action s'écrit au journal d'audit
(`admin.prerender_*`). Le contrat OpenAPI et le client généré sont mis à jour dans le même
commit. L'interface du pupitre (modale de lancement, suivi en direct, couverture, coûts,
historique) est décrite dans [`pre-rendu-admin-ux.md`](pre-rendu-admin-ux.md).

#### Capacité adaptative de la passe (décision du 2026-10-21)

**Constat.** Au rythme fixe (120 rendus par minute en prod), les 4,19 millions de pages
prennent 24 jours, et 97 jours à 30 par minute. Ce rythme passe en outre par le SSR des
visiteurs, qu'il ralentit dès la première page en vol : sur une pile locale aux quotas de la
prod, la médiane d'une page non gardée prend +45 % avec un rendu de la passe en vol, +90 %
avec deux, +250 % avec huit ; le retard p99 de la boucle d'événements du SSR passe de 13 à
50-60 ms, sa pression CPU (`cpu.pressure`) de 0,1 % à 15 %. Décision : la passe ne prend que
le CPU **libre** du VPS, avec une **marge large** : aucun ralentissement des visiteurs, aucun
effet sur les autres projets de l'hôte.

**Mesures du 2026-10-21** (pile `lodb-dev-e1` aux quotas de la prod, épinglée sur 4 CPU ;
étalonnage sur la fenêtre de précalcul du staging du 20 octobre ; 7 jours de
VictoriaMetrics) :

| Mesure | Valeur |
|---|---|
| CPU par page sur le VPS | 0,115 à 0,165 CPU-s : SSR 0,08-0,10, API 0,03-0,056 (écriture comprise), Postgres 0,005-0,009 |
| Un cœur de SSR | 600 à 750 pages/min au plus. Un seul thread Node, saturé dès 2 rendus en vol : au-delà, le débit stagne et la latence double |
| VPS sur 7 jours (moyennes de 5 min) | occupé, steal compris : moyenne 35 %, p95 48 %, p99 57,5 %, max 80 % ; consommé, steal exclu : moyenne 22 %, p95 25 %, max 51 % ; steal : moyenne 13 %, p95 24 %, max 45 % |
| SSR dédié de faible poids (`cpu_shares: 128`) | visiteurs inchangés au bruit près (±30 %) jusqu'à ~80 % d'occupation de l'hôte ; vers 89 %, avec 4 rendus en vol, leur p95 prend +50 % |
| Âge du patch | sans effet de 11.x à 16.x ; un très vieux patch (7.x) coûte ~1,5 fois plus de CPU à sa première unité (Data Dragon froid) |

Le steal vient des voisins de la machine physique : aucun poids ne l'arbitre. Et l'hébergeur
bride **tout le VPS** sur une consommation forte et **durable** (le 10/10 ; alerte
`HoteCpuRisqueBridage` d'`infra-vps` à 60 % sur 30 min) : la passe tient donc aussi une
moyenne sur 30 minutes. Trois leviers :

1. un **SSR réservé à la passe**, de faible priorité : le noyau ne lui donne que le CPU que
   personne ne demande ;
2. un **régulateur** dans la passe, qui monte doucement tant que l'hôte et les visiteurs sont
   calmes et recule vite au moindre signe ;
3. l'**ordre de la demande** : la liste des versions prioritaires collée dans la modale de
   lancement, tirée du journal de l'edge (déjà là, rien à changer).

**Le SSR de la passe (`web-ssr-prerender`).** La même image que `web-ssr`, un second service
que seule l'API appelle ; nginx ne le connaît pas.

| Réglage | Valeur | Pourquoi |
|---|---|---|
| Alias | `lodb-web-ssr-prerender` seul, jamais `lodb-web-ssr` | Docker répartirait sinon le trafic de nginx sur lui |
| `LoDb__Prerender__SsrBaseUrl` | `http://lodb-web-ssr-prerender:4000` | le SSR des visiteurs ne reçoit plus aucun rendu de la passe |
| `cpu_shares` | `128` en prod (un huitième des autres services), `64` au staging (sous ses `128`) | le noyau ne lui cède que le CPU libre. Plus bas (poids 1, ou `cpus: 0.5`), le débit est divisé par deux sans gain mesurable pour les visiteurs |
| `cpus` | `1.0` en prod, `0.25` au staging | le pire que puisse faire un régulateur cassé ; un thread Node n'en prend pas plus |
| `memory`, `pids` | `704m`, `256` | ceux de `web-ssr` ; le tas V8 reste borné par `NODE_OPTIONS` |
| Voie `prerender` | budget `LODB_PRERENDER_RENDERS_PER_MINUTE` = le plafond du régulateur (`LODB_PRERENDER_PACE_MAX`) ; `LODB_PRERENDER_MAX_IN_FLIGHT` (2, 1 au staging) ; `LODB_RENDER_MAX_IN_FLIGHT=4` et `LODB_PRERENDER_VISITOR_RESERVE=1` | le SSR borne lui-même la passe à son plafond, quoi que demande l'API |
| Bundles | ni `LODB_FRONT_BUILDS_DIR` ni volume `front-builds` | le volume partagé garde un seul écrivain ; la passe lit les bundles de l'image (`/build/`, `/_lodb/build-files`) |
| Sonde de santé | celle de `web-ssr`, toutes les 30 s, 60 s de grâce au démarrage | de faible poids, il démarre lentement sur un hôte chargé |
| Métriques | sur un hôte, réseau `observability` et labels `prometheus.scrape`/`prometheus.port`, comme `web-ssr` | `lodb_ssr_*{lane="prerender"}` passent sous `service="web-ssr-prerender"` : rien ne se perd, et les alertes qui lisent `current` et `pinned` ne changent pas |

S'il tombe, la passe attend sans compter de page (`SsrOutage` : une ligne au début de la
panne, une à la fin) ; les visiteurs n'en voient rien. Le déploiement ne l'attend pas pour
rendre le site : le job lève la page de maintenance dès que les services des visiteurs sont
sains, puis le démarre, et un échec n'y est qu'un avertissement. Le SSR des visiteurs garde
sa voie `prerender` et ses réglages, désormais inutilisés : la rouvrir à la passe ralentirait
les visiteurs dès le premier rendu.

**Ce que règle le régulateur** : les rendus que la passe démarre par minute (`RenderPacer`),
entre un **plancher** et un **plafond**. Les pages en vol restent fixes
(`LODB_PRERENDER_MAX_IN_FLIGHT`, 2 en prod) : au-delà de deux, le débit stagne ; et quand les
rendus ralentissent, cette borne freine déjà la passe d'elle-même.

**Ce qu'il lit**, toutes les 30 secondes, pendant que son instance écrit une passe :

| Signal | Source | Fenêtre | Vert : peut monter | Tenir | Orange : × 0,5 | Rouge : retenue |
|---|---|---|---|---|---|---|
| VPS occupé, steal compris (`1 − idle`) | `/proc/stat`, lu par l'API : celui de l'hôte | 1 min | < 50 % | 50-55 % | ≥ 55 % | ≥ 65 % |
| Steal | idem | 1 min | < 25 % | — | ≥ 25 % | ≥ 40 % |
| VPS consommé, steal exclu (`1 − idle − steal`, comme `noeud:cpu_utilise_ratio`) | idem | 30 min | < 40 % | 40-45 % | ≥ 45 % | ≥ 55 % |
| Retard p99 de la boucle d'événements | `/load` du SSR des visiteurs | 30 s | < 50 ms | 50-80 ms | ≥ 80 ms | ≥ 250 ms |
| Pression CPU du conteneur (`cpu.pressure`, *some avg10*) | idem | 10 s | < 2 % | 2-5 % | ≥ 5 % | ≥ 15 % |
| Rendu moyen du couloir `current` (5 rendus au moins) | idem | 30 s | < 0,3 s | 0,3-0,5 s | ≥ 0,5 s | — |
| Refus du couloir `current`, hors part d'un client | idem | 30 s | aucun | — | — | au moins un |
| Temps bridé de l'API (`throttled_usec` sur le temps écoulé) | cgroup de l'API | 1 min | < 2 % | 2-5 % | ≥ 5 % | ≥ 20 % |
| Pression CPU du SSR de la passe | `/load` du SSR de la passe | 10 s | < 20 % | 20-30 % | ≥ 30 % | — |
| Rendu médian de la passe (10 rendus au moins), face à sa base | l'API, autour de chaque rendu | 1 min | < 1,5 × | 1,5-2 × | ≥ 2 × et ≥ 0,25 s | ≥ 1,5 s |
| Une source illisible | — | — | — | — | orange | — |

- La **base** du rendu de la passe est la plus basse de ses médianes d'une minute des
  30 dernières minutes, 50 ms au moins. La médiane ignore les premiers rendus d'une unité
  (autant que de pages en vol), qui chargent son catalogue, et ne compte que les pages à
  stocker, jamais un refus ni une redirection.
- Trop peu de rendus dans la fenêtre ne disent rien : le signal est vert.
- Le signal le plus grave l'emporte ; le pupitre et les journaux nomment celui-là.
- D'où viennent les seuils : le VPS reste sous 50 % d'occupation 96 % du temps, la passe ne
  monte que dans ce creux ; 45 % sur 30 min reste 15 points sous l'alerte de bridage, qui
  n'est elle-même qu'un point de départ ; 25 % de steal est son p95 sur 7 jours, 40 % le
  plateau d'un bridage (`HoteCpuBrideParHebergeur`) ; le retard p99 de la prod a un p95 de
  40 à 50 ms sur 7 jours ; la pression du SSR des visiteurs vaut 0,1 % au repos, 0,2 à 3 %
  avec le SSR dédié et 15 % avec la passe partagée ; 20 % de bridage de l'API est le seuil
  de `ConteneurCpuBride` ; le SSR de la passe privé de CPU à 30 % dit un hôte pris.

**Comment il bouge** (montée additive, baisse multiplicative) :

- **Démarrage** au plancher, à chaque passe que son instance prend et après chaque
  redémarrage de l'API ; aucune montée avant une minute de mesures.
- **Montée** d'un pas (30 rendus par minute en prod) quand tous les signaux sont verts, au
  plus une fois par minute, et seulement si la passe a démarré au moins 80 % de son droit sur
  la minute écoulée : une passe qui attend un catalogue ou change d'unité ne gonfle pas un
  droit qu'elle n'use pas. Du plancher au plafond : 7 minutes en prod.
- **Tenue** : un signal dans sa bande « tenir » fige le rythme ; c'est l'hystérésis entre la
  montée et la baisse.
- **Baisse** : × 0,5 à chaque évaluation orange, toutes les 30 s, jamais sous le plancher
  (de 240 à 30 en trois évaluations, une minute), puis aucune montée pendant 3 minutes.
- **Retenue** : un signal rouge arrête les nouveaux rendus 5 minutes, prolongées tant qu'il
  reste rouge ; les pages en vol finissent. Ensuite, le plancher, puis la montée. La passe
  reste `running` en base : seul le régulateur la retient, et une pause ou une annulation se
  voit en 10 s au plus.
- **Source illisible** (fichier absent, SSR injoignable, compteur reparti de zéro après un
  redémarrage) : orange, donc le plancher tant qu'elle dure, jamais le rythme fixe, plus
  rapide. Un droit que le régulateur n'a pas renouvelé depuis 90 s retombe au plancher.

Au pire, régulateur cassé compris, la passe reste bornée par le budget de la voie de son SSR
(le plafond) et par son `cpus: 1.0`, au plus bas poids de l'hôte.

**Interrupteur et rythme fixe.** `LODB_PRERENDER_PACE_MODE=fixed`, puis un redéploiement de
l'API, rend le rythme fixe d'aujourd'hui, `LODB_PRERENDER_RENDERS_PER_MINUTE` (120 en prod),
sans rien lire, toujours sur le SSR dédié. Le levier immédiat reste la pause au pupitre. La
commande `prerender unit` garde le rythme fixe : bornée, lancée à la main, sans la tâche de
fond qui mesure.

**Avec ce qui existe.**

- L'admission du SSR dédié borne la passe à son plafond. Un refus (`503`) reste attendu selon
  son `Retry-After` et compté `refused`, comme aujourd'hui ; il ne devrait plus arriver.
- `SsrOutage` : le SSR de la passe injoignable rend sa source illisible, donc le plancher ; la
  passe attend comme avant.
- Pause, reprise, annulation : le régulateur ne tourne que pendant que son instance écrit une
  passe, et repart du plancher à la suivante.
- Deux instances, le temps d'un déploiement : seule celle qui tient le verrou de la passe
  règle ; l'autre ne lit rien.

**Où il vit.** Une tâche de fond sous `Workers/Prerender/` (`PeriodicTimer` de 10 s) relève
`/proc/stat` et le cgroup de l'API ; toutes les 30 s, pendant que le moteur de son instance
écrit une passe, elle lit les deux `/load` et décide. Sans verrou consultatif : elle ne règle
que la mémoire de son instance. `LoDb:Workers:Enabled=false` la coupe avec les autres, et le
rythme est alors le rythme fixe. Le `/proc/stat` d'un conteneur est celui de l'hôte : vérifié
en local (sans lxcfs), à vérifier une fois sur le VPS
([`configuration.md`](../guides/configuration.md) § 4.10).

**Le point `/load` des SSR.** `prom-client` remet à zéro l'histogramme du retard de la boucle
d'événements à chaque lecture de `/metrics` : un second lecteur tronquerait la série que lit
Grafana. Le SSR expose donc, sur son port des métriques (9464, jamais publié ni routé),
`GET /load`, du JSON sans effet de bord :

- `cpuSeconds` : le CPU du processus, cumulé ;
- `eventLoopDelayP99Seconds` : la dernière fenêtre close de 30 s, tenue par un moniteur à
  lui (`null` avant la première) ;
- `cpuPressureSomeAvg10`, `cpuThrottledSeconds` : le cgroup du conteneur (`null` s'il est
  illisible) ;
- par couloir (`current`, `pinned`, `prerender`) : rendus terminés et leur durée cumulée,
  refus hors part d'un client (`capacity`, `lane_capacity`, `lane_rate`).

**Réglages par environnement.**

| Réglage | Local (`compose.override.yaml`) | Staging (`compose.staging.yaml`) | Prod (défaut) |
|---|---|---|---|
| `LODB_PRERENDER_PACE_MODE` | `adaptive` | `adaptive` | `adaptive` |
| `LODB_PRERENDER_PACE_MIN` : plancher | `120` | `10` | `30` |
| `LODB_PRERENDER_PACE_MAX` : plafond, et budget de la voie du SSR dédié | `1200` | `30` | `240` |
| `LoDb__Prerender__Pace__StepRendersPerMinute` : pas de montée | `120` | `10` | `30` |
| `LODB_PRERENDER_RENDERS_PER_MINUTE` : rythme fixe (interrupteur, `prerender unit`) | `120` | `30` | `120` |
| `LODB_PRERENDER_MAX_IN_FLIGHT` | `2` | `1` | `2` |
| `web-ssr-prerender` : `cpus`, `cpu_shares` | sans limite | `0.25`, `64` | `1.0`, `128` |

- **Prod** : le plancher coûte ~2 % du VPS, ce que le staging fait déjà sans bruit. Au
  plafond, ~0,4 cœur au SSR dédié et ~0,22 à l'API, ~16 % du VPS en tout : le SSR reste sous
  le plancher de 0,5 cœur de `ConteneurCpuEnHausseBrutale`, l'API loin de ses 1,25 cœur. Le
  rythme fixe reste à 120, entre les deux.
- **Staging** : il partage le VPS avec la prod. Son plafond est son rythme d'aujourd'hui : le
  régulateur ne fait que le freiner quand l'hôte est chargé. La topologie est celle de la
  prod, pour éprouver le déploiement.
- **Local** : de quoi voir le régulateur monter en quelques minutes ; ses signaux sont ceux de
  la VM Docker.
- L'API refuse de démarrer sur un plancher au-dessus du plafond, un pas au-dessus du plafond,
  ou un rythme fixe au-dessus du plafond (la voie du SSR dédié le refuserait) ; le job de
  déploiement vérifie la même chose avant de toucher aux conteneurs.

**Ce que montre le pupitre.** Le relevé « Rythme » du suivi en direct gagne une ligne, tirée
de `pulse.pace` du suivi d'une passe (`GET …/passes/{id}/progress`) :

- « démarrage prudent : 30 p/min » ;
- « adapté : jusqu'à 180 p/min · CPU du VPS à 41 % » ;
- « au plafond : 240 p/min · CPU du VPS à 38 % » ;
- « ralenti à 60 p/min : visiteurs actifs » ;
- « en retenue jusqu'à 14:05 UTC : CPU du VPS à 67 % » ;
- « rythme fixe : 120 p/min (régulation coupée) ».

Une passe retenue plus de 3 minutes n'est plus « bloquée, cause inconnue » : le suivi dit que
la régulation la retient, et pourquoi. `pace` est nul quand l'instance qui répond n'écrit pas
la passe. Rien d'autre ne change à l'écran.

**Métriques et journaux** (meter `LoDb.Prerender`, [`metriques.md`](../guides/metriques.md)) :

- `lodb_prerender_pace_renders_per_minute` (jauge) : le droit du moment, 0 en retenue, le
  rythme fixe en mode fixe ;
- `lodb_prerender_pace_limit{reason}` (jauge, 1 pour la raison du moment, 0 pour les autres) :
  `none`, `warming_up`, `ceiling`, `host_cpu`, `host_cpu_sustained`, `host_steal`,
  `visitors_ssr`, `visitors_refused`, `api_cpu`, `pass_latency`, `pass_ssr_pressure`,
  `signal_missing`, `fixed` ;
- `lodb_prerender_pace_changes_total{change, reason}` (compteur), `change` : `increased`,
  `decreased`, `held`, `released` ;
- une ligne de journal par changement d'état, jamais par évaluation ni par page :
  `prerender.pace.slowed`, `prerender.pace.held` (Warning), `prerender.pace.recovered`,
  `prerender.pace.ceiling_reached`, `prerender.pace.fixed` au début d'une passe à rythme
  fixe, et `prerender.pace.signal_lost` (Warning) puis `prerender.pace.signal_restored`, une
  fois par panne de source. Les règles de montée et de baisse en bornent le nombre à
  quelques-unes par heure.

**Débit attendu.** Rejoué sur le profil réel des 7 jours du VPS : 150 à 220 pages par minute
en moyenne, selon le coût d'une page et la part de steal que le travail ajouté entraîne. Les
4,19 millions de pages en **13 à 19 jours**, contre 24 au rythme fixe de 120, qui ralentissait
les visiteurs, et 97 à 30. Le gain est de 1,5 à 2 fois, pas de 10 : la borne est le CPU
partagé de l'hôte et son steal, pas le code de la passe. Après une semaine sans alerte ni
ralentissement mesuré, le plafond peut passer à 300 par une ligne du `.env`.

**Preuve, avant la prod.** Sur une pile locale aux quotas de la prod, épinglée sur 4 CPU,
avec une charge voisine de 1,4 à 2,8 cœurs et la sonde des visiteurs des mesures : sans passe,
puis avec la passe régulée, la médiane et le p95 des visiteurs restent dans le bruit jusqu'à
~80 % d'occupation ; au-delà, le régulateur ralentit ou retient la passe en une évaluation
(30 s) ; l'interrupteur, une source coupée, le SSR de la passe arrêté et une retenue se voient
au pupitre, dans les métriques et en une ligne de journal chacun ; la série
`nodejs_eventloop_lag_p99_seconds` du SSR des visiteurs ne change pas quand l'API lit `/load`.
Au staging puis en prod, en lecture seule : `/proc/stat` d'un conteneur égal à celui de
l'hôte, le poids réel du SSR de la passe (`cpu.weight`), une petite passe suivie, et les
alertes `HoteCpu*`, `ConteneurCpu*` et `LodbSsrRefuseDesVisiteurs` muettes.

#### File automatique des pages refusées (décision du 2026-10-21)

**Constat.** Le premier jour de prod, le SSR des visiteurs refuse ~160 rendus d'anciens patchs
par minute (`503`, budget de la voie `pinned`, ~60 admis), et la passe manuelle avance au
rythme de ~27 pages par minute : le catalogue entier (~8 450 unités) demanderait des mois.
Les pages que les visiteurs et les robots demandent vraiment attendent leur tour dans l'ordre
des passes, pas dans celui de la demande.

**Décision.** Les passes manuelles restent telles quelles. À côté, une **file automatique au
niveau de la page** : chaque page d'ancien patch que le SSR des visiteurs refuse y entre, et
une tâche de l'API la précalcule d'elle-même dans l'epoch servie, la plus demandée d'abord,
sans passe ni clic.

- **Relevé, côté SSR.** L'admission marque un rendu refusé (`SHED_MARKER`) ; pour un visiteur
  (jamais un rendu de la passe), le chemin est normalisé en `/{locale}/{version}/{page}` comme
  le routeur le lit (décodé, sans query, fragment ni barre finale, version au format de Data
  Dragon, segments d'id et de slug seulement) et compté dans une table en mémoire
  (`src/server/demand/refused-pages.ts`) : **50 000 chemins** au plus (~10 Mo), le dixième le
  moins refusé part quand elle est pleine. Les comptes sont **cumulatifs** : `GET
  /refused?after={curseur}` sur le port de métriques (9464, jamais publié ni routé) rend les
  chemins changés après le curseur, 10 000 au plus par réponse, avec l'`instance` du serveur
  (tirée au démarrage). Une lecture ne change rien : plusieurs lecteurs, une lecture refaite ou
  un redémarrage de l'API ne comptent rien deux fois. Choix contre une lecture destructive,
  qui perd ce qu'une réponse non reçue emportait.
- **File, côté API.** Toutes les 30 s, `PrerenderDemandCollectWorker` lit ce qui a changé
  depuis son curseur (en mémoire ; une nouvelle instance du SSR est relue depuis zéro) et
  ajoute à `prerender_demand` (migration additive) l'**augmentation** de chaque compte : le
  compte cumulatif moins le dernier gardé, sous la même instance du SSR ; le compte entier si
  l'instance a changé ou si le compte a baissé (chemin évincé puis refusé de nouveau). Seules
  entrent les pages qu'une passe rendrait : version parmi les anciens patchs, page listée par
  le catalogue de la version, sous son chemin canonique (un slug qui n'est pas le canonique,
  une page du dernier patch, une URL de fantaisie restent dehors). Une page d'une version dont
  le catalogue n'est pas encore stocké est jugée à son prochain refus. Une page qu'une passe a
  déjà écrite dans l'epoch servie (unité complète, ou curseur au-delà de la page) entre
  `skipped`.
- **Rendu.** `PrerenderDemandWorker`, sous un verrou consultatif à lui (`prerender:demand`),
  prend les pages `pending` par lots de 20, la plus demandée d'abord, et les écrit par le
  **chemin même de la passe** : `SsrClient` vers le SSR de la passe, verdicts de
  `RenderVerdicts`, build copié d'abord (`BuildCopier`), objet de `PageObjects` à la clé
  `pages/{epoch}/{locale}/{version}/{page}` avec ses métadonnées (CSP, `Link`, build). Avant
  chaque page : version encore tenue pour la dernière par un cache (attente), catalogue non
  stocké (attente), page écrite entre-temps par une passe (`skipped`). Un refus du SSR de la
  passe est attendu selon son `Retry-After` ; un échec qui peut passer compte un essai et
  laisse la page pour un lot ultérieur (5 min au moins), en échec définitif au 3ᵉ. Un `200`
  sans la marque `archived` compte aussi un essai, au lieu d'être ignoré comme dans une
  passe : une page de la file ouvre souvent son (version, langue), et un rendu sur un
  catalogue encore froid revient d'une classe plus courte (relevé sur la pile locale, les
  listes d'un patch tout juste ingéré). Une redirection ou une `404` sont ignorées pour de
  bon. Un SSR injoignable ou un réglage qui refuserait toutes les pages arrête le lot jusqu'au
  tour suivant ; un tour en erreur remet au suivant les pages qu'il laissait en rendu. Une ligne de synthèse par lot (`prerender.demand.drained`), jamais par page.
- **Partage avec une passe.** Une seule enveloppe pour le SSR de la passe : le rythme de
  `RenderPacer` (chaque rendu, de la file ou de la passe, y prend son tour) et ses places en
  vol, `MaxInFlight`, la borne de la voie du SSR (`RenderSlots`). Pendant qu'une passe rend
  (une de ses pages a pris une place dans la minute), la file tient au plus
  **`QueueSharePercent` (50 %)** des places, une au moins ; la passe prend les autres et toutes
  celles que la file laisse ; sans passe, la file les prend toutes. Les places se donnent dans
  l'ordre des demandes : ni l'une ni l'autre n'attend indéfiniment.
- **Rythme sans passe.** Hors passe, `RenderPacer` donne le rythme fixe
  (`LoDb__Prerender__RendersPerMinute`), prévu pour l'unité d'une ligne de commande, que nul
  signal ne surveille. En mode adaptatif, la file s'en tient alors au plancher du régulateur
  (`Pace:MinRendersPerMinute`, 30/min en prod), le rythme qu'il prend lui-même quand il ne voit
  plus (`QueuePace`) : elle tourne en continu, sans regard sur l'hôte, l'API ni les visiteurs.
  Pendant une passe, elle partage le rythme piloté ; en mode fixe, elle suit le rythme fixe.
- **Bornes.** Toutes les heures (`prerender.demand.prune`) : les pages qu'aucun visiteur ne
  s'est vu refuser depuis 30 jours partent, quel que soit leur état (sauf en rendu), puis les
  pages `pending` les moins demandées au-delà de 100 000 ; une page en échec définitif
  depuis un jour repasse `pending` (toujours refusée aux visiteurs, elle ne serait jamais
  élaguée). Index partiels : `pending` par
  demande décroissante (lots et top 10 du pupitre), date d'écriture (dernière heure, dernier
  jour).
- **Epoch.** La file écrit l'epoch servie (`LoDb__Prerender__ServedEpoch`). Une page `done`,
  `skipped` ou `failed` dans une autre epoch repasse `pending` au premier lot qui suit le
  changement ; une page laissée `rendering` par un arrêt aussi.
- **Interrupteur.** `LoDb__Prerender__Demand__Enabled` (vrai par défaut, au staging comme en
  prod ; `LODB_PRERENDER_DEMAND_ENABLED=false` puis un déploiement le coupe). Sans stockage
  configuré, la file dort comme la passe ; sans `RefusedPagesUrl`, elle finit ce qu'elle tient
  et rien n'y entre.
- **Pupitre.** Un bloc sous les chiffres clés de `/admin/prerender` : état (active, coupée, en
  veille, sans relevé) et sa raison, pages en attente, écrites dans l'heure et le jour, en
  échec et ignorées, la part de la file pendant une passe, et les dix pages en attente les
  plus demandées (`GET /api/admin/prerender/demand`). Lecture seule.
- **Mesure.** `lodb_prerender_demand_pages_total{outcome}`,
  `lodb_prerender_demand_captured_total{result}` (`queued`, `rejected`, `deferred`) et la jauge
  `lodb_prerender_demand_pending` ([`metriques.md`](../guides/metriques.md)).

Tant que nginx ne sert pas le stockage (`LODB_PAGES_STORE_ENABLED=0`), le SSR des visiteurs
continue de refuser des pages déjà écrites : leur demande s'additionne, elles ne se
recalculent pas (`done` dans l'epoch servie). La file prépare le terrain du lot 3 : les pages
qu'il servira d'abord sont celles que les visiteurs demandent.

#### Vitesse réglable (décision du 2026-10-22)

**Constat, mesuré le 2026-10-21.** La première unité de prod (16.14.1 en anglais, 901 pages)
a pris 33 minutes, 27 pages par minute : à ce rythme, les quelque 8 450 unités du catalogue
(4 à 9 millions de pages) demandaient plus de 150 jours. Ce n'était ni le CPU ni le rendu :

| Mesure (prod, VictoriaMetrics, 17:31-18:04Z puis 21:20-21:30Z) | Valeur |
|---|---|
| Rendu d'une page par le moteur, durée | 0,10 à 0,14 s |
| CPU du moteur par page | 0,09 à 0,14 s (0,13 cœur à 84 pages/min) |
| CPU de l'API et de Postgres par page | environ 0,03 s, dans le bruit des visiteurs |
| CPU du VPS pendant la passe | 33 à 45 % occupé, dont 15 % de steal |
| Rythme accordé par le régulateur | 30 pages/min (plancher), puis 75 à 84 |

Le temps d'une page (2,2 s à 27 pages/min) était l'**espacement du régulateur** : parti du
plancher, il ne montait pas tant que le VPS dépassait 50 % (vol compris) ou qu'une mesure du
SSR des visiteurs passait à l'orange ; au mieux, son plafond de 240 donnait 12 à 26 jours.
Derrière lui, deux bornes dures : **un moteur est un thread Node, un cœur au plus**
(≈ 600 pages/min sur ce VPS à 0,1 s de CPU par page), et 2 pages en vol.

Banc local (emplacement `lodb-dev-e2`, quotas de la prod, rendus directs des moteurs sur
16.14.1) : un moteur plafonne à **1 cœur** (2 363 pages/min sur ce poste, 0,025 s de CPU par
page, p50 407 ms à 16 en vol, quelques `503` au-delà de ses 15 rendus simultanés) ; deux
moteurs font **4 557 pages/min** (× 1,93), chacun à 100 % de son cœur, p50 205 ms ; l'API reste
sous 0,1 cœur et Postgres sous 2 %. Le CPU d'une page du VPS est 3,5 à 4 fois celui de ce
poste : 2 moteurs y tiennent environ 1 000 à 1 200 pages/min.

**Ce qui change.**

- **Mode manuel**, réglé en direct au pupitre (bloc « Vitesse du précalcul ») : une cible en
  pages par minute, de 1 à `LODB_PRERENDER_MANUAL_MAX` (900 sur un hôte), gardée dans
  `prerender_pace_setting` (une ligne, migration additive). Le mode adaptatif reste le défaut ;
  `LODB_PRERENDER_PACE_MODE=fixed` reste le coupe-circuit et passe avant tout réglage.
  Une nouvelle cible s'applique tout de suite sur l'instance qui répond ; l'échantillonneur
  relit le réglage toutes les 30 s, et l'instance qui écrit la passe change de mode à sa
  décision suivante.
- **Frein d'urgence** du mode manuel (`ManualDecision`), lu à chaque décision (30 s) :
  - un visiteur du **dernier patch** refusé par le SSR des visiteurs (voie `current`, refus pour
    le serveur ou la voie, jamais la part d'un client) : retenue immédiate ;
  - 6 décisions de suite (3 min) avec le VPS à **90 %** ou plus (vol compris), avec un signal
    rouge de ce qu'attendent les visiteurs (boucle d'événements p99 ≥ 250 ms ou pression CPU
    ≥ 15 % de leur SSR, API bridée ≥ 20 % : les bandes rouges du régulateur), ou sans lecture
    du SSR des visiteurs ou de `/proc/stat` : retenue ;
  - une retenue arrête tout nouveau rendu 5 min après sa dernière décision de freinage, puis la
    cible reprend. Les `503` des anciens patchs (voie `pinned`, 160 à 380 par minute en prod
    pendant les crawls) ne freinent pas : cette voie refuse au-delà de son budget, quoi que
    fasse l'hôte. Une allocation qu'aucune décision n'a renouvelée depuis 90 s retombe au
    plancher, le mode manuel compris (le frein ne lit plus rien).
- **Plusieurs moteurs** : `web-ssr-prerender` passe en répliques (`LODB_PRERENDER_RENDERERS`,
  2 sur un hôte, 1 au staging et en local), chacune un cœur au plus, au poids le plus bas.
  L'API résout leur nom commun toutes les 30 s et envoie chaque rendu à l'adresse la moins
  occupée (`RendererSpreadHandler`), le `Host` public inchangé ; elle lit `/load` de chacune
  (le régulateur prend la plus sous pression). Chaque voie admet 15 rendus simultanés : les
  premières pages d'une unité attendent son catalogue ensemble, et un refus leur coûterait
  30 s. Son budget par minute est `LODB_PRERENDER_MANUAL_MAX`.
- **Pages en vol au rythme** : la fenêtre d'une unité suit l'allocation, 1,5 s par page
  (`InFlightWidth`), jusqu'à `LODB_PRERENDER_PAGES_IN_FLIGHT` (24) : à 900 pages/min, 23 pages
  rendues, téléversées et comptées à la fois ; au plancher, une. Une fenêtre fixe plus large
  ne ferait qu'attendre le cadenceur, et une pause l'attendrait.
- **Un cache des réponses du catalogue dans le moteur** n'est pas fait : l'API ne coûte
  qu'environ 0,03 s de CPU par page et le moteur reste la borne.

**Ressources et coût, à côté du réglage.** Lu toutes les 30 s par l'échantillonneur, qu'une
passe tourne ou non (`GET /api/admin/prerender/speed`) : CPU du VPS (steal compté occupé) et
cœurs libres, CPU des moteurs et leurs rendus par minute, CPU du SSR des visiteurs et ses refus
(dernier patch, anciens patchs), CPU de l'API. Pour la vitesse regardée : CPU prévu
(pages/min × 0,1 s de CPU par page pour les moteurs, mesuré sur 10 min dès 60 pages, + 0,03 s
pour l'API et Postgres), occupation du VPS ensuite et marge, avertissement au-delà du seuil du
frein, capacité des moteurs (moteurs × quota × 60 / CPU par page), fin de la passe active
(`pagesLeft`) et du reste du catalogue (unités manquantes de l'epoch servie × pages par
unité), stockage ajouté (pages × octets par page × prix One Zone) et part du VPS prise : le VPS
étant payé, la vitesse ne coûte rien de plus que ce stockage.

**Commande d'exploitation** : `prerender pause` met la passe en cours en pause avec une raison
à elle, `prerender resume` ne reprend que cette pause (`--any` : toute pause), une ligne de
synthèse chacune, codes `0` (fait, ou rien à faire), `1`, `2`.

**Débit attendu.** À 900 pages/min, 4,2 millions de pages en 3 jours et 6 heures, 9 millions
en 7 jours ; à 600, 5 à 10 jours. Le frein, les crawls et le steal en retirent une part : la
fin réelle se lit au pupitre.

### Lot 3 — nginx sert les anciens patchs depuis le stockage

Dans la location des versions épinglées (`sites/site.conf`), derrière
`LODB_PAGES_STORE_ENABLED` (à `0`, nginx se comporte exactement comme aujourd'hui) :

- **Sans query**, nginx cherche `pages/{epoch servie}$lodb_request_path` dans le stockage :
  - la clé se construit par une `map` à expression stricte ; un chemin hors grammaire va au
    SSR sans interroger le stockage (un `proxy_pass` à variable ne normalise pas l'URI) ;
  - le dernier patch épinglé dans le chemin va au SSR sans interroger le stockage, qui ne le
    tient jamais : le SSR répond sa `301` vers l'URL courte. nginx lit le dernier patch dans
    `GET /api/meta` toutes les 30 s (module njs, `njs/latest-version.js`), une promotion le
    changeant entre deux déploiements ; tant qu'aucune lecture n'a abouti, le stockage est
    interrogé comme pour toute version ;
  - `upstream` en HTTPS dans `sites/site.conf`, gabarit (`nginx.conf` n'en est pas un),
    avec `resolve` et `keepalive` : nginx démarre même si l'hôte ne se résout pas ;
  - `proxy_ssl_server_name on`, `proxy_ssl_name`, `proxy_ssl_verify on` sur le magasin de
    certificats de l'image ;
  - jamais `proxy-headers.conf` vers le stockage : `proxy_pass_request_headers off`, seuls
    `Host` et `Connection ""` partent. Un `Authorization` ou un `x-amz-*` du client
    n'atteint pas Scaleway ;
  - seuls `GET` et `HEAD` vont au stockage, sans corps (`proxy_pass_request_body off`,
    méthode fixée par `proxy_method`) : la `map` de la clé tient compte de
    `$request_method`, et toute autre méthode prend le chemin actuel vers le SSR. La
    politique du stockage n'est plus le seul garde-fou, surtout si la lecture devient
    publique (§ 3, accès) ;
  - délais courts (connexion 1 s, lecture 3 à 5 s), et un `proxy_cache_lock_timeout` court ;
  - `proxy_intercept_errors on` ; une 404 (page absente, `NoSuchKey`), une 403 (politique,
    adresse changée), une 429, une 5xx ou un délai dépassé mènent à une location nommée qui
    reprend le chemin actuel vers le SSR, à l'identique.
    Le freinage de Scaleway répond 429, comme `limit_req` : la location du stockage refuse
    en `limit_req_status 420`, renvoyé à `@lodb_rate_limited`. `error_page` ne s'hérite pas
    et se répète dans chaque location.
- **Avec une query**, le chemin actuel.
- **En-têtes** :
  - `proxy_hide_header` des `x-amz-*`, et d'`ETag`, `Last-Modified` et `Accept-Ranges`, qui
    décrivent l'objet gzip et non la réponse : une requête conditionnelle reçoit la page, jamais
    un `304` sur l'empreinte d'un autre corps, et un `Range` la page entière ;
  - les en-têtes du § 3 reposés depuis `$upstream_http_x_amz_meta_*`, y compris sur une
    copie servie par le cache (`HIT`) ;
  - la `Cache-Control` de la classe `archived`, posée par nginx
    (`proxy_ignore_headers Cache-Control Expires`, `proxy_cache_valid 200 365d`) ;
  - `X-LoDb-Source: cache|store|ssr` : un stockage injoignable qui renvoie sans bruit vers
    le SSR devient visible dans le journal de l'edge.

  Un client sans gzip reçoit la page décompressée (`gunzip on`, déjà en place).
- **`lodb_archive` reste devant**, comme cache chaud local : `MIN_USES=1`, 20 Go, 64 Mo de
  clés, 90 jours d'inactivité. La clé porte l'epoch servie (§ 3). La limite mémoire de nginx
  (256 Mo) devient trop juste : relevée, puis mesurée. La classe `archived` passe à
  `s-maxage` d'un an : le changement de `cache-control.ts` du commit `083d3be4` est
  repris, avec ses trois specs, `tools/routing/check-urls.sh` et le bouchon des tests
  nginx (`tools/nginx/lib/upstream-stub.conf`), ainsi que sa rétention de 400 jours pour
  `front-builds` (le déploiement refuse moins de 395) : la copie sous `build/` ne couvre
  pas une page que le SSR rend sur un raté du stockage et que `lodb_archive` garde jusqu'à
  un an. Le cache de 68 Go du commit est remplacé. La purge se fait par l'epoch, et par le
  CDN au lot 4.
- **`/build/`** : un bundle que le SSR ne connaît plus (404, et seulement 404) est cherché
  sous `build/` dans le stockage, avec les mêmes garde-fous que les pages :
  - la clé vient d'une `map` stricte (`^/build/(media/)?[A-Za-z0-9._-]+$`) ; hors de cette
    grammaire, la 404 du SSR, sans interroger le stockage ;
  - ni la query ni les en-têtes du client ne partent (`proxy_pass_request_headers off`,
    seuls `Host` et `Connection ""`) ;
  - seuls `GET` et `HEAD`, sans corps, comme pour les pages ; une autre méthode garde la
    réponse du SSR ;
  - `proxy_intercept_errors on` : une erreur du stockage rend au client la 404 du SSR,
    jamais le corps XML de Scaleway ;
  - mêmes délais et même vérification TLS que les pages.
- **Le miroir d'analytics** ne compte déjà pas les versions épinglées
  (`server.d/analytics.conf`) : rien à changer ici. La passe ne passe pas par nginx et
  n'ajoute aucune vue.

### Lot 4 — CDN Cloudflare

**4a. `infra-vps` d'abord, en production avant la bascule du DNS.**

- **Adresse réelle** : sur les sites de LoDb, quand le pair est dans les plages de Cloudflare,
  `X-Forwarded-For` reçoit `CF-Connecting-IP`. C'est le schéma du snippet `(umami)` de
  `edge/caddy/Caddyfile`, dont `validate.sh` compare déjà les plages à la liste officielle.
  Sans cela, les limites par adresse de nginx (`limit_req`) traiteraient chaque nœud de
  Cloudflare comme un seul client et renverraient des 429 à tous ses visiteurs. Le snippet
  est en prod avant qu'un label de LoDb l'importe : un import inconnu fait tomber tous les
  sites du VPS.
- **Origine fermée** : les hôtes de LoDb derrière le CDN (`caddy_0`, `.fr` compris)
  refusent un pair hors des plages de Cloudflare. Sinon, les robots contournent le CDN par
  l'adresse du VPS. La fermeture :
  - répond 403, pas `abort`, qui fausserait `edge:echecs_s` (code 0) ;
  - se coupe par une variable de l'edge, coupée par défaut ;
  - s'allume **48 h au moins après le changement de serveurs de noms** (la délégation du
    `.com` a un TTL de 172 800 s), pour ne pas bloquer les clients encore sur l'ancienne ;
  - se coupe **avant** tout retour en « DNS only », qui sinon répondrait 403 à tout le
    monde ;
  - laisse passer les sondes de l'hôte ;
  - ne touche ni `api.*` ni `stats.*` (§ 2).
- **Journal de l'edge** (`observability/vector`), avant **tout** nuage orange, répétition du
  staging comprise (une ligne de journal ne se corrige pas après coup) :
  - `ip_client`, `asn` et `pays` sont tirés de `CF-Connecting-IP` quand le pair est un nœud
    Cloudflare et que l'en-tête est une adresse valide ; un en-tête forgé par un pair direct
    est ignoré ;
  - le nœud et `CF-Ray` vont dans des champs à part, avec `cdn` (`cloudflare` ou `direct`) ;
  - `source`, tiré de `X-LoDb-Source` (lot 3), dès le déploiement du lot 3 ;
  - les tests VRL (`observability/vector/tests.yaml`) suivent.
- **Certificats** (§ 2) : Caddy essaie aussi le défi HTTP-01, que Cloudflare laisse passer
  sur le port 80 tant que « Always Use HTTPS » reste coupé. On garde Let's Encrypt et on le
  prouve sur le staging avec un nom jetable proxifié ; le défi DNS seulement en cas d'échec.

**4b. Cloudflare**, réglages décrits dans `docs/guides/cdn.md` et appliqués par un script
idempotent `tools/cdn/` (API Cloudflare) :

- **DNS** : relevé complet des enregistrements chez Hostinger (MX, SPF, DKIM, DMARC, TXT de
  Search Console, `stats.`, `test.`, `api.`, `api.test.`), DNSSEC retiré chez le registraire
  s'il est actif, puis les deux zones créées sur le plan gratuit et recréées à l'identique.
  Le changement de serveurs de noms et le passage en orange sont deux étapes : les serveurs
  changent d'abord avec **tous les enregistrements en « DNS only »**, sans effet sur le
  trafic. Seuls les hôtes du § 2 passent ensuite en orange.
- **Règles de cache** (le plan gratuit n'a pas l'opérateur `matches`, donc aucune
  expression régulière) :
  - éligibles, une règle bâtie sur `starts_with()` : les 21 préfixes `/{locale}/`,
    `/build/`, `/cdn/blobs/` ; durées de l'origine (`s-maxage` pour le CDN, `max-age` pour
    le navigateur, « Respect Existing Headers ») ;
  - jamais en cache, une règle placée après, qui l'emporte : `/` (sa 302 dépend
    d'`Accept-Language` et du cookie, et Cloudflare ne distingue pas les réponses selon
    `Accept-Language`), `/api/`, `/v1/`, `/webhooks/`, `/admin`, `/b/`, `/u/` (Umami),
    `/healthz`, `/readyz`. Les pages `/{locale}/account` sont déjà `private, no-store`.
- **Smart Tiered Cache** activé.
- **Rien qui réécrit le HTML ou ajoute un script** : Web Analytics (RUM, allumé d'office sur
  le plan gratuit pour les visiteurs hors UE), Rocket Loader, Email Address Obfuscation,
  Zaraz, Automatic HTTPS Rewrites, et le reste de la liste de `cdn.md`. La page servie doit
  rester identique à l'octet près, sa CSP en dépend.
- **Pas de Bot Fight Mode** : sur le plan gratuit, on ne peut pas l'exclure par chemin, et il
  gênerait `/v1` et l'app Android. Browser Integrity Check coupé aussi. Les robots IA restent
  admis en prod, comme aujourd'hui : les trois catégories (Search, Agent, Training) sur
  « Allow », sans `robots.txt` géré par Cloudflare (défauts des zones créées depuis le
  15 septembre 2026).
- **Le staging répète la bascule** : `test.league-of-data-base.com` passe seul en orange,
  une fois la zone, commune au staging et à la prod, déplacée en « DNS only ».

**4c. LoDb.**

- **Purge** : `_deploy.yml` purge le CDN après le déploiement de prod et après une bascule
  de l'epoch servie. Secrets `CLOUDFLARE_ZONE_ID` (la zone `.com` suffit : le `.fr` ne fait
  que rediriger) et `CLOUDFLARE_PURGE_TOKEN`, ce dernier limité à la purge de la zone.
  Après un déploiement, seuls les préfixes du dernier patch sont purgés
  (`/{locale}/champions`, `/items`, `/runes`, `/summoners`, `/patches` : 105 préfixes, deux
  appels), plus `/{locale}/` et les pages éditoriales. Une page d'ancien patch ne change pas
  avec un déploiement, puisque ses bundles sont gardés : purger tout lancerait une rafale de
  ratés sur ~4 millions d'adresses, de fausses alertes et de la sortie facturée. La purge
  complète ne suit qu'une bascule de l'epoch servie ou un correctif de sécurité du front.
- **Audience interne** : `NavigationBeacon` ne compte pas la première navigation, « déjà
  comptée par le miroir » de nginx. Une page servie par le CDN n'atteint jamais nginx. La
  balise compte désormais aussi la première vue, après le premier signe de présence (comme
  `audience-presence.ts`), avec une origine `arrival` qui porte le référent externe
  (`audienceReferrerOf(document.referrer)`) : `ViewEventFactory.RefererOf` réduit sinon une
  `Navigation` à `Internal`. Le miroir d'analytics des pages est retiré dans le même
  déploiement. La définition d'une vue change (ni robots, ni clients sans JavaScript, après
  présence) : ce déploiement part **au moins 7 jours avant** la bascule du proxy, pour que les
  deux ruptures aient chacune leur date, notée au changelog.
- **Umami ne change pas** : sa mesure se fait dans le navigateur, et le snippet `(umami)`
  traite déjà `CF-Connecting-IP`. Le staging n'a pas de site Umami et `AudienceSender`
  n'envoie que depuis `league-of-data-base.com` : la preuve se fait sur le site témoin de
  `validate.sh`, puis en prod juste après la bascule (pays en temps réel d'Umami, lignes
  `/u/api/send` du journal). `CLIENT_IP_HEADER` reste `x-forwarded-for`, jamais
  `cf-connecting-ip`, qu'un client direct pourrait forger.
- **Observabilité** (`infra-vps`) :
  - un exportateur des agrégats de l'API GraphQL de Cloudflare (requêtes, part en cache,
    statuts, pays, octets) vers VictoriaMetrics, avec un panneau CDN dans « Edge — trafic »,
    déployé dès que la zone est active ;
  - l'alerte concernée est `SiteTraficAnormal` : elle ne se déclenche que sur une hausse,
    donc pas sur la baisse de la bascule ; le vrai risque est la rafale de ratés d'une purge.
    Un silence borné (`amtool`, ~6 h) à la bascule et après une purge complète, pas 7 jours,
    qui couvriraient la semaine la plus risquée. À partir de la bascule, le trafic de LoDb se
    juge sur le total de Cloudflare.

### Lot 5 — Mesure de l'effet

Valeurs de départ relevées le 2026-10-20, puis relevées de nouveau juste avant que le lot 3
parte en prod, et une troisième fois juste avant la bascule du proxy. Le lot 3 se mesure
seul, 7 jours après le début de la passe de prod, avant tout changement de DNS ; puis 7 jours
après la bascule, et 4 à 8 semaines plus tard pour Search Console. Après le lot 4, la part de
503 par agent perd de son dénominateur les réponses servies par Cloudflare : les chiffres de
Cloudflare s'y ajoutent.

| Mesure | Source | Départ | Cible |
|---|---|---|---|
| 503 par jour sur le site | VictoriaLogs, `… \| stats by (statut) count()` | 142 467 | proche de 0 |
| Rendus épinglés refusés | `sum by (reason) (increase(lodb_ssr_renders_total{lane="pinned",outcome="refused"}[24h]))` | 139 165 | proche de 0 |
| Part des requêtes d'ancien patch servies sans rendu (CDN, `lodb_archive`, stockage) | `X-LoDb-Source` dans le journal de l'edge, et Cloudflare | 2,5 % | > 99 % |
| Part de 503 des robots déclarés | journal de l'edge, par agent, plus Cloudflare | Bing 65 %, ClaudeBot 53 %, GPTBot 47 %, Applebot 39 % | proche de 0 |
| Temps de réponse moyen de l'exploration | Search Console, statistiques d'exploration | à relever avant le lot 3 | en baisse |
| Pages indexées, impressions | Search Console | à relever avant le lot 3 | en hausse |
| Coût mensuel Scaleway | facture | — | < 5 € |

### Lot 6 — Plus tard : alléger les pages

Avant une seconde passe complète :

- sortir le CSS critique en ligne dans une feuille hashée ;
- mettre le catalogue i18n dans un fichier hashé par langue, chargé avant l'hydratation ;
- charger la liste des versions à la demande.

Cible : environ 17 Ko par fiche au lieu de 37,6. Le stockage est divisé par deux, et le
premier affichage s'accélère partout, puisque le navigateur garde ces fichiers en cache. Le
LCP et le CLS sont mesurés avant et après.

## 5. Ce qui ne change pas

- Le dernier patch, sa zone `lodb_pages` et ses budgets de rendu.
- La grammaire d'URL, les canoniques (amendement du 2026-10-14) et les sitemaps.
- Le SSR : aucun cookie lu, aucun secret détenu.
- La voie épinglée du SSR, qui devient le repli.
- `LODB_ARCHIVE_EPOCH` reste la purge d'urgence du cache nginx ; l'epoch servie est celle
  du stockage.

## 6. Documentation à mettre à jour avec le code

- ADR 0011 et les amendements de 0002, 0003, 0004 et 0005 (lot 0).
- `docs/architecture/pieges.md` :
  - les deux epochs du stockage, et `LODB_ARCHIVE_EPOCH` à côté ;
  - le repli vers le SSR, et le 429 du stockage distinct de celui de `limit_req` ;
  - l'en-tête de précalcul effacé côté public ;
  - l'hôte public envoyé par la passe ;
  - les en-têtes en métadonnées, sous 2 Ko ;
  - la clé du stockage tirée d'une `map` stricte, jamais de l'URI brute ;
  - `/` jamais en cache au CDN ;
  - la fermeture de l'origine coupée avant tout retour en « DNS only ».
- `docs/guides/configuration.md` : variables, secrets, stockage, Cloudflare ; le rythme
  adaptatif de la passe et son SSR (§ 4.10).
- `docs/guides/metriques.md` : `GET /load` des SSR, les métriques du régulateur, la voie
  `prerender` sous `service="web-ssr-prerender"`.
- `tools/prerender/README.md` : les étapes de la console Scaleway, le script, la sonde, la
  vérification de la facturation.
- `docs/guides/cdn.md` (nouveau) : réglages Cloudflare, bascule du DNS et retour arrière.
- `docs/guides/observabilite.md` : ce que voit chaque source de mesure derrière le CDN, et
  l'en-tête `X-LoDb-Source`.
- `docs/guides/migration-edge-proxy.md` : Cloudflare devant l'edge.
- Une entrée de changelog par changement visible : pages d'ancien patch sans erreur, site
  plus rapide dans le monde.

## 7. Tests

Selon `docs/guides/tests.md`, au plus près de la couche qui porte le comportement :

- **SSR (Vitest)** :
  - la voie `prerender` : l'en-tête l'emporte sur l'URL, budget et rafale, place laissée au
    trafic réel, aucune limite par client, comptage, aucune ligne de journal par page ;
  - une réserve ou un maximum de la voie **posés** sans place sous le total sont refusés
    au démarrage ; un total abaissé avec les valeurs par défaut de la voie démarre, la voie
    ramenée sous lui ;
  - sans l'en-tête, une requête reste dans sa voie ;
  - la marque de la classe `archived` et l'en-tête `X-LoDb-Build`, sur la voie `prerender`
    seulement ;
  - `/_lodb/build-files` répond 404 sans l'en-tête ;
  - `GET /load` : ses champs, cumulés ; la fenêtre du retard de la boucle d'événements
    tourne toutes les 30 s ; une lecture de `/load` ne remet rien à zéro de ce que lit
    `/metrics` ; le cgroup illisible donne `null`.
- **Régulateur du rythme (xUnit, horloge simulée, sources simulées)** : démarrage au
  plancher, montée d'un pas par minute de vert, tenue, baisse de moitié jamais sous le
  plancher, attente de 3 min après une baisse, retenue de 5 min prolongée puis reprise au
  plancher, aucune montée d'une passe qui n'use pas son droit, source illisible prise pour
  orange, droit périmé ramené au plancher, mode fixe sans lecture ; lecture de `/proc/stat`
  (steal, `guest` non compté deux fois) et de `cpu.stat` ; le `RenderPacer` à rythme variable
  et en retenue ; une ligne de journal par changement d'état ; `pulse.pace` du suivi.
- **API (xUnit, Postgres réel ; stockage S3 simulé par MinIO dans Testcontainers)** :
  - la liste des pages d'une (version, langue) : les 4 listes et toutes les entités
    listées, les sitemaps inchangés ;
  - l'ordre de passage ;
  - seule une `200` marquée `archived` et compressée est écrite, avec ses métadonnées ; une
    301 ou une 404 est sautée ;
  - le build copié avant ses pages, une seule fois ; une page d'un build inconnu
    (`X-LoDb-Build` absent de `prerender_build`) déclenche la copie avant son `PUT`, et une
    empreinte qui change entre la page et la liste fait rendre la page de nouveau ;
  - la reprise au curseur ;
  - le `Retry-After` respecté ;
  - une 401, une 403 ou une 404 sur un `PUT` met la passe en pause, sans compter la page ;
    une autre 4xx compte la page en échec sans pause ; une 409 sur un bundle est retentée ;
  - le `409` sur une seconde passe ;
  - l'accès administrateur seul et l'audit ;
  - les unités (une par langue) ajoutées à la promotion d'une version, ou rien quand
    `AutoEnqueueOnPromotion` est faux ;
  - la signature des écritures (vecteurs publiés d'AWS SigV4).

  MinIO sert aux tests et au développement seulement, épinglé par empreinte. Le
  comportement réel de Scaleway (lecture anonyme par adresse, métadonnées rendues) est
  prouvé par la sonde du lot 1, pas par la CI.
- **Script du stockage (`node --test`, faux `docker` et moto)** : création, rejeu sans
  changement, dérive corrigée, entrée invalide refusée avant tout appel, clé jamais en
  argument ni en sortie, politique sans l'opérateur refusée, retrait de l'epoch servie
  refusé.
- **nginx (`tools/nginx`, stockage simulé par un stub)** :
  - une page trouvée est servie avec ses en-têtes reposés, aussi depuis le cache, sans aucun
    en-tête du stockage (`x-amz-*`, `ETag`, `Last-Modified`, `Accept-Ranges`) : un `HEAD`, une
    requête conditionnelle sur les validateurs du stockage ou un `Range` reçoivent la page ;
  - une 404 (page absente), une 403 (politique, adresse changée), une 429, une 5xx ou un
    délai dépassé du stockage mènent au SSR ; la
    429 du stockage reste distincte de celle de `limit_req` ;
  - une query, un chemin hors grammaire ou une méthode autre que `GET` et `HEAD` (un
    `POST`, `PUT` ou `DELETE` avec un corps) mène au SSR sans interroger le stockage ;
  - le dernier patch épinglé dans le chemin (lu dans `/api/meta`) aussi, quelle que soit la
    langue, mais pas une version qui commence comme lui (`15.10.10` pour `15.10.1`) ;
  - le repli de `/build/`, sur une 404 du SSR seulement ; un nom hors grammaire, une query
    ou un en-tête `Authorization` / `x-amz-*` du client n'atteignent pas le stockage, et
    une erreur du stockage rend la 404 du SSR ;
  - un client sans gzip ;
  - l'en-tête de précalcul envoyé par un client est effacé ;
  - les valeurs de `X-LoDb-Source`, l'epoch servie dans la clé ;
  - `LODB_PAGES_STORE_ENABLED=0` identique à aujourd'hui, et un démarrage sans résolution
    de l'hôte du stockage ;
  - l'accord des défauts.
- **Front (Vitest)** : la balise compte la première vue après présence (lot 4c) ; les états
  du panneau.
- **E2E** : un parcours nginx → stockage simulé → page hydratée, que seule cette couche voit.
- **`infra-vps`** :
  - `validate.sh` : plages Cloudflare, `X-Forwarded-For`, refus d'un pair hors Cloudflare ;
  - tests VRL du journal.

## 8. Hors périmètre

- La limite par réseau des hébergeurs, un chantier à part.
- Les pages `?page=N` pré-rendues et R2 + Worker (§ 2).
- Les requêtes des rapports d'audience d'Écluse (`app-ia-auto-task`). Après la bascule, le
  journal de l'edge ne voit plus que ce qui atteint l'origine. Elles devront lire aussi les
  agrégats de Cloudflare.

## 9. Livraison

Selon `AGENTS.md` : conventions, garde-fous, un commit par changement cohérent, aucune ligne
d'attribution. Pas de push sur `dev`, `test` ni `main`.

1. **Cette branche (`feat/pre-rendu-catalogue`)** : lots 0 à 3, une pull request vers `dev`
   avec une CI verte. Avant la PR, sur la pile locale :
   - une passe sur quelques (version, langue) va au bout ;
   - nginx sert les pages depuis le stockage simulé ;
   - le repli vers le SSR fonctionne.

   Sur le staging, qui reste presque vide : le script, puis la sonde `verifier` depuis le
   VPS ; ensuite une passe manuelle sur un périmètre borné (environ 5 unités, ~2 500 pages),
   pour la justesse seulement. Aucune passe automatique, et les objets expirent après
   14 jours.
2. **Prod, lots 0 à 3**, dès qu'ils sont en prod et avant tout travail de DNS ou de
   Cloudflare :
   - le script et la sonde `verifier` sur le stockage de prod ; les mesures de départ
     relevées de nouveau ;
   - **la facturation, tranchée** : Scaleway compte « 1 Ko pendant 1 seconde comme
     1 Go-heure » et arrondit sur le total du projet, pas par objet (mesure du 20 octobre 2026
     sur le staging, ADR 0011, § Conséquences) : rien à confirmer avant la passe complète ;
   - une première petite unité à budget bas, pour mesurer le débit, puis la passe complète
     dans l'ordre de la demande ;
   - l'effet du lot 3 mesuré seul, 7 jours plus tard (lot 5).
3. **`infra-vps`, une branche à part** : lot 4a, en production avant la bascule.
4. **LoDb, branche suivante** partie de `dev` après la fusion du point 1 : lot 4c, au moins
   7 jours avant la bascule du proxy.
5. **Bascule du DNS** (lot 4b) :
   - les serveurs de noms changent, tous les enregistrements en « DNS only » ; l'exportateur
     est déployé ;
   - la fenêtre est annoncée et les mesures de départ du lot 5 relevées ;
   - répétition : `test.` seul en orange ;
   - le silence borné est posé, puis les hôtes de prod passent en orange ;
   - 48 h plus tard, la fermeture de l'origine ;
   - retour arrière, toujours dans cet ordre : couper la fermeture de l'origine, puis
     repasser les enregistrements en « DNS only » chez Cloudflare.
6. **Mesure du lot 5**, 7 jours après la bascule.
