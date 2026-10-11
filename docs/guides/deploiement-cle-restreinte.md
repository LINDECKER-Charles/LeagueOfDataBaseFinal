# Déploiement par clé restreinte — procédure du propriétaire, non activée

Aujourd'hui ([`github-actions-secrets.md`](github-actions-secrets.md)), le job de déploiement
ouvre un shell sur l'hôte (en `root` par défaut), y pousse le `.env` complet et le certificat
Data Protection, puis exécute le bloc distant de `.github/workflows/_deploy.yml`. Une clé de
déploiement est donc une clé de shell. La cible : **une clé par environnement qui ne peut que
déclencher le déploiement d'une révision déjà publiée sur la branche de cet environnement**,
et des secrets qui ne quittent plus l'hôte ni le poste du propriétaire. Rien de ceci n'est
activé : cette page est la procédure.

Ce que la cible garantit : une clé de CI volée n'ouvre aucun shell, ne fait suivre aucun
port, ne choisit pas l'environnement, ne déploie pas un commit absent de la branche, et ne
donne accès à aucun secret (GitHub n'en détient plus). Ce qu'elle ne garantit pas : la
révision déployée s'exécute avec les droits du groupe `docker`, c'est-à-dire ceux de `root`.
Ce qui atteint `test` et `main` reste de confiance ; la garde en est GitHub (environnements,
approbation de la prod, [`configuration.md`](configuration.md) § 3.3), pas l'hôte.

## 1. Avant, après

| | Aujourd'hui | Cible |
|---|---|---|
| Compte SSH | `root` (`*_SSH_USER` vide) | `deploy`, sans mot de passe, membre du groupe `docker` |
| Ce que permet la clé de CI | un shell | `/usr/local/sbin/lodb-deploy <env>`, rien d'autre (`restrict`) |
| Environnement visé | entrée du workflow | écrit dans la ligne de la clé |
| Ce que la CI envoie | le script entier, le `.env`, le certificat | un tag d'image ; sur l'entrée, le jeton du registre si les images sont privées |
| `.env` et secrets-fichiers | poussés à chaque déploiement depuis `ENV_*` | posés par le propriétaire (§ 7) ; `ENV_*` supprimés de GitHub |
| Certificat Data Protection | poussé à chaque déploiement | posé une fois |
| Clés de l'hôte | `ssh-keyscan` en repli, non vérifié | `*_SSH_KNOWN_HOSTS` obligatoire |
| Logique du déploiement | heredoc de `_deploy.yml` | `tools/deploy/remote.sh` à la révision déployée, même contenu |

## 2. Prérequis

- `_deploy.yml` assemble déjà tous les calques de chaque environnement (`compose.staging.yaml`,
  `compose.staging-auth.yaml` à la demande, `compose.umami.yaml` en prod) et contrôle
  `GET /u/script.js` : le script en reprend la liste, il ne la réinvente pas.
- Secrets en fichiers (`tools/deploy/split-env.sh`, `.deploy/secrets/`), en place.
- Connexion par clé seule sur l'hôte (`infra-vps`, `docs/DURCISSEMENT-HOTE.md` § 1).
- Protections des environnements GitHub ([`configuration.md`](configuration.md) § 3.3).
- `.env` des environnements tenus chiffrés sur le poste ([`secrets-poste.md`](secrets-poste.md)) :
  c'est de là qu'ils partent vers l'hôte.

## 3. Le contrat

### 3.1 Clés et commandes forcées

Les clés vont dans `/home/deploy/.ssh/authorized_keys`, propriété de `root`, `0644`, dossier
`.ssh` à `root`, `0755` : rien de ce qu'elles lancent n'y écrit. Le compte possède toutefois
son dossier personnel, où il pourrait remplacer `.ssh`, et le groupe `docker` vaut `root` :
ce qui borne une clé, c'est sa commande forcée, qui n'exécute que du code déjà sur `test` ou
`main`. Une ligne par clé :

```
restrict,command="/usr/local/sbin/lodb-deploy staging" ssh-ed25519 AAAA… lodb-staging deploy
restrict,command="/usr/local/sbin/lodb-deploy prod" ssh-ed25519 AAAA… lodb-prod deploy
restrict,command="/usr/local/sbin/lodb-env staging" ssh-ed25519 AAAA… lodb-staging env
restrict,command="/usr/local/sbin/lodb-env prod" ssh-ed25519 AAAA… lodb-prod env
```

L'environnement est l'argument écrit dans la ligne de la clé : la commande envoyée par
l'appelant (`SSH_ORIGINAL_COMMAND`) ne le choisit jamais, et la clé de `staging` ne peut pas
déployer la prod. `restrict` coupe terminal, redirections de ports, agent et X11. Les deux
premières clés sont celles de la CI (sans phrase de passe, une par environnement) ; les deux
autres, celles du propriétaire (avec phrase de passe, jamais dans GitHub).

### 3.2 Ce que `lodb-deploy` accepte

- `SSH_ORIGINAL_COMMAND` : **un seul jeton**, `^[0-9a-f]{40}$`, le tag immuable que
  `_build.yml` pousse (`:<sha>`, le commit de `dev`). Tout le reste : refus, code 64, une
  ligne d'explication, rien d'exécuté.
- Entrée standard : au plus une ligne, le `GITHUB_TOKEN` du run (`packages: read`), lue avec
  un délai, seulement si les packages sont privés ; l'entrée est ensuite fermée (un
  `docker compose` lirait le reste comme sa propre entrée). Avec des packages publics, rien.
- La CI passe en `staging` `github.sha`, en `prod` la révision que `:staging` désignait à la
  promotion (`_promote.yml` lit le label `org.opencontainers.image.revision` et la publie en
  sortie ; `deploy-prod` la transmet). Un `:staging` plus récent que `main` (un push de `dev`
  entre la validation du staging et la fusion) est alors refusé au lieu d'être déployé.

### 3.3 `lodb-deploy`, versionné dans `infra-vps`

Court et stable : il borne ce que la clé permet, puis passe la main à la révision.

1. `set -euo pipefail`, `umask 022`, `PATH` fixé ; environnement pris de `$1`, `staging` ou
   `prod` ; `/etc/lodb-deploy/<env>.conf` (à `root`, `0644`) donne `DEPLOY_PATH` et `BRANCH`
   (`test`, `main`) ; l'URL du dépôt y est fixe. Pas `umask 077` : le dépôt extrait est
   public, et des conteneurs non root en lisent des fichiers montés (la politique d'Anubis
   du [mode siège](mode-siege.md), lue par l'utilisateur `1000` de son image) ; les secrets
   ont leurs propres modes (`split-env.sh`, sauvegardes).
2. Tag validé (§ 3.2) ; `flock` sur `/run/lock/lodb-deploy-<env>.lock` : un déploiement à la
   fois par environnement, une seconde demande échoue au lieu d'attendre.
3. Jeton éventuel lu, entrée fermée ; une ligne `logger -t lodb-deploy` au début et à la fin
   (environnement, tag, résultat), jamais un secret.
4. Dans `DEPLOY_PATH` : `git fetch --no-tags` de la seule branche de l'environnement, refus si
   `git merge-base --is-ancestor <tag> origin/<branche>` échoue, puis
   `git checkout --force --detach <tag>` : les fichiers compose de la révision même des
   images. **Jamais** `git clean -x` : il effacerait `.env` et `.deploy/`.
5. `exec bash tools/deploy/remote.sh <env> <tag>`, le script de cette révision.

### 3.4 `tools/deploy/remote.sh`, le bloc distant de `_deploy.yml`

Le heredoc de `_deploy.yml` déplacé dans un fichier, **dans le même ordre**, et lu en place
plutôt que reçu : une seule copie de la logique, versionnée avec les fichiers compose
qu'elle déploie. La copier dans `infra-vps` n'apporterait aucune garantie (les fichiers
compose viennent du dépôt de toute façon) et ferait diverger deux copies à chaque calque.

1. Table par environnement : projet `lodb-<env>`, `LODB_NOINDEX` attendu, sous-réseau de la
   stack (`10.231.1.0/24`, `10.231.2.0/24`).
2. Contrôles du `.env` en place : projet, `LODB_NOINDEX`, mot de passe de la base, rétention
   des bundles ; en prod, relais SMTP et clés Stripe (fichiers non vides). Puis Docker
   Engine 25 ou plus, identifiant des pages du staging bien formé.
3. Calques : `compose.yaml`, `compose.deploy.yaml`, puis ceux de l'environnement et des
   interrupteurs du `.env` ; `COMPOSE_FILE` exporté et réécrit dans le `.env`, comme
   `IMAGE_TAG` (le tag reçu), `LODB_EDGE_CIDR` et `LODB_STACK_SUBNET`.
4. Réseaux `edge` et `observability` présents ; aucun réseau ne recouvre celui de la stack ;
   la configuration résolue ne fait croire à l'API et au SSR que ce sous-réseau.
5. `docker compose pull` (5 tentatives ; `docker login ghcr.io` le temps du seul pull, avec le
   jeton s'il est venu), sauvegarde de la base (`pg_dump -Fc`, relue, dix gardées).
6. Réseau de la stack recréé s'il est sur un autre sous-réseau ; `migrate` ;
   `up -d --no-build --remove-orphans --wait` ; sous-réseau vérifié.
7. Purge des bundles hors rétention ; administrateur racine du `.env`.
8. Smoke tests dans la stack (`/healthz`, `/readyz`, `/en/`, `api.`, `X-Robots-Tag`), contrôle
   de `GET /u/script.js`, révision annoncée, `docker image prune -f`.
9. TLS public de chaque domaine, avec relance de l'edge s'il échoue.

La sortie revient dans le journal du job (les lignes `::notice` et `::warning` y restent
lues) ; le code de sortie est le statut du job. `tools/deploy/test/deploy-checks.test.mjs`
exécute `remote.sh` avec `docker` et `git` simulés, comme il exécute le heredoc aujourd'hui.

### 3.5 `lodb-env`, versionné dans `infra-vps`

La seule voie d'écriture du `.env` et des secrets, pour la clé du propriétaire :

- `SSH_ORIGINAL_COMMAND` = `env` : l'entrée est le dotenv complet de l'environnement ; le
  `.env` en place est d'abord copié dans `.deploy/backups/<horodatage>.env` (dix gardés), puis
  `tools/deploy/split-env.sh .env .deploy/secrets` de la révision en place l'écrit
  (`LODB_SECRETS_REVISION` monte si un secret change : l'API sera recréée) ;
- `SSH_ORIGINAL_COMMAND` = `pfx` : l'entrée est le certificat Data Protection (PKCS#12),
  écrit en `.deploy/data-protection.pfx` (`0644` dans un dossier `0700`) ;
- tout autre mot : refus. Rien ne redémarre : le déploiement suivant applique, et réécrit
  les lignes que `remote.sh` calcule (`COMPOSE_FILE`, `IMAGE_TAG`, sous-réseaux).

## 4. Fichiers de l'hôte

| Chemin | Propriétaire, mode | Contenu | Écrit par |
|---|---|---|---|
| `/usr/local/sbin/lodb-deploy`, `lodb-env` | `root`, `0755` | § 3.3, § 3.5 | le propriétaire, depuis `infra-vps` |
| `/etc/lodb-deploy/{staging,prod}.conf` | `root`, `0644` | `DEPLOY_PATH`, `BRANCH` | le propriétaire |
| `/home/deploy/.ssh/authorized_keys` | `root`, `0644` | § 3.1 | le propriétaire |
| `$STAGING_PATH`, `$PROD_PATH` (dépôt) | `deploy` | fichiers compose, outils | `lodb-deploy` |
| `.env` | `deploy`, `0600` | réglages sans secrets ([`configuration.md`](configuration.md) § 4) | `lodb-env`, puis les lignes calculées par `remote.sh` |
| `.deploy/secrets/` | `deploy`, dossier `0700`, fichiers `0444` | un fichier par secret | `lodb-env` |
| `.deploy/data-protection.pfx` | `deploy`, `0644` | certificat Data Protection | `lodb-env` |
| `.deploy/backups/` | `deploy`, `0700` | `*.dump` (`remote.sh`), `*.env` (`lodb-env`) | les deux |
| `.deploy/android/` | inchangé | `latest.json`, `assetlinks.json` | le propriétaire |

## 5. Mise en place, dans l'ordre

1. **infra-vps** : `lodb-deploy` et `lodb-env` versionnés, vérifiés par `shellcheck` dans
   `./scripts/validate.sh`, décrits dans sa documentation de l'hôte.
2. **LoDb, PR sans effet** : le heredoc de `_deploy.yml` devient `tools/deploy/remote.sh`, que
   le job (toujours en `root`) appelle après avoir séparé `ENV_*` comme aujourd'hui ;
   `remote.sh` accepte encore le tag mobile de l'environnement. Tests du job adaptés.
3. **Hôte** (session du propriétaire) : compte `deploy` (`useradd --create-home --shell
   /bin/bash deploy`, sans mot de passe ; `usermod -aG docker deploy`) ; scripts et
   configurations installés (§ 4) ; `chown -R deploy:deploy` des deux dossiers de déploiement.
   Les conteneurs en service ne sont pas touchés ; le `.env`, les secrets-fichiers et le
   certificat du dernier déploiement restent : ils sont désormais « posés ».
4. **Clés** : quatre paires ed25519 ([`configuration.md`](configuration.md) § 6.3 pour la
   commande ; phrase de passe pour celles du propriétaire), lignes du § 3.1. GitHub :
   `*_SSH_KEY` remplacées, `*_SSH_USER` = `deploy`, `*_SSH_KNOWN_HOSTS` posés.
5. **Preuves à la main** (§ 6), puis un déploiement de `staging` à la main, d'une révision
   qui contient `remote.sh` : `ssh -i lodb-staging-deploy deploy@<hôte> <sha du dernier build>`.
6. **LoDb, bascule de `_deploy.yml`** : plus d'envoi du `.env` ni du certificat ; le
   déploiement est `ssh deploy@<hôte> "<tag>"`, jeton éventuel sur l'entrée ; clés de l'hôte
   exigées ; entrées `ENV_FILE`, `DATA_PROTECTION_PFX` et `DEPLOY_PATH` retirées ; `_promote.yml`
   publie la révision promue. Push de `dev` : `staging` ; fusion dans `main` : `prod`.
7. Deux déploiements réussis par environnement plus tard : supprimer de GitHub `ENV_STAGING`,
   `ENV_PROD`, `*_DATA_PROTECTION_PFX` et `*_PATH` ; retirer les clés de LoDb de
   `/root/.ssh/authorized_keys`.
8. **Les autres dépôts de l'hôte**, `infra-vps` compris, migrent leurs clés de la même façon.
   Un compte d'administration personnel (clé, `sudo`) et l'accès à la console du fournisseur
   sont vérifiés. Alors seulement, `PermitRootLogin no` (`infra-vps`,
   `docs/DURCISSEMENT-HOTE.md`, « Hors de cette check-list ») : avant, ce sont les
   déploiements de ces dépôts qui tomberaient.

## 6. Preuves

```bash
k=lodb-staging-deploy; h=deploy@<hôte>
ssh -i $k $h id                    # refus, code 64 : un tag d'image est attendu
ssh -i $k $h                       # même refus : aucun shell, aucun terminal
# Redirection refusée (restrict) : -N n'ouvre aucune session, la commande forcée ne tourne
# pas, et le refus de l'hôte arrête ssh aussitôt (code 255). Un -L, lui, ne serait refusé
# qu'à la première connexion locale : seul, il attendrait sans rien prouver.
ssh -i $k -N -o ExitOnForwardFailure=yes -R 18022:127.0.0.1:22 $h
ssh -i $k $h 0000000000000000000000000000000000000000   # refus : absent de test
ssh -i lodb-prod-deploy $h <sha présent sur test, pas sur main>   # refus : absent de main
ssh -i lodb-prod-env $h id         # refus : env ou pfx seulement
```

Sur l'hôte : `stat -c '%U %a %n' /usr/local/sbin/lodb-* /home/deploy/.ssh/authorized_keys`
(`root`, `755` et `644`) ; `journalctl -t lodb-deploy` montre les tentatives ; après l'étape 8,
`sshd -T | grep -E '^(permitrootlogin|passwordauthentication) '` rend `no` et `no`.

## 7. Changer un réglage ou un secret

Depuis le poste, le fichier chiffré jamais écrit en clair ([`secrets-poste.md`](secrets-poste.md)) :

```bash
sops edit prod/prod.sops.env
sops decrypt prod/prod.sops.env | ssh -i ~/.ssh/lodb-prod-env deploy@<hôte> env
age --decrypt -i "$SOPS_AGE_KEY_FILE" prod/data-protection.pfx.age \
  | ssh -i ~/.ssh/lodb-prod-env deploy@<hôte> pfx      # rare : il ne change plus une fois servi
```

Puis relancer le dernier job de déploiement de l'environnement : `up` applique le `.env` et
recrée l'API si un secret a changé.

## 8. Retour arrière

Jusqu'à l'étape 7, un `git revert` de la bascule de `_deploy.yml` et `*_SSH_USER` remis à vide
suffisent (les clés de `root` sont encore là). Après : recréer `ENV_*` et
`*_DATA_PROTECTION_PFX` depuis les fichiers chiffrés du poste, remettre une clé de CI à
`root`, puis le même `git revert`. Un refus de `lodb-deploy` ou un contrôle de `remote.sh` en
échec arrête le job avant `up` : la stack en service continue de servir.

## 9. Ce qu'on ne fait pas

| Action | Raison |
|---|---|
| Faire valider le compose rendu par une politique (`jq`, `conftest`) | décision du 2026-10-10 : la garantie porte sur ce qui atteint `test` et `main` (environnements GitHub), pas sur un filtre du compose |
| Choisir l'environnement par la commande envoyée | une clé de `staging` pourrait viser la prod |
| `from="…"` sur les clés de CI | les adresses des runners GitHub sont nombreuses et changent |
| Clé de CI à phrase de passe, clé du propriétaire sans | la première n'y gagne rien ; la seconde ouvre l'écriture des secrets |
| `git clean -x` dans un dossier de déploiement | il effacerait `.env` et `.deploy/` |
| `PermitRootLogin no` avant la migration de tous les dépôts | leurs déploiements tomberaient |
