# ADR 0011 — Catalogue pré-rendu et CDN

- **Statut** : acceptée — 2026-10-20
- **État** (2026-10-23) :
  - le stockage, la passe et la lecture par nginx (lots 0 à 3 de la spécification) sont en
    prod depuis le 2026-10-21 (PR #93), la lecture par nginx derrière
    `LODB_PAGES_STORE_ENABLED` ;
  - le SSR dédié et le régulateur sont en prod le 2026-10-21 (PR #96), la file de la demande
    et la vitesse réglable le 2026-10-22 (PR #99) ;
  - **le CDN (lot 4) n'est pas construit** : rien de Cloudflare dans le dépôt ;
  - les pages révisables (amendement du 2026-10-22) sont en vigueur en prod depuis le
    2026-10-22, avec la règle par état (ADR 0005, amendement du 2026-10-22) ; l'epoch 2,
    servie depuis, attend ses passes ;
  - nginx lit le stockage en prod depuis le 2026-10-23 (`LODB_PAGES_STORE_ENABLED=1`,
    PR #107), avec la file au pupitre et la vitesse manuelle jusqu'à 4 000 (amendements du
    2026-10-23).
- **Révise** (amendements du 2026-10-20) : [ADR 0002](0002-hote-serveur-unique.md) (un second
  hôte sortant), [ADR 0003](0003-ingestion-proactive-et-taches-de-fond.md) (la tâche de
  précalcul), [ADR 0004](0004-stockage-etat-et-scaling.md) (nginx sert aussi depuis un
  stockage objet), [ADR 0005](0005-web-ssr-urls-et-seo.md) (`lodb_archive` en cache chaud,
  la classe `archived` gardée un an, le CDN devant le site)
- **Amendements** :
  - le 2026-10-21, la passe a son propre SSR, de faible priorité, et un rythme qui suit le CPU
    libre de l'hôte (voir § « Amendement du 2026-10-21 — un SSR et un rythme pour la passe ») ;
  - le 2026-10-21 encore, une file automatique précalcule les pages que le SSR des visiteurs
    refuse, la plus demandée d'abord (voir § « Amendement du 2026-10-21 — file de la
    demande ») ;
  - le 2026-10-22, la vitesse d'une passe se règle à la main, sous un frein d'urgence, sur
    plusieurs moteurs (voir § « Amendement du 2026-10-22 — vitesse réglable ») ;
  - le 2026-10-22 encore, le stockage garde aussi des pages révisables, celles des anciens
    patchs dont l'état est encore ouvert, pré-calculées de nouveau page par page quand une
    promotion le clôt ; une passe peut se limiter aux ouvreurs (voir § « Amendement du
    2026-10-22 — pages révisables »). En vigueur en prod depuis le 2026-10-22 ;
  - le 2026-10-23, le maximum de la vitesse manuelle monte à 4 000 sur un hôte, avec un
    avertissement au pupitre au-delà de 1 300 (voir § « Amendement du 2026-10-23 — vitesse
    manuelle jusqu'à 4 000 »). En prod depuis le 2026-10-23 ;
  - le 2026-10-23 encore, les pages que la file de la demande écrit comptent dans leur epoch, une
    seule fois, et le pupitre montre la file page par page (voir § « Amendement du
    2026-10-23 — file de la demande au pupitre et dans l'epoch »). En prod depuis le
    2026-10-23.
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
                                                 │ absente (404), 403, 429, 5xx, délai
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
  `Link` et le build. Jamais une 503, une 5xx, une 404 ni une 301. *Amendé le 2026-10-22 :
  la `200` de classe `archived` ou `revisable`, chacune avec sa `Cache-Control` (amendement
  du 2026-10-22).*
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
  listées, tirées de la même fonction que les sitemaps. *Amendé le 2026-10-22 : les 4 listes,
  la page de patch, puis les entités, dans cet ordre figé (`VersionPages.Of`), les entités
  tirées du prédicat que partage l'index des états.*
- Elle appelle le SSR **en direct** sur le réseau de la pile, jamais à travers nginx, avec
  `X-LoDb-Prerender: 1`, l'hôte public de l'environnement (`X-Forwarded-Host`),
  `X-Forwarded-Proto: https` et `Accept-Encoding: gzip`. nginx efface toujours l'en-tête du
  précalcul côté public : un client ne peut pas se faire passer pour la passe.
- Le SSR l'admet dans une **voie `prerender`** : son propre budget, une place seulement si le
  trafic réel en garde, aucune limite par client, aucune ligne de journal par page. Il marque
  `X-LoDb-Cache-Class: archived` la réponse qu'il faut écrire (*ou `revisable`, depuis le
  2026-10-22*) et expose, à la passe seule, la liste des bundles du build
  (`/_lodb/build-files`).
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
  (`LoDb__Prerender__AutoEnqueueOnPromotion`, faux en prod tant que la passe complète, lancée
  à la main, n'est pas terminée). Le staging reste presque vide : des passes
  manuelles sur un petit périmètre. *Amendé le 2026-10-22 : seulement une fois que l'index
  des états couvre le patch promu ; ce réglage ne commande plus que ces unités, la file des
  états clos tourne sans lui, staging compris.*
- Un panneau d'administration pilote la passe sous `/api/admin/prerender` ; chaque action
  est auditée. Il montre l'epoch servie et la consigne de bascule dès qu'une epoch plus
  récente, que la passe active n'écrit pas, n'a plus aucun manque, quelles que soient les
  passes qui l'ont remplie (`EpochReadiness`).

### nginx

- Pour une page épinglée sans query, nginx cherche `pages/{epoch servie}{chemin}` dans le
  stockage, derrière `lodb_archive`, devenu un **cache chaud** (gardé à la 1ʳᵉ demande,
  20 Go, 90 jours d'inactivité). Une page absente répond `404 NoSuchKey` à une lecture
  anonyme depuis l'adresse du VPS (Scaleway le dit à une lecture que la politique admet, sans
  `s3:ListBucket`, là où AWS répondrait 403 ; mesuré le 21 octobre 2026) ;
  la `403` ne vient que d'une politique qui refuse ou d'une adresse changée. nginx traite les
  deux, comme une 429, une 5xx ou un délai dépassé, par le chemin actuel vers le SSR, à
  l'identique.
- La clé vient d'une `map` stricte sur le chemin ; un chemin hors grammaire ou une query va au
  SSR. Aucun en-tête ni aucun corps du client ne part vers le stockage, et seuls `GET` et
  `HEAD` l'interrogent. La 429 du stockage reste distincte de celle de `limit_req`.
- Les en-têtes de la page sont reposés depuis les métadonnées ; `X-LoDb-Source:
  cache|store|ssr` dit d'où vient chaque page épinglée.
- La classe `archived` passe à un `s-maxage` d'un an : une page d'ancien patch ne change
  qu'avec une epoch. *Amendé le 2026-10-22 : vrai d'une page figée seulement ; nginx garde
  chaque page autant que dit la `Cache-Control` de son objet, et donne au client celle de sa
  classe.*

  *Limite constatée le 2026-10-22 : sous la règle des canoniques du 2026-10-14, la canonique
  d'une fiche d'ancien patch dépend du dernier patch. Une page stockée garde celle de son
  rendu jusqu'à l'epoch suivante, même quand l'entité a été retirée ou renommée depuis : à la
  promotion, seule la version qui quitte le dernier patch est recalculée
  (`PrerenderPromotionListener`). Traitée par l'amendement du 2026-10-22 : seule une page
  d'état clos, qui ne change plus, garde l'année ; une page de l'état ouvert est
  `revisable` (une heure) et la file des états clos la recalcule quand une promotion clôt
  son état.*
- Un bundle que le SSR ne connaît plus (404) est cherché sous `build/` dans le stockage.
- `front-builds` garde ses bundles **400 jours** (`LODB_FRONT_BUILDS_RETENTION_DAYS`, le job
  de déploiement refuse moins de 395), plus que l'an de la classe `archived`. La copie sous
  `build/` ne vaut que pour un build dont la passe a écrit une page : une page rendue par le
  SSR sur un raté du stockage, que `lodb_archive` garde jusqu'à un an, nomme les bundles d'un
  build jamais copié (toutes les pages rendues avant la mise en service du stockage, celles
  du staging, sans passe automatique, et celles des déploiements entre deux passes).
- `LODB_PAGES_STORE_ENABLED=0` (défaut de `compose.yaml` et de l'image nginx) laisse nginx tel
  qu'avant ; la pile de développement et les E2E de la CI l'allument sur le stockage simulé
  (`compose.override.yaml`).

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

*Fait : les lots 0 à 3 sont en prod depuis le 2026-10-21. Le CDN n'est pas commencé ; sa date
reste à fixer.*

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
| MinIO en production | Toujours écarté ([ADR 0004](0004-stockage-etat-et-scaling.md)) ; il sert aux tests seulement (`PageStoreContainerFixture`). La pile de développement simule le stockage avec moto (`pages-store`, `compose.override.yaml`) : les images officielles de MinIO ne sont plus publiées, les tests tirent celle d'un fork communautaire épinglée par empreinte |

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
  renvoie **toutes** les pages épinglées vers le SSR saturé pendant toute la passe : les 503
  reviennent. On l'atténue par l'ordre de la demande et un budget de passe relevé. *La durée
  estimée ici (10 à 16 jours) est dépassée : 27 pages par minute mesurées le 2026-10-21, puis
  900 par minute au plus en mode manuel, soit 3 à 7 jours pour 4,2 à 9 millions de pages
  (amendement du 2026-10-22 — vitesse réglable ; spécification, « Débit attendu »).*
- **−** La lecture du stockage dépend de l'adresse du VPS : s'il en change, chaque lecture
  répond 403 et nginx retombe sans bruit sur le SSR, pendant que la passe continue d'écrire
  (l'écriture ne dépend pas de l'adresse). `X-LoDb-Source` le rend visible ; la politique
  est à mettre à jour avec l'hôte.
- **−** Une lecture anonyme conditionnée par l'adresse à côté de l'IAM n'est pas documentée
  par Scaleway, ni le passage intact des métadonnées. La sonde `verifier` les prouve avant
  que nginx lise le stockage en prod ; à défaut, la lecture devient publique sur `pages/*`
  et `build/*`. Les tests utilisent MinIO, la pile de développement moto : ni l'un ni l'autre
  ne prouve rien de Scaleway.
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

## Amendement du 2026-10-21 — un SSR et un rythme pour la passe

Mesuré sur une pile aux quotas de la prod, le rythme fixe de la passe (120 rendus par minute,
24 jours pour le catalogue) ralentit les visiteurs dès son premier rendu en vol, puisqu'il
passe par leur SSR : +45 % sur la médiane d'une page non gardée, +250 % à huit rendus.

- La passe rend ses pages sur **un second conteneur de la même image**, `web-ssr-prerender`
  (alias `lodb-web-ssr-prerender`), que nginx ne connaît pas : un poids CPU d'un huitième
  (`cpu_shares: 128`), pour que le noyau ne lui cède que le CPU libre, et un plafond dur
  (`cpus: 1.0`). Le SSR des visiteurs ne reçoit plus aucun rendu de la passe.
- Son rythme n'est plus fixe : un **régulateur** de l'API le monte d'un pas par minute tant
  que l'hôte, son steal, le SSR des visiteurs et l'API sont calmes, le divise par deux au
  premier signe et le retient au rouge, entre un plancher (30 par minute) et un plafond
  (240, aussi le budget de la voie du SSR dédié). Il tient aussi la moyenne de 30 minutes que
  l'hébergeur regarde avant de brider tout le VPS. Une source illisible vaut un ralentissement.
- Les SSR exposent `GET /load` sur leur port des métriques : la passe lit leur état sans
  remettre à zéro les histogrammes que lit la collecte.
- `LODB_PRERENDER_PACE_MODE=fixed` rend le rythme fixe d'avant, sur le SSR dédié.

Débit attendu : 150 à 220 pages par minute en moyenne, le catalogue en 13 à 19 jours, sans
ralentissement mesurable des visiteurs jusqu'à ~80 % d'occupation de l'hôte. Détail, seuils
et réglages : [spécification](../../produit/pre-rendu-catalogue-cdn.md), lot 2, « Capacité
adaptative de la passe ».

## Amendement du 2026-10-21 — file de la demande

Le premier jour de prod, le SSR des visiteurs refuse ~160 rendus d'anciens patchs par minute,
pendant que la passe manuelle avance dans l'ordre des unités : les pages vraiment demandées
attendent leur tour.

- Chaque page d'ancien patch que le SSR des visiteurs refuse est comptée par lui, en mémoire
  et de façon cumulative, sous son chemin canonique ; l'API relève ces comptes sur le port
  des métriques (`GET /refused`) toutes les 30 s (`PrerenderDemandCollectWorker`) et en tient
  la file `prerender_demand` (migration additive).
- `PrerenderDemandWorker` précalcule ces pages dans l'epoch servie, la plus demandée d'abord,
  par le chemin même de la passe (SSR de la passe, verdicts, build copié d'abord). Pendant une
  passe, la file tient au plus la moitié des places en vol (`QueueSharePercent`) ; sans passe,
  toutes. Les passes manuelles ne changent pas.
- Interrupteur : `LODB_PRERENDER_DEMAND_ENABLED`. Bornes, rythme et pupitre :
  [spécification](../../produit/pre-rendu-catalogue-cdn.md), lot 2, « File automatique des
  pages refusées ».

## Amendement du 2026-10-22 — vitesse réglable

La première unité de prod a avancé à 27 pages par minute : la borne était l'espacement du
régulateur, pas le CPU (un rendu coûte ~0,1 s de CPU au moteur).

- **Mode manuel**, réglé au pupitre : une cible de 1 à `LODB_PRERENDER_MANUAL_MAX` pages par
  minute (900 sur un hôte), gardée en base. Le mode adaptatif reste le défaut,
  `LODB_PRERENDER_PACE_MODE=fixed` le coupe-circuit.
- **Frein d'urgence** (`ManualDecision`) : un visiteur du dernier patch refusé, ou 3 minutes
  avec l'hôte à 90 % ou plus, ou un signal rouge de ce qu'attendent les visiteurs, arrêtent
  tout nouveau rendu pendant 5 minutes. Les 503 des anciens patchs ne freinent pas.
- **Plusieurs moteurs** : `web-ssr-prerender` en `LODB_PRERENDER_RENDERERS` répliques (2 sur un
  hôte), un cœur chacune, au poids CPU le plus bas ; l'API répartit les rendus.
- Commande d'exploitation `prerender pause|resume`, pour un déploiement pendant une passe.

Débit attendu à 900 pages par minute : 4,2 millions de pages en un peu plus de 3 jours. Détail :
[spécification](../../produit/pre-rendu-catalogue-cdn.md), lot 2, « Vitesse réglable ».

## Amendement du 2026-10-22 — pages révisables

*En vigueur en prod depuis le 2026-10-22, parti au même déploiement que la règle par état
de l'ADR 0005 (amendement du 2026-10-22), sa bascule.*

L'[ADR 0005](0005-web-ssr-urls-et-seo.md) (amendement du 2026-10-22) indexe une page par état
d'entité. La page d'un ancien patch n'est donc plus immuable dans tous les cas. Celle d'un
état **clos** ne change plus (pour un schéma d'empreinte donné). Celle d'un état **ouvert**,
celui du dernier patch, a pour canonique l'URL courte jusqu'à ce que l'entité change : sa
canonique, son titre et son historique changent alors. Cela concerne 5 à 15 % des pages
épinglées, et au plus ~24 000 pages par promotion
([spécification](../../produit/seo-une-page-par-etat.md), § 1.2, des majorants).

- **Deux classes dans le stockage** : `archived` (un an) pour une page figée, `revisable`
  (`public, max-age=3600, s-maxage=3600, stale-while-revalidate=3600`) pour une page
  révisable. Le SSR choisit la classe selon l'état que l'API rend ; un état inconnu sur une
  version épinglée donne `revisable`, jamais `archived`. Les verdicts de la passe admettent
  les deux marques, et la ligne « Ce qui est stocké » vaut pour les deux classes.
- **nginx** : pour les pages, le cache chaud suit la `Cache-Control` de l'objet
  (`s-maxage`, `stale-while-revalidate`) ; l'année forcée ne vaut plus qu'en repli, pour un
  objet sans `Cache-Control`, et reste pour les bundles. L'en-tête envoyé au client vaut
  `revisable` pour un objet révisable, `archived` par défaut. nginx n'impose plus `archived`
  à toute réponse du stockage.
- **À chaque promotion**, après la mise à jour de l'index des états :
  - les pages des états que la promotion a clos (toutes leurs versions, dans les 21 langues)
    entrent dans une **file par page** (`prerender_revision`), pour l'epoch servie et pour
    celle qu'écrit la passe active si elle diffère. Un worker la vide par le chemin de la file
    de la demande, en partageant ses places de rendu, et ne rend une page qu'une fois que
    l'API qui sert voit sa clôture. Cette file ne dépend pas de
    `LoDb__Prerender__AutoEnqueueOnPromotion` : elle tourne dès que le stockage est
    configuré, staging compris (ADR 0003, amendement du 2026-10-22) ;
  - comme avant, l'unité de la version qui quitte le dernier patch, sous
    `AutoEnqueueOnPromotion` ; elle porte la seule page de patch qu'une promotion change.

  Les copies de nginx de ces pages expirent en deux heures environ (celles du CDN aussi, une
  fois construit).

  *Précisé à l'implémentation* : ni l'unité ni la file ne démarrent tant que l'index des
  états ne couvre pas le patch promu (`prerender.promotion.index_behind`) ; le rapprochement
  du précalcul les ajoute ensuite. La file prend aussi la version qui vient de quitter le
  dernier patch : sa page a pu être stockée révisable pendant que l'index tenait l'état ouvert.
  Une page recalculée qui revient `revisable` est retentée (trois essais), et chaque version
  est rendue sous la graphie de son catalogue (la casse d'un id peut changer dans un état).
- **La passe** : une passe peut se limiter aux ouvreurs de chaque version (périmètre
  `pages: "openers"`, plus la page de patch), lancée avant la passe entière ; une promotion
  ne la rejoint jamais. La page de patch n'est pas un type de page : toute unité qui couvre
  tous les types la rend, et l'ordre des pages d'une version (listes, page de patch, entités)
  est figé. La file de la demande garde sa priorité et admet la page de patch.
- **Epoch 2** : au déploiement qui porte la nouvelle règle, `LODB_ARCHIVE_SERVE_EPOCH=2`,
  seule valeur changée. Changer l'epoch servie purge déjà le cache chaud, et aucune variable
  ne règle d'epoch d'écriture : les passes sont lancées au pupitre sur l'epoch 2, ouvreurs
  d'abord. Une page absente part au SSR, puis à la file de la demande : ~11 h pour les
  ouvreurs, ~3 jours pour le reste, accepté (relire l'epoch 1 ferait coexister les deux
  règles), et la part de 503 servies à Googlebot est suivie pendant la passe. L'epoch 1 est
  gardée deux semaines pour un retour arrière, puis retirée par
  `tools/prerender/scaleway-bucket.sh` (`RETIRED_EPOCHS=1`, règle `retire-pages-1`).
- **Conséquence** : la classe `archived` ne garde plus que des pages vraiment immuables. Une
  page révisable coûte une relecture du stockage par heure au plus.

## Amendement du 2026-10-23 — vitesse manuelle jusqu'à 4 000

Le propriétaire veut de la marge au pupitre : fixer une cible plus haute que ce que les
moteurs tiennent aujourd'hui, sans redéployer pour la relever.

- **Maximum** : `LODB_PRERENDER_MANUAL_MAX` passe de 900 à **4 000** sur un hôte (staging et
  local inchangés). Il reste le budget de la voie de chaque moteur.
- **Avertissement** : au pupitre, dès que la cible dépasse **1 300** pages/min
  (`Pace:ManualWarnRendersPerMinute`, affiché, jamais appliqué). Le VPS a 4 vCPU, chaque moteur
  est un thread Node d'un cœur au plus (~450 pages/min) : les 2 moteurs d'aujourd'hui tiennent
  ~900 pages/min, et ~1 300 est ce qu'en tiendraient 3, le plus que le VPS peut donner à la
  passe sans priver les visiteurs.
- **Ce qui ne change pas** : le débit réel reste borné par les moteurs
  (`LODB_PRERENDER_RENDERERS`) et par les pages en vol (`LODB_PRERENDER_PAGES_IN_FLIGHT`, 24) ;
  le frein d'urgence (hôte à 90 % de CPU pendant 3 min) peut couper les nouveaux rendus 5 min.
  Aller vraiment plus vite demande plus de moteurs, et des pages en vol en proportion.

Détail : [configuration](../../guides/configuration.md), § 4.10.

## Amendement du 2026-10-23 — file de la demande au pupitre et dans l'epoch

*En prod depuis le 2026-10-23 (PR #107).*

Les chiffres du stockage (pages, volume, coût de chaque epoch) se lisaient sur les compteurs
des unités des passes (`prerender_unit.pages_written`) : les pages que la file de la demande
écrit dans l'epoch servie y manquaient, et le pupitre n'en montrait qu'un résumé.

- **Registre de la file** (migration additive `PrerenderDemandLedger`) : une page `done` de
  `prerender_demand` garde ses octets (`bytes`) et sa classe (`stored_class`, `archived` ou
  `revisable`) ; une page qui repasse `pending` (changement d'epoch servie) les perd.
- **Une page, un compte.** Quand une passe écrit de nouveau une page que la file avait écrite
  dans la même epoch, la ligne de la file reçoit `superseded_at` : la page compte dès lors
  avec l'unité de la passe, jamais deux fois. Une passe sur une autre epoch n'y touche pas.
- **L'epoch compte la file.** Les pages, octets et coûts d'une epoch ajoutent ses pages `done`
  de la file non réécrites (`queuePages`, `queueBytes` à part) ; une epoch que la file seule a
  remplie apparaît, et les totaux suivent. La répartition par version reste celle des passes.
- **Rétention** : l'élagage de 30 jours ne supprime plus une page `done` non réécrite de
  l'epoch servie. Servie depuis le stockage, elle n'est plus refusée à personne et sa date de
  dernier refus vieillit, mais elle reste ce que la file tient dans l'epoch.
- **Pupitre** : un onglet « File des 503 » (pages par état, 50 par page, 24 dernières heures
  entrées et écrites, ce que la file tient dans l'epoch servie), lu sur
  `GET /api/admin/prerender/demand/pages` et le rapport de la file enrichi. Lecture seule.

Détail : [spécification](../../produit/pre-rendu-catalogue-cdn.md), lot 2, « File automatique
des pages refusées ».
