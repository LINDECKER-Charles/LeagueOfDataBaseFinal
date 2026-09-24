# Lot 8 — Préparation de la bascule

Objectif du lot : tout ce que la bascule exige et qui se construit dans le dépôt —
déploiement, outils, runbook, répétition locale —, pour que la fenêtre de bascule
elle-même, qui reste une opération hôte, se déroule sans improvisation.

Critère de sortie local : **répétition locale** réussie. Une copie de la base de dev de
l'ancienne stack est migrée (marquage de `Baseline`, migrations additives), la nouvelle
stack la sert (E2E en lecture seule, connexion de comptes existants), puis l'**ancienne
stack est relancée sur ce même schéma** et ses pages clés, sa connexion et `/v1/usage`
répondent : le retour arrière est prouvé.

Le critère du plan de migration (« 72 h sans régression après la bascule ») relève de
l'exploitation.

## L8.1 — Déploiement de la nouvelle stack

- **Objectif** : déployer `next` puis la prod par la même mécanique « build once, promote
  by retag », avec les migrations **avant** la bascule du trafic.
- **À lire** : `.github/workflows/{_build,_promote,_deploy}.yml`, `compose.deploy.yaml`,
  [`migration-edge-proxy.md`](../../guides/migration-edge-proxy.md),
  [`github-actions-secrets.md`](../../guides/github-actions-secrets.md), relevés mémoire de
  L0.3 et des E2E, `heritage.md` F1 à F3.
- **Périmètre** : `compose.next.deploy.yaml`, `.github/workflows/{next-deploy,
  next-promote}.yml`, section « nouvelle stack » du guide des secrets.
- **Conception** :
  - Images `:<sha>` construites une fois ; `next` déployé depuis `:next`, la prod promue par
    retag `:prod` après validation manuelle (environnement GitHub protégé).
  - Déploiement : `pull`, puis conteneur éphémère `migrate` de l'image `lodb-api` (L1.4 :
    marquage idempotent de `Baseline` sur une base Doctrine, puis migrations), puis
    `up -d`, puis smoke test — l'inverse de l'ordre actuel (F3). Pas d'image `efbundle`
    (écart consigné au §13 du plan).
  - Réseau `edge`, labels Caddy sur nginx pour `CADDY_DOMAINS` et `API_CADDY_DOMAINS` ;
    `noindex` d'edge sur `next` ; limites mémoire à deux fois le pic observé.
  - Base : `next` a sa propre base (dump anonymisé) ; en prod, la nouvelle stack rejoint la
    base existante (réseau externe paramétré), sans la déplacer.
  - Volume `storage` propre à la nouvelle stack, pré-rempli avant la bascule (L8.2).
- **Tests** : `docker compose config` en `next` et en prod ; `actionlint` ; déploiement de
  bout en bout simulé en local (`migrate` sur une copie de base, puis stack).
- **Dépend de** : lots 3 à 7. **Taille** : M.

## L8.2 — Outils de bascule

- **Objectif** : les scripts de la répétition et de la fenêtre. L'anonymisation,
  `baseline mark-applied` et `migrate` existent depuis L1.4.
- **À lire** : [`plan-migration.md`](../plan-migration.md) (bascule, table des 301),
  `tools/next/db/` (anonymisation), sous-commandes `migrate` et `ingest`.
- **Périmètre** : `tools/next/cutover/**`, étiquette `@readonly` sur les tests E2E
  concernés (ajout d'étiquette seulement), tests associés.
- **Conception** :
  - Script de répétition locale (critère de sortie du lot) : copie de la base de dev de
    l'ancienne stack, `migrate`, nouvelle stack servie dessus, E2E `@readonly`, connexion de
    comptes existants, puis ancienne stack relancée sur la même base (pages clés,
    connexion, `/v1/usage`).
  - Pré-ingestion : usage documenté de `ingest --latest 3 --languages all`.
  - Vérificateur des 301 : parcourt les URLs des anciens sitemaps (ou une liste), exige une
    301 vers la nouvelle forme puis un 200.
  - Smoke tests et E2E en lecture seule contre une URL de base quelconque.
  - Requêtes de surveillance de la fenêtre (LogsQL sur les clés d'événement : 5xx, 404 sur
    URLs héritées, métriques d'ingestion).
- **Tests** : vérificateur des 301 contre la stack locale ; script de répétition exécuté de
  bout en bout.
- **Dépend de** : lots 3 à 7. **Taille** : M.

## L8.3 — Runbook, retour arrière, *contract*, changelog joueurs

- **Objectif** : la procédure écrite de la bascule et de ses suites.
- **À lire** : `plan-migration.md` (bascule, retour arrière, *contract*, décommission),
  [`observabilite.md`](../../guides/observabilite.md), [`changelog/README.md`](../../changelog/README.md).
- **Périmètre** : `docs/reecriture/bascule.md`, `docs/reecriture/changelog-bascule/**`,
  migration de *contract* préparée **mais non appliquée** (fichier isolé, documenté).
- **Conception** :
  - Runbook pas à pas : gel, répétition avec dump réel chiffré puis destruction,
    pré-ingestion, fenêtre (sauvegarde Postgres + instantané du volume, migrations,
    bascule des labels Caddy, 301 actives, smoke tests, surveillance 72 h avec les requêtes
    de L8.2), critères de retour arrière et procédure.
  - *Contract* après 30 jours : suppression de `messenger_messages`,
    `doctrine_migration_versions` et `reset_password_request`, horodatages passés en
    `timestamptz`, colonnes mortes (dont `users.roles`).
  - Décommission : liste de ce qui disparaît (`app/`, `go/`, `docker/nginx/`,
    `docker/php/`, anciens compose et workflows, volumes après export de `var/state`), du
    déménagement de `app/public/changelog/` vers le front (et de l'outillage du changelog
    joueur qui l'écrit), et des docs à réécrire (`architecture/`, guides,
    `packaging-apk.md`).
  - Brouillons des entrées de changelog joueurs de la bascule (nouvelles URLs par langue,
    pages plus rapides, apps), au format de `docs/changelog/`, à dater le jour de la
    bascule.
- **Acceptation** : la répétition locale suit le runbook tel qu'écrit.
- **Dépend de** : L8.1, L8.2. **Taille** : M.
