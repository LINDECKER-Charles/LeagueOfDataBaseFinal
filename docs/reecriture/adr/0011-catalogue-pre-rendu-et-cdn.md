# ADR 0011 — Catalogue pré-rendu et CDN

- **Statut** : acceptée — 2026-10-20
- **Révise** (amendements du 2026-10-20) : [ADR 0002](0002-hote-serveur-unique.md) (un second
  hôte sortant), [ADR 0003](0003-ingestion-proactive-et-taches-de-fond.md) (la tâche de
  précalcul), [ADR 0004](0004-stockage-etat-et-scaling.md) (nginx sert aussi depuis un
  stockage objet), [ADR 0005](0005-web-ssr-urls-et-seo.md) (`lodb_archive` en cache chaud,
  la classe `archived` gardée un an, le CDN devant le site)
- **Remplace** : la spécification du cache recyclé de 68 Go, interrompue et jamais
  publiée
- **Détail** : [spécification](../../produit/pre-rendu-catalogue-cdn.md) (lots, réglages,
  tests, livraison)

## Contexte

Relevé des 24 h avant le 2026-10-20 à 10:30 UTC : 343 795 requêtes, six fois plus qu'une
semaine plus tôt, dont 87 % visent une version épinglée dans le chemin. Les pages des anciens
patchs sont rendues à la demande, dans un budget de 60 rendus par minute saturé en
permanence : 139 165 rendus refusés et **142 467 réponses 503** par jour. Les robots déclarés
les subissent (65 % de 503 pour Bingbot, 53 % pour ClaudeBot, 47 % pour GPTBot).

Le cache `lodb_archive` n'y peut rien : 2,5 % de `HIT`. Les robots demandent 1,5 fois chaque
adresse en 7 jours ; une page gardée à sa 2ᵉ demande et pendant une semaine n'est presque
jamais resservie. Un cache recyclé de 68 Go garderait au mieux 40 % du catalogue.

Le catalogue des anciens patchs est pourtant fini et immuable : 402 versions × 21 langues,
**4,19 millions de pages**, ~39 Ko chacune en gzip, **~155 Go**. Le VPS a 136 Go libres.
Ses datasets sont déjà ingérés : le calculer une fois ne télécharge rien. Enfin, un visiteur
lointain paie environ 3 allers-retours vers Paris avant le premier octet, même sur une page
gardée (0,25 s depuis New York, 0,7 à 0,85 s depuis Tokyo), et Googlebot explore surtout
depuis les États-Unis.

## Décision

**Calculer une fois chaque page d'ancien patch, la ranger dans un stockage objet Scaleway
(`fr-par`), que nginx sert avec le SSR en repli ; mettre Cloudflare devant le site.** Le
stockage supprime le rendu, le CDN la distance : un CDN seul garde mal 4 millions d'adresses
peu demandées, et chacun de ses ratés doit rester peu coûteux à l'origine.

```
visiteur ─► Cloudflare ── en cache ──────────────────────────────► servi
              │ absente
              ▼
           Caddy ─► nginx ── dernier patch ─► lodb_pages ─► SSR    (inchangé)
                       └──── ancien patch ──► lodb_archive ── en cache ─► servi
                                                 │ absente
                                                 ▼
                                              stockage fr-par ── trouvée ─► servi
                                                 │ absente, 403, 429, 5xx, délai
                                                 ▼
                                              SSR, voie épinglée          (repli)
```

### Stockage

- **Un projet Scaleway par environnement** (`lodb-pages-prod`, `lodb-pages-staging`), chacun
  avec son stockage privé du même nom. Les droits IAM de Scaleway portent sur un projet
  entier : une clé du staging ne peut jamais écrire en prod, même pendant la reprise d'une
  politique cassée. Le stockage des sauvegardes reste dans le projet par défaut.
- **Bucket policy** (version `2023-04-17`, `Allow` seulement), exactement trois principaux :
  l'opérateur (`user_id`, `s3:*`), l'application d'écriture de l'environnement
  (`s3:PutObject` sur `pages/*` et `build/*`), et tout le monde (`s3:GetObject` sur les mêmes
  préfixes) à la condition `aws:SourceIp` = l'adresse IPv4 de sortie du VPS. Un principal
  absent perd ses accès : la politique est posée en dernier et relue.
- **Écriture** par une application IAM par environnement, jeu `ObjectStorageObjectsWrite`
  seul sur son projet : elle ne liste, ne lit ni ne supprime. **Aucune application
  d'administration** : le script `tools/prerender/scaleway-bucket.sh` tourne depuis le poste
  de l'opérateur, propriétaire de l'organisation, avec une clé personnelle temporaire.
- **Classe posée à chaque écriture** : Scaleway n'a pas de classe par défaut, et un objet
  sans classe est Multi-AZ. `ONEZONE_IA` pour les pages, qui se recalculent ; Standard pour
  les bundles, qui ne se recalculent plus une fois le volume `front-builds` élagué.
- **Clés** adressées par chemin : `pages/{epoch}/{locale}/{version}/{ressource}[/{id}-{slug}]`
  (chemin canonique de la page), `build/{nom}`, `build/media/{nom}`. L'epoch `0` est réservée
  aux sondes.
- **Ce qui est stocké** : la `200` de classe `archived` du SSR, compressée en gzip une fois,
  avec `Content-Type`, `Content-Encoding` et `Cache-Control` en en-têtes de l'objet, et en
  métadonnées (sous 2 Ko) la CSP de la page, sa version Report-Only, `Reporting-Endpoints`,
  `Link` et le build. Jamais une 503, une 5xx, une 404 ni une 301.
- **Deux epochs du stockage** : celle d'écriture, celle de la passe en cours, et celle que
  sert nginx (`LODB_ARCHIVE_SERVE_EPOCH`). Une epoch se remplit pendant que l'autre reste
  servie ; la bascule se fait par la variable et un redéploiement, sans point d'accès.
  `LODB_ARCHIVE_EPOCH`, le compteur de la clé du cache nginx, reste : la clé de
  `lodb_archive` porte les deux.
- **Cycle de vie** : une règle par epoch retirée (`retire-pages-<N>`, préfixe `pages/<N>/`),
  posée par le script au retrait, jamais sur l'epoch servie ni sur celle en écriture. Au
  staging, une expiration permanente de `pages/` à 14 jours. Versionnage et Object Lock
  coupés.

### Passe de précalcul

- Une tâche de fond de l'API ([ADR 0003](0003-ingestion-proactive-et-taches-de-fond.md),
  amendement du 2026-10-20) parcourt les pages par (version, langue) : chaque catalogue se
  charge une fois pour toutes ses pages. Les pages sont les 4 listes et toutes les entités
  listées, tirées de la même fonction que les sitemaps.
- Elle appelle le SSR **en direct** sur le réseau de la pile, jamais à travers nginx, avec
  `X-LoDb-Prerender: 1`, l'hôte public de l'environnement (`X-Forwarded-Host`),
  `X-Forwarded-Proto: https` et `Accept-Encoding: gzip`. nginx efface toujours l'en-tête du
  précalcul côté public : un client ne peut pas se faire passer pour la passe.
- Le SSR l'admet dans une **voie `prerender`** : son propre budget, une place seulement si le
  trafic réel en garde, aucune limite par client, aucune ligne de journal par page. Il marque
  `X-LoDb-Cache-Class: archived` la réponse qu'il faut écrire et expose, à la passe seule, la
  liste des bundles du build (`/_lodb/build-files`).
- L'API copie **tout le jeu haché** d'un build avant sa première page (les chunks différés
  ne sont nommés que dans le JavaScript), puis écrit un `PUT` par page. Une 301 ou une 404
  est sautée ; une 503 fait reculer la tâche selon son `Retry-After`.
- Sur la voie `prerender`, chaque réponse porte `X-LoDb-Build: <empreinte>`, la même
  empreinte (sha256 des noms triés) que `/_lodb/build-files`. L'API n'écrit une page que si
  ce build est déjà dans `prerender_build` ; sinon elle tire la liste, copie le jeu, puis
  écrit la page, ou la fait rendre de nouveau si la liste porte une autre empreinte. Un SSR
  redéployé ou redémarré pendant la passe ne laisse ainsi aucune page dont les bundles
  manquent au stockage. La métadonnée `lodb-build` vient de cet en-tête.
- État en Postgres (`prerender_pass`, `prerender_unit`, `prerender_build`), migrations
  additives : une passe reprend au curseur à travers les déploiements, une seule est active
  (une seconde répond `409`). Les compteurs des unités donnent pages et octets par epoch,
  sans lister le stockage.
- À la promotion d'un patch, les unités (une par langue) du patch devenu épinglé
  rejoignent la passe active ou en ouvrent une, en prod seulement
  (`LoDb__Prerender__AutoEnqueueOnPromotion`). Le staging reste presque vide : des passes
  manuelles sur un petit périmètre.
- Un panneau d'administration pilote la passe sous `/api/admin/prerender` ; chaque action
  est auditée. Il montre l'epoch servie et, quand une passe est complète, la consigne de
  bascule.

### nginx

- Pour une page épinglée sans query, nginx cherche `pages/{epoch servie}{chemin}` dans le
  stockage, derrière `lodb_archive`, devenu un **cache chaud** (gardé à la 1ʳᵉ demande,
  20 Go, 90 jours d'inactivité). Une 403, une 404, une 429, une 5xx ou un délai dépassé
  reprennent le chemin actuel vers le SSR, à l'identique.
- La clé vient d'une `map` stricte sur le chemin ; un chemin hors grammaire ou une query va au
  SSR. Aucun en-tête ni aucun corps du client ne part vers le stockage, et seuls `GET` et
  `HEAD` l'interrogent. La 429 du stockage reste distincte de celle de `limit_req`.
- Les en-têtes de la page sont reposés depuis les métadonnées ; `X-LoDb-Source:
  cache|store|ssr` dit d'où vient chaque page épinglée.
- La classe `archived` passe à un `s-maxage` d'un an : une page d'ancien patch ne change
  qu'avec une epoch.
- Un bundle que le SSR ne connaît plus (404) est cherché sous `build/` dans le stockage.
- `front-builds` garde ses bundles **400 jours** (`LODB_FRONT_BUILDS_RETENTION_DAYS`, le job
  de déploiement refuse moins de 395), plus que l'an de la classe `archived`. La copie sous
  `build/` ne vaut que pour un build dont la passe a écrit une page : une page rendue par le
  SSR sur un raté du stockage, que `lodb_archive` garde jusqu'à un an, nomme les bundles d'un
  build jamais copié (toutes les pages avant le lot 3, celles du staging, sans passe
  automatique, et celles des déploiements entre deux passes).
- `LODB_PAGES_STORE_ENABLED=0` (défaut en local et en CI) laisse nginx tel qu'avant.

### CDN

Les options retenues pour les certificats, les hôtes proxifiés, la bascule d'epoch,
l'audience interne et les métriques de Cloudflare sont **provisoires**, révisables par le
propriétaire du site (§ 2 de la [spécification](../../produit/pre-rendu-catalogue-cdn.md)).

- **Cloudflare, plan gratuit**, devant `league-of-data-base.com`, `www`, `test` et le `.fr`.
  `api.*`, `api.test.*` et `stats.*` restent en « DNS only ».
- Les serveurs de noms changent d'abord, tous les enregistrements en « DNS only » ; les hôtes
  passent ensuite en orange, `test.` en premier. Rien ne réécrit le HTML ni n'ajoute de
  script (Web Analytics, Rocket Loader, Bot Fight Mode… coupés) ; les robots IA restent
  admis.
- Cache : les préfixes de locale, `/build/` et `/cdn/blobs/`, aux durées de l'origine ;
  jamais `/` (sa 302 dépend d'`Accept-Language`), `/api/`, `/v1/`, `/webhooks/`, `/u/`,
  `/b/`, `/admin`, `/healthz`, `/readyz`.
- Certificats : Let's Encrypt en HTTP-01 à travers Cloudflare, mode « Full (strict) »,
  « Always Use HTTPS » coupé (sinon le défi HTTP-01 n'atteint plus Caddy).
- `infra-vps` d'abord : l'edge transmet `CF-Connecting-IP` en `X-Forwarded-For` quand le
  pair est un nœud Cloudflare, et le journal de l'edge en tire l'adresse du visiteur. Une
  fermeture de l'origine (403 hors des plages de Cloudflare, sondes de l'hôte exemptées),
  coupable, s'allume 48 h après le changement de serveurs de noms et se coupe avant tout
  retour en « DNS only ».
- Après un déploiement, seuls les préfixes du dernier patch sont purgés ; la purge complète
  suit une bascule d'epoch ou un correctif de sécurité du front.
- L'audience interne compte la première vue dans le navigateur (origine `arrival`), le
  miroir de nginx est retiré, au moins 7 jours avant la bascule du proxy. Les chiffres de
  Cloudflare entrent dans VictoriaMetrics par un exportateur.

### Ordre

Les lots 0 à 3 (stockage, passe, nginx) partent en prod et la passe démarre **avant** tout
travail de DNS : le lot 3 soulage seul le budget saturé, et son effet se mesure seul. Le CDN
suit.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| Cache recyclé de 68 Go sur le VPS | Garde au mieux 40 % du catalogue ; un balayage au hasard rate plus d'une fois sur deux, et chaque raté repasse par le budget saturé |
| Précalcul complet sur le disque du VPS | ~165 Go pour 136 Go libres |
| Prérendu au build ([ADR 0005](0005-web-ssr-urls-et-seo.md)) | Toujours impossible : un nouveau patch imposerait un rebuild. Le précalcul tourne à côté du site, au fil des patchs |
| Cloudflare R2 + Worker | ~5 $ par mois de Workers et ~15 $ d'écritures à l'amorçage ; à revoir si le VPS ne devait plus voir du tout les anciens patchs |
| Un seul projet Scaleway, une politique par stockage | Supprimer une politique (la reprise documentée d'une erreur) rouvre le stockage à tout le projet : une clé du staging écrirait en prod |
| Une application IAM d'administration | Une clé de plus à garder ; la clé temporaire du propriétaire suffit, comme pour les sauvegardes |
| Bascule de l'epoch à chaud (fichier ou `map` lu par nginx) | Exige un signal de rechargement entre conteneurs, donc le socket Docker, que le durcissement refuse |
| Certificat d'origine Cloudflare | Refusé par les navigateurs : il casse le retour en « DNS only », les apps et les vérifications directes |
| Purge complète du CDN à chaque déploiement | Une rafale de ratés sur ~4 millions d'adresses, de fausses alertes et de la sortie facturée, pour des pages qui ne changent pas |
| MinIO en production | Toujours écarté ([ADR 0004](0004-stockage-etat-et-scaling.md)) ; il sert aux tests et au développement seulement |

## Conséquences

- **+** Les pages des anciens patchs ne passent plus par le budget des rendus : les 503 et
  les rendus refusés tendent vers 0.
- **+** Coût faible : ~1,25 €/mois de stockage One Zone pour 155 Go, le double tant que
  deux epochs coexistent, plus la sortie au-delà de 75 Go gratuits ; cible < 5 €/mois.
- **+** Une page stockée garde ses bundles aussi longtemps qu'on la sert, quelle que soit
  la rétention de `front-builds`. Celle-ci reste à 400 jours pour les pages rendues par le
  SSR, que `lodb_archive` garde un an.
- **+** Le CDN rapproche toutes les pages des visiteurs lointains et des robots.
- **−** Un correctif de sécurité du front qui bascule tout de suite sur une epoch vide
  renvoie **toutes** les pages épinglées vers le SSR saturé pendant les 10 à 16 jours de la
  passe : les 503 reviennent. On l'atténue par l'ordre de la demande et un budget de passe
  relevé.
- **−** La lecture du stockage dépend de l'adresse du VPS : s'il en change, chaque lecture
  répond 403 et nginx retombe sans bruit sur le SSR, pendant que la passe continue d'écrire
  (l'écriture ne dépend pas de l'adresse). `X-LoDb-Source` le rend visible ; la politique
  est à mettre à jour avec l'hôte.
- **−** Une lecture anonyme conditionnée par l'adresse à côté de l'IAM n'est pas documentée
  par Scaleway, ni le passage intact des métadonnées. La sonde `verifier` les prouve avant
  que nginx lise le stockage en prod ; à défaut, la lecture devient publique sur `pages/*`
  et `build/*`. Les tests et le développement utilisent MinIO, qui ne prouve rien de
  Scaleway.
- **+** **Facturation tranchée : l'arrondi se fait sur le total du projet, pas par objet.**
  Scaleway compte « 1 Ko pendant 1 seconde comme 1 Go-heure » sans dire sur quoi il arrondit.
  Mesure du 20 octobre 2026 : 300 objets de 1 Ko en `ONEZONE_IA`, écrits entre 18 h 33 et
  18 h 40 UTC dans `lodb-pages-staging` ; la première ligne de facture, environ 4 h plus tard
  sur le projet des pages, est « Standard One Zone / ONEZONE - fr-par », 3 Go-heures soit
  0,01 € (couverts par l'essai gratuit, −0,01 €). Un arrondi par objet aurait donné environ
  300 Go-heures par heure. Les 4 millions d'objets de la prod gardent donc le coût du volume
  (~1,25 €/mois), sans condition à lever avant la passe complète.
- **−** Une epoch retirée met au moins 9 jours à disparaître (500 000 objets supprimés par
  jour) ; plus vite, une suppression par lots depuis le poste de l'opérateur.
- **−** Derrière le CDN, le journal de l'edge et les métriques de Caddy ne voient plus que
  les ratés : les mesures d'audience et de trafic changent de source, avec une date chacune.
