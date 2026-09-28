# Runbook de bascule

Procédure pas à pas de la bascule du domaine de l'ancienne stack (Symfony + Go + Vue, projet
Compose `lodb-prod`) vers la nouvelle (.NET + Angular, projet `lodb-next-prod`). Elle met en
œuvre [`plan-migration.md`](plan-migration.md) (« Bascule (lot 8) ») avec les outils du lot 8 :

- déploiement et promotion (L8.1) : [`github-actions-secrets.md`](../guides/github-actions-secrets.md),
  section « Nouvelle stack » ;
- outils de la répétition et de la fenêtre (L8.2) : [`tools/next/cutover/`](../../tools/next/cutover/README.md) ;
- migration de *contract*, préparée et non appliquée (L8.3) : [`tools/next/contract/`](../../tools/next/contract/README.md).

La **répétition locale** (§ 3.1) suit ce runbook tel quel : c'est le critère de sortie du
lot 8, rejoué par son jalon. Tout écart entre la répétition et ce texte se corrige **ici**.

## 0. Repères

| Nom | Valeur |
|---|---|
| Domaine | `league-of-data-base.com` (canonique), `league-of-data-base.fr` ; API publique `api.league-of-data-base.com` |
| Ancienne prod | projet `lodb-prod`, dossier `$PROD_PATH` sur l'hôte, fichiers `compose.yaml` + `compose.deploy.yaml` |
| Nouvelle prod | projet `lodb-next-prod`, dossier `$PROD_NEXT_PATH` (neuf), fichiers `compose.next.yaml` + `compose.next.deploy.yaml` |
| `next` | projet `lodb-next`, dossier `$NEXT_PATH`, sa propre base (dump anonymisé) |
| Base partagée | le PostgreSQL de l'ancienne stack (`lodb-prod-postgres-1`, volume `lodb-prod_pgdata`), rejoint par la nouvelle stack sur le réseau `lodb-prod_default` ; **il ne s'arrête jamais** |
| Révision candidate | `REV` : SHA complet validé sur `next`, annoncé par « Deployed revision » |
| Registre | `ghcr.io/<owner>/lodb/{api,web-ssr,nginx}`, tags `:<sha>`, `:next`, `:next-prod` (jamais `:prod`, qui reste à l'ancienne stack) |

Sur l'hôte, toute commande `docker compose` de la nouvelle stack se lance depuis son dossier
avec ses fichiers :

```bash
cd "$PROD_NEXT_PATH" && export COMPOSE_FILE=compose.next.yaml:compose.next.deploy.yaml
# sur l'hôte next, en plus : export COMPOSE_PROFILES=bundled-database
```

Et celles de l'ancienne : `cd "$PROD_PATH" && export COMPOSE_FILE=compose.yaml:compose.deploy.yaml`.

### Calendrier

| Quand | Étape | § |
|---|---|---|
| J-21 | opérations hôte et humaines faites ou planifiées | 1 |
| J-14 | gel | 2 |
| J-14 à J-7 | répétition locale, puis répétition sur `next` avec le dump réel chiffré, puis destruction | 3 |
| J-3 | entrées de changelog datées, release publique préparée dans l'image candidate | 4 |
| J-2 | sauvegarde, migrations additives et pré-ingestion sur la base de prod, administrateurs | 5 |
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
| Pousser la branche d'intégration (`docs/reecriture-dotnet-angular`) ; `next-ci.yml` vert, images `:<sha>` et `:next` produites | tout déploiement | J-14 |
| Hôte `next` : DNS de son domaine et de `api.` + domaine, secrets `NEXT_*` et `ENV_NEXT`, dump anonymisé restauré, `COMPOSE_PROFILES=bundled-database` dans `ENV_NEXT` | répétition sur `next` | J-14 |
| Environnement GitHub `production` : *required reviewers*, *deployment branches* limitée à la branche des workflows ; secrets `PROD_NEXT_SSH_KEY`, `PROD_NEXT_HOST`, `PROD_NEXT_PATH`, `ENV_PROD_NEXT`, `PROD_NEXT_DATA_PROTECTION_PFX` déclarés **dans l'environnement** | promotion | J-3 |
| Certificat Data Protection **propre à la prod** généré (guide des secrets), `.pfx` gardé hors ligne, clé détruite | connexions en prod | J-3 |
| `ENV_PROD_NEXT` : `LODB_DB_NETWORK=lodb-prod_default`, `LODB_DB_NAME/USER/PASSWORD` de l'ancienne stack, `LODB_EDGE_CIDR` relevé sur l'hôte, `CADDY_DOMAINS` et `API_CADDY_DOMAINS` de l'ancienne stack, `LODB_PUBLIC_API_ORIGIN`, pas de `COMPOSE_PROFILES` | promotion | J-3 |
| Relais SMTP (`LoDb__Mail__*`, mêmes identifiants que le `MAILER_DSN` de l'ancienne prod), SPF/DKIM/DMARC du domaine expéditeur | e-mails de compte | J-3 |
| Stripe : clé live et secret `whsec_` de l'endpoint **existant** (`/webhooks/stripe` ne change pas) dans `ENV_PROD_NEXT` ; clés de test et endpoint propre sur `next` | paiements | J-3 |
| Google OAuth : ajouter l'URI de retour `https://league-of-data-base.com/api/account/google/callback` au client web, **sans retirer** celle de l'ancienne stack (retour arrière) ; clients Android et desktop | connexion Google | J-3 |
| `LoDb__Analytics__VisitorKey` = `APP_SECRET` de l'ancienne prod (mêmes visiteurs des deux côtés) ; `LoDb__Contact__Recipient` | analytics, contact | J-3 |
| `infra-vps` : collecte de `api:9464` des projets `lodb-next` et `lodb-next-prod` (jamais un port publié) ; vérifier que les requêtes `edge-*` de `queries.logsql` renvoient des lignes sur le VictoriaLogs de l'hôte (champs confirmés contre `docs/guides/observabilite.md` seulement) | surveillance | J-2 |
| Mémoire libre de l'hôte : pendant la fenêtre, l'ancienne stack et la nouvelle (limites : api 1152m, web-ssr 704m, nginx 64m, migrate 128m) tournent ensemble | fenêtre | J-2 |
| Espace disque : dump de la base, archives des volumes `lodb-prod_storage` et `lodb-prod_app_state`, volume `lodb-next-prod_storage` pré-rempli | sauvegarde, pré-ingestion | J-2 |
| Accès prêts : Search Console (propriété du domaine), tableau de bord Stripe, Grafana, reviewers de `production` joignables pendant la fenêtre | fenêtre | J |
| Fichiers Android de prod dans `$PROD_NEXT_PATH/.deploy/android` (`latest.json`, `assetlinks.json`, [`release-android.md`](../guides/release-android.md)) ; sans eux, les deux URL répondent 404 | App Links, mises à jour | J |
| Désactiver le workflow de déploiement de l'ancienne prod (`ci.yml`, Actions ▸ *Disable workflow*) : son `up -d` redémarrerait les conteneurs arrêtés, qui réclameraient à nouveau le domaine | après la fenêtre | J |
| Publication des apps (release desktop signée, piste Play) : elles visent `https://league-of-data-base.com/api` et n'ont de sens qu'après la bascule | lots 9 et 10 | J+3 |
| *Contract* puis décommission | fin de la période de retour arrière | J+30 |

Reste ouvert du lot 8, à corriger avant J-3 : `.env.next.example` incomplet (ni
`LODB_PUBLIC_API_ORIGIN` ni ligne `LoDb__*`, pas de variante prod ; `LODB_DB_*` y figure).

Constat, pas un défaut : la 301 `www.`/`.fr` de nginx porte ses en-têtes de sécurité. En
local (jalon du lot 8), `curl -sI -H 'Host: www.league-of-data-base.com'
http://localhost:18080/en/` renvoie une 301 avec `Strict-Transport-Security`,
`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` et
`Cross-Origin-Resource-Policy` ; seule la CSP manque, sans effet sur une redirection. `.fr`
passe par le même bloc `server` que `www.`.

## 2. Gel

À J-14, annoncé à tous ceux qui committent :

- **Ancienne stack** : correctifs seulement. **Aucune migration Doctrine** : `Baseline` en
  est le miroir exact et `migrate` refuse un schéma inattendu. Une migration Doctrine
  indispensable exige sa jumelle EF et `tools/next/schema/check.sh`
  ([rapport de schéma](rapports/schema-baseline.md)).
- **Nouvelle stack** : correctifs et écarts de parité seulement. Tout correctif de l'ancienne
  stack visible des joueurs se reporte dans la nouvelle avant J-3.
- La révision candidate `REV` est choisie dans la branche d'intégration gelée ; tout
  changement ultérieur relance la répétition sur `next` (§ 3.2, étapes 3 à 6).

## 3. Répétition

### 3.1 Répétition locale (critère de sortie du lot 8)

Sur le poste, depuis la racine du dépôt, ancienne stack lancée **depuis ce même dossier**
(projet `lodb`, ports 8080, 8090, 5432) ou arrêtée ; jamais en même temps qu'un build Android
en conteneur. La « nouvelle stack » du critère est ici l'emplacement `lodb-next-e2` (ports
18280, 18281, 18282, 15632, 18225) : c'est lui qui sert la copie migrée. La stack
d'intégration `lodb-next` reste intacte, sur sa propre base, pendant toute la répétition ;
il n'y a rien à y remettre ensuite.

```bash
test -f .env || cp .env.example .env        # valeurs de dev, jamais un secret réel
npm ci --prefix tests/LoDb.E2E && npm --prefix tests/LoDb.E2E run browsers:install
node --test 'tools/next/cutover/test/*.test.mjs'
tools/next/contract/check.sh                 # contract : préparé, vérifié, non appliqué
set -o pipefail                              # bash et zsh : le code du pipeline est celui du script
tools/next/cutover/rehearse.sh --slot 2 --anonymize --stop-legacy 2>&1 | tee /tmp/lodb-rehearsal.log
echo "code $?"                               # 0 : la répétition passe
```

`--stop-legacy` arrête l'ancienne stack à la fin ; l'omettre si elle doit continuer de
tourner. Le script remet toujours l'ancienne stack sur sa base, supprime l'emplacement
(`down -v`) et la copie.

**Juger la réussite** : le code du script est 0, et son résumé final montre **15 lignes**
`ok` suivies de « The rehearsal passes », qu'il n'imprime qu'en sortie 0. Sans
`set -o pipefail`, `$?` est celui de `tee`, 0 même si le script échoue : lire alors, juste après la commande,
`$pipestatus[1]` sous zsh (shell par défaut de macOS, où `${PIPESTATUS[0]}` est vide) ou
`${PIPESTATUS[0]}` sous bash.

Chaque étape du script correspond à une étape de ce runbook :

| Étape du script | Étape du runbook |
|---|---|
| Copy … into `lodb_rehearsal`, anonymized | § 3.2 étape 2 (dump restauré ailleurs que dans la base servie) |
| Existing accounts of the copy | § 3.2 étape 5 (comptes réels, un par format de hash) |
| Build the new stack | image candidate (§ 3.2 étape 1) |
| migrate on `lodb_rehearsal` (Baseline marked, additive migrations) | § 5.2 (deux passages : le second n'applique rien, comme celui de la fenêtre) |
| Pre-ingestion | § 5.3 |
| New stack up on `lodb_rehearsal` | § 6.1, étape 3 (même base que l'ancienne stack) |
| Smoke tests, 301 of the former sitemaps, Read-only E2E | § 6.2 |
| Existing accounts on the new stack, API key | § 6.2 (connexion, hash réécrit en argon2id, clé d'API, `/v1/usage`) |
| New stack stopped (rollback) | § 8.2, étape 1 |
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
réel, la reprise des agrégats et du journal d'audit, l'edge (TLS, labels Caddy, reprise
des domaines), la surveillance. Ni la connexion à l'ancienne stack d'un compte **créé** par la
nouvelle (seuls des comptes existants y sont repris) : à vérifier à la main sur la copie avec
`--keep` si la répétition doit le couvrir.

Consigner le résumé du script (durées comprises) dans le rapport du jalon, sous
`docs/reecriture/rapports/jalons/`.

### 3.2 Répétition sur `next` avec le dump réel chiffré

Entre J-14 et J-7, sur l'hôte (le VPS qui porte aussi la prod), en root. Le dump réel ne
quitte jamais l'hôte, n'est jamais écrit en clair sur le disque et est détruit le jour même.
Pendant la répétition, `next` sert des données réelles : fenêtre de quelques heures, adresse
non diffusée, `noindex` actif.

1. **`next` sur la révision candidate** : Actions ▸ *next deploy* (branche d'intégration),
   annonce « Deployed revision » = `REV`. Vérifier que `next` n'envoie aucun e-mail réel :
   `grep '^LoDb__Mail__Host=' "$NEXT_PATH/.env"` ne renvoie rien (les e-mails attendent
   alors dans `email_outbox`, détruite avec la copie). Stripe y est en clés de test.
2. **Dump chiffré, restauré à part** (phrase de passe saisie, jamais écrite) :

   ```bash
   install -d -m 700 /root/lodb-rehearsal && cd /root/lodb-rehearsal
   pg_user="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_USER)"
   pg_db="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_DB)"
   docker exec lodb-prod-postgres-1 pg_dump -U "$pg_user" -Fc "$pg_db" \
     | gpg --symmetric --cipher-algo AES256 --pinentry-mode loopback -o prod.dump.gpg
   cd "$NEXT_PATH" && export COMPOSE_FILE=compose.next.yaml:compose.next.deploy.yaml \
     COMPOSE_PROFILES=bundled-database
   docker compose exec -T postgres psql -U lodb -d postgres -c 'CREATE DATABASE lodb_rehearsal'
   gpg --decrypt --pinentry-mode loopback /root/lodb-rehearsal/prod.dump.gpg \
     | docker compose exec -T postgres pg_restore -U lodb -d lodb_rehearsal \
         --no-owner --no-privileges --exit-on-error
   ```

   (`lodb` : `LODB_DB_USER` de `next`.) Noter les comptes de lignes (`users`, `builds`,
   `api_keys`) de la source et de la copie.
3. **Migrations additives**, chronométrées, deux fois (le second passage n'applique rien) :

   ```bash
   export LODB_DB_NAME=lodb_rehearsal     # surcharge le .env pour toutes les commandes suivantes
   time docker compose run --rm migrate && docker compose run --rm migrate
   ```

   Compose peut recréer le conteneur `postgres` (sa variable `POSTGRES_DB` change) : son
   volume, donc la base de `next`, est conservé.

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
   ```

5. **Contrôles** depuis le poste (outils de L8.2), `NEXT=https://<domaine de next>` :

   ```bash
   tools/next/cutover/smoke.sh "$NEXT" "https://api.<domaine de next>"
   node tools/next/cutover/check-301.mjs --base "$NEXT" \
     --sitemap https://league-of-data-base.com/sitemap.xml --historical 2
   tools/next/cutover/readonly-e2e.sh "$NEXT" --workers=2 --retries=1
   ```

   Comptes réels : se connecter avec des comptes **dont on connaît le mot de passe** (les
   siens, un par format de hash présent en prod : bcrypt ancien, argon2). Vérifier la
   réécriture du hash sur la copie :
   `docker compose exec -T postgres psql -U lodb -d lodb_rehearsal -Atc "SELECT left(password, 10) FROM users WHERE username = '<pseudo>'"`
   → `$argon2id$`. Ouvrir le profil, un build, le portail des clés (`/v1/usage` d'une clé
   existante), le panneau admin (rôle donné sur la copie comme au § 5.4 :
   `docker compose run --rm --no-deps api admin create --email …`).
   Comparer quelques totaux des panneaux admin à ceux de l'ancienne prod.
6. **Consigner** dans `docs/reecriture/rapports/` : durées de `migrate`, des imports et du
   démarrage, résultats des contrôles, anomalies. Une anomalie se corrige, puis la répétition
   reprend à l'étape 1 avec la nouvelle `REV`.
7. **Destruction**, le jour même :

   ```bash
   unset LODB_DB_NAME && docker compose up -d --wait        # next revient sur sa base
   docker compose exec -T postgres psql -U lodb -d postgres \
     -c 'DROP DATABASE lodb_rehearsal WITH (FORCE)'
   # pages rendues avec des données réelles (profils publics…)
   docker compose stop nginx && docker volume rm lodb-next_pages-cache \
     && docker compose up -d --wait nginx
   shred -u /root/lodb-rehearsal/prod.dump.gpg && rmdir /root/lodb-rehearsal
   ```

   Vérifier qu'aucune copie ne subsiste (`ls /root`, `docker volume ls`, bases de `next` :
   `\l`). La destruction se note dans le rapport.

## 4. Changelog joueurs (J-3)

Les brouillons sont dans [`changelog-bascule/`](changelog-bascule/README.md). La page
`/changelog` de la nouvelle stack lit les releases publiées **au moment du build**
(`src/LoDb.Web/src/app/features/editorial/changelog/published/`) : la release de la bascule doit donc être dans l'image
promue.

1. Remplacer la date `AAAA-MM-JJ` des brouillons par la date de J, les copier dans
   `docs/changelog/<année>/<date>-<slug>.md` ; relire contre ce qui est réellement livré
   (apps publiées ou non, cf. README des brouillons).
2. Synthétiser ces entrées, et le backlog de `docs/changelog/<année>/`, en une release
   publique (`<date>-<nom>.json` et `manifest.json` dans ce même dossier), puis archiver les
   entrées dans `docs/changelog/archived/<année>/` ([`changelog/README.md`](../changelog/README.md)).
3. Commit sur la branche d'intégration, déploiement sur `next`, contrôle de `/fr/changelog` ;
   cette révision devient `REV`. Si J recule, redater et reconstruire.

## 5. Préparation de la prod (J-2 et J-1)

La pré-ingestion écrit le manifeste dans la base : les migrations additives sont donc
appliquées **à J-2**, avant elle, et non dans la fenêtre. Elles sont additives et la
répétition a prouvé que l'ancienne stack tourne sur le schéma migré ; le `migrate` de la
fenêtre n'applique alors rien, ou les seules migrations arrivées depuis.

### 5.1 Dossier de la nouvelle prod et sauvegarde

Le job de promotion refuse de toucher à l'hôte tant que l'ancienne stack porte le domaine :
cette préparation est manuelle, avec les mêmes fichiers que le job écrira.

```bash
install -d -m 700 "$PROD_NEXT_PATH" && cd "$PROD_NEXT_PATH"
git init -q && git remote add origin <url du dépôt> && git fetch origin docs/reecriture-dotnet-angular
git checkout -B docs/reecriture-dotnet-angular origin/docs/reecriture-dotnet-angular
(umask 077 && cat > .env)          # coller le contenu exact du secret ENV_PROD_NEXT, puis Ctrl-D
install -d -m 700 .deploy
(umask 022 && base64 -d > .deploy/data-protection.pfx)   # coller PROD_NEXT_DATA_PROTECTION_PFX, Ctrl-D
export COMPOSE_FILE=compose.next.yaml:compose.next.deploy.yaml COMPOSE_PROFILES= IMAGE_TAG="$REV"
docker compose config --quiet && docker compose pull api
```

`IMAGE_TAG="$REV"` surcharge le `next-prod` du `.env` : `:next-prod` n'existe qu'après la
promotion. Sauvegarde, avant toute écriture dans la base de prod :

```bash
dir=/root/lodb-backups/$(date -u +%Y%m%dT%H%MZ) && install -d -m 700 "$dir"
pg_user="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_USER)"
pg_db="$(docker exec lodb-prod-postgres-1 printenv POSTGRES_DB)"
docker exec lodb-prod-postgres-1 pg_dump -U "$pg_user" -Fc "$pg_db" > "$dir/lodb.dump"
docker exec -i lodb-prod-postgres-1 pg_restore --list < "$dir/lodb.dump" > /dev/null && echo dump lisible
```

### 5.2 Migrations additives

```bash
time docker compose run --rm migrate   # Baseline marquée, puis les migrations des lots
docker compose run --rm migrate        # « 0 migration(s) applied »
```

Un refus (`db.baseline.refused`, code 1) arrête tout : le schéma de prod diffère de
Doctrine, la base n'a pas été modifiée. Contrôler ensuite l'ancien site (pages clés,
connexion) : `tools/next/cutover/legacy-smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com`.

### 5.3 Pré-ingestion

```bash
time tools/next/cutover/pre-ingest.sh --latest 3 -- -p lodb-next-prod \
  -f compose.next.yaml -f compose.next.deploy.yaml
```

Remplit `lodb-next-prod_storage` et le manifeste des trois dernières versions dans toutes
les langues (183 s en local pour trois versions). Code 1 après la seconde passe : relancer
plus tard ; la longue traîne reste à la demande. Si Riot publie un patch entre J-2 et J,
relancer la pré-ingestion à J-1.

### 5.4 Administrateurs et anciens sitemaps

Les rôles Symfony (`users.roles`) ne sont pas repris : chaque administrateur reçoit le rôle
de la nouvelle stack (compte existant, mot de passe conservé ; TOTP enrôlé à la première
connexion) :

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

Heure creuse, au moins deux personnes : l'une exécute, l'autre suit la surveillance et
approuve dans `production`. Durée attendue : moins d'une heure.

### 6.1 Bascule

1. **Pré-contrôles** (T-30) : `REV` validée sur `next` ; les trois images `:<REV>` présentes ;
   pré-ingestion à jour (§ 5.3) ; `old-sitemaps/` complet ; mémoire et disque de l'hôte ;
   rien d'autre en cours de déploiement.
2. **Sauvegarde** (T-15) : dump de la base comme au § 5.1, puis instantané des volumes de
   l'ancienne stack :

   ```bash
   for volume in storage app_state; do
     docker run --rm -v "lodb-prod_$volume:/v:ro" -v "$dir:/out" alpine \
       tar -C /v -czf "/out/$volume.tgz" .
   done
   ```

3. **Promotion avec reprise des domaines** (T0) : Actions ▸ *next promote*, `revision` = `REV`,
   `branch` = branche d'intégration, `take_over_domains` coché ; approuver `production`.
   Le job, dans l'ordre : retag `:<REV>` → `:next-prod`, `pull`, `migrate` (rien à
   appliquer, ou le reliquat), arrêt des conteneurs de `lodb-prod` qui portent le domaine
   (`nginx`, `go-api`), `up -d --wait`, smoke test dans la stack, révision servie = `REV`,
   TLS public. En cas d'échec après l'arrêt, il **redémarre** les conteneurs de l'ancienne
   stack : le domaine lui revient seul, sans perte (même base).
4. Les **301 héritées** sont actives dès que le domaine pointe sur la nouvelle stack
   (`docker/next/nginx/server.d/legacy-redirects.conf`, résolues par l'API).

### 6.2 Contrôles (T+5)

Depuis le poste :

```bash
tools/next/cutover/smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com
node tools/next/cutover/check-301.mjs --base https://league-of-data-base.com \
  --sitemap old-sitemaps/latest.xml --sitemap old-sitemaps/<dernière version>.xml
tools/next/cutover/readonly-e2e.sh https://league-of-data-base.com --workers=2 --retries=1
```

Puis à la main : connexion d'un compte existant, `https://league-of-data-base.com/fr/`,
connexion Google, un `/b/<jeton>` connu, `/v1/usage` d'une clé existante, `/.well-known/assetlinks.json`,
`/robots.txt` sans `noindex` (`curl -sI … | grep -i x-robots-tag` ne renvoie rien).
Un échec ici est un critère de retour arrière (§ 8.1).

### 6.3 Reprise des agrégats et de l'audit (T+15)

Attendre que la nouvelle stack ait agrégé le jour en cours (tâche toutes les 5 min) :

```bash
cd "$PROD_PATH" && docker compose exec -T postgres psql -U "$pg_user" -d "$pg_db" \
  -Atc "SELECT day, source FROM analytics_daily ORDER BY day DESC LIMIT 2"   # aujourd'hui | events
```

Puis consolider l'ancienne stack (son `php` tourne encore) et reprendre ses fichiers :

```bash
docker compose exec -T -u www-data php php bin/console app:analytics:rollup
docker compose exec -T -u www-data php php bin/console app:audit:rollup
cd "$PROD_NEXT_PATH"
legacy=(-v lodb-prod_storage:/legacy-storage:ro -v lodb-prod_app_state:/legacy-state:ro)
docker compose run --rm --no-deps "${legacy[@]}" api analytics import --source /legacy-storage/analytics/daily
docker compose run --rm --no-deps "${legacy[@]}" api audit import \
  --source /legacy-state/audit/events --source /legacy-storage/audit
```

Un jour déjà agrégé est laissé tel quel : le jour J garde la seule part de la nouvelle stack
(la part de l'ancienne, de minuit à la bascule, n'est pas reprise — limite connue). Les deux
imports se relancent sans doublon. Ensuite seulement, arrêter le reste de l'ancienne stack,
**sauf son PostgreSQL** :

```bash
cd "$PROD_PATH" && docker compose stop php go-fetcher
docker compose ps    # postgres : running ; nginx, go-api, php, go-fetcher : exited
```

### 6.4 Après la fenêtre

- Search Console : soumettre `https://league-of-data-base.com/sitemap.xml` (index par locale),
  inspecter quelques anciennes et nouvelles URL.
- Désactiver le déploiement de l'ancienne prod (`ci.yml`), cf. § 1.
- Garder les sauvegardes de la fenêtre jusqu'au *contract* ; une copie qui quitte l'hôte est
  chiffrée (`gpg --symmetric`).

## 7. Surveillance renforcée (72 h)

Requêtes de [`queries.logsql`](../../tools/next/cutover/monitoring/queries.logsql) (Grafana,
VictoriaLogs) avec `{{stack}}` = `lodb-next-prod`, `{{domain}}` = `league-of-data-base.com`,
et de [`metrics.promql`](../../tools/next/cutover/monitoring/metrics.promql) ; elles portent
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
| `volume` ; mémoire `container_memory_working_set_bytes{stack="lodb-next-prod"}` | stable | service muet, ou mémoire > 80 % de sa limite |

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
| Défaut isolé (une page, un affichage, une URL hors table) | correction en avant : nouvelle `REV` validée sur `next`, puis promotion (sans reprise de domaines) |

Après les 72 h, seuls les deux premiers cas justifient encore un retour arrière. Après le
*contract*, il n'y en a plus : on revient par la sauvegarde d'avant *contract*.

### 8.2 Procédure

Tant que le schéma reste compatible (migrations additives, jusqu'au *contract*) :

1. La nouvelle stack rend le domaine ; ses tâches de fond s'arrêtent aussi :

   ```bash
   cd "$PROD_NEXT_PATH" && COMPOSE_FILE=compose.next.yaml:compose.next.deploy.yaml \
     docker compose stop nginx web-ssr api
   ```

2. L'ancienne stack reprend le domaine (mêmes conteneurs, jamais `up`, qui les recréerait) :

   ```bash
   cd "$PROD_PATH" && COMPOSE_FILE=compose.yaml:compose.deploy.yaml docker compose start
   ```

3. Contrôles : `tools/next/cutover/legacy-smoke.sh https://league-of-data-base.com https://api.league-of-data-base.com`,
   connexion d'un compte existant (son hash, réécrit en argon2id, est lu par PHP), `/v1/usage`
   d'une clé. Réactiver `ci.yml` si des correctifs de l'ancienne stack doivent partir.

Ce qui est conservé : comptes, builds, votes, favoris, clés et crédits créés entre-temps sont
dans les tables communes. Ce qui est perdu pour l'ancienne stack : les tables propres à la
nouvelle (analytics, audit, manifeste…), qu'elle ignore ; les sessions (chacun se
reconnecte) ; les nouvelles URL que les moteurs auraient déjà explorées, en 404 côté ancienne
stack jusqu'à la bascule suivante.

Rebasculer ensuite : correction, validation sur `next`, puis § 6.1 à partir de l'étape 1
(sauvegarde comprise), `take_over_domains` coché ; les imports du § 6.3 ne reprennent que ce
qui manque.

## 9. *Contract* (J+30)

Trente jours après la bascule sans retour arrière, et sur décision explicite : après lui,
l'ancienne stack ne peut plus tourner sur la base.

- Contenu, vérification et mise en œuvre : [`tools/next/contract/README.md`](../../tools/next/contract/README.md)
  (tables `messenger_messages`, `reset_password_request`, `doctrine_migration_versions` et
  colonne `users.roles` supprimées, sept horodatages passés en `timestamptz` UTC).
- Sauvegarde de la base juste avant, restaurée à blanc pour la vérifier.
- Le *contract* devient une migration EF ordinaire, appliquée par `migrate` : `next`
  d'abord, puis promotion en prod. Jamais de SQL à la main sur la prod.

## 10. Décommission

Après le *contract*, dans l'ordre.

### 10.1 La base quitte le projet `lodb-prod`

La base partagée vit encore dans le PostgreSQL de l'ancienne stack. Elle rejoint la nouvelle
prod par une maintenance courte : nouvelle stack arrêtée, sauvegarde, restauration dans le
PostgreSQL embarqué de `lodb-next-prod`, puis `LODB_DB_NETWORK` retiré de `ENV_PROD_NEXT`
et `COMPOSE_PROFILES=bundled-database` ajouté. `next-deploy.yml` exige aujourd'hui
`LODB_DB_NETWORK` en prod : le job change avec cette étape. L'ancien volume `lodb-prod_pgdata`
reste intact jusqu'à la fin de la décommission.

### 10.2 Ce qui disparaît

| Quoi | Où |
|---|---|
| Code de l'ancienne stack | `app/`, `go/`, `docker/nginx/`, `docker/php/` |
| Compose et environnements hérités | `compose.yaml`, `compose.override.yaml`, `compose.deploy.yaml`, `.env.example`, `.env.prod.example`, `.env.staging.example`, `.env.test.example`, `.dockerignore` (contexte des images php et nginx) |
| Workflows hérités | `.github/workflows/ci.yml`, `_build.yml`, `_tests.yml`, `_promote.yml`, `_deploy.yml` |
| À examiner, probablement hérités | `tailwind.config.js` racine, `tools/screenshots/`, `screenshot/` |
| Outils de la transition | `tools/next/cutover/`, `tools/next/schema/`, `tools/next/contract/`, `tools/next/parity/` et `tools/next/builds-parity/` (comparaisons avec l'ancienne stack), `Persistence/Baseline/doctrine-catalog.txt` et `tests/fixtures/schema/` quand `Baseline` ne se compare plus à Doctrine |
| Images | `ghcr.io/<owner>/lodb/app`, `lodb/go-fetcher`, `lodb/go-api` ; le tag `:prod` de `lodb/nginx` |
| Sur l'hôte | projets `lodb-prod` et `lodb-staging` (`docker compose down`), leurs volumes **après export**, dossiers `$PROD_PATH` et `STAGING_PATH`, secrets `PROD_*`, `STAGING_*`, `ENV_PROD`, `ENV_STAGING` |

Export avant suppression des volumes : `lodb-prod_app_state` (`var/state` : journal d'audit,
événements, GeoLite2) et les préfixes `analytics/` et `audit/` de `lodb-prod_storage`, en
archives chiffrées hors de l'hôte (même commande `tar` qu'au § 6.1). Les blobs Data Dragon ne
s'exportent pas : ils se régénèrent.

### 10.3 Déménagement du changelog public

**Fait avant la bascule**, avec l'archivage de l'ancienne stack sous `legacy/` : les releases
JSON et `manifest.json` vivent dans la feature qui les lit,
`src/LoDb.Web/src/app/features/editorial/changelog/published/`, un vrai dossier à la place de l'ancien
lien symbolique. La copie de `app/public/changelog/` dans l'image `web-ssr` et dans les
builds Android a disparu. Reste à faire : pointer l'outillage du changelog joueur (synthèse
de release, [`changelog/README.md`](../changelog/README.md)) sur ce dossier. Le contexte de
build de `web-ssr` peut redevenir `src/LoDb.Web`.

L'ancienne stack archivée n'a plus de changelog : relancée depuis `legacy/`, sa page
`/changelog` est vide et sa version affichée est `0.0.0`.

### 10.4 Documentation à réécrire

| Document | Changement |
|---|---|
| `CLAUDE.md` | décrit la stack en service : devient celui de la nouvelle (stack, commandes, garde-fous, scopes) ; les garde-fous PHP disparaissent |
| `README.md`, `CONTRIBUTING.md`, `docs/contribution.md`, `docs/README.md` | démarrage, commandes et arborescence de la nouvelle stack |
| `docs/architecture/` (`architecture.md`, `architecture-report.md`, `analytics.md`, `api-publique.md`, `responsive-mobile.md`) | réécrits d'après `docs/reecriture/README.md` et les ADR |
| `docs/guides/setup.md`, `docker.md`, `configuration.md` | remplacés par `dev-next.md` et la configuration `LoDb__*` |
| `docs/guides/github-actions-secrets.md` | sections de l'ancienne stack retirées ; la « Nouvelle stack » devient le guide |
| `docs/guides/observabilite.md`, `logging.md` | exemples sur `api` et `web-ssr` (`EventName`), plus de `php` ni `go-api` |
| `docs/guides/oauth-google-setup.md` | seule URI de retour `/api/account/google/callback` ; retirer l'ancienne du client OAuth |
| `docs/guides/packaging-apk.md` | supprimé, remplacé par l'ADR 0007 et `release-android.md` |
| `docs/guides/legal-info.md`, `migration-edge-proxy.md` | relus contre la nouvelle stack |
| `docs/changelog/README.md` | table des scopes (Twig, îlots Vue, Symfony, Go → front Angular, API .NET) |
| `docs/reecriture/` | statut « basculé » dans son README ; ce runbook complété des dates et relevés réels |
