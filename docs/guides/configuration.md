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
| Secrets de dépôt `STAGING_*`, `PROD_*` | Connexion SSH et dossier de l'hôte, ceux de l'ancienne stack, repris tels quels ; certificat Data Protection (depuis la bascule, à ne jamais remplacer) ; clés de l'hôte (`*_SSH_KNOWN_HOSTS`, optionnelles, non posées au 2026-10-22) | en place (§ 3.2) |
| Secrets de dépôt `ENV_STAGING`, `ENV_PROD` | Le `.env` complet de l'environnement, celui de la nouvelle stack depuis la bascule | l'exploitant (*Settings ▸ Secrets*) |
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

## 2. État en place, et historique de la mise en place

Les deux environnements servent la nouvelle stack depuis la bascule du **2026-10-04** : les
secrets du § 3.2 existent, et un déploiement ne demande qu'un push de `dev` (staging) ou la
fusion de `test` dans `main` (prod, promotion des digests `:staging-validated` en `:prod` et
`:latest`). Changer une variable : modifier `.env.staging` ou `.env.prod`, puis remettre le
fichier complet dans `ENV_STAGING` ou `ENV_PROD` (§ 4). **Ne jamais remplacer**
`STAGING_DATA_PROTECTION_PFX` ni `PROD_DATA_PROTECTION_PFX` : les clés Data Protection déjà
chiffrées deviendraient illisibles (sessions et jetons perdus, § 6.2).

### Historique : la bascule (faite le 2026-10-04, ne pas rejouer)

> Procédure suivie une seule fois par environnement, gardée pour mémoire. **Ne pas la
> rejouer** : `ENV_*` remplacé par un modèle ou certificat recréé casseraient
> l'environnement servi.

Les valeurs 🔁 ne se lisaient que dans le `.env` de l'hôte (`$STAGING_PATH/.env`,
`$PROD_PATH/.env`), les secrets GitHub n'étant pas relisibles. Elles ont été relevées
**avant** la bascule de chaque environnement, dont le premier déploiement a remplacé ce
fichier.

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
   tant que `main` portait l'ancienne stack, un push de `main` l'aurait redéployée avec ce
   `.env`.
4. SMTP, Stripe, Google : § 6.4 à 6.6.
5. Fusion de `test` dans `main` : promotion des digests `:staging-validated` en `:prod` et
   `:latest`, déploiement de `prod`.

Les sauvegardes hors hôte de la base de prod se sont activées ensuite, à part
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
| `STAGING_SSH_KEY` 🔒 | `PROD_SSH_KEY` 🔒 | en place | Clé privée SSH de déploiement. |
| `STAGING_HOST` | `PROD_HOST` | en place | IP ou FQDN de l'hôte. |
| `STAGING_PATH` | `PROD_PATH` | en place | Dossier du dépôt sur l'hôte, celui de l'ancienne stack : la nouvelle y a pris sa place. |
| `STAGING_SSH_USER` | `PROD_SSH_USER` | en place | Utilisateur SSH (➖, `root` par défaut). |
| `STAGING_SSH_KNOWN_HOSTS` | `PROD_SSH_KNOWN_HOSTS` | ➖ optionnel, non posé au 2026-10-22 | Lignes `known_hosts` des clés de l'hôte (§ 6.3) : `ssh` refuse alors toute autre clé. Absent : la clé est lue par `ssh-keyscan` à chaque run, sans vérification, et le run porte un avertissement *Host key not pinned*. |
| `ENV_STAGING` 🔒 | `ENV_PROD` 🔒 | en place, mis à jour à chaque changement de variable | Contenu complet de `.env.staging` / `.env.prod` (§ 4). |
| `STAGING_DATA_PROTECTION_PFX` 🔒 | `PROD_DATA_PROTECTION_PFX` 🔒 | en place depuis le 2026-10-04, **à ne jamais remplacer** | Le `.pfx` de l'environnement en base64, sur une ligne (§ 6.2). Celui de la prod ne change plus une fois servi : les clés déjà chiffrées deviendraient illisibles (sessions et jetons perdus). |

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
| `desktop-release` (ou dépôt) | `LODB_DESKTOP_UPDATE_PRIVATE_KEY`, clé de signature des flux, lue par le job `sign` | secret 🔒 |
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
- `LoDb__Prerender__AccessKey`, `LoDb__Prerender__SecretKey` (clé de l'application d'écriture
  du stockage des pages pré-rendues, § 4.8) ;
- `LoDb__ScalewayBilling__SecretKey` (clé de lecture de la facturation Scaleway, § 4.9) ;
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
| `API_CADDY_DOMAINS` | `api.test.league-of-data-base.com` 🔁 | `api.league-of-data-base.com` 🔁 | ✅ | Label `caddy_1` ; chaque hôte commence par `api.`. |
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
| `LODB_PAGES_RATE`, `LODB_PAGES_BURST`, `LODB_API_READS_*`, `LODB_LEGACY_*`, `LODB_SITEMAPS_*`, `LODB_READYZ_*`, `LODB_RATE_LIMITED_RETRY_AFTER`, `LODB_MAX_BODY_SIZE`, `LODB_WEBHOOK_MAX_BODY_SIZE`, `LODB_PAGE_CACHE_SIZE`, `LODB_PAGE_CACHE_KEYS_SIZE`, `LODB_PAGE_CACHE_INACTIVE`, `LODB_ARCHIVE_CACHE_SIZE`, `LODB_ARCHIVE_CACHE_KEYS_SIZE`, `LODB_ARCHIVE_CACHE_INACTIVE`, `LODB_ASSETS_CACHE_SIZE`, `LODB_ASSETS_CACHE_KEYS_SIZE`, `LODB_ASSETS_CACHE_INACTIVE` | ➖ | ➖ | ➖ | Bornes de nginx, par adresse : débit (`_RATE`) et rafale (`_BURST`) des pages (`20r/s`, `100`), des lectures publiques de l'API (`10r/s`, `40`), de l'ancien site (`2r/s`, `10`), de `/sitemaps/` (`1r/s`, `20`) et de `/readyz` (`1r/s`, `5`), au-delà `429` avec `Retry-After` (`10`) ; corps de requête (`128k`, `1m` pour `/webhooks/`) ; cache des pages du dernier patch (`2g`, clés `32m`, inactivité `24h`), des anciens patchs (`2g`, clés `16m`, inactivité `90d` : les seuls rendus du SSR, nginx ne gardant jamais la réponse du stockage objet, § 4.8) et des bundles de `/build/` (`1g`, clés `8m`, inactivité `30d`), soit ≈ 5 Go de disque au plus. Absents ou vides : ces défauts, ceux de `docker/nginx/Dockerfile`, que `compose.yaml` reprend (`tools/nginx` vérifie l'accord). |
| `LODB_ARCHIVE_EPOCH` | ➖ | ➖ | ➖ | Première partie de la clé du cache des anciens patchs (`3`), qu'on ne monte qu'à la main : voir « Monter l'epoch des anciens patchs » ci-dessous. Changer `LODB_ARCHIVE_SERVE_EPOCH`, la seconde partie, purge déjà ce cache : la bascule vers une page indexée par état la laisse à `3`. |
| `LODB_ARCHIVE_CACHE_MIN_USES` | ➖ | ➖ | ➖ | Rendus d'une page d'ancien patch par le SSR avant que nginx la garde (`1` : dès le premier ; une page du stockage objet n'est jamais gardée, § 4.8). Au-dessus, chaque URL distincte tient quand même une clé jusqu'à son inactivité, quelle que soit la réponse (`301`, `503` de délestage) : un balayage évince les pages les moins récemment demandées dès qu'il dépasse la zone des clés (`LODB_ARCHIVE_CACHE_KEYS_SIZE`, ≈ 8 000 clés par Mo). À `1`, seules les réponses gardées tiennent une clé. |
| `LODB_PAGES_STORE_ENABLED` | `1` | `1` depuis le 2026-10-23 | ➖ | `1` : nginx cherche une page d'ancien patch dans le stockage objet avant de la faire rendre par le SSR, qui reste le repli (§ 4.8). À poser une fois `scaleway-bucket.sh verifier` passé sur l'hôte. `0`, le défaut et celui d'une stack locale sans stockage : tout passe par le SSR. |
| `LODB_PAGES_STORE_BUCKET` | `lodb-pages-staging` | `lodb-pages-prod` | avec le précédent | Bucket que nginx lit, le même que `LoDb__Prerender__Bucket` (§ 4.2). Défaut `none` : aucun (une valeur vide est refusée par nginx). |
| `LODB_PAGES_STORE_SCHEME`, `LODB_PAGES_STORE_HOST`, `LODB_PAGES_STORE_PORT` | ➖ | ➖ | ➖ | Adresse du stockage pour nginx : `https`, `s3.fr-par.scw.cloud`, `443`. Le stockage simulé de la stack locale les remplace (§ 7). nginx lit sans clé, depuis l'adresse de l'hôte, la seule que la politique du bucket admette. |
| `LODB_ARCHIVE_SERVE_EPOCH` | `2` depuis la fusion de la règle par état (2026-10-22, § 4.8) | `2` depuis la bascule par état (2026-10-22, § 4.8) | ➖ | Epoch que nginx lit dans le stockage (`pages/<epoch>/…`), aussi transmise à l'API (`LoDb__Prerender__ServedEpoch`) pour le panneau d'administration. Changée à la main, puis redéploiement : voir « Changer l'epoch servie » (§ 4.8). Distincte de `LODB_ARCHIVE_EPOCH`, le compteur des clés du cache de nginx. |
| `LODB_PRERENDER_RENDERS_PER_MINUTE`, `LODB_PRERENDER_RENDER_BURST`, `LODB_PRERENDER_MAX_IN_FLIGHT`, `LODB_PRERENDER_VISITOR_RESERVE` | ➖ (§ 4.5) | ➖ | ➖ | Voie `prerender` du SSR des visiteurs : rendus par minute (`120`) et d'affilée (`10`), rendus simultanés de la passe (`2`, strictement sous `LODB_RENDER_MAX_IN_FLIGHT`), rendus toujours laissés aux visiteurs (`4`). Inutilisée depuis que la passe a son propre SSR (§ 4.10), gardée telle quelle. Deux de ces lignes servent ailleurs : `LODB_PRERENDER_RENDERS_PER_MINUTE` est aussi le **rythme fixe** de la passe (`LoDb__Prerender__RendersPerMinute`, § 4.3 : interrupteur `LODB_PRERENDER_PACE_MODE=fixed` et commande `prerender unit`), jamais au-dessus de `LODB_PRERENDER_PACE_MAX` ; `LODB_PRERENDER_RENDER_BURST` règle aussi la rafale de la voie des moteurs de la passe. `LODB_PRERENDER_MAX_IN_FLIGHT` ne donne plus les pages en vol de la passe (`LODB_PRERENDER_PAGES_IN_FLIGHT`, § 4.10) ni la voie de ses moteurs (`15`, fixe), sauf au staging qui la pose à `1`. |
| `LODB_PRERENDER_PACE_MODE` | ➖ | ➖ | ➖ | Rythme de la passe de précalcul (§ 4.10) : `adaptive` (défaut), il suit le CPU libre de l'hôte entre le plancher et le plafond ci-dessous ; `fixed`, il reste à `LODB_PRERENDER_RENDERS_PER_MINUTE` sans rien mesurer. L'interrupteur, appliqué par un redéploiement de l'API ; le levier immédiat reste la pause au pupitre. |
| `LODB_PRERENDER_MANUAL_MAX`, `LODB_PRERENDER_RENDERERS`, `LODB_PRERENDER_RENDERER_CPUS`, `LODB_PRERENDER_PAGES_IN_FLIGHT` | ➖ (§ 4.5) | ➖ | ➖ | Vitesse réglable de la passe (§ 4.10) : maximum du mode manuel (`4000` sur un hôte, avertissement au pupitre au-delà de `1300`, `600` en local, le plafond `30` au staging), aussi budget de la voie de **chaque** moteur ; moteurs de précalcul, répliques de `web-ssr-prerender` (`2` sur un hôte, `1` au staging et en local) ; quota de chacun (`1.0`) ; pages en vol au plus (`24`). |
| `LODB_PRERENDER_PACE_MIN`, `LODB_PRERENDER_PACE_MAX` | ➖ (§ 4.5) | ➖ | ➖ | Plancher (`30`) et plafond (`240`) du rythme adaptatif, en rendus par minute. Le plafond reste sous `LODB_PRERENDER_MANUAL_MAX`, budget de la voie des moteurs de la passe (`web-ssr-prerender`) : le moteur borne la passe même si l'API se trompe. L'API refuse de démarrer, et le job de déployer, sur un plancher, un pas ou un rythme fixe au-dessus du plafond. Monter le plafond à `300` après une semaine sans alerte ni ralentissement mesuré (§ 4.10). |
| `LODB_FRONT_BUILDS_RETENTION_DAYS` | `400` | `400` | ➖ | Lu par le job de déploiement : un bundle du volume `front-builds` qu'aucun build n'a livré depuis ce nombre de jours (`400`) est supprimé, `395` au moins (le job refuse en deçà). Jamais sous la durée de vie d'une page en cache (un an pour un ancien patch, son `s-maxage`, puis sa copie périmée servie le temps de son nouveau rendu) : un onglet ou une page gardée nommerait un bundle disparu. La copie des builds dans le stockage objet (ADR 0011) ne couvre pas les pages rendues par le SSR, que le cache des anciens patchs garde aussi (inactivité `90d`) : un filet de plus, pas une raison de baisser cette durée. Mesuré : 16 Mo en 9 jours, moins de 1 Go par an. |
| `LODB_ADMIN_LOGIN` | 🔁 `ADMIN_LOGIN` | idem | ➖ | Administrateur racine, tenu par le job à chaque déploiement (`api admin root`) : compte de ce nom, rôle admin, TOTP enrôlé depuis le lien d'invitation que `api admin mfa-invite --email …` affiche sur l'hôte (15 min ; `api admin mfa-reset` retire un TOTP perdu). Absent : aucun, seulement `api admin create`. |
| `LODB_ADMIN_PASSWORD` 🔒 | 🔁 `ADMIN_PASSWORD` | idem | avec le précédent | Son mot de passe, 12 caractères au moins, sans apostrophe ni guillemet. Changé ici, il est réappliqué au déploiement suivant (sessions fermées). Reste dans le `.env` : le job le passe à la commande par son entrée. |
| `LODB_ADMIN_EMAIL` | ➖ | ➖ | ➖ | Son e-mail, qui identifie le compte ; défaut `<login>@<LODB_CANONICAL_HOST>`. |
| `LODB_PG_MAX_CONNECTIONS`, `LODB_PG_SLOW_STATEMENT`, `LODB_PG_IDLE_IN_TRANSACTION_TIMEOUT`, `LODB_PG_SHM_SIZE` | ➖ | ➖ | ➖ | Serveur Postgres (`compose.yaml`) : connexions (`60`, pour un pool de l'API à `32` plus `migrate`, la sauvegarde du déploiement, les verrous des tâches et `psql`), requêtes journalisées au-delà de `500ms` (sans leurs paramètres, jamais), session laissée inactive dans une transaction fermée après `60s`, `/dev/shm` de `256m`. `pg_stat_statements` est toujours chargé. |
| `COMPOSE_FILE` | — | — | — | **Jamais dans `ENV_*`** : le job l'écrit dans le `.env` de l'hôte, avec les calques de l'environnement (§ 4.5 à 4.7), pour qu'une commande `docker compose` lancée à la main depuis le dossier les applique aussi. |
| `LODB_STAGING_BASIC_AUTH_USER`, `LODB_STAGING_BASIC_AUTH_HASH` 🔒 | ➖ | **jamais** | ➖ | Identifiant demandé sur les pages du staging (§ 4.5). Les deux ou aucun ; en prod, le job refuse de déployer. |

**Monter l'epoch des anciens patchs.** Les pages d'une version épinglée dans l'URL sont
gardées par nginx à travers les déploiements, avec le chrome, la CSP et les bundles du build
qui les a rendues : un an pour une page d'état clos (`archived`), une heure pour une page de
l'état ouvert (`revisable`, § 4.13). Monter `LODB_ARCHIVE_EPOCH` (`3` → `4`…) dans
`ENV_STAGING` / `ENV_PROD` quand le chrome (en-tête, pied, puce de release), la CSP ou le
contrat `/api` changent de façon visible, **après tout correctif de sécurité du front**, et
après des pages recalculées en place dans l'epoch servie (nouveau schéma d'empreinte, § 4.13) ; le déploiement suivant
recrée nginx sur des clés neuves, les anciennes s'éteignent après 90 jours sans demande
(`LODB_ARCHIVE_CACHE_INACTIVE`), ou plus tôt quand le cache est plein.

Cette epoch ne vide que le cache de nginx. Une fois qu'il lit le stockage
(`LODB_PAGES_STORE_ENABLED=1`, § 4.8), la requête suivante reprend la page de l'epoch servie,
rendue avec l'ancien chrome, l'ancienne CSP et les anciens bundles. Un changement visible du
chrome, de la CSP ou du contrat `/api` passe alors par une passe sur une epoch neuve, puis la
bascule de `LODB_ARCHIVE_SERVE_EPOCH` une fois l'epoch complète (« Changer l'epoch servie »,
§ 4.8), qui change aussi les clés du cache. D'ici là, `/api` doit rester compatible avec les
bundles de l'epoch servie, qui s'y hydratent ; sinon, basculer tout de suite, comme pour un
correctif de sécurité.

**Correctif de sécurité du front.** Le déployer avec `LODB_ARCHIVE_EPOCH` monté et, si une
passe a déjà écrit au stockage, `LODB_ARCHIVE_SERVE_EPOCH` sur une epoch neuve, dont on lance
la passe (§ 4.8) : les pages de l'epoch servie nomment le chunk vulnérable. Une fois le build
corrigé en service (le build vulnérable republierait son chunk dans l'heure), retirer ce
chunk, dans cet ordre :

1. du volume des bundles :

   ```bash
   docker compose exec -T web-ssr rm -f /srv/front-builds/<chunk>.js
   ```

2. du stockage, dans le bucket de chaque environnement dont une passe a copié le build, même
   avec `LODB_PAGES_STORE_ENABLED=0` (nginx le lira une fois allumé). Sur une `404` du SSR,
   nginx y cherche le bundle (`build/<nom>`) et le garde de nouveau un an ; `build/` n'a
   aucune règle de cycle de vie et la clé d'écriture ne supprime rien. Depuis le poste, avec
   la clé temporaire de l'opérateur
   ([`tools/prerender/README.md`](../../tools/prerender/README.md), § 1 et 4), puis de même
   pour un fichier `build/media/<nom>` en cause :

   ```bash
   AWS_ACCESS_KEY_ID="$AWS_ACCESS_KEY_ID@<projet lodb-pages-prod>" docker run --rm \
     -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY -e AWS_DEFAULT_REGION=fr-par \
     amazon/aws-cli:2.36.50@sha256:c240dd63cdbab17197486d55460675ef2b94c9cc372dedae34dee53ccd30657c \
     s3api delete-object --endpoint-url https://s3.fr-par.scw.cloud \
     --bucket lodb-pages-prod --key build/<chunk>.js
   ```

3. du cache des bundles de nginx, en dernier : plus tôt, une requête l'y remettrait depuis le
   stockage.

   ```bash
   docker compose exec -T nginx sh -c \
     "grep -rlax 'KEY: /build/<chunk>.js' /var/cache/nginx/assets | xargs -r rm -f"
   ```

**Compromission du serveur SSR** (exécution de code dans le conteneur `web-ssr`). Le volume
`front-builds` est le seul endroit où il écrit, et ce qu'il y écrit survit aux déploiements.
La passe lui a aussi pris de quoi remplir le stockage : les bundles de ses builds, sous
`build/`, jamais réécrits ni effacés, et les pages des epochs qu'elle écrivait. Annuler la
passe en cours (panneau d'administration), puis déployer le build corrigé avec
`LODB_ARCHIVE_EPOCH` monté et, si une passe a déjà écrit au stockage,
`LODB_ARCHIVE_SERVE_EPOCH` sur une epoch neuve (§ 4.8). Une fois ce build en service, dans
cet ordre :

1. vider le volume : le build corrigé y republie les siens dans l'heure, et `/build/` sert
   ceux de son image entre-temps ;

   ```bash
   docker compose exec -T web-ssr find /srv/front-builds -mindepth 1 -delete
   ```

2. vider `build/` dans le bucket de l'environnement, avec la commande du point 2 ci-dessus,
   `s3 rm --recursive s3://lodb-pages-prod/build/` à la place de `s3api delete-object …` ;
3. vider le cache des bundles de nginx, après le stockage pour la même raison :

   ```bash
   docker compose exec -T nginx find /var/cache/nginx/assets -mindepth 1 -delete
   ```

4. oublier les builds copiés, en base puis dans l'API, qui les garde en mémoire jusqu'à son
   redémarrage : sinon, la passe suivante les tiendrait pour stockés et ne recopierait pas
   leurs bundles ;

   ```bash
   docker compose exec -T postgres sh -c \
     'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "DELETE FROM prerender_build"'
   docker compose restart api
   ```

5. lancer la passe de l'epoch neuve (panneau), puis retirer les epochs que le serveur
   compromis a pu écrire ([`tools/prerender/README.md`](../../tools/prerender/README.md),
   § 4).

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
| `LoDb__Mail__From` | prod | `Nom <adresse>`, sur un domaine couvert par SPF, DKIM et DMARC (§ 6.4). Défaut : `LeagueOfDataBase <contact@charles-lindecker.com>`, l'expéditeur de la prod. |
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
| `LoDb__Seo__IndexNowKey` | prod | Clé IndexNow (`openssl rand -hex 16`), publique par nature : servie à `/{clé}.txt`. Posée, chaque promotion d'un patch soumet à IndexNow (Bing, Yandex, Naver, Seznam…), dans les 21 langues et dans cet ordre : toutes les URL courtes (accueil, quatre listes, `patches`, chaque entité listée) ; `/{locale}/changes` si le patch promu a changé quelque chose ; `/{locale}/{version}/changes` de chaque version couverte depuis le patch promu avant (compris) qui a changé quelque chose ; les ouvreurs (`/{locale}/{ouvreur}/{chemin}`) des états clos depuis ce patch. Requêtes de 10 000 URL au plus, les URL courtes d'abord ; un refus arrête le reste, sans nouvel essai (`seo.indexnow.submitted`, `.refused`). Rien si l'index des états ne couvre pas la version (`seo.indexnow.unindexed`) : les sitemaps datés prennent le relais. Absente, comme sur `staging` : rien n'est envoyé ; avec `LoDb__Seo__CanonicalOrigin`, une ligne `seo.indexnow.planned` donne les comptes par catégorie. L'historique n'est jamais soumis d'un coup, à la bascule par état non plus : les sitemaps l'annoncent. |
| `LoDb__Catalog__MaxEntries`, `PinnedLanguages`, `PinnedBudgetMegabytes` | ➖ | Catalogues en mémoire : ceux du dernier patch dans les langues épinglées (défaut : les 21 de l'interface, en_US en tête), jamais évincés, dans `256` Mio estimés ; `16` autres au plus, le moins récemment lu évincé. |
| `LoDb__Catalog__ColdLoadConcurrency`, `ColdLoadsPerSecond`, `ColdLoadBurst`, `PublicColdLoadsPerSecond`, `InternalColdLoadWait`, `MaxPendingLoads` | ➖ | Chargement d'un catalogue absent de la mémoire : `2` à la fois, `3` par seconde (rafale `6`), dont `1` pour les appels publics, refusés sans attente ; le SSR attend `2` s au plus, derrière `16` autres. Refus : `503` + `Retry-After`. |
| `LoDb__Catalog__WarmUp__MaxStreams`, `MaxStreamsPerClient`, `StreamLifetime`, `PerClientPerHour` | ➖ | Loader de changement de patch : `8` flux ouverts, `2` par client, `120` s chacun, `20` démarrages par heure et par client (`429` au-delà). |
| `LoDb__Ingestion__ColdDatasetsPerMinute`, `ColdImagesPerMinute`, `MaxWaitingIngestions`, `SlotWait` | ➖ | Ingestion à froid pour un visiteur : `6` couples (version, langue) et `1500` images par minute, `32` ingestions en attente d'un emplacement, `3` s d'attente au plus. Au-delà : catalogue « en attente », images en placeholders. |
| `LoDb__Prerender__Endpoint`, `LoDb__Prerender__Region` | avec le stockage | Point d'accès S3 du stockage des pages pré-rendues et sa région (`https://s3.fr-par.scw.cloud`, `fr-par`, § 4.8). En `https` seulement hors d'une stack locale. |
| `LoDb__Prerender__AllowInsecureEndpoint` | dev seulement | `true` autorise un `Endpoint` en `http`, celui du stockage simulé de `compose.override.yaml` (§ 7). **Jamais posé sur un hôte** : absent, un point d'accès en `http` est refusé au démarrage. |
| `LoDb__Prerender__Bucket` | avec le stockage | Bucket de l'environnement : `lodb-pages-staging`, `lodb-pages-prod`. Absent : la passe reste en sommeil. |
| `LoDb__Prerender__AccessKey`, `LoDb__Prerender__SecretKey` 🔒 | avec le stockage | Clé API de l'application `lodb-pages-writer-<env>` : écriture d'objets seule, sur le projet de l'environnement seul ([`tools/prerender/README.md`](../../tools/prerender/README.md)). Absente : la passe reste en sommeil, nginx sert par le SSR. |
| `LoDb__Prerender__AutoEnqueueOnPromotion` | ➖ | Ne commande que les unités du patch qui quitte le dernier rang, à sa promotion et au rapprochement ; la file des états clos (§ 4.13) n'en dépend pas. `false` tant que la passe complète de la prod n'est pas terminée : l'opérateur la lance à la main ; à `true`, le rapprochement (toutes les 30 min) ouvrirait de lui-même, sans promotion, une passe pour le patch précédent, et celle de l'opérateur répondrait `409`. Puis `true` en prod : un patch qui quitte le dernier rang rejoint la passe en cours, ou en ouvre une sur l'epoch servie, une fois l'index des états à jour. `false` sur le staging (son calque, § 4.5), qui reste presque vide. |
| `LoDb__ScalewayBilling__SecretKey` 🔒 | ➖ | Clé secrète de l'application `lodb-facturation-lecture` (§ 4.9), une par environnement. Absente : les panneaux « Sauvegardes » et « Précalcul du catalogue » disent que la facture n'est pas configurée. |
| `LoDb__ScalewayBilling__OrganizationId`, `BackupsProjectId`, `PagesProjectId` | avec la clé | Organisation, projet du bucket des sauvegardes et projet des pages de l'environnement, en UUID (§ 4.9). Requis une fois la clé posée ; un id qui n'est pas un UUID est refusé au démarrage, clé posée ou non. |
| `LoDb__Database__AppRole__StatementTimeout`, `ConnectionLimit` | ➖ | Transmis à `migrate` seulement, qui les pose sur le rôle de l'API (§ 4.3) : `00:00:15` (une requête plus longue est annulée par le serveur) et `50` sessions (`-1` : sans limite ; sous les 60 du serveur, moins les 3 réservées aux superutilisateurs). Pris en compte au déploiement suivant, par les nouvelles connexions. |
| `LoDb__Database__BackupRole__Password` 🔒 ⚙️ | prod, à l'activation des sauvegardes | Mot de passe de `lodb_sauvegarde`, le rôle des sauvegardes hors hôte (§ 4.7) : la même valeur que `SAUVEGARDE_LODB_MOT_DE_PASSE` d'`infra-vps` (`openssl rand -hex 32`). Fichier monté dans `migrate` seul, qui le pose sur le rôle. Posé, le job exige 16 caractères au moins parmi lettres, chiffres, `-` et `_` (le format d'`infra-vps`). Absent : un rôle neuf n'a pas de mot de passe, personne ne s'y connecte ; un rôle existant garde le sien. |

### 4.3 Fixés par les fichiers compose (hors du `.env`)

`ConnectionStrings__LoDb` (hôte `lodb-postgres`, base `LODB_DB_NAME`, **sans mot de passe** :
le service y pose `LoDb__Database__Password`, lu dans `/run/secrets` ; pour l'API, rôle
`lodb_app`, pool borné par `Maximum Pool Size=32;Timeout=5` ; pour `migrate`, `LODB_DB_USER` ;
pour `states`, `lodb_app` sous `Application Name=lodb-states`, avec `LoDb__Storage__Root`
sur `ddragon` monté en lecture seule),
`LoDb__Accounts__SiteOrigin`, `LoDb__Seo__CanonicalOrigin` et `LoDb__PublicApi__SiteOrigin`
(`https://` + `LODB_CANONICAL_HOST`), `LoDb__PublicApi__Reference__BaseUrl`
(`LODB_PUBLIC_API_ORIGIN`), `LoDb__DataProtection__CertificatePath`,
`LoDb__Prerender__SsrBaseUrl` (`http://lodb-web-ssr-prerender:4000` : la passe demande ses
pages à son propre SSR, directement, jamais à nginx ni au SSR des visiteurs, § 4.10),
`LoDb__Prerender__PublicHost` (`LODB_CANONICAL_HOST` : l'hôte
pour lequel chaque page est rendue), `LoDb__Prerender__ServedEpoch`
(`LODB_ARCHIVE_SERVE_EPOCH`), `LoDb__Prerender__RendersPerMinute` et
`LoDb__Prerender__MaxInFlight` (`LODB_PRERENDER_RENDERS_PER_MINUTE` et
`LODB_PRERENDER_MAX_IN_FLIGHT`, `120` et `2` à défaut, `30` et `1` au staging : le rythme
fixe de la passe et ses pages en vol), `LoDb__Prerender__Pace__Mode`,
`LoDb__Prerender__Pace__MinRendersPerMinute` et `LoDb__Prerender__Pace__MaxRendersPerMinute`
(`LODB_PRERENDER_PACE_MODE`, `LODB_PRERENDER_PACE_MIN`, `LODB_PRERENDER_PACE_MAX`),
`LoDb__Prerender__Pace__VisitorsLoadUrl` et `LoDb__Prerender__Pace__PassLoadUrl`
(`http://lodb-web-ssr:9464/load`, `http://lodb-web-ssr-prerender:9464/load` : l'état des
deux SSR que lit le régulateur), et côté `web-ssr-prerender` les variables du SSR des
visiteurs, sauf `LODB_FRONT_BUILDS_DIR`, avec `LODB_RENDER_MAX_IN_FLIGHT=4`,
`LODB_PRERENDER_VISITOR_RESERVE=1` et `LODB_PRERENDER_RENDERS_PER_MINUTE` tiré de
`LODB_PRERENDER_PACE_MAX`,
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
| `web-ssr-prerender` | `1.0` | `128` | `256` | `704m` |
| `api` | `1.25` | `1024` | `512` | `1152m` |
| `postgres` | `0.5` | `1024` | `256` | `512m` |
| `nginx` | `0.25` | `2048` | `256` | `512m` |
| `migrate` | — | — | `256` | `128m` |
| `states` | `0.5` | `512` | `256` | `512m` |

Les quotas des services des visiteurs totalisent 3 des 4 vCPU de l'hôte : sous une charge
qui les dépasse, la stack déleste (`503` avec `Retry-After`) au lieu de prendre l'hôte des
autres projets. Le SSR de la passe (`web-ssr-prerender`) s'y ajoute au poids le plus bas :
le noyau ne lui cède que le CPU que personne ne demande, et son régulateur le freine avant
(§ 4.10). `states` (§ 4.12) ne tourne que pendant le déploiement, avant la page de
maintenance, à côté des visiteurs : sous leur poids, au-dessus du SSR de la passe ; ses limites
et la durée de sa première construction sont à mesurer au staging. Le bridage se
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
| `api`, `migrate`, `states` | `app` (image chiseled) | `ddragon` (`api` ; `states` le lit en lecture seule), tmpfs `/tmp` (64 Mio) | — |
| `web-ssr` | `node` | `front-builds`, tmpfs `/tmp` (16 Mio) | — |
| `web-ssr-prerender` | `node` | tmpfs `/tmp` (16 Mio) : aucun volume | — |
| `nginx` | `nginx`, port 8080 | caches et fichiers temporaires (volumes), tmpfs `/tmp` (64 Mio : configuration rendue, pid) | — |
| `postgres` | root, puis `postgres` | `pgdata`, tmpfs `/var/run/postgresql` (8 Mio) et `/tmp` (16 Mio) | `CHOWN`, `DAC_OVERRIDE`, `FOWNER`, `SETGID`, `SETUID` (entrée de l'image), toutes parmi celles que Docker accorde par défaut |

Réseaux : `default` (nginx, SSR, API, `migrate`, `states`) porte le HTTP, sur
`LODB_STACK_SUBNET` (§ 6.8) ; `data` (Postgres, API, `migrate`, `states`) est interne sur un
hôte, sans route vers l'extérieur : nginx et les SSR ne joignent pas la base. nginx rejoint aussi `edge`, l'API et
les deux SSR `observability` ; en prod, Postgres rejoint `sauvegardes` (§ 4.7).

<details>
<summary>Réglages internes de l'API, avec leur défaut (non transmis par compose)</summary>

Pour en changer un sur un hôte, l'ajouter sous `services.api.environment` de
`compose.deploy.yaml`, puis dans le `.env`. Exception : sur le staging,
`LoDb__Ingestion__SyncConcurrency`, `LoDb__Ingestion__LanguageConcurrency` et
`LoDb__Egress__FetchConcurrency` passent déjà du `.env` (`compose.staging.yaml`, § 4.5) ; en
prod, ils gardent leur défaut.

| Section | Clés (défaut) |
|---|---|
| `LoDb:Hosting` | `HttpPort` (8080, attendu par nginx et le SSR), `MetricsPort` (9464, jamais publié), `MaxRequestBodySize` (131072 octets, soit 128 Kio ; au-delà, 413), `DatabaseBusyRetryAfter` (5 s, `Retry-After` du 503 quand le pool Postgres reste occupé), `SecretsDirectory` (`/run/secrets` : un fichier par réglage, nommé d'après sa clé avec `__`, lu au démarrage ; vide, il vaut un réglage absent ; il prime sur toute autre source) |
| `LoDb:Database` | `Password` (aucun : sans lui, la chaîne de connexion reste telle quelle ; posé, il y remplace le mot de passe) ; pour `migrate` seulement, `AppRole:Password` (aucun : le mot de passe de `lodb_app` reste tel quel ; ASCII imprimable), `AppRole:StatementTimeout` (15 s), `AppRole:ConnectionLimit` (50, `-1` sans limite), `BackupRole:Password` (aucun : le mot de passe de `lodb_sauvegarde` reste tel quel ; ASCII imprimable) |
| `LoDb:Workers` | `Enabled` (`true` ; seul `false` coupe toutes les tâches de fond) |
| `LoDb:Accounts` | `SecureCookies` (vrai hors Development) ; `VerificationResend:FreeResends` (5 renvois de l'e-mail de vérification par compte), puis un par `VerificationResend:Interval` (1 j) ; `RefreshTokens:ReuseGrace` (30 s, de 0 à 5 min : le jeton de rafraîchissement qu'un échange vient de remplacer s'échange encore pour la même génération, contre une réponse perdue ou deux échanges simultanés ; au-delà, il révoque sa famille) |
| `LoDb:RateLimiting` | par compte et par heure glissante, jamais par adresse : `CheckoutsPerHour` (10 checkouts de pack ou de plan), `ApiKeyIssuesPerHour` (5 clés créées ou régénérées), `BuildEditsPerHour` (60 builds créés ou remplacés), `VotesPerHour` (120 votes) ; au-delà, `429 rate-limited` |
| `LoDb:Builds:Votes` | `MinimumAccountAge` (1 j : âge du compte avant son premier vote ; l'e-mail vérifié est aussi exigé) |
| `LoDb:Mail` | `Timeout` (`00:00:30`) |
| `LoDb:Outbox` | `BatchSize` (20), `PollInterval` (5 s), `MaxAttempts` (10), `RetryDelay` (30 s), `MaxRetryDelay` (1 h), `Lease` (5 min) ; `HourlyCaps:ConfirmEmail` (100), `HourlyCaps:ResetPassword` (50), `HourlyCaps:ContactNotification` (20) : e-mails de chaque modèle envoyés par heure glissante pour tout le site, l'excédent attend dans la table (jauge `lodb_outbox_deferred`) ; à garder sous le quota du relais |
| `LoDb:Robots` | journal des robots (ADR 0013) : `QueueCapacity` (20000), `BatchSize` (1000), `BatchWindow` (2 s), `HitRetention` (30 j : durée de `robot_hit`, les comptes du jour restent), `UndeclaredThreshold` (40) pages en `UndeclaredWindow` (10 min) d'un même client avant de le tenir pour un robot, pendant `UndeclaredMemory` (24 h), `MaxTrackedClients` (100000), `DnsTimeout` (2 s), `DnsLookupsPerBatch` (32 ; 0 coupe toute recherche, les tests le posent), `DnsParallelism` (8 questions en vol au plus, abandonnées comprises), `DnsCacheSize` (20000), `DnsCacheLifetime` (6 h), `DnsFailureLifetime` (10 min), `JobStatementTimeout` (5 min) |
| `LoDb:Analytics` | `GeoIpDatabase` (aucune), `QueueCapacity` (10000), `BatchSize` (500), `BatchWindow` (2 s), `ClientDataRetention` (30 j : adresse et user-agent des vues stockées avant qu'ils ne le soient plus), `ClientDataEraseDays` (3, au moins 2 : jours lus avant celui du seuil ; deux passages de la tâche quotidienne peuvent être séparés de deux jours après un redémarrage, de trois après un échec ; à relever le temps d'un passage après une panne plus longue), `EventRetentionMonths` (13, plafond CNIL 25), `PartitionsAhead` (7), `MaxVisitorsPerDay` (200000 empreintes par jour, au-delà le jour est marqué `visitorsTruncated` dans `totals`), `MaxRefererHostsPerDay` (200 hôtes référents par jour, les moins comptés au-delà sous `(autres)`), `JobStatementTimeout` (5 min, de 1 s à 1 h : délai d'une requête de l'agrégation quotidienne et de l'effacement des données client, au-dessus des 15 s du rôle de l'API) |
| `LoDb:Egress` | `AllowedHosts` (`ddragon.leagueoflegends.com`, `raw.communitydragon.org`), `FetchConcurrency` (16), `AttemptTimeout` (15 s), `MaxRetryAttempts` (3), `RetryBaseDelay` (1 s), `MaxRedirects` (10), `MaxResponseBytes` (32 Mio) |
| `LoDb:Ingestion` | `WatchPeriod` (10 min), `MaxAttempts` (5), `RetryDelay` (10 min), `LanguageConcurrency` (4), `RecordBatchSize` (64), `SyncConcurrency` (4), `QueueCapacity` (256), `CrawlerRequestsPerMinute` (30) |
| `LoDb:Catalog` | `VersionsLifetime` (1 min), `ImageVerdictsLifetime` (10 min : verdicts d'images lus en base gardés en mémoire, délai avant qu'un `ingest --force` lancé à part se voie), `ImageVerdictVersions` (32 versions gardées), `ArtVariantsPeriod` (30 min : tâche `catalog.art-variants`, qui stocke les variantes WebP de l'art du dernier patch), `ArtVariantsConcurrency` (1 art téléchargé et transcodé à la fois, sous le quota CPU de l'API) |
| `LoDb:SecurityReports` | `MaxBodyBytes` (65536 : corps d'un envoi de rapports CSP, 413 au-delà, compté), `PerClientPerMinute` (30 envois par adresse et par minute, 429 au-delà), `PerMinute` (600 envois par minute, tous clients confondus, refusés avant d'être lus au-delà) |
| `LoDb:Telemetry` | `TraceSampleRatio` (`0.05`, de `0` à `1` : part des traces enregistrées et exportées quand `OTEL_EXPORTER_OTLP_ENDPOINT` est posé, choisie par l'identifiant de trace, jamais par le drapeau `sampled` d'un `traceparent` reçu) |
| `LoDb:PublicApi` | `KeyCacheLifetime` (1 min), `TrendsLifetime` (5 min), `MeteringInterval` (1 s) |
| `LoDb:Prerender` | `RequestTimeout` (30 s, de 5 min au plus : chaque requête au stockage des pages), `OneZoneStorageClass` (`ONEZONE_IA`, la classe des pages ; une autre exige `AllowInsecureEndpoint`, le stockage de développement seul), `PricePerGbMonthOneZone` et `PricePerGbMonthMultiAz` (§ 4.8) |
| `LoDb:Prerender:Pace` | `StepRendersPerMinute` (30 : le pas de montée du rythme adaptatif, posé par les calques du staging et local, § 4.10) ; `HostCpuSet` (aucun : la ligne `cpu` agrégée de `/proc/stat`, tous les CPU de l'hôte ; un banc seul la pose, `0-3` ou `0,2-3`, pour ne compter que les CPU où il épingle la pile, jamais un hôte) ; les seuils des signaux sont des constantes du code, revues avec ses tests |
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
| `LODB_PRERENDER_RENDERS_PER_MINUTE`, `LODB_PRERENDER_RENDER_BURST`, `LODB_PRERENDER_MAX_IN_FLIGHT`, `LODB_PRERENDER_VISITOR_RESERVE` | `30`, `5`, `1`, `1` | `120`, `10`, `2`, `4` |
| `LODB_PRERENDER_PACE_MIN`, `LODB_PRERENDER_PACE_MAX`, `LoDb__Prerender__Pace__StepRendersPerMinute` | `10`, `30`, `10` | `30`, `240`, `30` |
| `web-ssr-prerender` : `cpus`, `cpu_shares` | `0.25`, `64` | `1.0`, `128` |
| `states` (index des états, § 4.12) : `cpu_shares` | `128` | `512` |
| `LoDb__Prerender__AutoEnqueueOnPromotion` (unités du patch remplacé seulement ; la file des états clos tourne partout, § 4.13) | `false` | `false` jusqu'à la fin de la passe complète, puis `true` (`.env.prod.example`) |
| `LoDb__Ingestion__SyncConcurrency`, `LoDb__Ingestion__LanguageConcurrency`, `LoDb__Egress__FetchConcurrency` | `1`, `1`, `1` | `4`, `4`, `16` |
| Sonde de santé de l'API au démarrage | toutes les 10 s pendant 60 s | toutes les 2 s pendant 20 s |

Le couloir épinglé reste strictement sous le total des rendus, sinon le SSR refuse de
démarrer ; la part d'un client (`2` rendus, `6` épinglés par minute) tient dans ces totaux.
Le plafond du rythme adaptatif du staging est son rythme d'avant (`30` rendus par minute,
`1` page en vol) : le régulateur ne fait que le freiner quand l'hôte, qu'il partage avec la
prod, est chargé (§ 4.10). Son SSR de la passe pèse moins que ses autres services et
s'ajoute à leurs 0,75 cœur, sans rien prendre que le CPU libre.
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

### 4.8 Stockage des pages pré-rendues (Scaleway)

Les pages des anciens patchs sont rendues une fois par une passe de l'API, puis gardées dans un
bucket Scaleway Object Storage ([ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md)) ;
nginx les y lit avant de les demander au SSR, qui reste le repli. Mise en place, vérification
depuis l'hôte, retrait d'une epoch et reprise après une politique fautive :
[`tools/prerender/README.md`](../../tools/prerender/README.md).

| | Staging | Prod |
|---|---|---|
| Projet Scaleway et bucket (`fr-par`, privé, sans versionnage) | `lodb-pages-staging` | `lodb-pages-prod` |
| Application d'écriture (`ObjectStorageObjectsWrite`, son projet seul) | `lodb-pages-writer-staging` | `lodb-pages-writer-prod` |
| Cycle de vie | `expire-pages` : toute page effacée après 14 jours | une règle `retire-pages-<N>` par epoch retirée |
| Passe lancée à la promotion d'un patch | non : passes manuelles, sur peu de versions | oui |
| Pages des états clos recalculées à la promotion (§ 4.13) | oui | oui |

Clés des objets : `pages/{epoch}/{locale}/{version}/{ressource}[/{id}-{slug}]` (la page de
patch : `pages/{epoch}/{locale}/{version}/changes`), `build/{nom}`
et `build/media/{nom}` : tout le jeu de fichiers hachés de chaque build stocké, copié avant la
première de ses pages (les modules chargés à la demande ne sont nommés que dans le
JavaScript). L'epoch `0` est réservée aux sondes de `scaleway-bucket.sh verifier`. Chaque page
porte ses en-têtes : `Content-Type`, `Content-Encoding: gzip` et `Cache-Control` (celle de sa
classe, `archived` ou `revisable`) comme en-têtes de l'objet, la CSP, la CSP en rapport seul, `Reporting-Endpoints`, `Link` et le build qui l'a
rendue (`lodb-build`) en métadonnées (`x-amz-meta-*`, 2 Ko au plus).
L'API écrit avec sa clé (`LoDb__Prerender__*`, § 4.2) ; nginx lit sans clé, depuis l'adresse de
l'hôte, que la politique du bucket est seule à admettre.

**Mise en service, par environnement** : le script et sa vérification depuis l'hôte (README,
§ 1 et 2) ; puis, dans `ENV_*`, le bucket et la clé de l'application d'écriture, et un
déploiement : la passe se lance depuis l'administration ; enfin `LODB_PAGES_STORE_ENABLED=1` et
un déploiement : nginx lit le stockage. La facturation du stockage suit le volume, pas le
nombre d'objets (README, § 5).

**Changer l'epoch servie.** Une passe écrit une epoch pendant que nginx sert la précédente.
Quand l'administration la montre prête, poser `LODB_ARCHIVE_SERVE_EPOCH=<N>` dans `ENV_*`
et redéployer : nginx lit `pages/<N>/` (la clé de son cache comprend l'epoch servie, ses copies
de l'ancienne ne servent plus) et le panneau affiche `<N>`. Une epoch est prête quand elle n'a
plus aucun manque : chaque (ancien patch, langue) y est terminé sur tous les types de pages, ou
en échec définitif (version inconnue de Data Dragon, catalogue jamais arrivé), et ces unités
comme les pages en échec des unités terminées seront servies par le SSR ; plusieurs passes
ciblées peuvent la remplir. Le dialogue de bascule de l'administration donne la variable à
poser. Retirer ensuite l'ancienne (README, § 4) : le retrait se prépare depuis la ligne de
l'epoch retirée, une fois la bascule déployée. Un changement visible du chrome, de la CSP ou du contrat `/api` attend ainsi que
sa passe soit complète (§ 4.1). Après un **correctif de sécurité du front** ou une
**compromission du serveur SSR**, basculer tout de suite sur une epoch neuve, avant qu'elle
soit écrite : les pages pas encore écrites repassent par le SSR, borné par sa voie épinglée, le
temps de la passe. Le reste, retrait des bundles du stockage compris, est décrit avec
`LODB_ARCHIVE_EPOCH` (« Monter l'epoch des anciens patchs », § 4.1).

Deux epochs à ne pas confondre : `LODB_ARCHIVE_EPOCH` numérote les clés du cache de nginx,
`LODB_ARCHIVE_SERVE_EPOCH` le préfixe des pages dans le stockage.

**La bascule vers une page indexée par état** (spécification
[`seo-une-page-par-etat.md`](../produit/seo-une-page-par-etat.md), § 4) sert une epoch vide, sans
attendre qu'elle soit prête : relire l'ancienne ferait coexister les deux règles. Au staging,
`LODB_ARCHIVE_SERVE_EPOCH` passe sur une epoch neuve **avant** la fusion dans `dev` qui porte la
règle (ses pages stockées, gardées 14 jours, portent l'ancienne). En prod, dans cet ordre :
annuler au pupitre la passe active de l'epoch `1` ; `LODB_ARCHIVE_SERVE_EPOCH=2` dans
`ENV_PROD`, rien d'autre (`LODB_ARCHIVE_EPOCH` reste à `3`, aucune variable ne règle d'epoch
d'écriture) ; déployer ; puis, au pupitre, une passe `{"pages":"openers"}` sur l'epoch `2`,
puis une passe entière `{}`. Jusqu'à ce qu'elles l'aient remplie, une page absente part au SSR
(budget épinglé, 503 au-delà) et à la file de la demande. Retour arrière : annuler la passe
active de l'epoch `2`, `LODB_ARCHIVE_SERVE_EPOCH=1` et `LODB_ARCHIVE_EPOCH` monté à une valeur
explicite (celle du coffre plus un), puis le revert par le chemin normal. Deux semaines après
la bascule, retirer l'epoch `1` (`SERVED_EPOCH=2 WRITE_EPOCH=2 RETIRED_EPOCHS=1`,
[`tools/prerender/README.md`](../../tools/prerender/README.md), § 4). Toute valeur passe par
`coffre`, puis par `ENV_STAGING` ou `ENV_PROD`.

**Ce que coûte le stockage.** Le panneau « Précalcul du catalogue » (`GET /api/admin/prerender`)
montre ce que le stockage contient, epoch par epoch (servie, en écriture, en attente de bascule,
retirée), les builds copiés et, pour l'epoch servie, chaque version : pages et octets, tirés des
compteurs que la passe tient en écrivant (la clé d'écriture ne liste pas). Le coût mensuel
estimé multiplie ces octets, en Go de 10⁹ octets, par le prix de leur classe, arrondi à quatre
décimales :

| Réglage | Défaut | Classe |
|---|---|---|
| `LoDb__Prerender__PricePerGbMonthOneZone` | `0.00803` | Standard One Zone, celle des pages : 0,000011 € par Go et par heure (tarif du 1ᵉʳ juin 2026), sur 730 heures |
| `LoDb__Prerender__PricePerGbMonthMultiAz` | `0.01606` | Standard Multi-AZ, celle des bundles : le double |

Prix hors TVA, en euros, région `fr-par` ; un prix négatif est refusé au démarrage. Les deux
clés ne sont pas transmises par `compose.deploy.yaml` : les hôtes gardent les défauts, et suivre
un changement de tarif demande d'y ajouter la clé sans valeur, sous `api.environment`, puis de
la poser dans `ENV_*`. L'estimation laisse de côté la sortie (75 Go par mois gratuits, puis
0,01 € par Go), suivie dans Grafana. Une epoch retirée coûte jusqu'à son effacement par le
cycle de vie (9 jours au moins pour 4 millions d'objets), que l'API ne voit pas : sa ligne
garde son coût, mais le total la laisse de côté, pour ne pas surestimer la facture une fois
l'epoch effacée. Sur le staging, dont les pages expirent après 14 jours, les compteurs disent
ce qui a été écrit, pas ce qui reste.

### 4.9 Facture Scaleway dans l'administration

À côté des coûts estimés, les panneaux « Sauvegardes » et « Précalcul du catalogue » montrent
ce que Scaleway facture vraiment pour l'Object Storage de leur projet
(`GET /api/admin/scaleway/bill`) : le mois en cours, les cinq précédents et leurs lignes
(stockage, sortie, requêtes, déductions de l'essai gratuit et des paliers gratuits, en
négatif). Les montants sont hors TVA ; Scaleway ne les découpe que par projet, pas par
bucket : la facture d'un projet est celle de tous ses buckets.

| | Staging | Prod |
|---|---|---|
| Application IAM (`BillingReadOnly`, sur l'organisation, rien d'autre) | `lodb-facturation-lecture` | `lodb-facturation-lecture` |
| Clé (`LoDb__ScalewayBilling__SecretKey` 🔒) | celle du staging | celle de la prod |
| `LoDb__ScalewayBilling__BackupsProjectId` | projet du bucket `sauvegardes-vps` | le même |
| `LoDb__ScalewayBilling__PagesProjectId` | projet `lodb-pages-staging` | projet `lodb-pages-prod` |

L'organisation (`LoDb__ScalewayBilling__OrganizationId`) est la même partout. Les ids se
lisent dans la console Scaleway (paramètres de l'organisation, puis de chaque projet) ; ils
ne sont pas secrets mais restent hors du dépôt, dans `ENV_*`. La clé ne lit que la
facturation : l'API IAM, l'API Account et les buckets lui répondent 403.

**Lecture.** Rien n'est lu au démarrage ni en tâche de fond : l'API interroge
`https://api.scaleway.com/billing/v2beta1/consumptions` (catégorie « Object Storage », par
organisation, chaque page de chaque mois) quand un panneau demande la facture, puis la garde
une heure en mémoire, ou jusqu'au changement de mois (UTC) ; les panneaux qui la demandent en
même temps partagent une lecture. Les six mois sont demandés ensemble et une lecture abandonne
au bout de 20 s (10 s par requête), avant que le panneau ne cesse d'attendre (30 s). Une
lecture qui échoue, quelle qu'en soit la raison (refus, erreur, réponse sans la liste,
délai dépassé), garde la dernière facture lue, marquée périmée avec l'heure de sa lecture, et
la suivante attend 5 minutes ; une ligne `admin.scaleway_bill.unavailable` le signale,
`admin.scaleway_bill.read` chaque lecture réussie. Un redémarrage de l'API relit. Le panneau
date la facture de sa lecture : le champ `updated_at` de Scaleway donne l'heure de la
requête, pas celle des montants, que Scaleway met à jour avec quelques heures de retard. La
clé part dans l'en-tête `X-Auth-Token` de chaque requête, jamais dans une URL ni un journal ;
le client ne suit aucune redirection.

**Mise en service, par environnement** : dans `ENV_*`, la clé de l'environnement et les trois
ids (`.env.*.example`), puis un déploiement. En local, la facture reste éteinte
(`compose.override.yaml` ne pose pas la clé).

### 4.10 Rythme de la passe de précalcul

La passe rend ses pages sur son propre SSR, `web-ssr-prerender` : la même image que `web-ssr`,
au poids CPU le plus bas de l'hôte (`cpu_shares` `128`, `64` au staging), plafonnée
(`cpus` `1.0`, `0.25` au staging), que nginx ne connaît pas. Son rythme suit le CPU libre de
l'hôte : un régulateur de l'API le monte d'un pas par minute tant que tout est calme, le divise
par deux au premier signe (VPS occupé, steal, SSR des visiteurs lent ou sous pression, API
bridée, rendus de la passe ralentis, une mesure illisible) et le retient 5 minutes au rouge.
Signaux, seuils et règles : [spécification](../produit/pre-rendu-catalogue-cdn.md), lot 2,
« Capacité adaptative de la passe ».

| Réglage | Staging | Prod | Rôle |
|---|---|---|---|
| `LODB_PRERENDER_PACE_MODE` | `adaptive` | `adaptive` | `fixed` : le rythme fixe, sans mesure |
| `LODB_PRERENDER_PACE_MIN` | `10` | `30` | plancher, et rythme de départ de chaque passe |
| `LODB_PRERENDER_PACE_MAX` | `30` | `240` | plafond, et budget de la voie du SSR de la passe |
| `LODB_PRERENDER_RENDERS_PER_MINUTE` | `30` | `120` | rythme fixe : mode `fixed`, commande `prerender unit` |
| `LODB_PRERENDER_MANUAL_MAX` | `30` (le plafond) | `4000` | maximum du mode manuel, et budget de la voie de chaque moteur ; le pupitre avertit au-delà de `1300` |
| `LODB_PRERENDER_RENDERERS` | `1` | `2` | moteurs (répliques de `web-ssr-prerender`), lus aussi par l'API |
| `LODB_PRERENDER_RENDERER_CPUS` | `0.25` (fixe) | `1.0` | quota de chaque moteur, et sa capacité au pupitre |
| `LODB_PRERENDER_PAGES_IN_FLIGHT` | `1` (`LODB_PRERENDER_MAX_IN_FLIGHT`) | `24` | pages en vol au plus ; la fenêtre suit le rythme (1,5 s par page) |

**Vitesse réglable** (décision du 2026-10-22). Au pupitre, bloc « Vitesse du précalcul » :
**adaptatif** (le régulateur ci-dessus) ou **manuel**, une cible en pages par minute, de 1 à
`LODB_PRERENDER_MANUAL_MAX`, gardée en base (`prerender_pace_setting`) et appliquée sans
redéploiement : une nouvelle cible tout de suite par l'instance qui répond, un changement de
mode à la décision suivante (30 s au plus) par celle qui écrit la passe. Le mode manuel tient
la cible quelle que soit la charge, sauf **frein d'urgence** : plus aucun rendu pendant 5 min dès
qu'un visiteur du dernier patch est refusé, ou après 3 min au-delà de 90 % de CPU sur le VPS
(steal compris), avec un signal rouge côté visiteurs (boucle d'événements ou pression CPU de
leur SSR, bridage de l'API) ou sans mesure du SSR des visiteurs. Les refus de la voie des anciens patchs
ne freinent pas : elle refuse au-delà de son budget quoi que fasse l'hôte. Le coupe-circuit
`LODB_PRERENDER_PACE_MODE=fixed` passe avant tout réglage (le pupitre le dit et refuse, `409`).
**Au-delà de 1 300 pages/min** (décision du 2026-10-23), le pupitre avertit : le maximum monte à
4 000 sur un hôte pour laisser de la marge, mais le VPS a 4 vCPU et chaque moteur est un thread
Node, un cœur au plus, soit environ 450 pages/min ; deux moteurs tiennent environ 900, un
troisième environ 1 300. Au-dessus, la cible n'est pas atteignable : la vitesse réelle reste
bornée par les moteurs (et par les pages en vol, `LODB_PRERENDER_PAGES_IN_FLIGHT`), et le frein
d'urgence (hôte à 90 % de CPU pendant 3 min) peut couper les nouveaux rendus 5 min. Pour aller
vraiment plus vite, relever `LODB_PRERENDER_RENDERERS` (et les pages en vol). Le seuil est
`LoDb__Prerender__Pace__ManualWarnRendersPerMinute` (1300), affiché, jamais appliqué.
À côté, le pupitre montre le CPU du VPS (steal compté occupé), celui des moteurs, du SSR des
visiteurs et de l'API, lus toutes les 30 s ; le CPU prévu pour la vitesse choisie (pages/min ×
0,1 s de CPU par page pour les moteurs, mesurée sur 10 min dès 60 pages, + 0,03 s pour l'API et
Postgres), la marge restante, la capacité des moteurs, la fin de la passe active et du
catalogue à ce rythme, le stockage ajouté (€/mois) et la part du VPS prise (aucun coût en plus :
le VPS est payé).

**Plusieurs moteurs.** Chaque réplique de `web-ssr-prerender` est un thread Node, un cœur au
plus. L'API résout leur nom commun (`lodb-web-ssr-prerender`, toutes les 30 s) et envoie chaque
rendu à l'adresse la moins occupée ; elle lit la charge de chacune (`/load`) pour le pupitre et
le régulateur (la plus sous pression). Avec un seul moteur, rien ne change. Passer de 2 à 3 :
`LODB_PRERENDER_RENDERERS=3` dans `ENV_PROD`, puis un déploiement ; `LODB_PRERENDER_MANUAL_MAX`
suit (environ 450 pages/min par cœur sur ce VPS).

**Mettre la passe en pause pour un déploiement**, depuis l'hôte :
`docker compose exec -T api dotnet LoDb.Api.dll prerender pause`, puis
`prerender resume` une fois le déploiement fini. Une ligne de synthèse chacune ; code `0`
quand c'est fait ou qu'il n'y a rien à faire, `1` sur un refus (pré-calcul non configuré, passe
qui a bougé, pause choisie au pupitre que `resume` ne lève qu'avec `--any`), `2` sur un argument
inconnu.

**Couper la régulation.** Le levier immédiat est la pause de la passe au pupitre : plus aucune
page ne part, celles en vol finissent. Pour garder la passe à un rythme fixe : la ligne
`LODB_PRERENDER_PACE_MODE=fixed` dans `ENV_*`, puis un déploiement, qui recrée l'API. Le
pupitre dit alors « rythme fixe ».

**Relever le plafond.** Après une semaine de passe sans alerte (`HoteCpuRisqueBridage`,
`HoteCpuSature`, `ConteneurCpuBride`, `ConteneurCpuEnHausseBrutale`,
`LodbSsrRefuseDesVisiteurs`) ni hausse de la latence de l'edge : `LODB_PRERENDER_PACE_MAX=300`
dans `ENV_PROD`, puis un déploiement. Le pas et le plancher ne changent pas.

**À vérifier une fois par hôte** (lecture seule, depuis le VPS) :

- `/proc/stat` d'un conteneur est celui de l'hôte (pas de lxcfs), sinon le régulateur ne voit
  que le conteneur : `docker run --rm alpine grep -c '^cpu' /proc/stat` et
  `grep -c '^cpu' /proc/stat` doivent donner le même nombre ;
- le poids réel du SSR de la passe, que `runc` dérive de `cpu_shares` (5 ou 21 selon sa
  version, contre 39 ou 100 pour `1024`) :
  `docker compose exec -T web-ssr-prerender cat /sys/fs/cgroup/cpu.weight`.

**Ce qui le montre** : le bloc « Vitesse du précalcul » et le relevé « Rythme » du pupitre (droit du moment et sa raison), les
métriques `lodb_prerender_pace_*` ([`metriques.md`](metriques.md)) et les lignes
`prerender.pace.*` de l'API, une par changement d'état (`prerender.pace.setting_applied` à chaque réglage). Le SSR de la passe tombé n'arrête que
la passe, qui l'attend (`prerender.ssr.unreachable`) ; le déploiement ne l'attend pas pour
rendre le site.

### 4.11 File automatique des pages refusées

Chaque page d'ancien patch que le SSR des visiteurs refuse (`503`) entre dans
`prerender_demand`, puis l'API la précalcule d'elle-même dans l'epoch servie, la plus demandée
d'abord, sur le SSR de la passe et à son rythme (§ 4.10) ; sans passe en cours, elle pilote
elle-même le régulateur : du plancher en adaptatif, à la cible de la vitesse manuelle sinon,
sous le même frein. Elle dort sans stockage configuré,
comme la passe. Règles : [spécification](../produit/pre-rendu-catalogue-cdn.md), lot 2,
« File automatique des pages refusées ».

| Réglage | Staging | Prod | Rôle |
|---|---|---|---|
| `LODB_PRERENDER_DEMAND_ENABLED` | `true` | `true` | `false` : l'interrupteur, rien n'entre ni ne se rend |
| `LODB_PRERENDER_DEMAND_SHARE` | `50` | `50` | part (%) des places en vol du SSR de la passe pour la file pendant une passe manuelle ; toutes sans passe |
| `LoDb__Prerender__Demand__RefusedPagesUrl` | fixé | fixé | `http://lodb-web-ssr:9464/refused` (`compose.yaml`) : le port de métriques du SSR des visiteurs, jamais publié |
| `LoDb__Prerender__Demand__RetentionDays` | `30` | `30` | jours sans refus avant qu'une page quitte la table, sauf une page écrite que la file tient dans l'epoch servie (code) |
| `LoDb__Prerender__Demand__MaxPendingPages` | `100000` | `100000` | pages en attente gardées au plus, les plus demandées (code) |
| `LoDb__Prerender__Demand__MaxAttempts` | `3` | `3` | essais ratés avant l'échec définitif d'une page (code) |

**Couper la file.** `LODB_PRERENDER_DEMAND_ENABLED=false` dans `ENV_*`, puis un déploiement,
qui recrée l'API : le relevé et le rendu s'arrêtent, la table reste. Le pupitre dit alors
« coupée ». Les pages en vol finissent.

**Ce qui la montre** : la vue « Files automatiques » de `/admin/prerender?view=queues` (bloc
« File automatique des pages refusées » et liste « Pages de la file des refusées (503) »),
les métriques `lodb_prerender_demand_*` ([`metriques.md`](metriques.md)) et les lignes
`prerender.demand.*` de l'API, une par relevé qui apporte des pages et une par lot rendu.

### 4.12 Index des états d'entité

La canonique des fiches, les sitemaps des anciens patchs, les pages de patch, IndexNow et la
file des états clos (§ 4.13) lisent l'index des états d'entité, en Postgres
([spécification](../produit/seo-une-page-par-etat.md), § 3.2 et § 4). Aucune ligne du `.env`
ne le règle : il dépend du code (schéma d'empreinte) et des jeux en_US stockés.

**Le service `states`** (`compose.yaml`, surchargé par `compose.deploy.yaml`) : l'image de
l'API, commande `catalog states build`, `lodb_app` (`Application Name=lodb-states`, mot de
passe `LODB_APP_DB_PASSWORD` en fichier sur un hôte), `ddragon` en lecture seule, réseaux
`default` et `data`, `restart: "no"`, après `migrate` ; `api` attend sa réussite
(`service_completed_successfully`). Le déploiement le lance juste après `migrate`, avant la page
de maintenance (`docker compose run --rm --no-deps states`) ; son passage dans `up` ne fait
plus rien. Ressources : § 4.3 ; poids au staging : § 4.5.

**La commande** `catalog states build [--rebuild] [--ingest-missing]`, dans l'image de l'API
(un réglage d'hôte s'écrit `--clé=valeur`, jamais juste après une option) :

| Situation | Ce qu'elle fait |
|---|---|
| La génération courante du schéma compilé couvre déjà jusqu'au dernier patch promu | rien |
| Seule la fin de la couverture a avancé | elle étend la génération courante |
| Aucune génération courante, début de la couverture reculé, ou `--rebuild` | elle construit une génération neuve, la rend courante en une transaction, puis supprime l'ancienne |
| Une version listée après la fin de la couverture manque de son en_US (la promotion qui l'a sautée n'a pas pu l'ingérer) | rien : la génération courante reste, en retard (`states.coverage.interrupted`, Warning), jamais une plus courte à sa place ; la promotion suivante ou la réconciliation ingère la version et l'étend |

Elle lit les jeux en_US stockés, un catalogue à la fois, une transaction par version, et
reprend une construction interrompue. Elle ne télécharge aucun jeu, sauf avec
`--ingest-missing`, qui ingère d'abord l'en_US de chaque version listée qui en manque ; elle
lit en revanche la liste des versions de Data Dragon dès qu'une version est promue.

| Code | Cas |
|---|---|
| `0` | index à jour (rien, étendu ou construit) ; ou liste de Data Dragon illisible sans `--ingest-missing` alors qu'une génération est courante (`states.build.deferred`, Warning) : l'API sert l'index en retard jusqu'à sa réconciliation |
| `1` | liste illisible et aucune génération courante (`states.build.failed`, Error : premier déploiement, nouveau schéma d'empreinte), verrou `catalog:states` tenu par un autre écrivain pendant une minute, ou tout autre échec (`cli.command.failed`), liste illisible avec `--ingest-missing` compris |
| `2` | arguments invalides |

**Rattrapages.** La promotion d'un patch étend l'index en premier (étape `StateIndex`), en
ingérant l'en_US des versions listées après la fin de la couverture qui en manquent. La tâche
`catalog.states.reconcile` (toutes les 10 min, une instance à la fois, coupée par
`LoDb:Workers:Enabled=false`) fait de même, et construit l'index s'il n'y en a aucun ; aucun
passage n'est garanti au démarrage. Seule la commande reconstruit quand le début de la
couverture recule, en général au déploiement suivant.

**Copie en mémoire** : chaque processus de l'API tient la sienne, relit son repère au plus
toutes les `LoDb:Catalog:VersionsLifetime` (1 min) et la recharge quand la génération ou la fin
de couverture a changé. Sans copie et base injoignable : 503 `state-index-unavailable`
(§ « Index des états » de [`pieges.md`](../architecture/pieges.md)).

**Vérifier la couverture avant une bascule** (spécification, § 4.2 et 4.3) : chaque version
listée doit avoir ses jeux en_US stockés, sinon la couverture commence après le trou. Ingérer
ceux qui manquent, version par version (`--latest` est plafonné à 50) :

```bash
docker compose exec -T api dotnet LoDb.Api.dll ingest --version <v> --languages en_US
# ou, toutes les versions listées d'un coup, dans un conteneur qui écrit ddragon :
docker compose run --rm --no-deps api catalog states build --ingest-missing
```

**Reconstruire** après un changement de mapper qui modifie les entrées stockées (en_US
réingéré de toutes les versions d'abord) : `docker compose run --rm --no-deps states catalog
states build --rebuild`. Un nouveau schéma d'empreinte (`FingerprintScheme.Current`) se
construit de lui-même au déploiement qui le porte ; ses pages se recalculent ensuite (§ 4.13).

**Ce qui le montre** : la ligne `states.update.completed` de chaque mise à jour (déclencheur,
issue, début et fin de la couverture, états ouverts et clos, comptes par ressource), les
métriques `lodb_states_*` ([`metriques.md`](metriques.md)) et, au pupitre `/admin/prerender`,
les ouvreurs, pages figées et révisables attendus par version.

### 4.13 Pages révisables et file des états clos

Une page d'ancien patch dont l'état est clos est **figée** : classe `archived`, gardée un an.
Celle d'une version de l'état encore ouvert est **révisable** : classe `revisable`
(`public, max-age=3600, s-maxage=3600, stale-while-revalidate=3600`), puisque sa canonique,
son titre et son historique changent quand une promotion clôt l'état. Le SSR choisit la
classe d'une fiche selon l'état que rend l'API (une fiche épinglée sans état connu est
`revisable`, jamais `archived`) ; listes, accueil et pages de patch gardent la règle par
version. La passe, la file de la demande et cette file stockent les deux classes ; nginx garde
chaque page autant que dit son objet (§ 4.8). Aucune ligne du `.env` ne règle ce qui suit.

| Élément | Valeur |
|---|---|
| Producteur | l'écouteur de l'étape `Prerender` de chaque promotion, puis `prerender.reconcile` (toutes les 30 min), une fois l'index des états à jour jusqu'au dernier patch ; idempotent par une marque (`prerender_revision_watermark` : génération, dernière clôture traitée, fin de la couverture lue) ; sur une génération reconstruite, les clôtures d'après cette couverture, quelle que soit leur date |
| Pages mises en file | chaque version, de l'ouvreur à la dernière, de chaque état clos depuis la marque (la version qui quitte le dernier patch comprise), dans les 21 langues, sous la graphie de chaque version |
| Epochs | l'epoch servie (`LoDb__Prerender__ServedEpoch`), celle qu'écrit la passe active quand elle diffère, et toute epoch plus récente qu'une passe terminée a remplie (en attente de sa bascule) ; jamais celle d'une passe annulée |
| Délai avant rendu | `2 × LoDb:Catalog:VersionsLifetime` + 1 min (21 min) : tous les processus lisent alors un index qui couvre la clôture |
| Rendu | `PrerenderRevisionWorker`, toutes les 30 s, verrou `prerender:revision`, lots de 20, sur le SSR de la passe, au rythme et dans les places de la file de la demande (§ 4.11) |
| Essais | 3 ; nouvel essai 5 min après ; une page rendue de nouveau `revisable` compte comme un essai raté ; les lignes d'une epoch retirée sont sautées |
| Condition | le stockage et la passe configurés (§ 4.8), **quel que soit** `LoDb__Prerender__AutoEnqueueOnPromotion` : staging compris |

Au plus ~24 000 pages par promotion (spécification, § 1.2, un majorant), ~27 minutes à
900 pages par minute. Une génération neuve de l'index (reconstruction) n'est pas mise en
file : ses clôtures sont de l'historique, que les passes rendent ; seules celles de la
dernière promotion le sont (`prerender.revision.generation`).

**Passe « ouvreurs d'abord ».** Le formulaire de lancement du pupitre propose « Pages » :
toutes, ou les ouvreurs (`POST /api/admin/prerender/passes` avec `"pages": "openers"`, gardé
`{"pages":"openers"}`) : chaque unité rend la page de patch et les fiches qui ouvrent un état à
sa version, ouvert ou clos, lues dans l'index. Elle ne complète jamais une epoch et aucune
promotion ne la rejoint (les unités d'un patch promu attendent sa fin,
`prerender.promotion.deferred`) : une passe entière `{}` la suit. Avant un retour arrière vers
une image d'avant la règle par état, l'annuler : cette image lirait son périmètre comme toutes
les pages.

**Nouveau schéma d'empreinte** (spécification, § 3.7) : l'index se reconstruit au déploiement
qui le porte (§ 4.12). Peu de pages changent d'état : les recalculer dans l'epoch servie, puis
monter `LODB_ARCHIVE_EPOCH` (§ 4.1). Beaucoup : une passe sur une epoch neuve, puis la bascule
de `LODB_ARCHIVE_SERVE_EPOCH` (§ 4.8). Une page figée ne l'est que pour un schéma.

Une epoch qu'une passe terminée a remplie, plus récente que l'epoch servie, reçoit les pages
des états clos jusqu'à sa bascule : rien à rattraper avant de basculer. Une epoch laissée en
attente sans bascule les reçoit aussi, pour rien : basculer dessus, ou la retirer
(`tools/prerender/scaleway-bucket.sh`).

**Ce qui la montre** : le bloc « File des états clos » de la vue « Files automatiques » de
`/admin/prerender` (en attente, dues, écrites en 1 h et 24 h, en échec, par epoch ;
`GET /api/admin/prerender/revision`) et la colonne « Index » de la vue « Couverture » (ouvreurs,
pages figées et révisables attendues
par version et par langue) ; les métriques `lodb_prerender_revision_*`
([`metriques.md`](metriques.md)) et les lignes `prerender.revision.*` de l'API, une par passage
du producteur qui met en file et une par lot rendu.

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
| `archive-cache` | nouveau | Cache des pages des anciens patchs de nginx, clé sans révision : il survit aux déploiements (`LODB_ARCHIVE_EPOCH`). Les seuls rendus du SSR, pages que le stockage objet n'a pas (§ 4.8), 2 Go au plus. |
| `assets-cache` | nouveau | Cache des bundles de `/build/` de nginx, clé = nom du fichier : un déploiement le trouve chaud. |
| `seo-cache`, `legacy-cache` | nouveaux | Caches de nginx des fichiers pour les robots (sitemaps, `robots.txt`, `llms.txt`) et des 301 de l'ancien site, clé préfixée par la révision : un déploiement repart de zéro, les entrées précédentes s'éteignent (7 jours, 1 heure). |
| `nginx-temp` | nouveau | Fichiers temporaires de nginx : corps de requête et réponses plus grands que ses tampons, que le client n'a pas encore lus. Chacun est supprimé dès son ouverture : le volume ne contient que ce qui sert. |
| `front-builds` | nouveau | Bundles hachés des builds successifs : `web-ssr` y ajoute les siens au démarrage et rafraîchit leur date toutes les heures, `/build/` y trouve ceux des builds précédents ; le job supprime ceux qu'aucun build en service n'a livrés depuis `LODB_FRONT_BUILDS_RETENTION_DAYS` jours (400 par défaut, 395 au moins, § 4). |
| `storage`, `app_state` | ancienne stack, **laissés intacts** | Non montés : ni réutilisés (`storage` appartient à `www-data`, l'API ne pourrait pas y écrire) ni supprimés (retour arrière, reprise des agrégats et de l'audit). Supprimés à la décommission, après export. |

## 6. Obtenir les valeurs

### 6.1 DNS

Rien à changer : `staging` et `prod` servent les domaines de l'ancienne stack, qui
pointent déjà vers l'hôte. Pour un nouveau nom, enregistrements A (et AAAA) vers l'hôte
avant le déploiement : le job contrôle le TLS public.

### 6.2 Certificat Data Protection (un par environnement)

Ceux de `staging` et de `prod` existent depuis la bascule : ces commandes ne servent qu'à un
nouvel environnement, jamais à remplacer un certificat servi.

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
`charles-lindecker.com` le fait pour le relais de Hostinger (SPF
`include:_spf.mail.hostinger.com`, DKIM `hostingermail1`, DMARC `p=none`) ;
`league-of-data-base.com` ne publie aucun de ces enregistrements : un e-mail envoyé en son nom
serait refusé ou classé indésirable.

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
`compose.override.yaml` ajoute `ASPNETCORE_ENVIRONMENT=Development`, Mailpit
(`LoDb__Mail__Host=mailpit`, e-mails sur http://localhost:18025) et le stockage simulé des
pages pré-rendues (`pages-store`, alias `lodb-pages-store`, sans port publié : moto, l'API S3
en mémoire, sans vérification de signature). Il y crée le bucket `lodb-pages-dev`, lisible par
tous sous `pages/` et `build/`, à chaque démarrage : un redémarrage le vide. L'API y écrit
avec la clé fixe `lodb-dev` (`LoDb__Prerender__*`, dont `AllowInsecureEndpoint=true`, propre
à ce calque, pour son point d'accès en `http`), nginx l'y lit (`LODB_PAGES_STORE_*`), et
aucune passe ne part d'elle-même. La passe y a son SSR, `web-ssr-prerender`, sans quota, et
un rythme adaptatif plus large (`120` à `1200` rendus par minute, pas de `120`), pour voir le
régulateur monter en quelques minutes ; ses signaux sont ceux de la VM Docker. L'API s'y connecte aussi
comme `lodb_app`, mot de passe `lodb-app` (`migrate` le pose), et `migrate` comme `lodb`/`lodb` :
`psql` sur le port publié (15432) se connecte avec l'un ou l'autre. Un `.env` racine ne sert
qu'à surcharger un port ou une variable ; ne jamais y copier `.env.staging` ni `.env.prod`.
Ports et emplacements : [`developpement.md`](developpement.md).

L'ancienne stack archivée a son propre `.env`, `legacy/.env` (modèle
`legacy/.env.example`), lu par `docker compose --project-directory legacy …`.

## 8. Écarts connus

- **Pays des visiteurs** : aucun fichier GeoLite2 n'est monté et ni
  `LoDb__Analytics__GeoIpDatabase` ni `GEOIP_DB_PATH` ne sont transmis sur un hôte. Les
  vues de prod n'ont pas de pays, contrairement à l'ancienne stack.
- **Connexion Google sur Android** : aucun fichier d'environnement du front ne définit
  `googleClientId`. La connexion reste coupée dans l'app, quels que soient les
  `AppClients` du serveur.
- **Releases des apps** : aucun job de `ci.yml` n'appelle les workflows de release
  (« contrat d'appel » des guides, non câblé). Elles se lancent à la main.
- **Réglages internes** : en prod, aucun n'est réglable depuis le `.env` sans modifier
  `compose.deploy.yaml` (§ 4.3, encadré) ; le staging en reçoit trois de son `.env`
  (`compose.staging.yaml`, § 4.5).
