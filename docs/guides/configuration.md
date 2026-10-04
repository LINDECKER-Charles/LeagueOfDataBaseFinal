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
| Secrets de dépôt `STAGING_*`, `PROD_*` | Connexion SSH et dossier de l'hôte, **ceux de l'ancienne stack, réutilisés tels quels** ; certificat Data Protection (nouveau) | déjà en place, sauf le certificat |
| Secrets de dépôt `ENV_STAGING`, `ENV_PROD` | Le `.env` complet de l'environnement : **contenu remplacé** par celui de la nouvelle stack | l'exploitant (*Settings ▸ Secrets*) |
| Environnements GitHub `desktop-release`, `android-release`, variables de dépôt | Clés de signature des apps, drapeaux de release | l'exploitant |
| `.env` de l'hôte (`$STAGING_PATH/.env`, `$PROD_PATH/.env`) | Variables Compose et réglages de l'API (§ 4) | le job, depuis `ENV_STAGING` ou `ENV_PROD` |
| `.deploy/data-protection.pfx` de l'hôte | Certificat Data Protection | le job, depuis `STAGING_DATA_PROTECTION_PFX` ou `PROD_DATA_PROTECTION_PFX` |
| `.deploy/android/` de l'hôte | `latest.json`, `assetlinks.json` | l'exploitant, à chaque release Android |
| Services externes | DNS, SMTP, Stripe, Google, Play, Apple, Azure (§ 6) | l'exploitant |
| Poste de dev | rien : tout a une valeur par défaut (§ 7) | — |

Modèles versionnés : [`.env.staging.example`](../../.env.staging.example) et
[`.env.prod.example`](../../.env.prod.example). Remplis, ils deviennent `.env.staging` et
`.env.prod` à la racine du dépôt (ignorés par Git), dont le contenu est collé tel quel dans
`ENV_STAGING` et `ENV_PROD`.

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

## 3. GitHub

### 3.1 Environnements et packages

Les déploiements n'utilisent **aucun environnement GitHub** : comme pour l'ancienne stack,
ils lisent des secrets de dépôt, et la garde de la prod est la fusion manuelle de `test`
dans `main`. Seules les releases des apps ont leurs environnements.

| Environnement | Création | Protection | Contenu |
|---|---|---|---|
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
`DEPLOY_PATH`, `SSH_USER`, `ENV_FILE`, `DATA_PROTECTION_PFX`).

| `staging` | `prod` | Statut | Rôle |
|---|---|---|---|
| `STAGING_SSH_KEY` 🔒 | `PROD_SSH_KEY` 🔒 | existant | Clé privée SSH de déploiement. |
| `STAGING_HOST` | `PROD_HOST` | existant | IP ou FQDN de l'hôte. |
| `STAGING_PATH` | `PROD_PATH` | existant | Dossier du dépôt sur l'hôte, celui de l'ancienne stack : la nouvelle y prend sa place. |
| `STAGING_SSH_USER` | `PROD_SSH_USER` | existant | Utilisateur SSH (➖, `root` par défaut). |
| `ENV_STAGING` 🔒 | `ENV_PROD` 🔒 | **à remplacer** | Contenu complet de `.env.staging` / `.env.prod` (§ 4). |
| `STAGING_DATA_PROTECTION_PFX` 🔒 | `PROD_DATA_PROTECTION_PFX` 🔒 | **nouveau** | Le `.pfx` de l'environnement en base64, sur une ligne (§ 6.2). Celui de la prod ne change plus une fois servi : les clés déjà chiffrées deviendraient illisibles (sessions et jetons perdus). |

Un secret absent arrive vide : la première étape de `_deploy.yml` les vérifie et échoue en
nommant ceux qui manquent, avant toute connexion à l'hôte.

### 3.3 Garde de la prod

`main` n'est atteint que par une fusion manuelle de `test` : c'est la validation humaine
qui met en prod, sans rebuild ni nouveau passage des checks. Les secrets `PROD_*` étant des
secrets de dépôt, tout workflow du dépôt peut les lire, comme avant la bascule.

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

Le secret `ENV_STAGING` ou `ENV_PROD` est le fichier complet, une variable par ligne. Le job
l'écrit dans `$STAGING_PATH/.env` ou `$PROD_PATH/.env` (mode 600) à chaque déploiement.

**Pièges du fichier :**

- Pas de ligne `LoDb__*` vide : la commenter. Présente mais vide, elle est transmise à
  l'API comme une valeur vide, que certains réglages refusent au démarrage.
- Une valeur qui contient `$` se met entre apostrophes (`'…'`) : Compose l'interpolerait.
- La chaîne de connexion n'est pas quotée : un mot de passe de base qui contient `;` la
  casse (le changer d'abord, `ALTER ROLE`).

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
| `LODB_NOINDEX` ⚙️ | `1` | `0` | ✅ | `X-Robots-Tag: noindex, nofollow` ; le smoke test le vérifie. |
| `LODB_DB_NAME`, `LODB_DB_USER` | 🔁 `POSTGRES_DB`, `POSTGRES_USER` | idem | ✅ | La base existe déjà dans `pgdata`, créée avec ces identifiants ; chaîne de connexion de `api` et `migrate`. |
| `LODB_DB_PASSWORD` 🔒 | 🔁 `POSTGRES_PASSWORD` | idem | ✅ | Idem ; sans `;`. |
| `LODB_DATA_PROTECTION_CERT_FILE`, `LODB_ANDROID_DIR` | défauts | défauts | — | Ne pas définir : le job écrit aux emplacements par défaut (§ 5). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | ➖ | ➖ | ➖ | Collecteur de traces OTLP, quand `infra-vps` en expose un. |

Chaque environnement a son propre service `postgres` dans son projet, comme avant.
`CONTACT_RECIPIENT` et `PUBLIC_API_BASE_URL` n'ont d'effet qu'en local : sur un
environnement servi, `LoDb__Contact__Recipient` et `LODB_PUBLIC_API_ORIGIN` les remplacent.

### 4.2 Réglages de l'API transmis par le `.env`

Nom exact de la configuration, casse comprise. Absents du `.env`, ils ne sont pas transmis
et l'API garde sa valeur par défaut.

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
| `LoDb__Accounts__Google__AppClients__N__ClientSecret` | desktop | Secret du client desktop (livré avec l'app, donc public) ; aucun pour Android. |
| `LoDb__Contact__Recipient` | ➖ | Destinataire des messages de contact. Absent : stockés pour l'admin, sans notification. |
| `LoDb__Contact__NotificationLocale` | ➖ | Langue de la notification, `Fr` par défaut (`Ar`… `ZhHant`). Une valeur invalide ne se voit qu'au premier message. |
| `LoDb__Analytics__VisitorKey` 🔒 | recommandé | Clé du hachage des visiteurs. Absente : une clé aléatoire par processus, les visiteurs sont recomptés à chaque redémarrage. |

### 4.3 Fixés par les fichiers compose (hors du `.env`)

`ConnectionStrings__LoDb` (assemblée depuis `LODB_DB_*`, hôte `postgres`),
`LoDb__Accounts__SiteOrigin`, `LoDb__Seo__CanonicalOrigin` et `LoDb__PublicApi__SiteOrigin`
(`https://` + `LODB_CANONICAL_HOST`), `LoDb__PublicApi__Reference__BaseUrl`
(`LODB_PUBLIC_API_ORIGIN`), `LoDb__DataProtection__CertificatePath`,
`LoDb__Hosting__KnownProxyNetworks__0..2`, `LoDb__Storage__Root` (`/srv/storage`, volume
`ddragon`, que nginx sert), et côté SSR `LODB_API_ORIGIN`, `LODB_TRUST_PROXY_HEADERS`,
`NODE_OPTIONS`. `ASPNETCORE_ENVIRONMENT` reste **absente** sur un hôte : `Development`
exposerait OpenAPI et retirerait `Secure` des cookies.

<details>
<summary>Réglages internes de l'API, avec leur défaut (non transmis par compose)</summary>

Pour en changer un sur un hôte, l'ajouter sous `services.api.environment` de
`compose.deploy.yaml`, puis dans le `.env`.

| Section | Clés (défaut) |
|---|---|
| `LoDb:Hosting` | `HttpPort` (8080, attendu par nginx et le SSR), `MetricsPort` (9464, jamais publié) |
| `LoDb:Workers` | `Enabled` (`true` ; seul `false` coupe toutes les tâches de fond) |
| `LoDb:Accounts` | `SecureCookies` (vrai hors Development) |
| `LoDb:Mail` | `Timeout` (`00:00:30`) |
| `LoDb:Outbox` | `BatchSize` (20), `PollInterval` (5 s), `MaxAttempts` (10), `RetryDelay` (30 s), `MaxRetryDelay` (1 h), `Lease` (5 min) |
| `LoDb:Analytics` | `GeoIpDatabase` (aucune), `QueueCapacity` (10000), `BatchSize` (500), `BatchWindow` (2 s), `ClientDataRetention` (30 j), `EventRetentionMonths` (13, plafond CNIL 25), `PartitionsAhead` (7) |
| `LoDb:Egress` | `AllowedHosts` (`ddragon.leagueoflegends.com`, `raw.communitydragon.org`), `FetchConcurrency` (16), `AttemptTimeout` (15 s), `MaxRetryAttempts` (3), `RetryBaseDelay` (1 s), `MaxRedirects` (10), `MaxResponseBytes` (32 Mio) |
| `LoDb:Ingestion` | `WatchPeriod` (10 min), `MaxAttempts` (5), `RetryDelay` (10 min), `LanguageConcurrency` (4), `RecordBatchSize` (64), `SyncConcurrency` (4), `QueueCapacity` (256), `CrawlerRequestsPerMinute` (30) |
| `LoDb:Catalog` | `MaxEntries` (16), `VersionsLifetime` (1 min) |
| `LoDb:PublicApi` | `KeyCacheLifetime` (1 min), `TrendsLifetime` (5 min), `MeteringInterval` (1 s) |

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
| `ADMIN_LOGIN`, `ADMIN_PASSWORD` | aucune : chaque administrateur reçoit le rôle par `api admin create --email …`, TOTP à la première connexion ([runbook](../reecriture/bascule.md)) |
| `GEOIP_DB_PATH` | aucune sur un hôte (§ 8) |
| `HTTP_PORT`, `MAILPIT_UI_PORT` | aucune : ports locaux de l'ancienne stack |

## 5. Fichiers et volumes de l'hôte

| Chemin, dans `$STAGING_PATH` ou `$PROD_PATH` | Écrit par | Rôle |
|---|---|---|
| dépôt (branche `test` ou `main`) | le job (`git reset --hard`) | Fichiers compose à la racine. |
| `.env` | le job (`ENV_STAGING`, `ENV_PROD`, plus `LODB_EDGE_CIDR`), mode 600 | § 4. |
| `.deploy/data-protection.pfx` | le job (`*_DATA_PROTECTION_PFX`) | Monté en secret dans `api`. Dossier en 700, fichier en 644 : l'utilisateur non root de l'API le lit. Absent, la stack ne démarre pas. |
| `.deploy/android/` | l'exploitant, à chaque release Android | `latest.json` (asset `lodb-android-latest.json`) et `assetlinks.json` ([`release-android.md`](release-android.md)). Vide : les deux URL répondent 404. |

Volumes du projet (`lodb-staging_…`, `lodb-prod_…`) :

| Volume | Origine | Rôle |
|---|---|---|
| `pgdata` | **repris** de l'ancienne stack | La base, mise à jour par `migrate` (une base Doctrine est d'abord marquée à `Baseline`). |
| `ddragon` | nouveau, vide au premier déploiement | Blobs Data Dragon, remplis par l'ingestion. |
| `pages-cache` | nouveau | Cache des pages de nginx. |
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

### 6.3 Clé SSH de déploiement

Aucune à créer : `STAGING_SSH_KEY` et `PROD_SSH_KEY`, celles de l'ancienne stack, sont déjà
autorisées sur l'hôte. Seul un nouvel hôte a besoin de la sienne :

```bash
ssh-keygen -t ed25519 -N '' -C 'lodb-staging deploy' -f lodb-staging-deploy
```

La clé publique (`.pub`) va dans `~/.ssh/authorized_keys` de l'utilisateur de déploiement
sur cet hôte ; la clé privée, complète, dans `STAGING_SSH_KEY` ou `PROD_SSH_KEY`.

### 6.4 SMTP

Mêmes identifiants que le `MAILER_DSN` de l'ancienne stack du même environnement (§ 4.4).
Le domaine de `LoDb__Mail__From` doit autoriser le relais : SPF, DKIM et DMARC.

### 6.5 Stripe

| | `staging` | `prod` |
|---|---|---|
| Clés | mode test (`sk_test_…`) 🔁 | mode live (`sk_live_…`) 🔁 |
| Endpoint | `https://test.league-of-data-base.com/webhooks/stripe`, celui de l'ancien staging 🔁 | l'endpoint **existant**, `https://league-of-data-base.com/webhooks/stripe`, inchangé 🔁 |
| Événements | `checkout.session.completed`, `customer.subscription.deleted` | idem |

Le secret de signature (`whsec_…`) est celui de l'endpoint de chaque environnement.

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

### 6.7 Sous-réseau de l'edge

Rien à relever. Docker choisit ce sous-réseau quand `infra-vps` crée le réseau : à chaque
déploiement, le job le lit (`docker network inspect edge`) et écrit `LODB_EDGE_CIDR` dans
le `.env` de l'hôte, qui sert aussi aux commandes `docker compose` lancées à la main depuis
ce dossier. Une ligne `LODB_EDGE_CIDR` venue de `ENV_*` est remplacée.

## 7. En local

Aucun `.env` n'est nécessaire : `compose.yaml` donne une valeur par défaut à tout, et
`compose.override.yaml` ajoute `ASPNETCORE_ENVIRONMENT=Development` et Mailpit
(`LoDb__Mail__Host=mailpit`, e-mails sur http://localhost:18025). Un `.env` racine ne sert
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
