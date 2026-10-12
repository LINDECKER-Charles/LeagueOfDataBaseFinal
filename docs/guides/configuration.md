# Configuration de la nouvelle stack : secrets et variables

Inventaire complet de ce qu'il faut configurer pour faire tourner la nouvelle stack (.NET +
Angular) en local, sur `staging` et en `prod`, et pour publier les apps. Le déroulé du
pipeline est dans [`github-actions-secrets.md`](github-actions-secrets.md), celui de la
bascule dans le [runbook](../reecriture/bascule.md). La configuration de l'ancienne stack
est archivée dans [`legacy/docs/guides/configuration.md`](../../legacy/docs/guides/configuration.md).

La nouvelle stack reprend les conventions de l'ancienne : mêmes environnements (`staging`,
`prod`), mêmes projets Compose (`lodb-staging`, `lodb-prod`), mêmes dossiers sur l'hôte,
mêmes secrets de dépôt. Seul le contenu des `.env` change, et un certificat s'ajoute par
environnement.

Légende : ✅ requis · ➖ optionnel · 🔒 secret · ⚙️ contrôlé par le job de déploiement avant
toute modification de l'hôte · 🔁 repris du `.env` de l'ancienne stack du même
environnement.

## 1. Où vit chaque réglage

| Où | Contenu | Écrit par |
|---|---|---|
| Secrets de dépôt `STAGING_*`, `PROD_*` | Connexion SSH et dossier de l'hôte, **ceux de l'ancienne stack, réutilisés tels quels** ; certificat Data Protection (nouveau) ; clés de l'hôte (`*_SSH_KNOWN_HOSTS`, nouveau) | déjà en place, sauf le certificat et les clés de l'hôte |
| Secrets de dépôt `ENV_STAGING`, `ENV_PROD` | Le `.env` complet de l'environnement : **contenu remplacé** par celui de la nouvelle stack | l'exploitant (*Settings ▸ Secrets*) |
| Environnements GitHub `staging`, `production` | Règles de protection des déploiements (§ 3.3) | GitHub au premier déploiement ; règles par l'exploitant |
| Environnements GitHub `desktop-release`, `android-release`, variables de dépôt | Clés de signature des apps, drapeaux de release | l'exploitant |
| `.env` de l'hôte (`$STAGING_PATH/.env`, `$PROD_PATH/.env`) | Variables Compose et réglages de l'API (§ 4), **sans les secrets** | le job, depuis `ENV_STAGING` ou `ENV_PROD` |
| `.deploy/secrets/` de l'hôte | Un fichier par secret 🔒 du `.env`, monté dans `/run/secrets` des services qui le lisent (§ 4) | le job, depuis `ENV_STAGING` ou `ENV_PROD` |
| `.deploy/data-protection.pfx` de l'hôte | Certificat Data Protection | le job, depuis `STAGING_DATA_PROTECTION_PFX` ou `PROD_DATA_PROTECTION_PFX` |
| `.deploy/android/` de l'hôte | `latest.json`, `assetlinks.json` | l'exploitant, à chaque release Android |
| Services externes | DNS, SMTP, Stripe, Google, Play, Apple, Azure (§ 6) | l'exploitant |
| Poste de dev | rien : tout a une valeur par défaut (§ 7) | — |

Modèles versionnés : [`.env.staging.example`](../../.env.staging.example) et
[`.env.prod.example`](../../.env.prod.example). Remplis, ils deviennent `.env.staging` et
`.env.prod` à la racine du dépôt (ignorés par Git), dont le contenu est collé tel quel dans
`ENV_STAGING` et `ENV_PROD`. Ces copies sont les seules lisibles : les tenir chiffrées hors du
dépôt et les envoyer à GitHub sans fichier en clair.

## 2. Mise en place, dans l'ordre

Les valeurs 🔁 ne se lisent que dans le `.env` de l'hôte (`$STAGING_PATH/.env`,
`$PROD_PATH/.env`), les secrets GitHub n'étant pas relisibles. Les relever **avant** la
bascule de l'environnement : son premier déploiement remplace ce fichier.

**`staging`** (au moment de fusionner la bascule dans `dev`, dont le push déploie) :

1. `.env.staging` rempli depuis `.env.staging.example`, valeurs 🔁 de l'ancien staging.
2. Certificat Data Protection de `staging` (§ 6.2).
3. Secret `ENV_STAGING` **remplacé** par le contenu de `.env.staging` ; secret
   `STAGING_DATA_PROTECTION_PFX` créé (§ 3.2).
4. Push de `dev` : checks, fusion dans `test`, build, déploiement de `staging`.

**`prod`** (juste avant de fusionner `test` dans `main`, [runbook](../reecriture/bascule.md)) :

1. Certificat Data Protection **propre à la prod** (§ 6.2), jamais celui de `staging`.
2. `.env.prod` rempli depuis `.env.prod.example`, valeurs 🔁 de l'ancienne prod (§ 4.4).
3. Secret `ENV_PROD` **remplacé** ; secret `PROD_DATA_PROTECTION_PFX` créé. Pas avant :
   tant que `main` porte l'ancienne stack, un push de `main` la redéploierait avec ce `.env`.
4. SMTP, Stripe, Google : § 6.4 à 6.6.
5. Fusion de `test` dans `main` : promotion de `:staging` en `:prod`, déploiement de `prod`.

Les sauvegardes hors hôte de la base de prod s'activent ensuite, à part
([`sauvegardes.md`](sauvegardes.md)) : sans elles, le déploiement reste vert.

## 3. GitHub

### 3.1 Environnements et packages

Le job de `_deploy.yml` tourne dans l'environnement GitHub de sa cible : `staging` pour
`deploy-staging`, `production` pour `deploy-prod` (un job qui appelle un workflow réutilisable
n'accepte pas `environment` : il est posé dans `_deploy.yml`). GitHub crée l'environnement à
son premier déploiement, **sans règle** : les protections sont à poser par l'exploitant
(§ 3.3). Les secrets restent des secrets de dépôt, passés par `ci.yml` (§ 3.2).

| Environnement | Création | Protection | Contenu |
|---|---|---|---|
| `staging` | par GitHub, au premier déploiement | à poser : branche de déploiement `dev` | aucun secret (ceux de dépôt, `STAGING_*`) |
| `production` | par GitHub, au premier déploiement | à poser : branche de déploiement `main`, approbation requise | aucun secret (ceux de dépôt, `PROD_*`) |
| `desktop-release` | à la main | branche des workflows lancés | secrets et variables du desktop (§ 3.4) |
| `android-release` | à la main | branche `main` ([`release-android.md`](release-android.md)) | secrets d'Android (§ 3.4) |

`GITHUB_TOKEN` est fourni par GitHub : la CI, la fusion dans `test`, le build des images,
le retag et le `pull` sur l'hôte n'utilisent aucun autre secret. Le job de déploiement
connecte l'hôte à GHCR avec le jeton du run le temps du `pull`, puis le déconnecte : les
packages `lodb/api` et `lodb/web-ssr`, nouveaux, peuvent rester privés. Une commande tirée
à la main sur l'hôte (runbook, § 5.1) n'a pas ce jeton : packages publics, comme les autres
`lodb/*`, ou `docker login ghcr.io` le temps de la commande (PAT `read:packages`).
`lodb/nginx` est partagé avec l'ancienne stack.

### 3.2 Secrets de déploiement

`ci.yml` les passe à `_deploy.yml` sous des noms neutres (`SSH_KEY`, `SSH_HOST`,
`DEPLOY_PATH`, `SSH_USER`, `SSH_KNOWN_HOSTS`, `ENV_FILE`, `DATA_PROTECTION_PFX`).

| `staging` | `prod` | Statut | Rôle |
|---|---|---|---|
| `STAGING_SSH_KEY` 🔒 | `PROD_SSH_KEY` 🔒 | existant | Clé privée SSH de déploiement. |
| `STAGING_HOST` | `PROD_HOST` | existant | IP ou FQDN de l'hôte. |
| `STAGING_PATH` | `PROD_PATH` | existant | Dossier du dépôt sur l'hôte, celui de l'ancienne stack : la nouvelle y prend sa place. |
| `STAGING_SSH_USER` | `PROD_SSH_USER` | existant | Utilisateur SSH (➖, `root` par défaut). |
| `STAGING_SSH_KNOWN_HOSTS` | `PROD_SSH_KNOWN_HOSTS` | **nouveau**, à créer | Lignes `known_hosts` des clés de l'hôte (§ 6.3) : `ssh` refuse alors toute autre clé. Absent : la clé est lue par `ssh-keyscan` à chaque run, sans vérification, et le run porte un avertissement *Host key not pinned*. |
| `ENV_STAGING` 🔒 | `ENV_PROD` 🔒 | **à remplacer** | Contenu complet de `.env.staging` / `.env.prod` (§ 4). |
| `STAGING_DATA_PROTECTION_PFX` 🔒 | `PROD_DATA_PROTECTION_PFX` 🔒 | **nouveau** | Le `.pfx` de l'environnement en base64, sur une ligne (§ 6.2). Celui de la prod ne change plus une fois servi : les clés déjà chiffrées deviendraient illisibles (sessions et jetons perdus). |

Un secret absent arrive vide : la première étape de `_deploy.yml` les vérifie et échoue en
nommant ceux qui manquent, avant toute connexion à l'hôte.

### 3.3 Garde de la prod

`main` n'est atteint que par une fusion manuelle de `test` : c'est la validation humaine
qui met en prod, sans rebuild ni nouveau passage des checks. `ci.yml` ne se déclenche plus
que sur les pushs des branches qui déploient (`dev`, `main`) et sur les pull requests.

**À poser par l'exploitant** (*Settings ▸ Environments*), une fois chaque environnement créé
par son premier déploiement :

- `production` : *Required reviewers* (le propriétaire) et *Deployment branches* limitées à
  `main`. Le déploiement de la prod attend alors une approbation. `_promote.yml` pose `:prod`
  sur les digests validés par `staging` **avant** cette approbation : refusée, `:prod` désigne
  une image que l'hôte ne sert pas encore, sans effet tant qu'on n'y tire rien à la main.
- `staging` : *Deployment branches* limitées à `dev`.

Les secrets `PROD_*` restent des secrets de dépôt, lisibles par tout workflow d'une branche
poussée. Pour les réserver aux runs approuvés : les recréer comme secrets de l'environnement
`production` sous les noms des entrées de `_deploy.yml` (`SSH_KEY`, `SSH_HOST`, `DEPLOY_PATH`,
`SSH_USER`, `SSH_KNOWN_HOSTS`, `ENV_FILE`, `DATA_PROTECTION_PFX`) : un secret d'environnement
prend le pas sur celui que l'appelant passe sous le même nom. Vérifier un déploiement, puis
supprimer les `PROD_*` de dépôt ; même chose pour `staging`.

### 3.4 Releases des apps

La façon d'obtenir chaque valeur est dans [`release-desktop.md`](release-desktop.md)
(§ Signature) et [`release-android.md`](release-android.md) (§ Secrets et variables).

| Où | Nom | Type |
|---|---|---|
| `desktop-release` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | secrets 🔒 |
| `desktop-release` | `APPLE_DEVELOPER_ID_P12`, `APPLE_DEVELOPER_ID_P12_PASSWORD`, `APPLE_NOTARY_KEY_P8`, `APPLE_NOTARY_KEY_ID`, `APPLE_NOTARY_ISSUER_ID` | secrets 🔒 |
| `desktop-release` | `AZURE_SIGNING_ENDPOINT`, `AZURE_SIGNING_ACCOUNT`, `AZURE_SIGNING_PROFILE`, `APPLE_APP_IDENTITY`, `APPLE_INSTALL_IDENTITY` | variables |
| `android-release` | `ANDROID_UPLOAD_KEYSTORE_BASE64`, `ANDROID_UPLOAD_STORE_PASSWORD`, `ANDROID_UPLOAD_KEY_PASSWORD` | secrets 🔒 |
| `android-release` | `ANDROID_APP_SIGNING_KEYSTORE_BASE64`, `ANDROID_APP_SIGNING_STORE_PASSWORD`, `ANDROID_APP_SIGNING_KEY_PASSWORD` | secrets 🔒 |
| `android-release` | `LODB_LIVE_UPDATE_PRIVATE_KEY`, `PLAY_SERVICE_ACCOUNT_JSON` | secrets 🔒 |
| dépôt | `ANDROID_UPLOAD_KEY_ALIAS`, `ANDROID_APP_SIGNING_KEY_ALIAS`, `LODB_LIVE_UPDATE_PUBLIC_KEY`, `ANDROID_TRANSITIONAL_CHANNEL`, `ANDROID_PLAY_ENABLED`, `ANDROID_APP_LINK_HOST` | variables |

Sans ces secrets, la release desktop échoue à la signature (`signing.sh`) et la release
Android à la signature des bundles ou de l'AAB. Les deux releases se lancent à la main
(`gh workflow run …`) : aucun déploiement ne les appelle encore (§ 8).

### 3.5 Autres secrets de dépôt

`ENV_TEST` (tests de l'ancienne stack) n'est lu par aucun workflow de la nouvelle ; la CI
n'a besoin d'aucun `.env`. `ACME_EMAIL` relève de `infra-vps`. Les garder jusqu'à la
décommission ([runbook](../reecriture/bascule.md)).

## 4. Le `.env` d'un environnement servi

Le secret `ENV_STAGING` ou `ENV_PROD` est le fichier complet, une variable par ligne. À chaque
déploiement, le job le pose sur l'hôte (`.deploy/incoming.env`, root seul), puis, le dépôt à
la révision déployée, [`tools/deploy/split-env.sh`](../../tools/deploy/README.md) le sépare et
le supprime. Il le sépare d'abord dans un dossier temporaire, que le job contrôle (§ 4.1, ⚙️) :
le `.env` et les fichiers en place ne changent qu'une fois ces contrôles passés, un conteneur
qui redémarre montant les fichiers tels qu'ils sont alors.

- chaque secret de la liste ci-dessous va dans un fichier de `.deploy/secrets/` portant
  son nom (fichier en 444, dossier en 700), vide si la ligne est absente ;
- le reste va dans `$STAGING_PATH/.env` ou `$PROD_PATH/.env` (mode 600), suivi de
  `LODB_SECRETS_REVISION`.

`compose.deploy.yaml` monte chaque fichier dans `/run/secrets` des seuls services qui le
lisent : aucun secret n'est dans l'environnement d'un conteneur, que `docker inspect` montre.
L'API lit un fichier nommé d'après la clé de son réglage (`LoDb__Billing__StripeSecretKey`) ;
un fichier y prime sur toute autre source, un fichier vide vaut un réglage absent. Les secrets
mis en fichiers :

- `LODB_DB_PASSWORD` ;
- `LoDb__DataProtection__CertificatePassword`, `LoDb__Mail__Password` ;
- `LoDb__Billing__StripeSecretKey`, `LoDb__Billing__StripeWebhookSecret` ;
- `LoDb__Accounts__Google__ClientSecret`, `LoDb__Accounts__Google__AppClients__N__ClientSecret`
  (`N` = `0` ou `1`) ;
- `LoDb__Analytics__VisitorKey`, `LoDb__Audit__IdentifierKey` ;
- `LoDb__Audience__Username`, `LoDb__Audience__Password` (prod seulement, § 4.6) ;
- `LoDb__Database__BackupRole__Password` (prod seulement, monté dans `migrate` seul, § 4.7).

Un secret ne vient **jamais** de `ENV_*` : `LODB_APP_DB_PASSWORD`, le mot de passe du rôle
Postgres de l'API (`lodb_app`, § 4.3). Le premier déploiement le tire sur l'hôte
(`openssl rand`, 64 caractères hexadécimaux) dans `.deploy/secrets/LODB_APP_DB_PASSWORD`, que
les suivants gardent ; il n'existe nulle part ailleurs, ni dans GitHub ni dans les sauvegardes
du `.env`, et n'est jamais affiché. `migrate` le pose sur le rôle à chaque passage, l'API se
connecte avec. Un `ENV_*` qui le définit est refusé avant toute écriture. Pour le changer,
supprimer le fichier et relancer le déploiement : un nouveau est tiré, `migrate` le pose,
puis `up` recrée l'API.

`LODB_ADMIN_PASSWORD` reste dans le `.env` : aucun conteneur ne le reçoit par l'environnement
(§ 4.1).

**Pièges du fichier :**

- Pas de ligne `LoDb__*` vide hors secrets : la commenter. Présente mais vide, elle est
  transmise à l'API comme une valeur vide, que certains réglages refusent au démarrage.
- Une valeur qui contient `$` se met entre apostrophes (`'…'`) : Compose l'interpolerait. Le
  job refuse un secret mis en fichier avec un `$` hors apostrophes, un `\` entre guillemets
  ou devant le guillemet fermant, un guillemet non fermé ou la forme `CLÉ: valeur`, avant
  d'écrire quoi que ce soit et sans afficher la valeur.

**Changer un secret** : modifier sa ligne dans `ENV_*` et relancer le déploiement. Le fichier
change, `LODB_SECRETS_REVISION` monte d'un cran et `up` recrée l'API, qui ne lit ses fichiers
qu'au démarrage (un conteneur garde le fichier monté à sa création). Mot de passe de la base :
`ALTER ROLE` d'abord, la nouvelle valeur ensuite ; `POSTGRES_PASSWORD_FILE` ne sert qu'à la
création d'une base, sur un volume vide.

### 4.1 Variables Compose

| Variable | `staging` | `prod` | | Rôle |
|---|---|---|---|---|
| `COMPOSE_PROJECT_NAME` ⚙️ | `lodb-staging` | `lodb-prod` | ✅ | Le projet de l'ancienne stack : la nouvelle y prend sa place (même volume `pgdata`). |
| `REGISTRY` | `ghcr.io/lindecker-charles/lodb` | idem | ✅ | Registre des images. |
| `IMAGE_TAG` ⚙️ | `staging` | `prod` | ✅ | Tag déployé : dernier build de `dev`, ou images promues. |
| `CADDY_DOMAINS` | `test.league-of-data-base.com` 🔁 | `league-of-data-base.com, league-of-data-base.fr` 🔁 | ✅ | Label `caddy_0` du site ; DNS pointé d'abord. |
| `API_CADDY_DOMAINS` | `api.test.league-of-data-base.com` 🔁 | `api.league-of-data-base.com, api.league-of-data-base.fr` 🔁 | ✅ | Label `caddy_1` ; chaque hôte commence par `api.`. |
| `LODB_CANONICAL_HOST` | `test.league-of-data-base.com` | `league-of-data-base.com` | ✅ | Hôte canonique : cible des 301 (`www.`, `.fr`), origine des liens d'e-mail, des sitemaps et des `share_url` de `/v1`. |
| `LODB_ALLOWED_HOSTS` | `test.league-of-data-base.com` | `league-of-data-base.com` | ✅ | Hôtes que le SSR accepte (virgules) ; les autres reçoivent un 400. |
| `LODB_PUBLIC_API_ORIGIN` | `https://api.test.league-of-data-base.com` | `https://api.league-of-data-base.com` | ✅ | Origine de `/v1` documentée par `/developers`. |
| `LODB_EDGE_CIDR` | — | — | — | Seul pair cru sur `X-Forwarded-For`. **Jamais dans `ENV_*`** : le job l'écrit depuis le réseau `edge` (§ 6.7). |
| `LODB_STACK_SUBNET` | — | — | — | Sous-réseau du réseau privé de la stack (`10.231.1.0/24` en staging, `10.231.2.0/24` en prod). **Jamais dans `ENV_*`** : le job l'écrit selon la cible (§ 6.8). |
| `LODB_EDGE_SECRET` | — | — | — | Valeur que l'edge envoie à nginx dans `X-LoDb-Edge` ; sans elle, nginx répond `403` (hors `/healthz`). **Jamais dans `ENV_*`** (une ligne qui la nomme est remplacée) : le job la génère une fois par hôte et l'écrit dans le `.env` (§ 6.7). Vide en local : rien n'est exigé. |
| `LODB_NOINDEX` ⚙️ | `1` | `0` | ✅ | `X-Robots-Tag: noindex, nofollow` ; le smoke test le vérifie. Les robots d'IA (`$lodb_ai_crawler`, `docker/nginx/nginx.conf`) y reçoivent un `403` ; les moteurs de recherche passent et lisent le noindex. |
| `LODB_DB_NAME`, `LODB_DB_USER` | 🔁 `POSTGRES_DB`, `POSTGRES_USER` | idem | ✅ | La base existe déjà dans `pgdata`, créée avec ces identifiants. `LODB_DB_NAME` : base de `api` et `migrate` ; `LODB_DB_USER` : le superutilisateur, rôle de `migrate` (et de l'ancienne stack) seulement, l'API se connectant comme `lodb_app` (§ 4.3). |
| `LODB_DB_PASSWORD` 🔒 ⚙️ | 🔁 `POSTGRES_PASSWORD` | idem | ✅ | Idem. Fichier (§ 4) : `migrate` le lit en `LoDb__Database__Password`, qu'il pose dans sa chaîne de connexion (un `;` y est donc permis), `postgres` en `POSTGRES_PASSWORD_FILE`. L'API ne le reçoit pas. |
| `LODB_APP_DB_PASSWORD` 🔒 | — | — | — | **Jamais dans `ENV_*`** : tiré sur l'hôte au premier déploiement (§ 4). Fichier : l'API le lit en `LoDb__Database__Password`, `migrate` en `LoDb__Database__AppRole__Password`. En local, `lodb-app`. |
| `LODB_SECRETS_REVISION` | — | — | — | **Jamais dans `ENV_*`** : le job l'écrit et le monte d'un cran quand un fichier secret change ; label de `api`, il fait recréer l'API (§ 4). |
| `LODB_DATA_PROTECTION_CERT_FILE`, `LODB_ANDROID_DIR` | défauts | défauts | — | Ne pas définir : le job écrit aux emplacements par défaut (§ 5). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | ➖ | ➖ | ➖ | Collecteur de traces OTLP, quand `infra-vps` en expose un. L'API en exporte alors une part (`LoDb:Telemetry:TraceSampleRatio`, 5 %), choisie par l'identifiant de trace ; `OTEL_TRACES_SAMPLER` est sans effet. Sans lui, aucune n'est enregistrée. |
| `LODB_RENDER_MAX_IN_FLIGHT`, `LODB_PINNED_RENDER_MAX_IN_FLIGHT`, `LODB_PINNED_RENDERS_PER_MINUTE`, `LODB_PINNED_RENDER_BURST` | ➖ | ➖ | ➖ | Admission des rendus du SSR : rendus simultanés (`8`), dont pages d'une version épinglée dans l'URL (`3`), rendus de versions épinglées par minute (`60`) et d'affilée (`10`). Au-delà, `503` immédiat avec `Retry-After` (nginx sert sa copie périmée s'il en a une). Absents : ces défauts. |
| `LODB_CURRENT_RENDERS_PER_MINUTE`, `LODB_CURRENT_RENDER_BURST` | ➖ | ➖ | ➖ | Rendus des pages du dernier patch par minute (`300`) et d'affilée (`60`), tous clients confondus. |
| `LODB_RENDER_PER_CLIENT_MAX_IN_FLIGHT`, `LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE`, `LODB_RENDER_TRACKED_CLIENTS` | ➖ | ➖ | ➖ | Part d'un client (une adresse IPv4, un /64 IPv6) : rendus simultanés (`2`), rendus de versions épinglées par minute (`6`) ; clients suivis en mémoire (`50000`). Une part ne dépasse jamais le tout : absente, elle s'y ramène ; posée au-dessus, le serveur refuse de démarrer. |
| `LODB_CURRENT_RENDERS_PER_CLIENT_PER_MINUTE`, `LODB_CURRENT_RENDER_PER_CLIENT_BURST` | ➖ | ➖ | ➖ | Part d'un client des pages du dernier patch : rendus par minute (`60`) et d'affilée (`20`), un cinquième et un tiers du couloir. Vérifiée avant le budget du couloir : un rendu refusé n'en dépense rien, le reste demeure aux autres visiteurs. Refus : `503` avec `Retry-After`, compté `current`/`client_rate` dans `ssr.admission.summary`. Sans effet pour un lecteur : un chargement complet rend une page, la navigation dans le site n'en rend aucune côté serveur, et une adresse partagée (NAT, CGNAT) reste loin d'un rendu par seconde. Un robot sur une seule adresse reçoit toujours les pages que nginx a en cache ; seules ses pages absentes du cache au-delà d'une par seconde reçoivent un `503`. Même règle de part que la ligne précédente. |
| `LODB_RENDER_DEADLINE_SECONDS`, `LODB_RENDER_API_TIMEOUT_SECONDS` | ➖ | ➖ | ➖ | Attente de l'API par un rendu (`20`) et par appel (`10`, borné par le temps restant) ; au-delà, page `502`. |
| `LODB_QUERY_MAX_PARAMS`, `LODB_QUERY_MAX_PARAM_LENGTH` | ➖ | ➖ | ➖ | Paramètres gardés par l'URL d'une liste ou d'une fiche (`16`, de `200` caractères au plus) ; les autres sont retirés par une `301`. |
| `LODB_HTML_GZIP_LEVEL` | ➖ | ➖ | ➖ | Niveau gzip (`6`, de `1` à `9`) auquel le SSR compresse une page, une fois par rendu ; nginx garde la copie compressée. |
| `LODB_API_META_TTL_SECONDS` | ➖ | ➖ | ➖ | Durée (`60`, le `max-age` de l'API) pendant laquelle le SSR réutilise sa copie de `/api/meta` pour tous les rendus ; quand l'API ne répond pas, il sert la dernière. |
| `LODB_PAGES_RATE`, `LODB_PAGES_BURST`, `LODB_API_READS_*`, `LODB_LEGACY_*`, `LODB_SITEMAPS_*`, `LODB_READYZ_*`, `LODB_RATE_LIMITED_RETRY_AFTER`, `LODB_MAX_BODY_SIZE`, `LODB_WEBHOOK_MAX_BODY_SIZE`, `LODB_PAGE_CACHE_SIZE`, `LODB_PAGE_CACHE_KEYS_SIZE`, `LODB_PAGE_CACHE_INACTIVE`, `LODB_ARCHIVE_CACHE_SIZE`, `LODB_ARCHIVE_CACHE_KEYS_SIZE`, `LODB_ARCHIVE_CACHE_INACTIVE`, `LODB_ASSETS_CACHE_SIZE`, `LODB_ASSETS_CACHE_KEYS_SIZE`, `LODB_ASSETS_CACHE_INACTIVE` | ➖ | ➖ | ➖ | Bornes de nginx, par adresse : débit (`_RATE`) et rafale (`_BURST`) des pages (`20r/s`, `100`), des lectures publiques de l'API (`10r/s`, `40`), de l'ancien site (`2r/s`, `10`), de `/sitemaps/` (`1r/s`, `20`) et de `/readyz` (`1r/s`, `5`), au-delà `429` avec `Retry-After` (`10`) ; corps de requête (`128k`, `1m` pour `/webhooks/`) ; cache des pages du dernier patch (`2g`, clés `32m`, inactivité `24h`), des anciens patchs (`6g`, clés `32m`, inactivité `7d`) et des bundles de `/build/` (`1g`, clés `8m`, inactivité `30d`), soit ≈ 9 Go de disque au plus. Absents ou vides : ces défauts, ceux de `docker/nginx/Dockerfile`, que `compose.yaml` reprend (`tools/nginx` vérifie l'accord). |
| `LODB_ARCHIVE_EPOCH` | ➖ | ➖ | ➖ | Première partie de la clé du cache des anciens patchs (`2`), leur seule purge : voir « Monter l'epoch des anciens patchs » ci-dessous. |
| `LODB_ARCHIVE_CACHE_MIN_USES` | ➖ | ➖ | ➖ | Demandes d'une page d'ancien patch avant que nginx la garde (`2`) : un rendu demandé une fois n'atteint pas le disque. En deçà, chaque URL distincte tient quand même une clé jusqu'à son inactivité, quelle que soit la réponse (`301`, `503` de délestage) : un balayage évince les pages les moins récemment demandées dès qu'il dépasse la zone des clés (`LODB_ARCHIVE_CACHE_KEYS_SIZE`, ≈ 8 000 clés par Mo). À `1`, seules les réponses gardées tiennent une clé. |
| `LODB_FRONT_BUILDS_RETENTION_DAYS` | ➖ | ➖ | ➖ | Lu par le job de déploiement : un bundle du volume `front-builds` qu'aucun build n'a livré depuis ce nombre de jours (`30`) est supprimé, `15` au moins (le job refuse en deçà). Jamais sous la durée de vie d'une page en cache (deux semaines pour un ancien patch : une semaine de `s-maxage`, puis une dernière copie périmée servie avant 7 jours d'inactivité) : un onglet ou une page gardée nommerait un bundle disparu. |
| `LODB_ADMIN_LOGIN` | 🔁 `ADMIN_LOGIN` | idem | ➖ | Administrateur racine, tenu par le job à chaque déploiement (`api admin root`) : compte de ce nom, rôle admin, TOTP enrôlé depuis le lien d'invitation que `api admin mfa-invite --email …` affiche sur l'hôte (15 min ; `api admin mfa-reset` retire un TOTP perdu). Absent : aucun, seulement `api admin create`. |
| `LODB_ADMIN_PASSWORD` 🔒 | 🔁 `ADMIN_PASSWORD` | idem | avec le précédent | Son mot de passe, 12 caractères au moins, sans apostrophe ni guillemet. Changé ici, il est réappliqué au déploiement suivant (sessions fermées). Reste dans le `.env` : le job le passe à la commande par son entrée. |
| `LODB_ADMIN_EMAIL` | ➖ | ➖ | ➖ | Son e-mail, qui identifie le compte ; défaut `<login>@<LODB_CANONICAL_HOST>`. |
| `LODB_PG_MAX_CONNECTIONS`, `LODB_PG_SLOW_STATEMENT`, `LODB_PG_IDLE_IN_TRANSACTION_TIMEOUT`, `LODB_PG_SHM_SIZE` | ➖ | ➖ | ➖ | Serveur Postgres (`compose.yaml`) : connexions (`60`, pour un pool de l'API à `32` plus `migrate`, la sauvegarde du déploiement, les verrous des tâches et `psql`), requêtes journalisées au-delà de `500ms` (sans leurs paramètres, jamais), session laissée inactive dans une transaction fermée après `60s`, `/dev/shm` de `256m`. `pg_stat_statements` est toujours chargé. |
| `COMPOSE_FILE` | — | — | — | **Jamais dans `ENV_*`** : le job l'écrit dans le `.env` de l'hôte, avec les calques de l'environnement (§ 4.5 à 4.7), pour qu'une commande `docker compose` lancée à la main depuis le dossier les applique aussi. |
| `LODB_STAGING_BASIC_AUTH_USER`, `LODB_STAGING_BASIC_AUTH_HASH` 🔒 | ➖ | **jamais** | ➖ | Identifiant demandé sur les pages du staging (§ 4.5). Les deux ou aucun ; en prod, le job refuse de déployer. |

**Monter l'epoch des anciens patchs.** Les pages d'une version épinglée dans l'URL sont
gardées par nginx à travers les déploiements, avec le chrome, la CSP et les bundles du build
qui les a rendues. Monter `LODB_ARCHIVE_EPOCH` (`2` → `3`…) dans `ENV_STAGING` / `ENV_PROD`
quand le chrome (en-tête, pied, puce de release), la CSP ou le contrat `/api` changent de
façon visible, et **après tout correctif de sécurité du front** ; le déploiement suivant
recrée nginx sur des clés neuves, les anciennes s'éteignent en 7 jours. Pour un correctif
de sécurité, retirer aussi le chunk vulnérable, du volume des bundles et du cache de nginx,
une fois le build corrigé en service (le build vulnérable le republierait dans l'heure) :

```bash
docker compose exec -T web-ssr rm -f /srv/front-builds/<chunk>.js
docker compose exec -T nginx sh -c \
  "grep -rlax 'KEY: /build/<chunk>.js' /var/cache/nginx/assets | xargs -r rm -f"
```

Le volume `front-builds` est le seul endroit où le serveur SSR écrit, et ce qu'il y écrit
survit aux déploiements. Après un correctif du **serveur SSR lui-même** (exécution de code
dans le conteneur `web-ssr`), monter l'epoch comme ci-dessus, puis, une fois le build corrigé
en service, vider ce volume et le cache des bundles de nginx en entier : le build corrigé y
republie les siens dans l'heure, et `/build/` sert ceux de son image entre-temps.

```bash
docker compose exec -T web-ssr find /srv/front-builds -mindepth 1 -delete
docker compose exec -T nginx find /var/cache/nginx/assets -mindepth 1 -delete
```

Chaque environnement a son propre service `postgres` dans son projet, comme avant.
`CONTACT_RECIPIENT` et `PUBLIC_API_BASE_URL` n'ont d'effet qu'en local : sur un
environnement servi, `LoDb__Contact__Recipient` et `LODB_PUBLIC_API_ORIGIN` les remplacent.

### 4.2 Réglages de l'API transmis par le `.env`

Nom exact de la configuration, casse comprise. Absents du `.env`, ils ne sont pas transmis
et l'API garde sa valeur par défaut. Les lignes 🔒 deviennent des fichiers (§ 4), lus sous le
même nom.

| Ligne | Requis | Effet, et effet de l'absence |
|---|---|---|
| `LoDb__DataProtection__CertificatePassword` 🔒 | si le `.pfx` en a un | Mot de passe du certificat. Faux : l'API refuse de démarrer. |
| `LoDb__Mail__Host` ⚙️ | prod (et `staging`, comme l'ancien staging) | Relais SMTP. Absent : les e-mails attendent dans `email_outbox`, rien n'est envoyé. |
| `LoDb__Mail__Port` | ➖ | `587` par défaut. |
| `LoDb__Mail__Security` | ➖ | `Auto` par défaut ; `None`, `SslOnConnect`, `StartTls`, `StartTlsWhenAvailable`. |
| `LoDb__Mail__Username`, `LoDb__Mail__Password` 🔒 | si le relais authentifie | Identifiants SMTP. |
| `LoDb__Mail__From` | prod | `Nom <adresse>`, sur un domaine couvert par SPF, DKIM et DMARC. Défaut : `no-reply@leagueofdatabase.gg`, **un autre domaine** : toujours le définir. |
| `LoDb__Billing__StripeSecretKey` 🔒 ⚙️ | prod | `sk_live_…` en prod, `sk_test_…` sur `staging`. Absente : dons et achats de crédits en 503. |
| `LoDb__Billing__StripeWebhookSecret` 🔒 ⚙️ | prod | `whsec_…` de l'endpoint `/webhooks/stripe` de l'environnement. Absent : webhook en 503, alerte `billing.webhook.unconfigured`. |
| `LoDb__Accounts__Google__ClientId` | ➖ | Client OAuth web. Absent : connexion Google coupée (`google-unavailable`). |
| `LoDb__Accounts__Google__ClientSecret` 🔒 | avec le précédent | Secret du client web. |
| `LoDb__Accounts__Google__AppClients__0__ClientId` (et `__1__`) | ➖ | Clients des apps acceptés par l'échange de code ; jamais sans le client web. Deux au plus sans modifier `compose.deploy.yaml`. |
| `LoDb__Accounts__Google__AppClients__N__ClientSecret` 🔒 | desktop | Secret du client desktop (livré avec l'app, donc public) ; aucun pour Android. |
| `LoDb__Contact__Recipient` | ➖ | Destinataire des messages de contact. Absent : stockés pour l'admin, sans notification. |
| `LoDb__Contact__NotificationLocale` | ➖ | Langue de la notification, `Fr` par défaut (`Ar`… `ZhHant`). Une valeur invalide ne se voit qu'au premier message. |
| `LoDb__Analytics__VisitorKey` 🔒 | recommandé | Clé du hachage des visiteurs. Absente : une clé aléatoire par processus, les visiteurs sont recomptés à chaque redémarrage. |
| `LoDb__Audit__IdentifierKey` 🔒 | recommandé | Clé du condensat (HMAC) que le journal garde à la place de l'identifiant saisi lors d'une connexion refusée : il relie les tentatives sur un même identifiant sans en garder le texte. Absente : une clé aléatoire par processus, les condensats ne se recoupent plus d'un redémarrage à l'autre. |
| `LoDb__Audience__Username`, `LoDb__Audience__Password` 🔒 | prod, une fois le compte créé | Compte Umami en lecture seule du tirage des tendances (§ 4.6). Absents : le tirage reste en sommeil (une ligne `audience.trends.dormant` au démarrage), `/v1/trends` lit la mesure maison. |
| `LoDb__Audience__TrendsSwitchDate` | ➖ (prod) | Premier jour où `/v1/trends` lit les chiffres d'Umami (§ 4.6). Absente : la mesure maison seule. |
| `LoDb__Seo__SitemapVersionWindow` | ➖ | Versions passées annoncées par l'index des sitemaps, après le dernier patch : `12` par défaut, `0` pour le dernier patch seul. Les sitemaps plus anciens restent servis, et leurs pages indexables. |
| `LoDb__Catalog__MaxEntries`, `PinnedLanguages`, `PinnedBudgetMegabytes` | ➖ | Catalogues en mémoire : ceux du dernier patch dans les langues épinglées (défaut : les 21 de l'interface, en_US en tête), jamais évincés, dans `256` Mio estimés ; `16` autres au plus, le moins récemment lu évincé. |
| `LoDb__Catalog__ColdLoadConcurrency`, `ColdLoadsPerSecond`, `ColdLoadBurst`, `PublicColdLoadsPerSecond`, `InternalColdLoadWait`, `MaxPendingLoads` | ➖ | Chargement d'un catalogue absent de la mémoire : `2` à la fois, `3` par seconde (rafale `6`), dont `1` pour les appels publics, refusés sans attente ; le SSR attend `2` s au plus, derrière `16` autres. Refus : `503` + `Retry-After`. |
| `LoDb__Catalog__WarmUp__MaxStreams`, `MaxStreamsPerClient`, `StreamLifetime`, `PerClientPerHour` | ➖ | Loader de changement de patch : `8` flux ouverts, `2` par client, `120` s chacun, `20` démarrages par heure et par client (`429` au-delà). |
| `LoDb__Ingestion__ColdDatasetsPerMinute`, `ColdImagesPerMinute`, `MaxWaitingIngestions`, `SlotWait` | ➖ | Ingestion à froid pour un visiteur : `6` couples (version, langue) et `1500` images par minute, `32` ingestions en attente d'un emplacement, `3` s d'attente au plus. Au-delà : catalogue « en attente », images en placeholders. |
| `LoDb__Database__AppRole__StatementTimeout`, `ConnectionLimit` | ➖ | Transmis à `migrate` seulement, qui les pose sur le rôle de l'API (§ 4.3) : `00:00:15` (une requête plus longue est annulée par le serveur) et `50` sessions (`-1` : sans limite ; sous les 60 du serveur, moins les 3 réservées aux superutilisateurs). Pris en compte au déploiement suivant, par les nouvelles connexions. |
| `LoDb__Database__BackupRole__Password` 🔒 ⚙️ | prod, à l'activation des sauvegardes | Mot de passe de `lodb_sauvegarde`, le rôle des sauvegardes hors hôte (§ 4.7) : la même valeur que `SAUVEGARDE_LODB_MOT_DE_PASSE` d'`infra-vps` (`openssl rand -hex 32`). Fichier monté dans `migrate` seul, qui le pose sur le rôle. Posé, le job exige 16 caractères au moins parmi lettres, chiffres, `-` et `_` (le format d'`infra-vps`). Absent : un rôle neuf n'a pas de mot de passe, personne ne s'y connecte ; un rôle existant garde le sien. |

### 4.3 Fixés par les fichiers compose (hors du `.env`)

`ConnectionStrings__LoDb` (hôte `lodb-postgres`, base `LODB_DB_NAME`, **sans mot de passe** :
le service y pose `LoDb__Database__Password`, lu dans `/run/secrets` ; pour l'API, rôle
`lodb_app`, pool borné par `Maximum Pool Size=32;Timeout=5` ; pour `migrate`, `LODB_DB_USER`),
`LoDb__Accounts__SiteOrigin`, `LoDb__Seo__CanonicalOrigin` et `LoDb__PublicApi__SiteOrigin`
(`https://` + `LODB_CANONICAL_HOST`), `LoDb__PublicApi__Reference__BaseUrl`
(`LODB_PUBLIC_API_ORIGIN`), `LoDb__DataProtection__CertificatePath`,
`LoDb__Hosting__KnownProxyNetworks__0` (`LODB_STACK_SUBNET` seul), `AllowedHosts` (les hôtes de
`CADDY_DOMAINS` et `API_CADDY_DOMAINS`, puis `lodb-api`, `localhost` et `127.0.0.1` : tout autre
`Host` reçoit un `400`, le port des métriques excepté), `LoDb__Storage__Root` (`/srv/storage`, volume
`ddragon`, que nginx sert), et côté SSR `LODB_API_ORIGIN`, `LODB_TRUST_PROXY_HEADERS`,
`LODB_STACK_NETWORKS` (`LODB_STACK_SUBNET` seul), `LODB_FRONT_BUILDS_DIR` (`/srv/front-builds`,
volume `front-builds`), `NODE_OPTIONS`. `ASPNETCORE_ENVIRONMENT` reste **absente** sur un hôte : `Development`
exposerait OpenAPI et retirerait `Secure` des cookies.

**Rôles Postgres.** `migrate` se connecte comme le superutilisateur de l'image (`LODB_DB_USER`,
celui de l'ancienne stack, laissé intact jusqu'au *contract*) et, après les migrations, tient
en place quatre rôles (`Persistence/Roles/`), de façon idempotente : il ne réécrit que ce qui
diffère.

| Rôle | Connexion | Droits |
|---|---|---|
| `lodb_owner` | jamais | Propriétaire des tables, vues et séquences du schéma `public`. |
| `lodb_partitions` | jamais | Propriétaire d'`analytics_event`, de ses partitions quotidiennes et des deux fonctions `SECURITY DEFINER` qui les créent et les suppriment (`lodb_create_analytics_partition(date)`, `lodb_drop_analytics_partition(date)`). |
| `lodb_app` (l'API) | oui, `CONNECTION LIMIT 50` | Lecture et écriture de lignes (`SELECT`, `INSERT`, `UPDATE`, `DELETE`), lecture seule des historiques de migration, séquences ; `EXECUTE` sur les deux fonctions, son seul DDL. Ni `TRUNCATE`, ni création (tables, index, schémas, tables temporaires, fonctions), ni appartenance à un autre rôle, ni rôle superutilisateur ; `statement_timeout` de 15 s. |
| `lodb_sauvegarde` (sauvegardes hors hôte) | oui, `CONNECTION LIMIT 2`, par son mot de passe, posé en prod seulement (§ 4.7) | Membre de `pg_read_all_data` : lit toute la base, de quoi faire un `pg_dump`. Sa seule écriture : `INSERT` dans `backup_run`, le journal de ses passes. |

Chaque passage rend les objets du schéma à leur propriétaire et refait les droits de
`lodb_app` (un droit accordé à la main, sur une table, le schéma ou la base, comme une
appartenance à un autre rôle ou un réglage du rôle, même propre à une base, ne survit pas au
déploiement suivant) ; les privilèges
par défaut couvrent une table qu'une migration crée, dès sa création. Les tâches qui lisent
une journée entière de vues (agrégation, effacement des données client) relèvent leur propre
délai le temps de leur transaction (`LoDb:Analytics:JobStatementTimeout`, 5 min). Une commande
lancée dans le conteneur `api` (`admin`, `ingest`, `analytics import`…) a les droits de l'API ;
ce qui change le schéma passe par `migrate` : `docker compose run --rm migrate` (ou
`docker compose run --rm migrate baseline mark-applied`).

Ressources de chaque service, dans `compose.deploy.yaml` (la prod, et le staging sous son
calque, § 4.5) : quota CPU (`cpus`), poids sous contention (`cpu_shares`) et plafond de
processus (`pids`).

| Service | `cpus` | `cpu_shares` | `pids` | Mémoire |
|---|---|---|---|---|
| `web-ssr` | `1.0` | `1024` | `256` | `704m` |
| `api` | `1.25` | `1024` | `512` | `1152m` |
| `postgres` | `0.5` | `1024` | `256` | `512m` |
| `nginx` | `0.25` | `2048` | `256` | `256m` |
| `migrate` | — | — | `256` | `128m` |

Les quotas totalisent 3 des 4 vCPU de l'hôte : sous une charge qui les dépasse, la stack
déleste (`503` avec `Retry-After`) au lieu de prendre l'hôte des autres projets. Le bridage se
lit dans `container_cpu_cfs_throttled_seconds_total` (alerte `ConteneurCpuBride`,
`infra-vps`) : recalibrer après 7 jours, en regard de la latence. La sonde de santé de l'API
démarre un runtime .NET dans ce quota : toutes les 60 s en marche, toutes les 2 s au
démarrage (`start_interval`, Docker Engine 25 au moins, vérifié par le job).

Exécution, dans `compose.yaml` (en local comme sur un hôte) : chaque service tourne sans
aucune capacité Linux (`cap_drop: [ALL]`), sans élévation possible (`no-new-privileges`) et
sur un système de fichiers racine en lecture seule ; il n'écrit que dans ses volumes et dans
un tmpfs borné, compté dans sa limite mémoire.

| Service | Utilisateur | Écrit dans | Capacités reprises |
|---|---|---|---|
| `api`, `migrate` | `app` (image chiseled) | `ddragon` (`api`), tmpfs `/tmp` (64 Mio) | — |
| `web-ssr` | `node` | `front-builds`, tmpfs `/tmp` (16 Mio) | — |
| `nginx` | `nginx`, port 8080 | caches et fichiers temporaires (volumes), tmpfs `/tmp` (64 Mio : configuration rendue, pid) | — |
| `postgres` | root, puis `postgres` | `pgdata`, tmpfs `/var/run/postgresql` (8 Mio) et `/tmp` (16 Mio) | `CHOWN`, `DAC_OVERRIDE`, `FOWNER`, `SETGID`, `SETUID` (entrée de l'image), toutes parmi celles que Docker accorde par défaut |

Réseaux : `default` (nginx, SSR, API, `migrate`) porte le HTTP, sur `LODB_STACK_SUBNET`
(§ 6.8) ; `data` (Postgres, API, `migrate`) est interne sur un hôte, sans route vers
l'extérieur : nginx et le SSR ne joignent pas la base. nginx rejoint aussi `edge`, l'API et
le SSR `observability` ; en prod, Postgres rejoint `sauvegardes` (§ 4.7).

<details>
<summary>Réglages internes de l'API, avec leur défaut (non transmis par compose)</summary>

Pour en changer un sur un hôte, l'ajouter sous `services.api.environment` de
`compose.deploy.yaml`, puis dans le `.env`.

| Section | Clés (défaut) |
|---|---|
| `LoDb:Hosting` | `HttpPort` (8080, attendu par nginx et le SSR), `MetricsPort` (9464, jamais publié), `MaxRequestBodySize` (131072 octets, soit 128 Kio ; au-delà, 413), `DatabaseBusyRetryAfter` (5 s, `Retry-After` du 503 quand le pool Postgres reste occupé), `SecretsDirectory` (`/run/secrets` : un fichier par réglage, nommé d'après sa clé avec `__`, lu au démarrage ; vide, il vaut un réglage absent ; il prime sur toute autre source) |
| `LoDb:Database` | `Password` (aucun : sans lui, la chaîne de connexion reste telle quelle ; posé, il y remplace le mot de passe) ; pour `migrate` seulement, `AppRole:Password` (aucun : le mot de passe de `lodb_app` reste tel quel ; ASCII imprimable), `AppRole:StatementTimeout` (15 s), `AppRole:ConnectionLimit` (50, `-1` sans limite), `BackupRole:Password` (aucun : le mot de passe de `lodb_sauvegarde` reste tel quel ; ASCII imprimable) |
| `LoDb:Workers` | `Enabled` (`true` ; seul `false` coupe toutes les tâches de fond) |
| `LoDb:Accounts` | `SecureCookies` (vrai hors Development) ; `VerificationResend:FreeResends` (5 renvois de l'e-mail de vérification par compte), puis un par `VerificationResend:Interval` (1 j) ; `UnverifiedPurge:After` (7 j : un compte encore non vérifié est effacé, sauf build, clé API, don, rôle, bannissement, soutien ou Google), `UnverifiedPurge:BatchSize` (500 comptes par passage horaire) ; `RefreshTokens:ReuseGrace` (30 s, de 0 à 5 min : le jeton de rafraîchissement qu'un échange vient de remplacer s'échange encore pour la même génération, contre une réponse perdue ou deux échanges simultanés ; au-delà, il révoque sa famille) |
| `LoDb:RateLimiting` | par compte et par heure glissante, jamais par adresse : `CheckoutsPerHour` (10 checkouts de pack ou de plan), `ApiKeyIssuesPerHour` (5 clés créées ou régénérées), `BuildEditsPerHour` (60 builds créés ou remplacés), `VotesPerHour` (120 votes) ; au-delà, `429 rate-limited` |
| `LoDb:Builds:Votes` | `MinimumAccountAge` (1 j : âge du compte avant son premier vote ; l'e-mail vérifié est aussi exigé) |
| `LoDb:Mail` | `Timeout` (`00:00:30`) |
| `LoDb:Outbox` | `BatchSize` (20), `PollInterval` (5 s), `MaxAttempts` (10), `RetryDelay` (30 s), `MaxRetryDelay` (1 h), `Lease` (5 min) ; `HourlyCaps:ConfirmEmail` (100), `HourlyCaps:ResetPassword` (50), `HourlyCaps:ContactNotification` (20) : e-mails de chaque modèle envoyés par heure glissante pour tout le site, l'excédent attend dans la table (jauge `lodb_outbox_deferred`) ; à garder sous le quota du relais |
| `LoDb:Analytics` | `GeoIpDatabase` (aucune), `QueueCapacity` (10000), `BatchSize` (500), `BatchWindow` (2 s), `ClientDataRetention` (30 j : adresse et user-agent des vues stockées avant qu'ils ne le soient plus), `ClientDataEraseDays` (3, au moins 2 : jours lus avant celui du seuil ; deux passages de la tâche quotidienne peuvent être séparés de deux jours après un redémarrage, de trois après un échec ; à relever le temps d'un passage après une panne plus longue), `EventRetentionMonths` (13, plafond CNIL 25), `PartitionsAhead` (7), `MaxVisitorsPerDay` (200000 empreintes par jour, au-delà le jour est marqué `visitorsTruncated` dans `totals`), `MaxRefererHostsPerDay` (200 hôtes référents par jour, les moins comptés au-delà sous `(autres)`), `JobStatementTimeout` (5 min, de 1 s à 1 h : délai d'une requête de l'agrégation quotidienne et de l'effacement des données client, au-dessus des 15 s du rôle de l'API) |
| `LoDb:Egress` | `AllowedHosts` (`ddragon.leagueoflegends.com`, `raw.communitydragon.org`), `FetchConcurrency` (16), `AttemptTimeout` (15 s), `MaxRetryAttempts` (3), `RetryBaseDelay` (1 s), `MaxRedirects` (10), `MaxResponseBytes` (32 Mio) |
| `LoDb:Ingestion` | `WatchPeriod` (10 min), `MaxAttempts` (5), `RetryDelay` (10 min), `LanguageConcurrency` (4), `RecordBatchSize` (64), `SyncConcurrency` (4), `QueueCapacity` (256), `CrawlerRequestsPerMinute` (30) |
| `LoDb:Catalog` | `VersionsLifetime` (1 min), `ImageVerdictsLifetime` (10 min : verdicts d'images lus en base gardés en mémoire, délai avant qu'un `ingest --force` lancé à part se voie), `ImageVerdictVersions` (32 versions gardées), `ArtVariantsPeriod` (30 min : tâche `catalog.art-variants`, qui stocke les variantes WebP de l'art du dernier patch), `ArtVariantsConcurrency` (1 art téléchargé et transcodé à la fois, sous le quota CPU de l'API) |
| `LoDb:SecurityReports` | `MaxBodyBytes` (65536 : corps d'un envoi de rapports CSP, 413 au-delà, compté), `PerClientPerMinute` (30 envois par adresse et par minute, 429 au-delà), `PerMinute` (600 envois par minute, tous clients confondus, refusés avant d'être lus au-delà) |
| `LoDb:Telemetry` | `TraceSampleRatio` (`0.05`, de `0` à `1` : part des traces enregistrées et exportées quand `OTEL_EXPORTER_OTLP_ENDPOINT` est posé, choisie par l'identifiant de trace, jamais par le drapeau `sampled` d'un `traceparent` reçu) |
| `LoDb:PublicApi` | `KeyCacheLifetime` (1 min), `TrendsLifetime` (5 min), `MeteringInterval` (1 s) |
| `LoDb:Audience` | `Host`, `WebsiteId` (posés par `compose.umami.yaml`), `Username`, `Password` (secrets) : sans l'un d'eux, le tirage dort ; `PullPeriod` (1 h), `RefreshDays` (2 : aujourd'hui et hier, réécrits à chaque tirage), `PageSize` (500 chemins par requête), `MaxPages` (40 requêtes par jour au plus), `RequestTimeout` (30 s), `MaxResponseBytes` (4 Mio) ; `TrendsSwitchDate` (aucune : `/v1/trends` lit la mesure maison ; posée, Umami à partir de ce jour), `ClipFactor` (3) et `ClipWindowDays` (28) : le jour d'une entité compte au plus 3 fois la médiane de ses jours des 28 derniers (Umami collecte sans authentification) |

</details>

### 4.4 Correspondance avec l'ancienne stack (`.env` de l'hôte)

Valable pour les deux environnements : chacun reprend les valeurs de son propre `.env`.

| Ancienne variable | Nouvelle ligne |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `LODB_DB_NAME`, `LODB_DB_USER`, `LODB_DB_PASSWORD` |
| `APP_SECRET` | `LoDb__Analytics__VisitorKey` (mêmes visiteurs des deux côtés de la bascule) |
| `MAILER_DSN` `smtp://USER:MDP@HÔTE:587` | `LoDb__Mail__Host`, `Port` `587`, `Security` `StartTls`, `Username`, `Password` (décodés d'URL) ; `smtps://…:465` : `Port` `465`, `Security` `SslOnConnect` |
| `MAILER_FROM` | `LoDb__Mail__From` |
| `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET` | `LoDb__Billing__StripeSecretKey`, `LoDb__Billing__StripeWebhookSecret` |
| `OAUTH_GOOGLE_CLIENT_ID`, `OAUTH_GOOGLE_CLIENT_SECRET` | `LoDb__Accounts__Google__ClientId`, `LoDb__Accounts__Google__ClientSecret` |
| `CONTACT_RECIPIENT` | `LoDb__Contact__Recipient` |
| `COMPOSE_PROJECT_NAME`, `REGISTRY`, `IMAGE_TAG`, `CADDY_DOMAINS`, `API_CADDY_DOMAINS` | mêmes noms, mêmes valeurs |
| `ADMIN_LOGIN`, `ADMIN_PASSWORD` | `LODB_ADMIN_LOGIN`, `LODB_ADMIN_PASSWORD` : un compte administrateur racine, TOTP enrôlé sur invitation (`api admin mfa-invite`) ; les autres administrateurs reçoivent le rôle par `api admin create --email …` ([runbook](../reecriture/bascule.md)) |
| `GEOIP_DB_PATH` | aucune sur un hôte (§ 8) |
| `HTTP_PORT`, `MAILPIT_UI_PORT` | aucune : ports locaux de l'ancienne stack |

### 4.5 Calques du staging

Le staging tourne sur l'hôte de la prod et sert les mêmes chemins : un crawl de `test.*`
coûte autant qu'un crawl du site. Le job de déploiement lui ajoute donc
[`compose.staging.yaml`](../../compose.staging.yaml), jamais en prod : environ 0,75 cœur en
tout, le poids le plus bas (`cpu_shares: 128`) et des files à sa taille. Les réglages de
l'API et du SSR se changent par une ligne du `.env` du staging, sous le même nom ; les
quotas se revoient avec ceux de la prod :

| Réglage | Staging | Prod (défaut) |
|---|---|---|
| `cpus` : `api`, `web-ssr`, `postgres`, `nginx` | `0.25`, `0.25`, `0.15`, `0.1` | § 4.3 |
| `LODB_RENDER_MAX_IN_FLIGHT`, `LODB_PINNED_RENDER_MAX_IN_FLIGHT` | `3`, `1` | `8`, `3` |
| `LODB_PINNED_RENDERS_PER_MINUTE`, `LODB_PINNED_RENDER_BURST` | `10`, `5` | `60`, `10` |
| `LoDb__Ingestion__SyncConcurrency`, `LoDb__Ingestion__LanguageConcurrency`, `LoDb__Egress__FetchConcurrency` | `1`, `1`, `1` | `4`, `4`, `16` |
| Sonde de santé de l'API au démarrage | toutes les 10 s pendant 60 s | toutes les 2 s pendant 20 s |

Le couloir épinglé reste strictement sous le total des rendus, sinon le SSR refuse de
démarrer ; la part d'un client (`2` rendus, `6` épinglés par minute) tient dans ces totaux.
Un nouveau patch met plus longtemps à arriver sur le staging.

**Identifiant sur les pages (facultatif).** Avec `LODB_STAGING_BASIC_AUTH_USER` et
`LODB_STAGING_BASIC_AUTH_HASH` dans le `.env` du staging, le job ajoute
[`compose.staging-auth.yaml`](../../compose.staging-auth.yaml) : l'edge demande cet
identifiant (`basic_auth` de Caddy) sur les pages, jamais sur `/api/*`, `/v1/*`,
`/webhooks/*`, `/healthz`, `/build/*`, `/cdn/blobs/*` ni `/u/*` (apps, canal desktop beta,
webhooks Stripe de test, retours OAuth, sondes). Il écarte les robots et l'indexation ; ce
n'est pas une frontière d'authentification.

- Le hash : bcrypt, par `caddy hash-password` (présent dans le conteneur de l'edge), mis
  **entre apostrophes** dans le `.env` (il contient des `$`) ; le job refuse un hash mal formé
  ou non quoté avant toute modification, car il atteindrait l'edge, partagé par tous les
  projets. Le nom : lettres, chiffres et `-` seulement (un `.` ou un `_` y prendrait un
  sens pour l'edge, qui lit le nom dans la clé d'un label).
- Pour le retirer : supprimer les deux lignes du secret `ENV_STAGING`, puis redéployer.
- **Compromis à peser avant de l'activer** : l'edge vérifie l'identifiant par bcrypt, sur le
  CPU que partagent tous les projets de l'hôte et qu'aucun quota de cette stack ne borne.
  Désactivé par défaut ; à n'activer que si des robots coûtent au staging, en surveillant le
  CPU de l'edge, et à retirer s'il monte.
- Les smoke tests du déploiement passent par l'intérieur de la stack ; le test TLS public lit
  `/healthz`, exclu de l'identifiant, comme les deux chemins du contrôle de l'en-tête de
  l'edge (§ 6.7).

### 4.6 Calque de la prod : Umami

La prod seule reçoit [`compose.umami.yaml`](../../compose.umami.yaml), ajouté par le job à
`COMPOSE_FILE` (jamais par une variable que le staging laisserait vide : un `import` vide
fait refuser à l'edge la configuration de tous les sites de l'hôte). Il déclare le site à
l'Umami partagé d'`infra-vps` (son `docs/APPLICATIONS.md` § 6) par les labels de nginx :

| Label | Valeur | Rôle |
|---|---|---|
| `caddy_0.import` | `umami` | L'edge envoie `/u/script.js` et `/u/api/send` du site à Umami ; jamais `caddy.import` (sans numéro, hors de tout site) ni `caddy_1` (le sous-domaine de l'API n'a pas d'audience). |
| `umami.website` | l'UUID du site, littéral | Choisi une fois et versionné ; le front en garde la même copie (`core/analytics/audience/audience-site.ts`), `tools/umami/test` vérifie leur accord. |
| `umami.domain`, `umami.name`, `umami.team` | `league-of-data-base.com`, `LeagueOfDataBase`, `lodb` | La team range le site : c'est par elle qu'un compte en lecture seule le voit. |
| `caddy_2`, `caddy_2.import` | `stats.league-of-data-base.com`, `umami-tableau <UUID>` | Le tableau de bord sous son propre domaine (DNS vers l'hôte) ; `/` mène au tableau du site. |

Le même calque passe à l'API `LoDb__Audience__WebsiteId` (l'UUID) et `LoDb__Audience__Host`
(le domaine du tableau) : la tâche `audience.trends` y tire chaque heure les vues par page
pour `/v1/trends`, avec le compte de `LoDb__Audience__Username` et `__Password`. En fin de
déploiement, le job attend que `https://<hôte canonique>/u/script.js` réponde le JavaScript
d'Umami (un GET, jamais un POST qui compterait une visite) et échoue sinon.

`/v1/trends` lit encore la mesure maison : une ligne `LoDb__Audience__TrendsSwitchDate`
(`aaaa-mm-jj`) du `.env` de la prod, transmise par le même calque, la fait lire les chiffres
d'Umami à partir de ce jour. La poser après deux semaines de tirage comparé à la mesure
maison ; la retirer revient à la mesure maison seule.

Une fois, dans l'UI d'Umami, avec le compte `admin` : *Administrateur → Utilisateurs* crée
le compte du tirage, rôle **Consultation** ; *Administrateur → Équipes → lodb* l'y ajoute,
rôle **Consultation** ; ses identifiants vont dans `ENV_PROD`. Le site garde le même UUID d'un
hôte à l'autre : seul son historique dépend de la sauvegarde d'Umami (`infra-vps`).

### 4.7 Calque de la prod : sauvegardes hors hôte

La prod seule reçoit [`compose.sauvegarde.yaml`](../../compose.sauvegarde.yaml), ajouté par le
job à `COMPOSE_FILE` après `compose.umami.yaml`. Postgres y rejoint le réseau `sauvegardes`
d'`infra-vps` (son `docs/APPLICATIONS.md` § 7) sous l'alias `lodb-prod-postgres`, préfixé par
sa pile : le staging partage l'hôte, et toutes les bases de l'hôte sont sur ce réseau.
Interne, sans route vers l'extérieur, il est créé par `infra-vps` même quand ses sauvegardes
dorment : le job vérifie qu'il existe, sans le créer, et s'arrête sinon avant toute
modification. Aucun service de la stack n'appelle Postgres par cet alias : l'API et `migrate`
gardent `lodb-postgres`, sur `data`.

Chaque nuit, le conteneur de sauvegarde d'`infra-vps` s'y connecte comme `lodb_sauvegarde`
(§ 4.3), avec le mot de passe de `LoDb__Database__BackupRole__Password` (§ 4.2), envoie un
`pg_dump` de la base, chiffré, vers un stockage objet, et note chaque étape dans
`backup_run`, que la page `/admin/backups` affiche. Activation, changement du mot de passe et
retrait : [`sauvegardes.md`](sauvegardes.md).

## 5. Fichiers et volumes de l'hôte

| Chemin, dans `$STAGING_PATH` ou `$PROD_PATH` | Écrit par | Rôle |
|---|---|---|
| dépôt (branche `test` ou `main`) | le job (`git reset --hard`) | Fichiers compose à la racine. |
| `.env` | le job (`ENV_STAGING`, `ENV_PROD` sans ses secrets, plus `LODB_SECRETS_REVISION`, `LODB_EDGE_CIDR`, `LODB_STACK_SUBNET`, `LODB_EDGE_SECRET` et `COMPOSE_FILE`), mode 600 | § 4. |
| `.deploy/secrets/` | le job (`ENV_STAGING`, `ENV_PROD`), à chaque déploiement | Un fichier par secret (§ 4), monté dans `/run/secrets` des services qui le lisent. Dossier en 700, fichiers en 444 : Compose monte un fichier secret avec son propriétaire et son mode, et l'API comme l'entrée de Postgres le lisent sans être root. Tous présents, vides pour un secret absent : Docker monterait un fichier manquant comme un dossier. `LODB_APP_DB_PASSWORD` y est tiré une fois, puis gardé : sa seule copie (§ 4). |
| `.deploy/secrets/LODB_EDGE_SECRET` | le job, au premier déploiement qui ne le trouve pas | Valeur de l'en-tête de l'edge (`openssl rand -hex 32`), mode 600, jamais montée : le job la recopie dans le `.env` à chaque déploiement (§ 6.7). |
| `.deploy/incoming.env` | le job, le temps d'un déploiement | `ENV_*` tel quel, root seul, supprimé dès qu'il est séparé (et à la fin du script, quoi qu'il arrive). |
| `.deploy/data-protection.pfx` | le job (`*_DATA_PROTECTION_PFX`) | Monté en secret dans `api`. Dossier en 700, fichier en 644 : l'utilisateur non root de l'API le lit. Absent, la stack ne démarre pas. |
| `.deploy/backups/` | le job, à chaque déploiement | `<horodatage>.env` : le `.env` remplacé (sans secret depuis les fichiers secrets ; les copies plus anciennes en contiennent encore) ; `<horodatage>.dump` : la base avant `migrate` (`pg_dump -Fc`, relu par `pg_restore --list`). Les dix plus récents de chaque, dossier en 700. Ce que le retour arrière restaure ([runbook](../reecriture/bascule.md), § 8.2). |
| `.deploy/android/` | l'exploitant, à chaque release Android | `latest.json` (asset `lodb-android-latest.json`) et `assetlinks.json` ([`release-android.md`](release-android.md)). Vide : les deux URL répondent 404. |
| `.deploy/maintenance/` | le job, à chaque déploiement (`tools/deploy/maintenance.sh`) | `active.json` : l'interrupteur de la page de maintenance, posé juste avant que le job arrête un conteneur et retiré dès que la nouvelle stack répond à ses sondes ; tant qu'il est là, nginx répond à chaque page du site par la page de maintenance (503), avec le début du déploiement et la durée attendue. `last-duration` : la durée, en secondes, du dernier passage en maintenance, que le suivant annonce aux visiteurs (absente, la page affiche « 10 à 15 minutes »). Monté en lecture seule dans nginx (`/srv/maintenance`), dossier et fichiers lisibles par tous. Un déploiement qui échoue entre les deux laisse la page en place : la supprimer à la main (`rm .deploy/maintenance/active.json`) une fois la stack rétablie. |

Volumes du projet (`lodb-staging_…`, `lodb-prod_…`) :

| Volume | Origine | Rôle |
|---|---|---|
| `pgdata` | **repris** de l'ancienne stack | La base, mise à jour par `migrate` (une base Doctrine est d'abord marquée à `Baseline`). |
| `ddragon` | nouveau, vide au premier déploiement | Blobs Data Dragon, remplis par l'ingestion. |
| `pages-cache` | nouveau | Cache des pages de nginx. |
| `archive-cache` | nouveau | Cache des pages des anciens patchs de nginx, clé sans révision : il survit aux déploiements (`LODB_ARCHIVE_EPOCH`). |
| `assets-cache` | nouveau | Cache des bundles de `/build/` de nginx, clé = nom du fichier : un déploiement le trouve chaud. |
| `seo-cache`, `legacy-cache` | nouveaux | Caches de nginx des fichiers pour les robots (sitemaps, `robots.txt`, `llms.txt`) et des 301 de l'ancien site, clé préfixée par la révision : un déploiement repart de zéro, les entrées précédentes s'éteignent (7 jours, 1 heure). |
| `nginx-temp` | nouveau | Fichiers temporaires de nginx : corps de requête et réponses plus grands que ses tampons, que le client n'a pas encore lus. Chacun est supprimé dès son ouverture : le volume ne contient que ce qui sert. |
| `front-builds` | nouveau | Bundles hachés des builds successifs : `web-ssr` y ajoute les siens au démarrage et rafraîchit leur date toutes les heures, `/build/` y trouve ceux des builds précédents ; le job supprime ceux qu'aucun build en service n'a livrés depuis 30 jours. |
| `storage`, `app_state` | ancienne stack, **laissés intacts** | Non montés : ni réutilisés (`storage` appartient à `www-data`, l'API ne pourrait pas y écrire) ni supprimés (retour arrière, reprise des agrégats et de l'audit). Supprimés à la décommission, après export. |

## 6. Obtenir les valeurs

### 6.1 DNS

Rien à changer : `staging` et `prod` servent les domaines de l'ancienne stack, qui
pointent déjà vers l'hôte. Pour un nouveau nom, enregistrements A (et AAAA) vers l'hôte
avant le déploiement : le job contrôle le TLS public.

### 6.2 Certificat Data Protection (un par environnement)

```bash
openssl req -x509 -newkey rsa:3072 -nodes -days 3650 -subj "/CN=lodb data protection" \
  -keyout dp.key -out dp.crt
openssl pkcs12 -export -inkey dp.key -in dp.crt -out dp.pfx   # mot de passe demandé
base64 < dp.pfx | tr -d '\n'   # valeur de STAGING_ ou PROD_DATA_PROTECTION_PFX
```

Le mot de passe va dans `LoDb__DataProtection__CertificatePassword`. Garder `dp.pfx` hors
ligne, détruire `dp.key`. Le certificat chiffre les clés des cookies et des jetons : le
changer déconnecte tout le monde.

### 6.3 Clé SSH de déploiement et clés de l'hôte

**Une clé par environnement.** `STAGING_SSH_KEY` et `PROD_SSH_KEY` sont celles de l'ancienne
stack, déjà autorisées sur l'hôte. Si c'est la même clé, en créer une pour chacun : un run de
`dev` ne doit rien pouvoir faire avec la clé de la prod.

```bash
ssh-keygen -t ed25519 -N '' -C 'lodb-staging deploy' -f lodb-staging-deploy
ssh-keygen -t ed25519 -N '' -C 'lodb-prod deploy' -f lodb-prod-deploy
```

Chaque clé publique (`.pub`) va dans `~/.ssh/authorized_keys` de l'utilisateur de
déploiement ; chaque clé privée, complète, dans `STAGING_SSH_KEY` ou `PROD_SSH_KEY`. Retirer
l'ancienne clé commune d'`authorized_keys` une fois les deux déploiements passés.

**Clés de l'hôte** (`STAGING_SSH_KNOWN_HOSTS`, `PROD_SSH_KNOWN_HOSTS`), relevées sous le nom
exact de `STAGING_HOST` ou `PROD_HOST`, et comparées à l'empreinte que l'hôte donne lui-même
(console du fournisseur, ou session déjà vérifiée) :

```bash
ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub        # sur l'hôte : la référence
ssh-keyscan -t ed25519 <hôte> > lodb-known-hosts        # depuis un poste
ssh-keygen -lf lodb-known-hosts                         # même empreinte, sinon s'arrêter
```

Le contenu de `lodb-known-hosts` va dans le secret de l'environnement (même valeur pour les
deux quand ils partagent l'hôte). À refaire si l'hôte change de clé : le déploiement échoue
alors à la connexion (`Host key verification failed`).

### 6.4 SMTP

Mêmes identifiants que le `MAILER_DSN` de l'ancienne stack du même environnement (§ 4.4).
Le domaine de `LoDb__Mail__From` doit autoriser le relais : SPF, DKIM et DMARC.

### 6.5 Stripe

| | `staging` | `prod` |
|---|---|---|
| Clés | mode test (`sk_test_…`) 🔁 | mode live (`sk_live_…`) 🔁 |
| Endpoint | `https://test.league-of-data-base.com/webhooks/stripe`, celui de l'ancien staging 🔁 | l'endpoint **existant**, `https://league-of-data-base.com/webhooks/stripe`, inchangé 🔁 |
| Événements | les huit ci-dessous | idem |

Le secret de signature (`whsec_…`) est celui de l'endpoint de chaque environnement.

Événements à cocher sur l'endpoint (**Developers → Webhooks → l'endpoint → Select
events**), tous : un type absent laisse l'état du site diverger de Stripe.

- `checkout.session.completed`, `checkout.session.async_payment_succeeded`,
  `checkout.session.async_payment_failed` ;
- `customer.subscription.updated`, `customer.subscription.deleted`,
  `invoice.payment_failed` ;
- `charge.refunded`, `charge.dispute.created`.

Les sessions ouvertes par le site n'acceptent que la carte : les moyens de paiement actifs
du dashboard ne changent rien. Ce que chaque événement fait :
[`api-publique.md`](../architecture/api-publique.md), « Cycle de vie d'un paiement ».

### 6.6 Google OAuth

Le projet Google Cloud et l'écran de consentement :
[`oauth-google-setup.md`](oauth-google-setup.md), § 1 et 2.

- **Client web** (celui de l'ancienne stack 🔁) : ajouter l'URI de redirection
  `https://<hôte canonique>/api/account/google/callback` et l'origine
  `https://<hôte canonique>`, **sans retirer** celles de l'ancienne stack avant la
  décommission (retour arrière). Même chose pour `test.league-of-data-base.com`.
- **Client desktop** (type *Desktop app*) : son id est compilé dans l'app (propriété MSBuild
  `LoDbDesktopGoogleClientId`) et déclaré côté serveur en `AppClients__N__ClientId` avec son
  secret, sur chaque API que vise le canal (stable : `prod`, beta : `staging`).
- **Client Android** : déclaré en `AppClients__N__ClientId`, sans secret ; la redirection
  passe par l'App Link `https://<hôte>/app/oauth/google`. Voir § 8 : l'app ne le lit pas
  encore.

### 6.7 Sous-réseau et en-tête de l'edge

Rien à relever. Docker choisit ce sous-réseau quand `infra-vps` crée le réseau : à chaque
déploiement, le job le lit (`docker network inspect edge`) et écrit `LODB_EDGE_CIDR` dans
le `.env` de l'hôte, qui sert aussi aux commandes `docker compose` lancées à la main depuis
ce dossier. Une ligne `LODB_EDGE_CIDR` venue de `ENV_*` est remplacée.

Tout conteneur du réseau `edge`, quel que soit son projet, joint le port de nginx : seul
l'edge doit pouvoir y déclarer l'adresse d'un visiteur. Il pose donc sur chaque requête
l'en-tête `X-LoDb-Edge` (labels `caddy_0` et `caddy_1` de `compose.deploy.yaml`, qui
remplacent la valeur qu'un client aurait envoyée) ; nginx répond `403` à toute requête qui ne
le porte pas avec la bonne valeur, sauf `/healthz` (sonde du conteneur), et ne le transmet ni
à l'API ni au SSR. La valeur, `LODB_EDGE_SECRET`, n'est à relever nulle part : au premier
déploiement qui ne la trouve pas, le job la génère (`openssl rand -hex 32`) dans
`.deploy/secrets/LODB_EDGE_SECRET`, puis la recopie à chaque déploiement dans le `.env`, sans
jamais l'afficher. Elle n'est secrète que pour les autres conteneurs : les labels de nginx,
que l'edge lit, la configuration qu'il en tire et le journal où il consigne cette
configuration à chaque changement la contiennent. Le job vérifie que nginx refuse une
requête sans l'en-tête, puis, par l'edge, que deux chemins auxquels nginx répond lui-même
(`/cdn/blobs/edge-check` sur le site, `/edge-check` sur le sous-domaine `api.`) reçoivent
sa `404`, et non un `403`.

Pour la changer : supprimer le fichier sur l'hôte, puis redéployer. nginx et les labels
changent ensemble ; le temps que l'edge relise les labels (quelques secondes), les requêtes
reçoivent un `403`.

### 6.8 Sous-réseau de la stack

Rien à relever non plus. Le réseau privé de la stack (`default`, celui de nginx, du SSR, de
l'API et de `migrate` ; Postgres est sur `data`, § 4.3) a un sous-réseau figé par cible,
hors des plages que Docker distribue (172.17-31/16, 192.168/16) : `10.231.1.0/24` en
staging, `10.231.2.0/24` en prod. L'API ne croit les `X-Forwarded-*` que de lui et ne sert
le port 8080 qu'à lui et à la loopback ; le SSR ne sert son port 4000 qu'à eux aussi
(`LODB_STACK_NETWORKS`) : un conteneur d'un réseau partagé (`observability`) reçoit un 404,
et ne joint que les ports 9464. Le job écrit `LODB_STACK_SUBNET` dans le `.env`, refuse de
continuer si un autre réseau Docker recouvre ce sous-réseau, et recrée une fois le réseau de
la stack s'il en a un autre (`docker compose down` sans `-v`, environ une minute de coupure,
volumes conservés).

## 7. En local

Aucun `.env` n'est nécessaire : `compose.yaml` donne une valeur par défaut à tout, et
`compose.override.yaml` ajoute `ASPNETCORE_ENVIRONMENT=Development` et Mailpit
(`LoDb__Mail__Host=mailpit`, e-mails sur http://localhost:18025). L'API s'y connecte aussi
comme `lodb_app`, mot de passe `lodb-app` (`migrate` le pose), et `migrate` comme `lodb`/`lodb` :
`psql` sur le port publié (15432) se connecte avec l'un ou l'autre. Un `.env` racine ne sert
qu'à surcharger un port ou une variable ; ne jamais y copier `.env.staging` ni `.env.prod`.
Ports et emplacements : [`developpement.md`](developpement.md).

L'ancienne stack archivée a son propre `.env`, `legacy/.env` (modèle
`legacy/.env.example`), lu par `docker compose --project-directory legacy …`.

## 8. Écarts connus, à trancher avant la bascule

- **Pays des visiteurs** : aucun fichier GeoLite2 n'est monté et ni
  `LoDb__Analytics__GeoIpDatabase` ni `GEOIP_DB_PATH` ne sont transmis sur un hôte. Les
  vues de prod n'auront pas de pays, contrairement à l'ancienne stack.
- **Connexion Google sur Android** : aucun fichier d'environnement du front ne définit
  `googleClientId`. La connexion reste coupée dans l'app, quels que soient les
  `AppClients` du serveur.
- **Releases des apps** : les workflows attendent un appel après le déploiement de `prod`
  (« contrat d'appel » des guides de release), que `ci.yml` ne fait pas. Elles se lancent
  donc à la main.
- **Réglages internes** : aucun n'est réglable depuis le `.env` sans modifier
  `compose.deploy.yaml` (§ 4.3, encadré).
