# Runbook de bascule

Procédure pas à pas de la bascule de l'ancienne stack (Symfony + Go + Vue) vers la nouvelle
(.NET + Angular), **en place** : la nouvelle stack prend la place de l'ancienne dans le même
projet Compose, le même dossier de l'hôte et la même base (`lodb-staging`, puis `lodb-prod`).
Elle met en œuvre [`plan-migration.md`](plan-migration.md) (« Bascule (lot 8) ») avec les
outils du lot 8 :

- déploiement et promotion (L8.1) : [`github-actions-secrets.md`](../guides/github-actions-secrets.md)
  et [`configuration.md`](../guides/configuration.md) ;
- outils de la répétition et de la fenêtre (L8.2) : [`tools/cutover/`](../../tools/cutover/README.md) ;
- migration de *contract*, préparée et non appliquée (L8.3) : [`tools/contract/`](../../tools/contract/README.md).

La **répétition locale** (§ 3.1) suit ce runbook tel quel : c'est le critère de sortie du
lot 8, rejoué par son jalon. Tout écart entre la répétition et ce texte se corrige **ici**.

## 0. Repères

| Nom | Valeur |
|---|---|
| Domaine | `league-of-data-base.com` (canonique), `league-of-data-base.fr` ; API publique `api.league-of-data-base.com` |
| Prod | projet `lodb-prod`, dossier `$PROD_PATH` (checkout de `main`), secrets `PROD_*`, `.env` = `ENV_PROD` |
| Staging | projet `lodb-staging`, dossier `$STAGING_PATH` (checkout de `test`), secrets `STAGING_*`, `.env` = `ENV_STAGING`, `test.league-of-data-base.com` |
| Base | service `postgres` du projet, volume `<projet>_pgdata` : **gardé**. La bascule recrée le conteneur avec la définition de la nouvelle stack ; `migrate` marque la base Doctrine à `Baseline` puis applique les migrations additives |
| Volumes | `<projet>_ddragon` : blobs de la nouvelle stack. `<projet>_storage` et `<projet>_app_state` de l'ancienne : **intacts** (reprise des agrégats et de l'audit, retour arrière) |
| Après la bascule | les fichiers de l'ancienne stack sont sous `legacy/` du même checkout (retour arrière, § 8.2) |
| Révision candidate | `REV` : SHA de `dev` dont les images sont `:staging`, annoncé par « Deployed revision » du déploiement de staging |
| Registre | `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}`, tags `:<sha>`, `:staging`, `:prod`, `:latest`. `lodb/nginx` est aussi l'image de l'ancienne stack : la bascule écrase ses `:staging` et `:prod`, d'où le tag local `legacy-*` posé avant (§ 1.1, § 5.1) |

Sur l'hôte, en root, toute commande `docker compose` d'un environnement se lance depuis son
dossier : `cd "$PROD_PATH" && export COMPOSE_FILE=compose.yaml:compose.deploy.yaml` (même
chose pour l'ancienne stack avant la bascule, et depuis `legacy/` après).

### Calendrier

| Quand | Étape | § |
|---|---|---|
| J-21 | opérations hôte et humaines faites ou planifiées | 1 |
| J-14 au plus tard | bascule de `staging` (fusion dans `dev`) ; gel | 1.1, 2 |
| J-14 à J-7 | répétition locale, puis sur `staging` avec le dump réel chiffré, retour arrière compris | 3 |
| J-3 | entrées de changelog datées, release publique dans l'image candidate | 4 |
| J-2 | dossier candidat, sauvegarde, migrations additives et pré-ingestion sur la base de prod, administrateurs | 5 |
| J-1 | anciens sitemaps enregistrés | 5.4 |
| J | fenêtre de bascule | 6 |
| J à J+3 | surveillance renforcée (72 h) ; retour arrière selon les critères | 7, 8 |
| J+30 | *contract*, si aucun retour arrière | 9 |
| après le *contract* | décommission | 10 |

## 1. Opérations hôte et humaines

Liste à jour du § 11 de [`plan-implementation.md`](plan-implementation.md) pour la bascule :
tout ce qui ne se fait pas dans le dépôt. Chaque ligne est faite avant l'étape indiquée.

| Opération | Pour | Avant |
|---|---|---|
| Certificat Data Protection **propre à chaque environnement** ([`configuration.md`](../guides/configuration.md)), `.pfx` gardé hors ligne ; secrets `STAGING_DATA_PROTECTION_PFX` et `PROD_DATA_PROTECTION_PFX` | connexions | J-14 / J |
| `.env.staging` et `.env.prod` complets (modèles `.env.staging.example`, `.env.prod.example`) : `LODB_DB_NAME/USER/PASSWORD` = `POSTGRES_*` de l'ancienne stack du même environnement (la base est déjà dans `pgdata`), `LODB_EDGE_CIDR` relevé sur l'hôte, domaines de l'ancienne stack, `LODB_PUBLIC_API_ORIGIN` | déploiements | J-14 / J-3 |
| Relais SMTP (`LoDb__Mail__*` d'après le `MAILER_DSN` de l'ancienne stack), SPF/DKIM/DMARC du domaine expéditeur | e-mails de compte | J-3 |
| Stripe : clé live et secret `whsec_` de l'endpoint **existant** (`/webhooks/stripe` ne change pas) ; clés de test et endpoint de staging | paiements | J-3 |
| Google OAuth : ajouter les URI de retour `https://league-of-data-base.com/api/account/google/callback` et celle de `test.`, **sans retirer** celles de l'ancienne stack (retour arrière) ; clients Android et desktop | connexion Google | J-3 |
| `LoDb__Analytics__VisitorKey` = `APP_SECRET` de l'ancienne prod (mêmes visiteurs des deux côtés) ; `LoDb__Contact__Recipient` | analytics, contact | J-3 |
| `infra-vps` : collecte de `api:9464` des projets `lodb-staging` et `lodb-prod` (jamais un port publié) ; vérifier que les requêtes `edge-*` de `queries.logsql` renvoient des lignes sur le VictoriaLogs de l'hôte (champs confirmés contre `docs/guides/observabilite.md` seulement) | surveillance | J-2 |
| Mémoire libre de l'hôte : la pré-ingestion (conteneur ponctuel `api`, limite 1152m) tourne à côté de l'ancienne prod | pré-ingestion | J-2 |
| Espace disque : dump de la base, archives des volumes `lodb-prod_storage` et `lodb-prod_app_state`, volume `lodb-prod_ddragon` pré-rempli | sauvegarde, pré-ingestion | J-2 |
| Accès prêts : Search Console (propriété du domaine), tableau de bord Stripe, Grafana, `gh` connecté (secrets) | fenêtre | J |
| Fichiers Android de prod dans `$PROD_PATH/.deploy/android` (`latest.json`, `assetlinks.json`, [`release-android.md`](../guides/release-android.md)) ; sans eux, les deux URL répondent 404 | App Links, mises à jour | J |
| Publication des apps (release desktop signée, piste Play) : elles visent `https://league-of-data-base.com/api` et n'ont de sens qu'après la bascule | lots 9 et 10 | J+3 |
| *Contract* puis décommission | fin de la période de retour arrière | J+30 |

Chaque secret et chaque ligne des `.env`, avec l'origine de sa valeur :
[`configuration.md`](../guides/configuration.md).

Constat, pas un défaut : la 301 `www.`/`.fr` de nginx porte ses en-têtes de sécurité. En
local (jalon du lot 8), `curl -sI -H 'Host: www.league-of-data-base.com'
http://localhost:18080/en/` renvoie une 301 avec `Strict-Transport-Security`,
`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` et
`Cross-Origin-Resource-Policy` ; seule la CSP manque, sans effet sur une redirection. `.fr`
passe par le même bloc `server` que `www.`.

### 1.1 Bascule de `staging`

La même que celle de la prod (§ 6.1), déclenchée par la fusion de la bascule dans `dev`.

1. Sur l'hôte, garder ce que la bascule écrase :

   ```bash
   install -d -m 700 /root/lodb-backups
   install -m 600 "$STAGING_PATH/.env" /root/lodb-backups/staging-legacy.env
   registry="$(grep -E '^REGISTRY=' "$STAGING_PATH/.env" | cut -d= -f2)"
   docker tag "$registry/nginx:staging" "$registry/nginx:legacy-staging"
   ```

2. Secrets : `ENV_STAGING` ← `.env.staging`, `STAGING_DATA_PROTECTION_PFX` ← le `.pfx` en
   base64. Les autres (`STAGING_SSH_KEY`, `STAGING_HOST`, `STAGING_PATH`, `STAGING_SSH_USER`)
   ne changent pas.
3. Fusionner dans `dev` et pousser. `ci.yml` : contrôles, fusion `dev`→`test`, images `:<sha>`
   et `:staging`, puis `_deploy.yml` sur `$STAGING_PATH` : `pull`, `migrate`, `up -d
   --remove-orphans` (retire `php`, `go-fetcher`, `go-api`), smoke tests, TLS.
4. Contrôles : `tools/cutover/smoke.sh https://test.league-of-data-base.com
   https://api.test.league-of-data-base.com`, connexion d'un compte existant de staging.

## 2. Gel

À J-14, annoncé à tous ceux qui committent :

- **Ancienne stack** : correctifs seulement. **Aucune migration Doctrine** : `Baseline` en
  est le miroir exact et `migrate` refuse un schéma inattendu. Une migration Doctrine
  indispensable exige sa jumelle EF et `tools/schema/check.sh`
  ([rapport de schéma](rapports/schema-baseline.md)).
- **Nouvelle stack** : correctifs et écarts de parité seulement. Tout correctif de l'ancienne
  stack visible des joueurs se reporte dans la nouvelle avant J-3.
- La révision candidate `REV` est choisie dans `dev`, gelée ; tout changement ultérieur
  relance la répétition sur `staging` (§ 3.2, étapes 3 à 6). La promotion reprend ce que
  `:staging` désigne au moment du merge `test`→`main` : **aucun push sur `dev`** entre la
  validation de `REV` et la fenêtre.

## 3. Répétition

### 3.1 Répétition locale (critère de sortie du lot 8)

Sur le poste, depuis la racine du dépôt, ancienne stack lancée **depuis `legacy/` de ce même
dossier** (projet `lodb`, ports 8080, 8090, 5432) ou arrêtée ; jamais en même temps qu'un
build Android en conteneur. La « nouvelle stack » du critère est ici l'emplacement
`lodb-dev-e2` (ports 18280, 18281, 18282, 15632, 18225) : c'est lui qui sert la copie migrée.
La stack locale `lodb-dev` reste intacte pendant toute la répétition.

```bash
test -f legacy/.env || cp legacy/.env.example legacy/.env   # dev, jamais un secret réel
npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install
node --test 'tools/cutover/test/*.test.mjs'
tools/contract/check.sh                 # contract : préparé, vérifié, non appliqué
set -o pipefail                              # bash et zsh : le code du pipeline est celui du script
tools/cutover/rehearse.sh --slot 2 --anonymize --stop-legacy 2>&1 | tee /tmp/lodb-rehearsal.log
echo "code $?"                               # 0 : la répétition passe
```

`--stop-legacy` arrête l'ancienne stack à la fin ; l'omettre si elle doit continuer de
tourner. Le script remet toujours l'ancienne stack sur sa base, supprime l'emplacement
(`down -v`) et la copie.

**Juger la réussite** : le code du script est 0, et son résumé final montre **15 lignes**
`ok` suivies de « The rehearsal passes », qu'il n'imprime qu'en sortie 0. Sans
`set -o pipefail`, `$?` est celui de `tee`, 0 même si le script échoue : lire alors, juste
après la commande, `$pipestatus[1]` sous zsh (shell par défaut de macOS, où
`${PIPESTATUS[0]}` est vide) ou `${PIPESTATUS[0]}` sous bash.

Chaque étape du script correspond à une étape de ce runbook :

| Étape du script | Étape du runbook |
|---|---|
| Copy … into `lodb_rehearsal`, anonymized | § 3.2 étape 2 (dump restauré ailleurs que dans la base servie) |
| Existing accounts of the copy | § 3.2 étape 5 (comptes réels, un par format de hash) |
| Build the new stack | image candidate (§ 3.2 étape 1) |
| migrate on `lodb_rehearsal` (Baseline marked, additive migrations) | § 5.2 (deux passages : le second n'applique rien, comme celui de la fenêtre) |
| Pre-ingestion | § 5.3 |
| New stack up on `lodb_rehearsal` | § 6.1, étape 4 (même base que l'ancienne stack) |
| Smoke tests, 301 of the former sitemaps, Read-only E2E | § 6.2 |
| Existing accounts on the new stack, API key | § 6.2 (connexion, hash réécrit en argon2id, clé d'API, `/v1/usage`) |
| New stack stopped (rollback) | § 8.2, étape 2 |
| Legacy stack on `lodb_rehearsal` ; key pages ; sign-in and `/v1/usage` | § 8.2, étapes 2 et 3 (retour arrière prouvé sur le schéma migré) |
| Legacy stack back on its own database | remise en état |

**Comptes existants.** La base de dev de l'ancienne stack peut n'avoir aucun compte à mot de
passe : celle du poste compte 1 utilisateur, `password IS NULL`. `--anonymize` n'a alors
aucun compte de la base à connecter ; le script l'écrit sur stderr (ligne `WARNING`, reprise
dans le journal par `2>&1`) sans faire échouer l'étape. Les 8 comptes `cutover_c01` à
`cutover_c08`, écrits dans la copie **avant** `migrate` comme Doctrine les écrit (un par
format de hash de `tests/fixtures/hashes`), tiennent lieu de comptes existants : présents
avant la migration, ils satisfont le critère.

Ce que la répétition locale ne couvre pas, et que § 3.2 et la fenêtre couvrent : le dump
réel, la reprise des agrégats et du journal d'audit, l'edge (TLS, labels Caddy), le
remplacement des conteneurs dans le même projet (recréation de `postgres`,
`--remove-orphans`) et le retour arrière depuis `legacy/`, la surveillance. Ni la connexion
à l'ancienne stack d'un compte **créé** par la nouvelle (seuls des comptes existants y sont
repris) : à vérifier à la main sur la copie avec `--keep` si la répétition doit le couvrir.

Consigner le résumé du script (durées comprises) dans le rapport du jalon, sous
`docs/reecriture/rapports/jalons/`.

### 3.2 Répétition sur `staging` avec le dump réel chiffré

Entre J-14 et J-7, sur l'hôte (le VPS qui porte aussi la prod), en root, `staging` déjà
basculé (§ 1.1). Le dump réel ne quitte jamais l'hôte, n'est jamais écrit en clair sur le
disque et est détruit le jour même. Il est restauré dans une base à part, `lodb_rehearsal` :
la base `lodb` de staging n'est pas touchée. Pendant la répétition, staging sert des données
réelles : fenêtre de quelques heures, `noindex` actif, **relais SMTP coupé**.

1. **`staging` sur la révision candidate** : déployé par `ci.yml` au push de `dev`, annonce
   « Deployed revision » = `REV`. Stripe y est en clés de test.
2. **Dump de staging, puis dump de prod chiffré, restauré à part** (phrase de passe saisie,
   jamais écrite) :

   ```bash
   install -d -m 700 /root/lodb-rehearsal && cd /root/lodb-rehearsal
   pg_user="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_USER)"
   pg_db="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_DB)"
   docker exec lodb-prod-postgres-1 pg_dump -U "$pg_user" -Fc "$pg_db" \
     | gpg --symmetric --cipher-algo AES256 --pinentry-mode loopback -o prod.dump.gpg
   cd "$STAGING_PATH" && export COMPOSE_FILE=compose.yaml:compose.deploy.yaml
   st_user="$(docker compose exec -T postgres printenv POSTGRES_USER)"
   docker compose exec -T postgres pg_dump -U "$st_user" -Fc lodb > /root/lodb-backups/staging.dump
   docker compose exec -T postgres psql -U "$st_user" -d postgres -c 'CREATE DATABASE lodb_rehearsal'
   gpg --decrypt --pinentry-mode loopback /root/lodb-rehearsal/prod.dump.gpg \
     | docker compose exec -T postgres pg_restore -U "$st_user" -d lodb_rehearsal \
         --no-owner --no-privileges --exit-on-error
   ```

   Noter les comptes de lignes (`users`, `builds`, `api_keys`) de la source et de la copie.
3. **Migrations additives**, chronométrées, deux fois (le second passage n'applique rien) :

   ```bash
   # surchargent le .env pour toutes les commandes suivantes ; un hôte SMTP vide coupe l'envoi
   export LODB_DB_NAME=lodb_rehearsal LoDb__Mail__Host=
   time docker compose run --rm migrate && docker compose run --rm migrate
   ```

   Compose recrée le conteneur `postgres` (sa variable `POSTGRES_DB` change) : son volume,
   donc la base `lodb` de staging, est conservé.

4. **Reprise des agrégats et de l'audit** depuis les volumes de l'ancienne prod, montés en
   lecture seule ; d'abord à blanc :

   ```bash
   legacy=(-v lodb-prod_storage:/legacy-storage:ro -v lodb-prod_app_state:/legacy-state:ro)
   docker compose run --rm --no-deps "${legacy[@]}" api analytics import \
     --source /legacy-storage/analytics/daily --dry-run
   time docker compose run --rm --no-deps "${legacy[@]}" api analytics import \
     --source /legacy-storage/analytics/daily
   time docker compose run --rm --no-deps "${legacy[@]}" api audit import \
     --source /legacy-state/audit/events --source /legacy-storage/audit
   docker compose up -d --wait
   docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' \
     "$(docker compose ps -q api)" | grep -E '^LoDb__Mail__Host='   # vide
   ```

5. **Contrôles** depuis le poste (outils de L8.2), `STAGING=https://test.league-of-data-base.com` :

   ```bash
   tools/cutover/smoke.sh "$STAGING" https://api.test.league-of-data-base.com
   node tools/cutover/check-301.mjs --base "$STAGING" \
     --sitemap https://league-of-data-base.com/sitemap.xml --historical 2
   tools/cutover/readonly-e2e.sh "$STAGING" --workers=2 --retries=1
   ```

   Comptes réels : se connecter avec des comptes **dont on connaît le mot de passe** (les
   siens, un par format de hash présent en prod : bcrypt ancien, argon2). Vérifier la
   réécriture du hash sur la copie :
   `docker compose exec -T postgres psql -U "$st_user" -d lodb_rehearsal -Atc "SELECT left(password, 10) FROM users WHERE username = '<pseudo>'"`
   → `$argon2id$`. Ouvrir le profil, un build, le portail des clés (`/v1/usage` d'une clé
   existante), le panneau admin (rôle donné sur la copie comme au § 5.4 :
   `docker compose run --rm --no-deps api admin create --email …`).
   Comparer quelques totaux des panneaux admin à ceux de l'ancienne prod.
6. **Consigner** dans `docs/reecriture/rapports/` : durées de `migrate`, des imports et du
   démarrage, résultats des contrôles, anomalies. Une anomalie se corrige, puis la répétition
   reprend à l'étape 1 avec la nouvelle `REV`.
7. **Destruction**, le jour même :

   ```bash
   unset LODB_DB_NAME LoDb__Mail__Host && docker compose up -d --wait   # staging revient sur lodb
   docker compose exec -T postgres psql -U "$st_user" -d postgres \
     -c 'DROP DATABASE lodb_rehearsal WITH (FORCE)'
   # pages rendues avec des données réelles (profils publics…) : le volume doit être libre
   docker compose rm -sf nginx && docker volume rm lodb-staging_pages-cache \
     && docker compose up -d --wait nginx
   shred -u /root/lodb-rehearsal/prod.dump.gpg && rmdir /root/lodb-rehearsal
   ```

   Vérifier qu'aucune copie ne subsiste (`ls /root`, `docker volume ls`, bases de staging :
   `\l`). La destruction se note dans le rapport ; `staging.dump` se garde jusqu'à la fin.
8. **Retour arrière répété** : § 8.2 sur staging (`$STAGING_PATH`, `staging-legacy.env`, tag
   `legacy-staging`), contrôles de l'ancien staging, puis rebascule en relançant le job
   `deploy-staging` du dernier run de `ci.yml` (*Re-run job*).

## 4. Changelog joueurs (J-3)

Les brouillons sont dans [`changelog-bascule/`](changelog-bascule/README.md). La page
`/changelog` de la nouvelle stack lit les releases publiées **au moment du build**
(`src/LoDb.Web/src/app/features/editorial/changelog/published/`) : la release de la bascule
doit donc être dans l'image promue.

1. Remplacer la date `AAAA-MM-JJ` des brouillons par la date de J, les copier dans
   `docs/changelog/<année>/<date>-<slug>.md` ; relire contre ce qui est réellement livré
   (apps publiées ou non, cf. README des brouillons).
2. Synthétiser ces entrées, et le backlog de `docs/changelog/<année>/`, en une release
   publique (`<date>-<nom>.json` et `manifest.json` dans ce même dossier), puis archiver les
   entrées dans `docs/changelog/archived/<année>/` ([`changelog/README.md`](../changelog/README.md)).
3. Commit sur `dev` (déploiement automatique sur staging), contrôle de `/fr/changelog` ;
   cette révision devient `REV`. Si J recule, redater et reconstruire.

## 5. Préparation de la prod (J-2 et J-1)

La pré-ingestion écrit le manifeste dans la base : les migrations additives sont donc
appliquées **à J-2**, avant elle, et non dans la fenêtre. Elles sont additives et la
répétition a prouvé que l'ancienne stack tourne sur le schéma migré ; le `migrate` de la
fenêtre n'applique alors rien, ou les seules migrations arrivées depuis.

### 5.1 Dossier candidat et sauvegarde

La prod ne se déploie que par le merge `test`→`main` (§ 6.1). La préparation se fait donc à
la main, depuis un dossier à part, `$CANDIDATE_PATH` (`/root/lodb-candidate`), qui porte les
fichiers de la nouvelle stack et le `.env` de la prod. Ce `.env` nomme le projet `lodb-prod` :
depuis ce dossier, **seulement** `docker compose run --rm --no-deps …`, jamais `up`, `down`
ni un `run` sans `--no-deps`, qui recréerait le `postgres` de l'ancienne prod.

```bash
install -d -m 700 "$CANDIDATE_PATH" && cd "$CANDIDATE_PATH"
git init -q && git remote add origin <url du dépôt> && git fetch origin test
git checkout -B test origin/test
(umask 077 && cat > .env)          # coller le contenu de .env.prod, puis Ctrl-D
install -d -m 700 .deploy
(umask 022 && base64 -d > .deploy/data-protection.pfx)   # coller le .pfx en base64, Ctrl-D
export COMPOSE_FILE=compose.yaml:compose.deploy.yaml IMAGE_TAG="$REV"
docker compose config --quiet && docker compose pull api
```

`IMAGE_TAG="$REV"` surcharge le `prod` du `.env` : `lodb/api:prod` n'existe qu'après la
promotion. Sauvegarde, avant toute écriture dans la base de prod, avec ce que la bascule
écrase (le `.env` de l'hôte, réécrit depuis `ENV_PROD`, et `lodb/nginx:prod`) :

```bash
dir=/root/lodb-backups/$(date -u +%Y%m%dT%H%MZ) && install -d -m 700 "$dir"
pg_user="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_USER)"
pg_db="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_DB)"
docker exec lodb-prod-postgres-1 pg_dump -U "$pg_user" -Fc "$pg_db" > "$dir/lodb.dump"
docker exec -i lodb-prod-postgres-1 pg_restore --list < "$dir/lodb.dump" > /dev/null && echo dump lisible
install -m 600 "$PROD_PATH/.env" "$dir/legacy.env"
registry="$(grep -E '^REGISTRY=' "$PROD_PATH/.env" | cut -d= -f2)"
docker tag "$registry/nginx:prod" "$registry/nginx:legacy-prod"
docker image inspect --format '{{index .RepoDigests 0}}' "$registry/nginx:prod" \
  > "$dir/legacy-nginx.digest"
```

### 5.2 Migrations additives

Depuis `$CANDIDATE_PATH` ; `--no-deps` : le `postgres` de l'ancienne prod tourne déjà.

```bash
time docker compose run --rm --no-deps migrate   # Baseline marquée, puis les migrations des lots
docker compose run --rm --no-deps migrate        # « 0 migration(s) applied »
```

Un refus (`db.baseline.refused`, code 1) arrête tout : le schéma de prod diffère de
Doctrine, la base n'a pas été modifiée. Contrôler ensuite l'ancien site (pages clés,
connexion) : `tools/cutover/legacy-smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com`.

### 5.3 Pré-ingestion

```bash
time tools/cutover/pre-ingest.sh --latest 3 -- -p lodb-prod \
  -f compose.yaml -f compose.deploy.yaml
```

Remplit `lodb-prod_ddragon` (créé ici, repris tel quel par la bascule) et le manifeste des
trois dernières versions dans toutes les langues (183 s en local pour trois versions). Sans
risque pour l'ancienne prod : un conteneur ponctuel `api` (`--no-deps`), qui ne recrée ni
n'arrête rien, ne monte pas l'ancien `storage` et joint la base par le réseau du projet.
Code 1 après la seconde passe : relancer plus tard ; la longue traîne reste à la demande.
Si Riot publie un patch entre J-2 et J, relancer à J-1. Sans pré-ingestion, l'API ingère
après la bascule : les premières pages d'une version pas encore ingérée sont plus lentes.

### 5.4 Administrateurs et anciens sitemaps

Les rôles Symfony (`users.roles`) ne sont pas repris : chaque administrateur reçoit le rôle
de la nouvelle stack (compte existant, mot de passe conservé ; TOTP enrôlé à la première
connexion), depuis `$CANDIDATE_PATH` :

```bash
docker compose run --rm --no-deps api admin create --email <adresse de l'administrateur>
```

À J-1, enregistrer les sitemaps de l'ancien site (après la bascule, `/sitemap.xml` est celui
de la nouvelle stack), sur le poste :

```bash
mkdir -p old-sitemaps && cd old-sitemaps
curl -fsS https://league-of-data-base.com/sitemap.xml -o index.xml
grep -o '<loc>[^<]*' index.xml | cut -c6- | while read -r loc; do
  curl -fsS "$loc" -o "$(basename "$loc")"; done
```

## 6. Fenêtre de bascule (J)

Heure creuse, au moins deux personnes : l'une exécute, l'autre suit la surveillance. Durée
attendue : moins d'une heure.

### 6.1 Bascule

1. **Pré-contrôles** (T-30) : `REV` validée sur staging et `:staging` = `REV` (depuis le
   poste, même digest pour `ghcr.io/<owner>/lodb/api:staging` et `:$REV`,
   `docker buildx imagetools inspect`) ; `test` contient `REV` ; pré-ingestion à jour
   (§ 5.3) ; `old-sitemaps/` complet ; mémoire et disque de l'hôte ; aucun run de `ci.yml`
   en cours.
2. **Consolidation et sauvegarde** (T-15). Le `php` de l'ancienne stack disparaît à T0 : ses
   journées closes se consolident avant.

   ```bash
   cd "$PROD_PATH" && export COMPOSE_FILE=compose.yaml:compose.deploy.yaml
   docker compose exec -T -u www-data php php bin/console app:analytics:rollup
   docker compose exec -T -u www-data php php bin/console app:audit:rollup
   ```

   Puis sauvegarde complète comme au § 5.1 (nouveau `$dir`), et instantané des volumes de
   l'ancienne stack :

   ```bash
   for volume in storage app_state; do
     docker run --rm -v "lodb-prod_$volume:/v:ro" -v "$dir:/out" alpine \
       tar -C /v -czf "/out/$volume.tgz" .
   done
   ```

3. **Secrets** (T-5) : `ENV_PROD` ← `.env.prod`, `PROD_DATA_PROTECTION_PFX` ← le `.pfx` en
   base64. Plus rien d'autre ne part sur `main` avant l'étape suivante.
4. **Merge `test`→`main`** (T0), poussé. `ci.yml` retague `:staging` → `:prod` et
   `:latest`, puis `_deploy.yml` sur `$PROD_PATH` : `.env` et certificat écrits, checkout de
   `main`, contrôle du `.env` (projet `lodb-prod`, tag `prod`, SMTP et Stripe présents),
   `pull`, `migrate` (rien à appliquer, ou le reliquat ; Compose recrée `postgres` avec sa
   nouvelle définition, quelques secondes sans base pour l'ancienne stack), `up -d
   --remove-orphans --wait` (nginx remplacé ; `php`, `go-fetcher`, `go-api` retirés), smoke
   test dans la stack, TLS public. Un échec **avant** `up` laisse l'ancienne stack servir ;
   **après**, elle est déjà retirée : correction immédiate ou retour arrière (§ 8.2).
5. Les **301 héritées** sont actives dès que la nouvelle stack sert le domaine
   (`docker/nginx/server.d/legacy-redirects.conf`, résolues par l'API).

### 6.2 Contrôles (T+5)

Depuis le poste :

```bash
tools/cutover/smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com
node tools/cutover/check-301.mjs --base https://league-of-data-base.com \
  --sitemap old-sitemaps/latest.xml --sitemap old-sitemaps/<dernière version>.xml
tools/cutover/readonly-e2e.sh https://league-of-data-base.com --workers=2 --retries=1
```

Puis à la main : connexion d'un compte existant, `https://league-of-data-base.com/fr/`,
connexion Google, un `/b/<jeton>` connu, `/v1/usage` d'une clé existante,
`/.well-known/assetlinks.json`, `/robots.txt` sans `noindex` (`curl -sI … | grep -i
x-robots-tag` ne renvoie rien). Un échec ici est un critère de retour arrière (§ 8.1).

### 6.3 Reprise des agrégats et de l'audit (T+15)

Attendre que la nouvelle stack ait agrégé le jour en cours (tâche toutes les 5 min) :

```bash
cd "$PROD_PATH" && docker compose exec -T postgres psql -U "$pg_user" -d "$pg_db" \
  -Atc "SELECT day, source FROM analytics_daily ORDER BY day DESC LIMIT 2"   # aujourd'hui | events
```

Puis reprendre les fichiers de l'ancienne stack, dans ses volumes restés en place :

```bash
legacy=(-v lodb-prod_storage:/legacy-storage:ro -v lodb-prod_app_state:/legacy-state:ro)
docker compose run --rm --no-deps "${legacy[@]}" api analytics import --source /legacy-storage/analytics/daily
docker compose run --rm --no-deps "${legacy[@]}" api audit import \
  --source /legacy-state/audit/events --source /legacy-storage/audit
```

Un jour déjà agrégé est laissé tel quel : le jour J garde la seule part de la nouvelle stack
(la part de l'ancienne, de minuit à la bascule, n'est pas reprise — limite connue). Les deux
imports se relancent sans doublon.

### 6.4 Après la fenêtre

- Search Console : soumettre `https://league-of-data-base.com/sitemap.xml` (index par locale),
  inspecter quelques anciennes et nouvelles URL.
- `rm -rf "$CANDIDATE_PATH"` : il porte le `.env` de la prod.
- Garder les sauvegardes de la fenêtre (dont `legacy.env` et le tag `nginx:legacy-prod`)
  jusqu'au *contract* ; une copie qui quitte l'hôte est chiffrée (`gpg --symmetric`).

## 7. Surveillance renforcée (72 h)

Requêtes de [`queries.logsql`](../../tools/cutover/monitoring/queries.logsql) (Grafana,
VictoriaLogs) avec `{{stack}}` = `lodb-prod`, `{{domain}}` = `league-of-data-base.com`,
et de [`metrics.promql`](../../tools/cutover/monitoring/metrics.promql) ; elles portent
sur des clés d'événement, jamais sur le niveau deviné ([`observabilite.md`](../guides/observabilite.md)).

| Période | Rythme | `{{window}}` |
|---|---|---|
| T0 à T+2 h | toutes les 15 min | `15m` |
| T+2 h à J+1 | toutes les 2 h (jour) | `2h` |
| J+1 à J+3 | matin et soir | `12h` |
| J+1 à J+7 | Search Console, chaque jour | — |

| Requête | Attendu | Alerte |
|---|---|---|
| `edge-5xx`, `ssr-5xx`, `ssr-failures`, `api-5xx` (PromQL) | rares, isolés | taux de 5xx > 1 % des requêtes sur 15 min (`edge-traffic`) |
| `edge-legacy-4xx`, `legacy-unresolved` | aucun, hors URLs hors table | une ancienne URL récurrente en 4xx : ligne de table manquante |
| `api-errors`, `jobs-failed`, `jobs-stale` | aucune clé nouvelle ; chaque tâche a réussi dans sa période | une tâche sans succès depuis deux périodes |
| `ingestion`, `versions-ready`, `queue-depth`, `on-demand-refused` | versions complètes ; file qui se vide | `ingest.version.failed`, file qui grossit |
| `accounts-mail`, `outbox-pending` | file vide en quelques minutes | `outbox.message.dead`, file qui grossit |
| `payments` | aucun rejet | tout `billing.webhook.rejected` ou `failed` sur un vrai paiement |
| `public-api` | aucun | `publicapi.request.unavailable`, `publicapi.usage.flush_failed` |
| `egress-refused` | aucun | tout refus (événement de sécurité) |
| `volume` ; mémoire `container_memory_working_set_bytes{stack="lodb-prod"}` | stable | service muet, ou mémoire > 80 % de sa limite |

Search Console : erreurs d'exploration, pages 404, « Page avec redirection » (attendu pour les
anciennes URL), couverture des nouveaux sitemaps, erreurs hreflang.

Le critère du plan, **72 h sans régression**, est atteint quand aucune alerte n'est restée
sans correction ni explication pendant ces 72 h. La bascule est alors confirmée ; le retour
arrière reste possible jusqu'au *contract*.

## 8. Retour arrière

### 8.1 Critères

| Situation | Décision |
|---|---|
| Connexion impossible pour des comptes existants, ou écritures perdues (comptes, builds, votes, favoris) | retour arrière **immédiat** |
| 5xx > 1 % sur 15 min, ou rendu SSR en échec continu | retour arrière si pas corrigé en 30 min |
| Paiements Stripe ou `/v1` en échec pour des clients réels | retour arrière si pas corrigé en 1 h |
| Anciennes URL massivement en 4xx (> 5 % des accès aux anciennes URL) | retour arrière si pas corrigé en 2 h |
| Défaut isolé (une page, un affichage, une URL hors table) | correction en avant : nouvelle `REV` validée sur staging, puis merge `test`→`main` |

Après les 72 h, seuls les deux premiers cas justifient encore un retour arrière. Après le
*contract*, il n'y en a plus : on revient par la sauvegarde d'avant *contract*.

### 8.2 Procédure

Tant que le schéma reste compatible (migrations additives, jusqu'au *contract*), sur l'hôte,
en root, **sans la CI** : les workflows de l'ancienne stack sont archivés, et un push de
`main` redéploierait la nouvelle. Rien ne part sur `main` jusqu'à la rebascule ; `ENV_PROD`
garde le contenu de la nouvelle stack, qui servira à la rebascule.

1. Remettre le `.env` et l'image `nginx` de l'ancienne stack. Le checkout de `$PROD_PATH`
   porte ses fichiers sous `legacy/`, dont le `.env`, ignoré par Git, survit aux `git reset
   --hard` des déploiements suivants :

   ```bash
   dir=/root/lodb-backups/<dossier de la fenêtre>
   install -m 600 "$dir/legacy.env" "$PROD_PATH/legacy/.env"
   registry="$(grep -E '^REGISTRY=' "$dir/legacy.env" | cut -d= -f2)"
   docker tag "$registry/nginx:legacy-prod" "$registry/nginx:prod"
   # tag local perdu : docker pull "$(cat "$dir/legacy-nginx.digest")", puis docker tag
   ```

2. L'ancienne stack reprend sa place (même projet `lodb-prod`, même `pgdata`) :

   ```bash
   cd "$PROD_PATH/legacy" && export COMPOSE_FILE=compose.yaml:compose.deploy.yaml
   docker compose config | grep -m1 '^name:'      # name: lodb-prod
   docker compose up -d --no-build --pull never --remove-orphans --wait
   docker compose ps    # php, nginx, go-fetcher, go-api, postgres ; plus d'api ni de web-ssr
   ```

   `--pull never` : les `:prod` d'`app`, `go-fetcher` et `go-api` sont ceux de l'ancienne
   stack, que la nouvelle ne pousse jamais (absents de l'hôte : `docker compose pull php
   go-fetcher go-api` d'abord). `nginx` et `postgres` sont recréés avec leur ancienne
   définition, `api` et `web-ssr` retirés ; les volumes restent (`ddragon` sert à la
   rebascule). Aucune migration Doctrine (gel).
3. Contrôles : `tools/cutover/legacy-smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com`,
   connexion d'un compte existant (son hash, réécrit en argon2id, est lu par PHP), `/v1/usage`
   d'une clé.

Ce qui est conservé : comptes, builds, votes, favoris, clés et crédits créés entre-temps sont
dans les tables communes. Ce qui est perdu pour l'ancienne stack : les tables propres à la
nouvelle (analytics, audit, manifeste…), qu'elle ignore ; les sessions (chacun se
reconnecte) ; les nouvelles URL que les moteurs auraient déjà explorées, en 404 côté ancienne
stack jusqu'à la bascule suivante.

Rebasculer ensuite : correction, validation sur staging, puis § 6.1 à partir de l'étape 1
(consolidation et sauvegarde comprises) ; le déploiement réécrit le `.env` depuis `ENV_PROD`
et retire de nouveau l'ancienne stack. Les imports du § 6.3 ne reprennent que ce qui manque.

## 9. *Contract* (J+30)

Trente jours après la bascule sans retour arrière, et sur décision explicite : après lui,
l'ancienne stack ne peut plus tourner sur la base.

- Contenu, vérification et mise en œuvre : [`tools/contract/README.md`](../../tools/contract/README.md)
  (tables `messenger_messages`, `reset_password_request`, `doctrine_migration_versions` et
  colonne `users.roles` supprimées, sept horodatages passés en `timestamptz` UTC).
- Sauvegarde de la base juste avant, restaurée à blanc pour la vérifier.
- Le *contract* devient une migration EF ordinaire, appliquée par `migrate` : staging
  d'abord, puis merge `test`→`main`. Jamais de SQL à la main sur la prod.

## 10. Décommission

Après le *contract*. La base n'a pas à déménager : elle est déjà celle du service `postgres`
des projets `lodb-staging` et `lodb-prod`.

### 10.1 Ce qui disparaît

Le code, les compose, les environnements, les workflows et les guides de l'ancienne stack sont
déjà **archivés sous `legacy/`** (tag `archive/stack-php` sur `main` avant l'archivage) : la
décommission supprime ce dossier.

| Quoi | Où |
|---|---|
| Ancienne stack archivée | `legacy/` : `app/`, `go/`, `docker/{nginx,php}/`, `compose*.yaml`, `.env*.example`, `.dockerignore`, `.github/workflows/` (`ci.yml`, `_*.yml`), `tailwind.config.js`, `tools/screenshots/`, `screenshot/`, `README.md`, `docs/guides/` |
| Outils de la transition | `tools/cutover/`, `tools/schema/`, `tools/contract/`, `tools/parity/` et `tools/builds-parity/` (comparaisons avec l'ancienne stack), `Persistence/Baseline/doctrine-catalog.txt` et `tests/fixtures/schema/` quand `Baseline` ne se compare plus à Doctrine |
| Images | `ghcr.io/<owner>/lodb/app`, `lodb/go-fetcher`, `lodb/go-api` ; les tags `<sha>` de l'ancienne stack sur `lodb/nginx` |
| Sur l'hôte | volumes `storage` et `app_state` de `lodb-prod` et `lodb-staging` **après export**, `legacy/.env` des deux checkouts, tags locaux `nginx:legacy-prod` et `nginx:legacy-staging`, sauvegardes `legacy.env` ; secret `ENV_TEST` (plus lu). **Garder** `STAGING_*`, `PROD_*`, `ENV_STAGING` et `ENV_PROD`, que la nouvelle stack utilise |

Export avant suppression des volumes : `lodb-prod_app_state` (`var/state` : journal d'audit,
événements, GeoLite2) et les préfixes `analytics/` et `audit/` de `lodb-prod_storage`, en
archives chiffrées hors de l'hôte (même commande `tar` qu'au § 6.1). Les blobs Data Dragon ne
s'exportent pas : ils se régénèrent.

### 10.2 Déménagement du changelog public

**Fait avant la bascule**, avec l'archivage de l'ancienne stack sous `legacy/` : les releases
JSON et `manifest.json` vivent dans la feature qui les lit,
`src/LoDb.Web/src/app/features/editorial/changelog/published/`, un vrai dossier à la place de
l'ancien lien symbolique. La copie de `app/public/changelog/` dans l'image `web-ssr` et dans
les builds Android a disparu. Reste à faire : pointer l'outillage du changelog joueur
(synthèse de release, [`changelog/README.md`](../changelog/README.md)) sur ce dossier. Le
contexte de build de `web-ssr` peut redevenir `src/LoDb.Web`.

L'ancienne stack archivée n'a plus de changelog : relancée depuis `legacy/`, sa page
`/changelog` est vide et sa version affichée est `0.0.0`.

### 10.3 Documentation à réécrire

| Document | Changement |
|---|---|
| `AGENTS.md` | **fait** : décrit la nouvelle stack (stack, commandes, garde-fous, scopes), seul fichier d'instructions versionné ; les règles PHP sont dans `legacy/AGENTS.md`, supprimé avec `legacy/` |
| `README.md`, `CONTRIBUTING.md`, `docs/README.md` | **fait** avec l'archivage (l'ancien README est `legacy/README.md`) |
| `docs/contribution.md` | démarrage, commandes et garde-fous de la nouvelle stack, en FR, EN et ES (bandeau d'avertissement en attendant) |
| `docs/architecture/` (`architecture.md`, `architecture-report.md`, `analytics.md`, `api-publique.md`, `responsive-mobile.md`) | réécrits d'après `docs/reecriture/README.md` et les ADR |
| `docs/guides/setup.md`, `docker.md`, `configuration.md` | **fait** : archivés sous `legacy/docs/guides/`, remplacés par `developpement.md` et le nouveau `configuration.md` |
| `docs/guides/github-actions-secrets.md` | **fait** : guide du pipeline de la nouvelle stack ; l'ancien est sous `legacy/docs/guides/` |
| `docs/guides/observabilite.md`, `logging.md` | exemples sur `api` et `web-ssr` (`EventName`), plus de `php` ni `go-api` |
| `docs/guides/oauth-google-setup.md` | à jour pour la nouvelle stack ; à la décommission, retirer du client OAuth l'URI `/connect/google/check` et le § 6 |
| `docs/guides/packaging-apk.md` | **fait** : archivé sous `legacy/docs/guides/`, remplacé par l'ADR 0007 et `release-android.md` |
| `docs/guides/legal-info.md`, `migration-edge-proxy.md` | relus contre la nouvelle stack |
| `docs/changelog/README.md` | table des scopes (Twig, îlots Vue, Symfony, Go → front Angular, API .NET) |
| `docs/reecriture/` | statut « basculé » dans son README ; ce runbook complété des dates et relevés réels |

## Points ouverts

- **Branche `test`** : la fusion automatique `dev`→`test` de `ci.yml` échoue si `test` a
  divergé (correctif poussé directement sur `test` ou `main`) ; vérifier avant § 1.1.
- **Recréation de `postgres` par `migrate`** : le job lance `docker compose run --rm migrate`
  sans `--no-deps`. Que Compose recrée ou non le `postgres` de l'ancienne stack à cette
  étape, les données restent (volume nommé) ; à observer sur staging (§ 1.1) pour chiffrer la
  coupure et vérifier que le contrôle de santé (`pg_isready -U $POSTGRES_USER -d
  $POSTGRES_DB`, alimenté par `LODB_DB_USER`/`LODB_DB_NAME`) passe sur la base existante.
- **Coupure SMTP de la répétition** : `LoDb__Mail__Host=` vide dans l'environnement du shell
  doit primer sur le `.env` (étape 4 du § 3.2 le vérifie) ; sinon, commenter la ligne dans
  `$STAGING_PATH/.env` le temps de la répétition.
