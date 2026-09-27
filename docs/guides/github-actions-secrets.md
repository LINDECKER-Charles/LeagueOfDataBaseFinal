# 🔐 GitHub Actions — Secrets

Secrets à configurer pour le pipeline `CI/CD` (`.github/workflows/ci.yml`).

**Où** : `Settings` → `Secrets and variables` → `Actions` → onglet `Repository secrets`.

## 🔁 Flux (promotion à deux étages, *build once*)

```
push dev  ─▶ _tests ─▶ merge-to-test (dev→test)
          ─▶ _build (GHCR: :<sha> + :staging) ─▶ _deploy staging  (test.league-of-data-base.com)

merge test→main (manuel)  ─▶ push main
          ─▶ _promote : retag :staging→:prod (sans rebuild) ─▶ _deploy prod (league-of-data-base.fr/.com)
```

### 🧩 Structure des workflows (reusable, responsabilité unique)

| Fichier | Rôle |
|---|---|
| `ci.yml` | Orchestrateur : déclencheurs `dev`/`main`, gardes `if`, câblage. Aucun secret en dur. |
| `_tests.yml` | PHP / Go / JS. |
| `_build.yml` | Build + push des 3 images (`:<sha>` + tag mouvant). |
| `_deploy.yml` | Déploiement SSH d'**un** hôte, **paramétré** → appelé pour staging **et** prod (DRY). |
| `_promote.yml` | Retag `:staging → :prod` (aucun rebuild). |

- `test` est mis à jour automatiquement depuis `dev` et **ne déclenche jamais** le workflow (pas de boucle).
- `main` n'est atteint que par un **merge manuel `test → main`** : c'est le *gate* humain qui met en production.
- La prod **ne rebuild pas** : elle retague et déploie **exactement l'image validée en staging**
  (`:staging` courant → `:prod`). Corollaire : toujours passer par `test → main` — un push direct
  sur `main` déploierait l'image staging courante, pas le code poussé.

---

## 🧪 Tests appli (workflow `_tests.yml` — jobs `php` / `go` / `js`)

| Secret | Requis | Description |
|---|:---:|---|
| `ENV_TEST` | ✅ | Dotenv de test appli **complet** (source : `.env.test`). Écrit dans `app/.env.test.local` avant PHPUnit / lints. **Sans rapport avec le déploiement staging.** |

---

## 🟡 Déploiement staging (job `deploy-staging`)

Son `.env` doit fixer `COMPOSE_PROJECT_NAME=lodb-staging`, `IMAGE_TAG=staging` et
`CADDY_DOMAINS=test.league-of-data-base.com`. Le VPS peut être mutualisé avec la
prod (et d'autres projets) : l'isolation vient du `COMPOSE_PROJECT_NAME` distinct
et le TLS d'un **edge proxy partagé**, fourni par le dépôt d'infrastructure `infra-vps`
(section TLS ci-dessous).

| Secret | Requis | Description |
|---|:---:|---|
| `STAGING_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur staging. |
| `STAGING_HOST` | ✅ | Hôte staging (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `STAGING_PATH` | ✅ | Chemin absolu du projet sur le serveur (dossier des `compose.*.yaml`). Cible du `git pull origin test` et du `docker compose`. |
| `STAGING_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_STAGING` | ✅ | Dotenv staging **complet**. Poussé dans `${STAGING_PATH}/.env`. Doit inclure `COMPOSE_PROJECT_NAME=lodb-staging`, `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=staging`, les secrets applicatifs (`APP_SECRET`, `ADMIN_*`, `POSTGRES_PASSWORD`, `STRIPE_*` — cf. section base de données & Stripe) **et** `CADDY_DOMAINS=test.league-of-data-base.com`. Aucun paramètre de l'edge (contact Let's Encrypt) ici : il vit dans `infra-vps`. |

---

## 🚀 Déploiement production (jobs `promote` + `deploy-prod`)

Son `.env` doit fixer `COMPOSE_PROJECT_NAME=lodb-prod`, `IMAGE_TAG=prod` et les domaines apex.
Peut cohabiter avec staging sur le même VPS (projets Compose distincts + edge partagé).

| Secret | Requis | Description |
|---|:---:|---|
| `PROD_SSH_KEY` | ✅ | Clé privée SSH (PEM complet) chargée dans `ssh-agent`. Clé publique dans les `authorized_keys` du serveur prod. |
| `PROD_HOST` | ✅ | Hôte prod (IP ou FQDN). `ssh-keyscan` + connexions SSH. |
| `PROD_PATH` | ✅ | Chemin absolu du projet sur le serveur. Cible du `git pull origin main` et du `docker compose`. |
| `PROD_SSH_USER` | ➖ | Utilisateur SSH. **Optionnel**, défaut `root`. |
| `ENV_PROD` | ✅ | Dotenv prod **complet**. Poussé dans `${PROD_PATH}/.env`. Doit inclure `COMPOSE_PROJECT_NAME=lodb-prod`, `REGISTRY=ghcr.io/<owner>/lodb`, `IMAGE_TAG=prod`, les secrets applicatifs (dont `POSTGRES_PASSWORD` et `STRIPE_*` — cf. section suivante) **et** `CADDY_DOMAINS=league-of-data-base.fr, league-of-data-base.com`. Aucun paramètre de l'edge (contact Let's Encrypt) ici : il vit dans `infra-vps`. |

---

## 🗄️ Base de données & Stripe — lignes à porter dans `ENV_STAGING` / `ENV_PROD` / `ENV_TEST`

Ces variables sont des **lignes des dotenv** ci-dessus (pas des secrets GitHub distincts).

| Variable | Où | Description |
|---|---|---|
| `POSTGRES_PASSWORD` | `ENV_STAGING`, `ENV_PROD` | Mot de passe du service Postgres du stack. **Fort et unique par environnement** — le défaut compose (`lodb`) n'est acceptable qu'en dev local. |
| `DATABASE_URL` | optionnel | Assemblée par le compose depuis `POSTGRES_*` ; ne la définir explicitement que pour pointer une base **externe** au stack (format `postgresql://user:pass@host:5432/db?serverVersion=17&charset=utf8`). |
| `STRIPE_SECRET_KEY` | `ENV_PROD` (`sk_live_…`), `ENV_STAGING`/`ENV_TEST` (`sk_test_…`) | Clé API secrète Stripe de la page de don ; vide ⇒ passerelle désactivée proprement. |
| `STRIPE_WEBHOOK_SECRET` | `ENV_STAGING`, `ENV_PROD` | Secret de signature `whsec_…` de l'endpoint `POST /webhooks/stripe` (un endpoint Stripe distinct par environnement). |

> ℹ️ Les tests CI actuels (`ENV_TEST` → PHPUnit `tests/Unit`) ne touchent pas la base.
> Si des tests fonctionnels DB apparaissent un jour, `ENV_TEST` (côté CI) devra porter
> un `DATABASE_URL` pointant un service Postgres du job.

---

## 🔒 TLS / reverse-proxy — edge partagé (caddy-docker-proxy)

Les stacks app **ne publient plus** `80/443` et n'embarquent plus Caddy. Le point
d'entrée TLS est un **edge proxy unique et global** au VPS, partagé par staging, prod
et tout futur projet. Il est **déployé par le dépôt d'infrastructure `infra-vps`
(privé)**, seul propriétaire de l'edge et des réseaux Docker externes `edge` /
`observability` — jamais par ce dépôt. Il détecte les domaines via des **labels**
sur le conteneur nginx et émet/renouvelle seul les certificats Let's Encrypt.
Chaque stack app se contente de déclarer `CADDY_DOMAINS` (→ label) et de rejoindre
le réseau externe `edge` (via `compose.deploy.yaml`). Onboarding d'un nouveau projet :
**`docs/guides/migration-edge-proxy.md`** (« la méthode »).

**Ordre de déploiement** : `infra-vps` doit avoir convergé l'hôte **avant** tout projet
applicatif. Le job `_deploy.yml` ne converge plus rien côté edge : il **vérifie** seulement
que le réseau `edge` existe et **échoue explicitement** sinon (« deploy infra-vps before
this project »). Aucun secret lié à l'edge n'est requis dans ce dépôt : le contact
Let's Encrypt est configuré dans `infra-vps`.

| Variable | Où | Staging | Prod |
|---|---|---|---|
| `CADDY_DOMAINS` | `ENV_STAGING`/`ENV_PROD` | `test.league-of-data-base.com` | `league-of-data-base.fr, league-of-data-base.com` |

> ⚠️ **Ordre au premier déploiement** : les enregistrements DNS (A/AAAA) de chaque
> domaine doivent pointer vers le VPS **avant** que le déploiement tourne, et les
> ports **80 + 443** doivent être joignables — l'émission ACME échoue sinon. Caddy
> réessaie tout seul une fois le DNS propagé.

### 🖥️ Prérequis serveur (one-shot)

**Sur le VPS, une fois** :

1. Docker Engine + plugin `docker compose` installés.
2. **Déployer `infra-vps` d'abord** : edge (caddy-docker-proxy) + réseaux `edge` et
   `observability`. Sans ça, le job de ce dépôt s'arrête sur l'assertion réseau.
3. Ports **80/443** ouverts et DNS des domaines pointé (cf. ci-dessus).
4. `docker login ghcr.io` persistant si les packages GHCR sont **privés** (PAT `read:packages`),
   sinon les rendre publics — sans ça `docker compose pull` échoue.
5. Le `*_PATH` peut être vide : le job initialise le dépôt (`git init` + `reset`), pousse le `.env`,
   vérifie le réseau `edge`, puis déploie. staging suit `test`, prod suit `main`.

---

## ⚙️ Secrets automatiques (aucune action requise)

| Secret | Description |
|---|---|
| `GITHUB_TOKEN` | Fourni automatiquement par GitHub Actions. Utilisé pour le merge `dev → test`, le push et le retag des images sur GHCR. **Ne pas créer manuellement.** |

---

## 📝 Notes

- **Mapping fichier → secret** : le dotenv local de chaque environnement devient le secret correspondant.
  - `.env.test` → secret **`ENV_TEST`** → env de test appli (PHPUnit/CI ; écrit dans `app/.env.test.local`)
  - `.env.staging` → secret **`ENV_STAGING`** → déploiement staging (`test.league-of-data-base.com`)
  - `.env.prod` → secret **`ENV_PROD`** → déploiement prod
  - ⚠️ `test` (env applicatif) ≠ `staging` (déploiement pré-prod) : deux choses distinctes, deux fichiers, deux secrets.
- **`CADDY_DOMAINS`** est une **ligne** de ces dotenv (donc dans `ENV_STAGING` / `ENV_PROD`), jamais dans les workflows. Le contact Let's Encrypt de l'edge ne s'y trouve pas : il est géré par `infra-vps`.
- **`ENV_*`** contiennent le fichier dotenv intégral (une variable par ligne), pas une valeur unique — coller le contenu complet du `.env` correspondant.
- **Staging et prod ne diffèrent que par leur `.env`** (`COMPOSE_PROJECT_NAME`, `IMAGE_TAG`, `CADDY_DOMAINS`, secrets) : mêmes fichiers compose. Le `COMPOSE_PROJECT_NAME` distinct est ce qui les isole sur un VPS mutualisé.
- **Plus aucune variable `MINIO_*`** : le stockage Data Dragon est un volume Docker (`storage`), sans identifiants ni bucket. Les lignes `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` encore présentes dans `ENV_STAGING` / `ENV_PROD` ne sont plus lues et peuvent être retirées. L'ancien volume `<projet>_minio_data` reste sur l'hôte jusqu'à suppression manuelle (`docker volume rm lodb-staging_minio_data`, idem `lodb-prod_minio_data`).
- **`*_SSH_KEY`** : copier l'intégralité du fichier clé, en-têtes `-----BEGIN … PRIVATE KEY-----` / `-----END … PRIVATE KEY-----` inclus.
- Les secrets ne sont **jamais** affichés dans les logs (masqués par GitHub) ; leur mise à jour ne s'applique qu'aux exécutions suivantes.

---

## 🆕 Nouvelle stack (`lodb-next`, réécriture .NET + Angular)

Workflows propres à la réécriture, indépendants de `ci.yml` et des `_*.yml` ci-dessus :

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

**Retour arrière** manuel, tant que le schéma reste compatible (migrations additives) :

```bash
# La nouvelle stack rend les domaines, puis l'ancienne les reprend.
cd "$PROD_NEXT_PATH" && COMPOSE_FILE=compose.next.yaml:compose.next.deploy.yaml docker compose stop nginx
cd "$PROD_PATH" && COMPOSE_FILE=compose.yaml:compose.deploy.yaml docker compose start nginx go-api
```

Revenir à une révision précédente de la nouvelle stack : relancer `next promote` avec son
SHA.
