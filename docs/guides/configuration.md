# Configuration de la nouvelle stack : secrets et variables

Inventaire complet de ce qu'il faut configurer pour faire tourner la nouvelle stack (.NET +
Angular) en local, sur `preprod` et en production, et pour publier les apps. Le déroulé du
pipeline est dans [`github-actions-secrets.md`](github-actions-secrets.md), celui de la
bascule dans le [runbook](../reecriture/bascule.md). La configuration de l'ancienne stack
est archivée dans [`legacy/docs/guides/configuration.md`](../../legacy/docs/guides/configuration.md).

Légende : ✅ requis · ➖ optionnel · 🔒 secret · ⚙️ contrôlé par le job de déploiement avant
toute modification de l'hôte · 🔁 repris de l'ancienne stack (`ENV_PROD`).

## 1. Où vit chaque réglage

| Où | Contenu | Écrit par |
|---|---|---|
| Secrets de dépôt de l'ancienne stack | Connexion SSH au VPS : `PROD_SSH_KEY`, `PROD_HOST`, `PROD_SSH_USER`, **réutilisés tels quels** | déjà en place |
| Environnements GitHub `preprod`, `production` | Par environnement : son `.env` et son certificat (2 secrets) | l'exploitant (*Settings ▸ Environments*) |
| Environnements GitHub `desktop-release`, `android-release`, variables de dépôt | Clés de signature des apps, drapeaux de release | l'exploitant |
| `.env` de l'hôte (`/opt/lodb-preprod/.env`, `/opt/lodb-production/.env`) | Variables Compose et réglages de l'API (§ 4) | le job, depuis `ENV_FILE` ou `ENV_FILE` |
| `.deploy/data-protection.pfx` de l'hôte | Certificat Data Protection | le job, depuis `*_DATA_PROTECTION_PFX` |
| `.deploy/android/` de l'hôte | `latest.json`, `assetlinks.json` | l'exploitant, à chaque release Android |
| Services externes | DNS, SMTP, Stripe, Google, Play, Apple, Azure (§ 6) | l'exploitant |
| Poste de dev | rien : tout a une valeur par défaut (§ 7) | — |

Modèles prêts à remplir : [`.env.preprod.example`](../../.env.preprod.example) (`ENV_FILE`) et
[`.env.production.example`](../../.env.production.example) (`ENV_FILE`).

## 2. Mise en place, dans l'ordre

**`preprod`** (préproduction, déployée à chaque push de `dev`) :

1. Hôte prêt : Docker Engine et `docker compose`, `infra-vps` déployé (edge Caddy, réseaux
   `edge` et `observability`), ports 80/443 ouverts, `docker login ghcr.io` persistant si
   les packages GHCR sont privés (PAT `read:packages`).
2. DNS de `CADDY_DOMAINS` et `API_CADDY_DOMAINS` pointés vers l'hôte (§ 6.1).
3. Certificat Data Protection de `preprod` (§ 6.2). La connexion SSH réutilise les secrets de
   l'ancienne stack (§ 3.2) : rien à générer.
4. `.env.preprod.example` rempli ; son contenu devient le secret `ENV_FILE`.
5. `ENV_FILE` et `DATA_PROTECTION_PFX` dans l'environnement GitHub `preprod` (§ 3.2).
6. Dump anonymisé restauré dans le Postgres de `preprod` avant le premier `migrate`
   ([`github-actions-secrets.md`](github-actions-secrets.md), « Avant le premier
   déploiement de `preprod` »).
7. Push de `dev` : build, puis déploiement de `preprod`.

**Production** (à J-3 de la bascule, [runbook](../reecriture/bascule.md) § 1) :

1. Environnement GitHub `production` créé : *required reviewers*, *deployment branches*
   limitée à `dev`.
2. Certificat Data Protection **propre à la prod** (§ 6.2), jamais celui de `preprod`.
3. `.env.production.example` rempli avec les valeurs 🔁 de l'ancienne prod (§ 4.4) ; son
   contenu devient `ENV_FILE`.
4. `ENV_FILE` et `DATA_PROTECTION_PFX` dans l'environnement `production`
   (§ 3.2).
5. SMTP, Stripe, Google : § 6.4 à 6.6.

## 3. GitHub

### 3.1 Environnements

| Environnement | Création | Protection | Contenu |
|---|---|---|---|
| `preprod` | à la main, ou automatique au premier run | aucune | `ENV_FILE`, `DATA_PROTECTION_PFX` |
| `production` | **à la main, avant la première promotion** | *required reviewers* ; *deployment branches* : `dev` | `ENV_FILE`, `DATA_PROTECTION_PFX` (jamais en secrets de dépôt) |
| `desktop-release` | à la main | branche des workflows lancés | secrets et variables du desktop (§ 3.4) |
| `android-release` | à la main | branche `main` ([`release-android.md`](release-android.md)) | secrets d'Android (§ 3.4) |

`GITHUB_TOKEN` est fourni par GitHub : la CI, le build des images et le retag n'utilisent
aucun autre secret.

### 3.2 Secrets de déploiement

**Repris de l'ancienne stack, rien à créer.** Les deux stacks tournent sur le même VPS : le
job de déploiement lit les secrets de dépôt existants, pour `preprod` comme pour la prod.

| Secret de dépôt | Rôle |
|---|---|
| `PROD_SSH_KEY` 🔒 | Clé privée SSH de déploiement. |
| `PROD_HOST` | IP ou FQDN du VPS. |
| `PROD_SSH_USER` | Utilisateur SSH (➖, `root` par défaut). |

**Nouveaux : deux par environnement**, leur contenu n'existe pas dans l'ancienne stack.

| Environnement | Secret | Valeur |
|---|---|---|
| `preprod` | `ENV_FILE` 🔒 | Contenu complet de `.env.preprod.example` rempli (§ 4). |
| `preprod` | `DATA_PROTECTION_PFX` 🔒 | Le `.pfx` de `preprod` encodé en base64, sur une ligne (§ 6.2). |
| `production` | `ENV_FILE` 🔒 | Contenu complet de `.env.production.example` rempli (§ 4). |
| `production` | `DATA_PROTECTION_PFX` 🔒 | Le `.pfx` **propre à la prod**, en base64. Il ne change plus une fois servi : les clés déjà chiffrées deviendraient illisibles (sessions et jetons perdus). |

Dossiers sur l'hôte : `/opt/lodb-preprod` et `/opt/lodb-production` (`/opt/<projet>`),
créés par le job. Ils doivent différer de `PROD_PATH`, que l'ancienne stack garde pour le retour
arrière. Tant qu'un secret manque, le job échoue à « Check the secrets » et les nomme.

`PROD_SSH_KEY` étant un secret de dépôt, tout job du dépôt peut le lire, comme aujourd'hui
pour l'ancienne stack ; la mise en prod reste gardée par l'approbation de `production`.

**Surcharges, seulement si un environnement change d'hôte** : les secrets `SSH_KEY`,
`SSH_HOST`, `SSH_USER` et `DEPLOY_PATH`, déclarés dans cet environnement, remplacent les
valeurs ci-dessus.

### 3.3 Approbation de la prod

L'environnement `production` porte l'approbation qui met en prod (*required reviewers*) et
limite les déploiements à `dev` (*deployment branches*). Ses deux secrets y sont déclarés,
jamais en secrets de dépôt : ils ne sont délivrés qu'au job approuvé.

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

### 3.5 Secrets de l'ancienne stack

`PROD_SSH_KEY`, `PROD_HOST` et `PROD_SSH_USER` servent la nouvelle stack : **ne jamais les
supprimer**. `STAGING_SSH_KEY`, `STAGING_HOST`, `STAGING_PATH`, `STAGING_SSH_USER`,
`ENV_STAGING`, `PROD_PATH`, `ENV_PROD` et `ENV_TEST` ne sont plus lus par aucun workflow
actif. Les garder jusqu'à la décommission (runbook § 10) : `ENV_PROD` est la source des
valeurs 🔁.

## 4. Le `.env` d'un environnement servi

Le secret `ENV_FILE` ou `ENV_FILE` est le fichier complet, une variable par ligne. Le
job l'écrit dans `/opt/<projet>/.env` (mode 600) à chaque déploiement.

**Pièges du fichier :**

- Pas de ligne `LoDb__*` vide : la commenter. Présente mais vide, elle est transmise à
  l'API comme une valeur vide, que certains réglages refusent au démarrage.
- Une valeur qui contient `$` se met entre apostrophes (`'…'`) : Compose l'interpolerait.
- Mots de passe en hexadécimal (`openssl rand -hex 24`) : la chaîne de connexion n'est pas
  quotée, un `;` la casse.

### 4.1 Variables Compose

| Variable | `preprod` | prod | | Rôle |
|---|---|---|---|---|
| `COMPOSE_PROJECT_NAME` ⚙️ | `lodb-preprod` | `lodb-production` | ✅ | Isole la stack sur le VPS ; jamais `lodb-prod` ni `lodb-staging`. |
| `REGISTRY` | `ghcr.io/lindecker-charles/lodb` | idem | ✅ | Registre des images. |
| `IMAGE_TAG` ⚙️ | `preprod` | `production` | ✅ | Tag déployé ; jamais `prod` (celui de l'ancienne stack). |
| `COMPOSE_PROFILES` | `bundled-database` | absente | ➖ | Postgres embarqué ; le job l'impose, la ligne sert aux commandes lancées à la main. |
| `CADDY_DOMAINS` | domaine de `preprod` | `league-of-data-base.com, league-of-data-base.fr` 🔁 | ✅ | Label `caddy_0` du site ; DNS pointé d'abord. |
| `API_CADDY_DOMAINS` | `api.` + domaine | `api.league-of-data-base.com, api.league-of-data-base.fr` 🔁 | ✅ | Label `caddy_1` ; chaque hôte commence par `api.`. |
| `LODB_CANONICAL_HOST` | domaine de `preprod` | `league-of-data-base.com` | ✅ | Hôte canonique : cible des 301 (`www.`, `.fr`), origine des liens d'e-mail, des sitemaps et des `share_url` de `/v1`. |
| `LODB_ALLOWED_HOSTS` | domaine de `preprod` | `league-of-data-base.com` | ✅ | Hôtes que le SSR accepte (virgules) ; les autres reçoivent un 400. |
| `LODB_PUBLIC_API_ORIGIN` | `https://api.` + domaine | `https://api.league-of-data-base.com` | ✅ | Origine de `/v1` documentée par `/developers`. |
| `LODB_EDGE_CIDR` | sous-réseau d'`edge` | idem | ✅ | Seul pair cru sur `X-Forwarded-For` (§ 6.7). |
| `LODB_NOINDEX` ⚙️ | `1` | `0` | ✅ | `X-Robots-Tag: noindex, nofollow` ; le smoke test le vérifie. |
| `LODB_DB_NETWORK` ⚙️ | **absente** | `lodb-prod_default` | prod | Réseau de la base : absente, Postgres embarqué ; présente, la base de l'ancienne stack, jamais déplacée. |
| `LODB_DB_HOST`, `LODB_DB_PORT` | défauts | défauts | ➖ | `postgres` et `5432`, le service Postgres du réseau dans les deux cas. |
| `LODB_DB_NAME`, `LODB_DB_USER` | `lodb` | 🔁 `POSTGRES_DB`, `POSTGRES_USER` | ✅ | Chaîne de connexion de `api` et `migrate`. |
| `LODB_DB_PASSWORD` 🔒 | fort, propre à `preprod` | 🔁 `POSTGRES_PASSWORD` | ✅ | Idem ; sans `;`. |
| `LODB_DATA_PROTECTION_CERT_FILE`, `LODB_ANDROID_DIR` | défauts | défauts | — | Ne pas définir : le job écrit aux emplacements par défaut (§ 5). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | ➖ | ➖ | ➖ | Collecteur de traces OTLP, quand `infra-vps` en expose un. |

`CONTACT_RECIPIENT` et `PUBLIC_API_BASE_URL` n'ont d'effet qu'en local : sur un
environnement servi, `LoDb__Contact__Recipient` et `LODB_PUBLIC_API_ORIGIN` les remplacent.

### 4.2 Réglages de l'API transmis par le `.env`

Nom exact de la configuration, casse comprise. Absents du `.env`, ils ne sont pas transmis
et l'API garde sa valeur par défaut.

| Ligne | Requis | Effet, et effet de l'absence |
|---|---|---|
| `LoDb__DataProtection__CertificatePassword` 🔒 | si le `.pfx` en a un | Mot de passe du certificat. Faux : l'API refuse de démarrer. |
| `LoDb__Mail__Host` ⚙️ | prod | Relais SMTP. Absent : les e-mails attendent dans `email_outbox`, rien n'est envoyé (voulu sur `preprod`). |
| `LoDb__Mail__Port` | ➖ | `587` par défaut. |
| `LoDb__Mail__Security` | ➖ | `Auto` par défaut ; `None`, `SslOnConnect`, `StartTls`, `StartTlsWhenAvailable`. |
| `LoDb__Mail__Username`, `LoDb__Mail__Password` 🔒 | si le relais authentifie | Identifiants SMTP. |
| `LoDb__Mail__From` | prod | `Nom <adresse>`, sur un domaine couvert par SPF, DKIM et DMARC. Défaut : `no-reply@leagueofdatabase.gg`, **un autre domaine** : toujours le définir. |
| `LoDb__Billing__StripeSecretKey` 🔒 ⚙️ | prod | `sk_live_…` en prod, `sk_test_…` sur `preprod`. Absente : dons et achats de crédits en 503. |
| `LoDb__Billing__StripeWebhookSecret` 🔒 ⚙️ | prod | `whsec_…` de l'endpoint `/webhooks/stripe` de l'environnement. Absent : webhook en 503, alerte `billing.webhook.unconfigured`. |
| `LoDb__Accounts__Google__ClientId` | ➖ | Client OAuth web. Absent : connexion Google coupée (`google-unavailable`). |
| `LoDb__Accounts__Google__ClientSecret` 🔒 | avec le précédent | Secret du client web. |
| `LoDb__Accounts__Google__AppClients__0__ClientId` (et `__1__`) | ➖ | Clients des apps acceptés par l'échange de code ; jamais sans le client web. Deux au plus sans modifier `compose.deploy.yaml`. |
| `LoDb__Accounts__Google__AppClients__N__ClientSecret` | desktop | Secret du client desktop (livré avec l'app, donc public) ; aucun pour Android. |
| `LoDb__Contact__Recipient` | ➖ | Destinataire des messages de contact. Absent : stockés pour l'admin, sans notification. |
| `LoDb__Contact__NotificationLocale` | ➖ | Langue de la notification, `Fr` par défaut (`Ar`… `ZhHant`). Une valeur invalide ne se voit qu'au premier message. |
| `LoDb__Analytics__VisitorKey` 🔒 | recommandé | Clé du hachage des visiteurs. Absente : une clé aléatoire par processus, les visiteurs sont recomptés à chaque redémarrage. |

### 4.3 Fixés par les fichiers compose (hors du `.env`)

`ConnectionStrings__LoDb` (assemblée depuis `LODB_DB_*`), `LoDb__Accounts__SiteOrigin`,
`LoDb__Seo__CanonicalOrigin` et `LoDb__PublicApi__SiteOrigin` (`https://` +
`LODB_CANONICAL_HOST`), `LoDb__PublicApi__Reference__BaseUrl` (`LODB_PUBLIC_API_ORIGIN`),
`LoDb__DataProtection__CertificatePath`, `LoDb__Hosting__KnownProxyNetworks__0..2`,
`LoDb__Storage__Root` (`/srv/storage`, que nginx sert), et côté SSR `LODB_API_ORIGIN`,
`LODB_TRUST_PROXY_HEADERS`, `NODE_OPTIONS`. `ASPNETCORE_ENVIRONMENT` reste **absente** sur
un hôte : `Development` exposerait OpenAPI et retirerait `Secure` des cookies.

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

### 4.4 Correspondance avec l'ancienne prod (`ENV_PROD`)

| Ancienne variable | Nouvelle ligne |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `LODB_DB_NAME`, `LODB_DB_USER`, `LODB_DB_PASSWORD` |
| `APP_SECRET` | `LoDb__Analytics__VisitorKey` (mêmes visiteurs des deux côtés) |
| `MAILER_DSN` `smtp://USER:MDP@HÔTE:587` | `LoDb__Mail__Host`, `Port` `587`, `Security` `StartTls`, `Username`, `Password` (décodés d'URL) ; `smtps://…:465` : `Port` `465`, `Security` `SslOnConnect` |
| `MAILER_FROM` | `LoDb__Mail__From` |
| `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET` | `LoDb__Billing__StripeSecretKey`, `LoDb__Billing__StripeWebhookSecret` |
| `OAUTH_GOOGLE_CLIENT_ID`, `OAUTH_GOOGLE_CLIENT_SECRET` | `LoDb__Accounts__Google__ClientId`, `LoDb__Accounts__Google__ClientSecret` |
| `CONTACT_RECIPIENT` | `LoDb__Contact__Recipient` |
| `CADDY_DOMAINS`, `API_CADDY_DOMAINS` | mêmes noms |
| `ADMIN_LOGIN`, `ADMIN_PASSWORD` | aucune : chaque administrateur reçoit le rôle par `api admin create --email …`, TOTP à la première connexion (runbook § 5.4) |
| `GEOIP_DB_PATH` | aucune sur un hôte (§ 8) |
| `REGISTRY`, `IMAGE_TAG`, `COMPOSE_PROJECT_NAME` | valeurs propres à la nouvelle stack (§ 4.1) |

## 5. Fichiers et volumes de l'hôte

| Chemin, dans `/opt/<projet>` | Écrit par | Rôle |
|---|---|---|
| `.env` | le job (`ENV_*`), mode 600 | § 4. |
| `.deploy/data-protection.pfx` | le job (`*_DATA_PROTECTION_PFX`) | Monté en secret dans `api`. Dossier en 700, fichier en 644 : l'utilisateur non root de l'API le lit. Absent, la stack ne démarre pas. |
| `.deploy/android/` | l'exploitant, à chaque release Android | `latest.json` (asset `lodb-android-latest.json`) et `assetlinks.json` ([`release-android.md`](release-android.md)). Vide : les deux URL répondent 404. |

Volumes : `<projet>_storage` (Data Dragon, propre à la nouvelle stack, pré-rempli avant la
bascule en prod), `<projet>_pages-cache`, et `<projet>_pgdata` sur `preprod` seulement.

## 6. Obtenir les valeurs

### 6.1 DNS

Enregistrements A (et AAAA) vers l'hôte pour chaque nom de `CADDY_DOMAINS` et de
`API_CADDY_DOMAINS`, avant le premier déploiement : le job contrôle le TLS public. Pour
`preprod`, le domaine de l'ancien staging (`test.league-of-data-base.com`,
`api.test.league-of-data-base.com`) est le candidat naturel : il pointe déjà vers l'hôte
si l'ancien staging y tournait, et c'est celui que vise le canal beta du desktop. Il faut
alors arrêter d'abord le `nginx` et le `go-api` de `lodb-staging` : sur `preprod`, le job
refuse de reprendre un domaine.

### 6.2 Certificat Data Protection (un par environnement)

```bash
openssl req -x509 -newkey rsa:3072 -nodes -days 3650 -subj "/CN=lodb data protection" \
  -keyout dp.key -out dp.crt
openssl pkcs12 -export -inkey dp.key -in dp.crt -out dp.pfx   # mot de passe demandé
base64 < dp.pfx | tr -d '\n'   # valeur de *_DATA_PROTECTION_PFX
```

Le mot de passe va dans `LoDb__DataProtection__CertificatePassword`. Garder `dp.pfx` hors
ligne, détruire `dp.key`. Le certificat chiffre les clés des cookies et des jetons : le
changer déconnecte tout le monde.

### 6.3 Clé SSH de déploiement

Aucune à créer : `PROD_SSH_KEY`, celle de l'ancienne stack, est déjà autorisée sur le VPS.
Seule une stack déplacée sur un autre hôte a besoin de la sienne :

```bash
ssh-keygen -t ed25519 -N '' -C 'lodb-preprod deploy' -f lodb-preprod-deploy
```

La clé publique (`.pub`) va dans `~/.ssh/authorized_keys` de l'utilisateur de déploiement
sur cet hôte ; la clé privée, complète, dans le secret `SSH_KEY` de l'environnement.

### 6.4 SMTP

Mêmes identifiants que le `MAILER_DSN` de l'ancienne prod (§ 4.4). Le domaine de
`LoDb__Mail__From` doit autoriser le relais : SPF, DKIM et DMARC.

### 6.5 Stripe

| | `preprod` | prod |
|---|---|---|
| Clés | mode test (`sk_test_…`) | mode live (`sk_live_…`) 🔁 |
| Endpoint | `https://<domaine de preprod>/webhooks/stripe`, créé pour `preprod` | l'endpoint **existant**, `https://league-of-data-base.com/webhooks/stripe`, inchangé 🔁 |
| Événements | `checkout.session.completed`, `customer.subscription.deleted` | idem |

Le secret de signature (`whsec_…`) est celui de l'endpoint de chaque environnement.

### 6.6 Google OAuth

Le projet Google Cloud et l'écran de consentement :
[`oauth-google-setup.md`](oauth-google-setup.md), § 1 et 2.

- **Client web** (celui de l'ancienne stack 🔁) : ajouter l'URI de redirection
  `https://<hôte canonique>/api/account/google/callback` et l'origine
  `https://<hôte canonique>`, **sans retirer** celles de l'ancienne stack avant la
  décommission (retour arrière). Même chose pour le domaine de `preprod`.
- **Client desktop** (type *Desktop app*) : son id est compilé dans l'app (propriété MSBuild
  `LoDbDesktopGoogleClientId`) et déclaré côté serveur en `AppClients__N__ClientId` avec son
  secret, sur chaque API que vise le canal (stable : prod, beta : `test.`).
- **Client Android** : déclaré en `AppClients__N__ClientId`, sans secret ; la redirection
  passe par l'App Link `https://<hôte>/app/oauth/google`. Voir § 8 : l'app ne le lit pas
  encore.

### 6.7 Sous-réseau de l'edge

Sur l'hôte : `docker network inspect edge --format '{{(index .IPAM.Config 0).Subnet}}'`.

## 7. En local

Aucun `.env` n'est nécessaire : `compose.yaml` donne une valeur par défaut à tout, et
`compose.override.yaml` ajoute `ASPNETCORE_ENVIRONMENT=Development` et Mailpit
(`LoDb__Mail__Host=mailpit`, e-mails sur http://localhost:18025). Un `.env` racine ne sert
qu'à surcharger un port ou une variable ; ne jamais y copier un modèle d'environnement
servi. Ports et emplacements : [`developpement.md`](developpement.md).

L'ancienne stack archivée a son propre `.env`, `legacy/.env` (modèle
`legacy/.env.example`), lu par `docker compose --project-directory legacy …`.

## 8. Écarts connus, à trancher avant la bascule

- **Pays des visiteurs** : aucun fichier GeoLite2 n'est monté et ni
  `LoDb__Analytics__GeoIpDatabase` ni `GEOIP_DB_PATH` ne sont transmis sur un hôte. Les
  vues de prod n'auront pas de pays, contrairement à l'ancienne stack.
- **Connexion Google sur Android** : aucun fichier d'environnement du front ne définit
  `googleClientId`. La connexion reste coupée dans l'app, quels que soient les
  `AppClients` du serveur.
- **Releases des apps** : les workflows attendent un appel après le déploiement de prod
  (« contrat d'appel » des guides de release), que `promote.yml` ne fait pas. Elles se
  lancent donc à la main.
- **Réglages internes** : aucun n'est réglable depuis le `.env` sans modifier
  `compose.deploy.yaml` (§ 4.3, encadré).
