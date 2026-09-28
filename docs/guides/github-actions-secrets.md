# 🔐 GitHub Actions — Secrets (nouvelle stack)

Workflows de la nouvelle stack. Ceux de l'ancienne (`ci.yml`, `_*.yml`) sont archivés sous
`legacy/.github/workflows/` et ne tournent plus ; leur guide est
[`legacy/docs/guides/github-actions-secrets.md`](../../legacy/docs/guides/github-actions-secrets.md).
La liste complète de ce qu'il faut configurer est dans [`configuration.md`](configuration.md).

| Fichier | Rôle |
|---|---|
| `next-ci.yml` | Déclenché par `push` (branche d'intégration `docs/reecriture-dotnet-angular`, `dev`, `main`) et `pull_request`, filtré sur les chemins de la nouvelle stack. Jobs parallèles : `dotnet` (build + tests, Testcontainers), `front` (lint, typecheck, tests, `build:web`, `build:shell`), `contract` (`api:check`), `i18n` (`i18n:report`, non bloquant), `e2e` (stack `lodb-next` + Playwright). |
| `next-build.yml` | Réutilisable, appelé par `next-ci.yml` sur `push` de la branche d'intégration une fois les jobs bloquants verts : images `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}` taguées `:<sha>` + `:next`, label OCI et `APP_REVISION` = SHA. |
| `next-deploy.yml` | Déploiement SSH, manuel pour `next` (`workflow_dispatch`, entrée `branch`), appelé par `next-promote.yml` pour la prod. `pull`, puis conteneur éphémère `migrate` **avant** `up -d --wait`, puis smoke test (nginx, `/readyz`, `/en/`, sous-domaine `api.`, `X-Robots-Tag`, TLS public avec relance de l'edge). |
| `next-promote.yml` | Manuel (`workflow_dispatch`, entrées `revision`, `branch`, `take_over_domains`) : vérifie que les trois images `:<revision>` existent, puis, **après approbation** de l'environnement `production`, les retague `:next-prod` (sans rebuild) et les déploie par `next-deploy.yml`. |

```
push docs/reecriture-dotnet-angular ─▶ next-ci (dotnet, front, contract, e2e ; i18n informatif)
                                    ─▶ next-build (GHCR :<sha> + :next)
Actions ▸ next deploy ▸ Run workflow ─▶ hôte next : pull ─▶ migrate ─▶ up -d ─▶ smoke test
                                       (annonce « Deployed revision : <sha> »)
Actions ▸ next promote (revision = <sha>) ─▶ images :<sha> présentes ?
        ─▶ approbation « production » ─▶ retag :<sha> → :next-prod
        ─▶ hôte prod : pull ─▶ migrate ─▶ [reprise des domaines] ─▶ up -d ─▶ smoke test
```

Les jobs de CI et de build n'utilisent aucun secret (seulement `GITHUB_TOKEN`). Le job de
déploiement tourne dans l'environnement GitHub `next` ou `production` :

- **`next`** : créé à la première exécution ; ses secrets peuvent y être déclarés (ou en
  secrets de dépôt).
- **`production`** : à créer **avant** la première promotion, avec des *required
  reviewers* (la validation manuelle qui met en prod) et une règle *deployment branches*
  limitée à la branche qui porte les workflows. Ses secrets `PROD_NEXT_*` y sont
  déclarés, jamais en secrets de dépôt : ils ne sont alors délivrés qu'au job approuvé.

**Tags d'images** : `:<sha>` (immuable, poussé par `next-build.yml`), `:next` (dernier build
de la branche d'intégration) et `:next-prod` (la révision promue). **Jamais `:prod`** :
`ghcr.io/<owner>/lodb/nginx` est partagé avec l'ancienne stack, dont les déploiements
tirent `:prod`, et le retour arrière de la bascule a besoin de cette image intacte.

### Secrets du déploiement `next` (préfixe `NEXT`)

| Secret | Requis | Description |
|---|:---:|---|
| `NEXT_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` de l'hôte. |
| `NEXT_HOST` | ✅ | Hôte `next` (IP ou FQDN), le même VPS que staging et prod le cas échéant. |
| `NEXT_PATH` | ✅ | Chemin absolu du projet sur l'hôte, **distinct** de `STAGING_PATH` et `PROD_PATH` (chaque dossier a son `.env`). Le job y initialise le dépôt et suit la branche passée en entrée. |
| `NEXT_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_NEXT` | ✅ | Dotenv `next` **complet**, écrit dans `${NEXT_PATH}/.env` (lisible par root seul). Modèle : `.env.next.example` et le tableau ci-dessous. |
| `NEXT_DATA_PROTECTION_PFX` | ✅ | Certificat Data Protection (PKCS#12, `.pfx`) **encodé en base64**, écrit dans `${NEXT_PATH}/.deploy/data-protection.pfx`. Il chiffre les clés des cookies et des jetons : hors Development, aucune connexion sans lui. Son mot de passe va dans `ENV_NEXT`. |

### Secrets du déploiement prod (préfixe `PROD_NEXT`, environnement `production`)

Mêmes rôles que ci-dessus : `PROD_NEXT_SSH_KEY`, `PROD_NEXT_HOST`, `PROD_NEXT_PATH`,
`PROD_NEXT_SSH_USER` (optionnel), `ENV_PROD_NEXT` et `PROD_NEXT_DATA_PROTECTION_PFX`.

- `PROD_NEXT_PATH` est un dossier **neuf**, distinct de `PROD_PATH` (l'ancienne stack y
  garde son `.env` et ses fichiers pour le retour arrière).
- `PROD_NEXT_DATA_PROTECTION_PFX` est un certificat **propre à la prod**, jamais celui de
  `next` ; il ne change plus une fois servi, sinon les clés déjà chiffrées deviennent
  illisibles (sessions et jetons perdus).

Certificat (une fois par environnement, hors du dépôt) :

```bash
openssl req -x509 -newkey rsa:3072 -nodes -days 3650 -subj "/CN=lodb data protection" \
  -keyout dp.key -out dp.crt
openssl pkcs12 -export -inkey dp.key -in dp.crt -out dp.pfx   # mot de passe demandé
base64 < dp.pfx | tr -d '\n'   # valeur du secret ; garder dp.pfx hors ligne, détruire dp.key
```

### Secrets des releases des apps (environnements `desktop-release` et `android-release`)

Les workflows `next-release-desktop.yml` et `next-release-android.yml` ont chacun leur
environnement GitHub ; leurs secrets n'y sont délivrés qu'aux jobs qui signent et publient.
Les variables du desktop vivent dans son environnement, celles d'Android au niveau du dépôt
(`stage` lit la clé publique hors de l'environnement). La liste complète, avec la façon
d'obtenir chaque valeur, est tenue dans leur guide :

| Environnement | Secrets | Variables | Guide |
|---|---|---|---|
| `desktop-release` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `APPLE_DEVELOPER_ID_P12`, `APPLE_DEVELOPER_ID_P12_PASSWORD`, `APPLE_NOTARY_KEY_P8`, `APPLE_NOTARY_KEY_ID`, `APPLE_NOTARY_ISSUER_ID` | `AZURE_SIGNING_ENDPOINT`, `AZURE_SIGNING_ACCOUNT`, `AZURE_SIGNING_PROFILE`, `APPLE_APP_IDENTITY`, `APPLE_INSTALL_IDENTITY` | [`release-desktop.md`](release-desktop.md) § Signature |
| `android-release` | `ANDROID_UPLOAD_KEYSTORE_BASE64`, `ANDROID_UPLOAD_STORE_PASSWORD`, `ANDROID_UPLOAD_KEY_PASSWORD`, `ANDROID_APP_SIGNING_KEYSTORE_BASE64`, `ANDROID_APP_SIGNING_STORE_PASSWORD`, `ANDROID_APP_SIGNING_KEY_PASSWORD`, `LODB_LIVE_UPDATE_PRIVATE_KEY`, `PLAY_SERVICE_ACCOUNT_JSON` | `ANDROID_UPLOAD_KEY_ALIAS`, `ANDROID_APP_SIGNING_KEY_ALIAS`, `LODB_LIVE_UPDATE_PUBLIC_KEY`, `ANDROID_TRANSITIONAL_CHANNEL`, `ANDROID_PLAY_ENABLED`, `ANDROID_APP_LINK_HOST` | [`release-android.md`](release-android.md) § Secrets et variables |

Sans ces secrets, la release desktop échoue à la signature (`signing.sh`) et la release
Android à la signature des bundles ou de l'AAB ; sans `LODB_LIVE_UPDATE_PUBLIC_KEY`, l'app
accepterait des bundles non signés, ce que l'étape « Check the LiveUpdate settings of the
APKs » refuse.

### Lignes de `ENV_NEXT` et `ENV_PROD_NEXT`

Variables lues par `compose.next.yaml` et `compose.next.deploy.yaml`. Le job **vérifie**
celles marquées ⚙️ avant toute modification de l'hôte et s'arrête si elles ne
correspondent pas à la cible.

| Variable | `next` | prod | Description |
|---|---|---|---|
| `COMPOSE_PROJECT_NAME` ⚙️ | `lodb-next` | `lodb-next-prod` | Isolation des stacks du VPS ; jamais `lodb-prod` ni `lodb-staging` (l'ancienne stack). |
| `REGISTRY` | `ghcr.io/<owner>/lodb` | idem | Registre des images. |
| `IMAGE_TAG` ⚙️ | `next` | `next-prod` | Tag déployé (voir « Tags d'images »). |
| `CADDY_DOMAINS` | domaine de `next` | `league-of-data-base.com, league-of-data-base.fr` (ceux de l'ancienne stack) | Label Caddy `caddy_0` du site. Les hôtes `www.` et `.fr` reçoivent une 301 vers l'hôte canonique ; un `www.` ajouté ici exige d'abord son DNS (contrôle TLS). |
| `API_CADDY_DOMAINS` | `api.` + domaine | `api.league-of-data-base.com, api.league-of-data-base.fr` | Label `caddy_1` ; chaque hôte **doit commencer par `api.`** (nginx le reconnaît à ce préfixe). |
| `LODB_CANONICAL_HOST` | domaine de `next` | `league-of-data-base.com` | Hôte canonique : cible des 301, origine des liens des e-mails, des sitemaps et des `share_url` de `/v1`. |
| `LODB_ALLOWED_HOSTS` | domaine de `next` | l'hôte canonique | Hôtes que le serveur SSR accepte, séparés par des virgules. |
| `LODB_PUBLIC_API_ORIGIN` | `https://api.` + domaine | `https://api.league-of-data-base.com` | Origine de `/v1` documentée par `/developers`. |
| `LODB_EDGE_CIDR` | sous-réseau d'`edge` | idem | Seul pair cru sur `X-Forwarded-For` : `docker network inspect edge --format '{{(index .IPAM.Config 0).Subnet}}'`. |
| `LODB_NOINDEX` ⚙️ | `1` | `0` ou absente | `X-Robots-Tag: noindex, nofollow` sur les réponses de nginx ; le smoke test contrôle l'en-tête. |
| `LODB_DB_NETWORK` ⚙️ | **absente** | `lodb-prod_default` | Réseau de la base. Absente : la stack a son Postgres (dump anonymisé) sur le réseau `lodb-next-db`, que le job crée. Présente : le Postgres embarqué est écarté et `api` et `migrate` rejoignent la base existante sur ce réseau, **sans la déplacer**. |
| `COMPOSE_PROFILES` | `bundled-database` | **absente** | Active le Postgres embarqué. Le job l'impose de lui-même (`bundled-database` sur `next`, vide en prod, quel que soit le `.env`) ; la ligne ne sert qu'aux commandes `docker compose` lancées à la main sur l'hôte `next`. |
| `LODB_DB_HOST`, `LODB_DB_PORT` | défauts | défauts | `postgres` et `5432` : le nom du service Postgres sur ce réseau, dans les deux cas. |
| `LODB_DB_NAME`, `LODB_DB_USER`, `LODB_DB_PASSWORD` | Postgres de `next` (mot de passe fort et unique) | ceux de l'ancienne stack (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` de `ENV_PROD`) | Chaîne de connexion de l'API et de `migrate`. |
| `LODB_DATA_PROTECTION_CERT_FILE`, `LODB_ANDROID_DIR` | défauts | défauts | Fichiers de l'hôte, voir plus bas. Ne pas les définir : le job écrit aux emplacements par défaut. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | optionnel | optionnel | Collecteur de traces, quand `infra-vps` en expose un. |

Réglages de l'API, sous leur nom de configuration exact (`LoDb__<Section>__<Clé>`, casse
comprise) : absents du `.env`, ils ne sont pas transmis et l'API garde sa valeur par
défaut. Le job exige en prod ceux marqués ⚙️.

| Ligne | Description |
|---|---|
| `LoDb__DataProtection__CertificatePassword` | Mot de passe du `.pfx` de `*_DATA_PROTECTION_PFX`. |
| `LoDb__Mail__Host` ⚙️ | Relais SMTP. Absent : les e-mails attendent dans `email_outbox` (rien n'est perdu, rien n'est envoyé). |
| `LoDb__Mail__Port`, `LoDb__Mail__Security`, `LoDb__Mail__Username`, `LoDb__Mail__Password`, `LoDb__Mail__From` | Port (587), mode TLS (`Auto`, `StartTls`, `SslOnConnect`…), identifiants et expéditeur (`Nom <adresse>`, domaine autorisé par SPF/DKIM/DMARC). |
| `LoDb__Billing__StripeSecretKey` ⚙️, `LoDb__Billing__StripeWebhookSecret` ⚙️ | Clé `sk_…` et secret `whsec_…` de l'endpoint `POST /webhooks/stripe` (un endpoint Stripe par environnement). Clés de test sur `next`. |
| `LoDb__Accounts__Google__ClientId`, `LoDb__Accounts__Google__ClientSecret` | Client OAuth web, retour `https://<hôte canonique>/api/account/google/callback` ([`oauth-google-setup.md`](oauth-google-setup.md)). |
| `LoDb__Accounts__Google__AppClients__0__ClientId` (et `__ClientSecret`), `…__1__…` | Clients Android et desktop, jamais sans le client web. |
| `LoDb__Contact__Recipient`, `LoDb__Contact__NotificationLocale` | Destinataire des messages de contact (vide : stockés, non notifiés) et langue de la notification. |
| `LoDb__Analytics__VisitorKey` | Clé du hachage des visiteurs ; en prod, l'`APP_SECRET` de l'ancienne stack garde les mêmes visiteurs des deux côtés. |

### Fichiers de l'hôte

Dans `${*_PATH}`, à côté du dépôt suivi par le job :

| Chemin | Écrit par | Rôle |
|---|---|---|
| `.env` | le job (`ENV_*`), mode 600 | Variables ci-dessus. |
| `.deploy/data-protection.pfx` | le job (`*_DATA_PROTECTION_PFX`) | Monté en secret dans `api` (`/run/secrets/data-protection.pfx`). Dossier `.deploy/` en 700, fichier en 644 : l'utilisateur non root de l'API le lit par le montage. Absent, la stack ne démarre pas. |
| `.deploy/android/` | l'exploitant, à chaque release Android | `latest.json` (asset `lodb-android-latest.json` de la release) et `assetlinks.json` ([`release-android.md`](release-android.md)), montés en lecture seule sur `/etc/nginx/android`. Vide : les deux URL répondent 404. |

Volumes : `<projet>_storage` (Data Dragon, propre à la nouvelle stack : `lodb-next-prod_storage`
en prod, pré-rempli avant la bascule), `<projet>_pages-cache`, et `<projet>_pgdata` sur
`next` seulement.

### Ce que fait le job sur l'hôte, dans l'ordre

1. Synchronise le dépôt sur la branche, contrôle le `.env` (lignes ⚙️), le réseau de la
   base (créé sur `next`, exigé en prod) et le réseau `edge` (exigé, jamais créé).
2. Cherche les conteneurs **d'autres projets** dont les labels Caddy réclament un domaine
   de `CADDY_DOMAINS` ou `API_CADDY_DOMAINS`. S'il en trouve sans `take_over_domains`, il
   s'arrête là, sans rien avoir modifié.
3. `docker compose pull` (5 tentatives), puis `docker compose run --rm migrate` : la base
   passe à la dernière migration (une base Doctrine est d'abord marquée à `Baseline`).
   Un échec arrête le job ; les conteneurs en place continuent de servir.
4. Avec `take_over_domains` : arrête (`docker stop`) les conteneurs trouvés au point 2.
5. `docker compose up -d --no-build --wait`, puis smoke test dans la stack. Si l'un échoue
   après le point 4, le job arrête le nginx de la nouvelle stack et **redémarre** les
   conteneurs arrêtés : les domaines reviennent à leur ancien propriétaire.
6. Annonce la révision servie (`::notice` « Deployed revision »), qui doit égaler la
   révision promue en prod, puis contrôle le TLS public (avec relance de l'edge).

### Avant le premier déploiement de `next`

1. Les prérequis serveur ci-dessus (edge `infra-vps`, `docker login ghcr.io` si les
   packages sont privés) valent aussi pour `next` ; DNS de `CADDY_DOMAINS` **et** de
   `API_CADDY_DOMAINS` pointés vers l'hôte.
2. La branche d'intégration doit être poussée (l'hôte la récupère) et avoir produit des
   images `:next` (un `push` avec `next-ci.yml` vert).
3. Les packages GHCR `lodb/api` et `lodb/web-ssr` sont nouveaux : leur visibilité se règle
   comme celle des autres. `lodb/nginx` est partagé avec l'ancienne stack, qui n'utilise
   jamais les tags `next` et `next-prod`.
4. Base de `next` : un dump anonymisé (`tools/next/db/anonymize.sh`) restauré dans le
   Postgres de la stack avant le premier `migrate` (`docker compose up -d --wait postgres`,
   puis `pg_restore --no-owner --no-privileges`). Sans dump, `migrate` part d'une base vide.

### Promouvoir en prod, et bascule

1. Relever la révision annoncée par le dernier déploiement de `next` validé.
2. **Actions ▸ next promote** : `revision` = ce SHA complet. Le job `candidate` vérifie
   les trois images (et signale si `:next` a bougé depuis) ; les reviewers de
   `production` approuvent ; le retag puis le déploiement suivent.
3. **Bascule** (fenêtre du runbook, sauvegarde de la base faite) : même promotion avec
   `take_over_domains` coché. Les migrations additives passent pendant que l'ancienne
   stack sert encore ; ses conteneurs porteurs des domaines (`nginx`, `go-api` de
   `lodb-prod`) ne sont arrêtés qu'ensuite. Son Postgres, qui est la base partagée, ne
   s'arrête jamais.
4. Après la bascule, **plus aucun déploiement de l'ancienne prod** (`ci.yml` sur `main`) :
   son `up -d` redémarrerait les conteneurs arrêtés, qui réclameraient à nouveau les
   domaines.

**Retour arrière** manuel, tant que le schéma reste compatible (migrations additives) ;
critères et contrôles : [runbook de bascule](../reecriture/bascule.md), § 8.

```bash
# La nouvelle stack rend les domaines et arrête ses tâches de fond, puis l'ancienne reprend
# ses conteneurs arrêtés (start, jamais up) ; son PostgreSQL n'a jamais été arrêté.
cd "$PROD_NEXT_PATH" && COMPOSE_FILE=compose.next.yaml:compose.next.deploy.yaml \
  docker compose stop nginx web-ssr api
cd "$PROD_PATH" && COMPOSE_FILE=compose.yaml:compose.deploy.yaml docker compose start
```

Revenir à une révision précédente de la nouvelle stack : relancer `next promote` avec son
SHA.
