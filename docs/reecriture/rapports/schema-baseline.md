# Schéma : `Baseline` face aux migrations Doctrine (L1.4)

La migration EF `20260719150001_Baseline` décrit le schéma que créent les 11 migrations
Doctrine de l'ancienne stack. Ce rapport dit comment on le vérifie, ce qui a été vérifié et
les seuls écarts, tous voulus.

## Méthode

`tools/next/schema/check.sh`, lancé depuis la racine du dépôt, ancienne stack démarrée :

1. **Référence Doctrine.** Crée une base vide dédiée `lodb_schema_ref` sur le serveur
   PostgreSQL de l'ancienne stack, y applique les migrations Doctrine par le conteneur `php`
   (`-u www-data`, `DATABASE_URL` pointée sur cette base), puis la supprime. La base de dev
   `lodb` n'est jamais touchée ; le script refuse de tourner si `lodb_schema_ref` existe déjà.
2. **Figer.** Écrit, depuis cette base :
   - `tests/fixtures/schema/doctrine-schema.sql` : `pg_dump --schema-only --no-owner
     --no-privileges` normalisé (sans commentaires `--`, lignes `SET`, `SELECT
     pg_catalog.set_config`, `\restrict`/`\unrestrict` au jeton aléatoire, lignes vides) ;
   - `tests/fixtures/schema/doctrine-versions.txt` : les 11 lignes de
     `doctrine_migration_versions` ;
   - `src/LoDb.Infrastructure/Persistence/Baseline/doctrine-catalog.txt` : le catalogue
     (`schema-catalog.sql`) que `migrate` et `baseline mark-applied` comparent avant de
     marquer une base.
3. **Comparer.** Lance `migrate` sur un conteneur `postgres:17-alpine` jetable, en fait le
   même dump sans les tables propres à la nouvelle stack, et le compare au dump Doctrine.

Le **catalogue** décrit tables, colonnes (type, nullabilité, défaut), contraintes
(`pg_get_constraintdef`), index (`pg_get_indexdef`) et séquences. Il ignore l'ordre des
colonnes et les objets d'extension : une base de production restaurée, dont les colonnes
viennent des mêmes `ALTER TABLE`, y est comparée sans faux écart. Le dump, lui, compare
aussi l'ordre des colonnes.

Les tests rejouent la comparaison sans l'ancienne stack, à partir des fichiers figés
(Testcontainers, `postgres:17-alpine`).

## Résultat

Sortie de `tools/next/schema/check.sh` (2026-09-26) :

```text
Applying the Doctrine migrations to lodb_schema_ref…
Doctrine: 11 migrations, 157 catalog lines.
Running migrate on an empty database…
pg_dump: pg_dump (PostgreSQL) 17.11 (Doctrine), pg_dump (PostgreSQL) 17.11 (EF)
EF history: 20260719150001_Baseline, 20260926022150_Lot1DataDragon
Excluded (new stack only): __EFMigrationsHistory ddragon_asset ddragon_version periodic_job
IDENTICAL: the EF migrations reproduce the Doctrine schema and history.
```

Hors des tables exclues, le schéma est **identique** : colonnes et leur ordre, types,
défauts, séquences d'identité, clés, index (dont `LOWER(email)` et `LOWER(username)`),
clés étrangères et leurs `ON DELETE`, noms Doctrine compris. L'historique Doctrine l'est
aussi : `Baseline` inscrit les 11 versions, si bien qu'une base créée par EF reste
utilisable par l'ancienne stack, qui ne rejoue rien.

Tests correspondants (`tests/LoDb.Infrastructure.Tests/Persistence/`) :

| Test | Vérifie |
|---|---|
| `BaselineSchemaTests` | `Baseline` seule = dump Doctrine ; toutes les migrations = dump Doctrine + les seules tables nouvelles ; catalogue figé = catalogue de `Baseline` ; modèle EF sans changement en attente |
| `DatabaseMigratorTests` | base vide → tout appliqué ; base Doctrine → marquage puis lot 1 ; second passage sans effet ; six schémas inattendus refusés sans rien toucher ; `mark-applied` idempotent ; quatre `migrate` concurrents ne marquent qu'une fois |

## Écarts

| Objet | Écart | Justification |
|---|---|---|
| `__EFMigrationsHistory` | table en plus (`migration_id`, `product_version`, clé `pk___ef_migrations_history`) | historique d'EF, sans lequel `migrate` ne saurait pas quoi appliquer ; nom par défaut d'EF, colonnes en snake_case comme le reste du modèle |
| `ddragon_asset` | table en plus (lot 1) | manifeste des images (ADR 0004) : (version, type, clé) → statut, SHA-256, extension, date |
| `ddragon_version` | table en plus (lot 1) | état d'ingestion des versions : statut, tentatives, prochaine tentative, découverte, mise à jour, `ready_at`, `promoted_at` |
| `periodic_job` | table en plus, **hors plan** | voir ci-dessous |
| contraintes `ck_ddragon_*` | contraintes en plus, sur les tables du lot 1 | statut, SHA-256 (64 hexadécimaux minuscules), extension et cohérence présent/absent : une ligne incohérente est refusée par la base, quelle que soit l'instance qui écrit |

**`periodic_job`** (nom, dernier départ, dernier succès) n'était pas prévue par le plan.
Le verrou consultatif empêche deux exécutions simultanées, pas deux exécutions dans la même
période : deux instances dont les minuteries sont décalées, ou une instance qui redémarre,
relanceraient la tâche. La base des tâches périodiques ne lance donc une exécution que si le
dernier départ, toutes instances confondues, date d'au moins une période (moins une
tolérance d'un dixième de période, une minute au plus). La décision est un seul `INSERT …
ON CONFLICT … WHERE`, arbitré par le verrou de ligne de PostgreSQL.

Aucun autre écart : les tables de l'ancienne stack que la nouvelle n'utilise pas
(`messenger_messages`, `reset_password_request`) et les index fonctionnels sont créés par
SQL brut dans `Baseline`, à l'identique.

## Horodatages et convertisseur UTC

L'ancienne stack écrit en UTC (`date.timezone = UTC`) dans des colonnes
`timestamp(0) without time zone`, que Npgsql refuse d'associer à une date UTC. Ces colonnes,
**et elles seules**, passent par `LegacyUtcTimestamps.Converter` :

| Table | Colonnes |
|---|---|
| `build_votes` | `created_at` |
| `builds` | `created_at`, `updated_at` |
| `contact_messages` | `created_at`, `handled_at` |
| `donations` | `created_at` |
| `users` | `created_at` |

- À l'écriture : l'instant est ramené en UTC et **tronqué** à la seconde, comme Doctrine
  (PostgreSQL arrondirait). À la lecture : la valeur est rendue en UTC (décalage nul).
- Les colonnes héritées déjà en `timestamptz` (`api_keys.created_at`,
  `api_keys.revoked_at`, `users.banned_at`) n'ont pas de convertisseur ; toutes les colonnes
  nouvelles sont en `timestamptz`.
- Npgsql n'écrit dans une colonne `timestamptz` qu'une date au décalage nul : passer
  `TimeProvider.GetUtcNow()`, jamais une heure locale.
- `LegacyUtcTimestampsTests` vérifie que le convertisseur porte exactement sur cette liste,
  la troncature, la lecture d'une ligne écrite par l'ancienne stack et la conservation de
  l'instant en `timestamptz`. La liste disparaît avec la phase *contract*, qui passe ces
  colonnes en `timestamptz`.

## `migrate` et `baseline mark-applied`

| Base | `migrate` | `baseline mark-applied` |
|---|---|---|
| vide | crée l'historique EF puis applique tout ; code 0 | refuse (rien à marquer) ; code 1 |
| schéma Doctrine exact, sans historique EF | marque `Baseline`, puis applique les migrations suivantes ; code 0 | marque `Baseline` seule ; code 0 |
| déjà marquée | applique ce qui manque (rien au second passage) ; code 0 | ne fait rien ; code 0 |
| tout autre schéma | refuse sans rien écrire, journalise les écarts (`db.baseline.refused`) ; code 1 | idem |

La vérification et le marquage se font dans une transaction, sous un verrou consultatif de
transaction (`db:baseline`) : deux `migrate` simultanés ne marquent pas deux fois.

Le service Compose `migrate` (même image que l'API) passe avant `api`, qui attend sa
réussite (`service_completed_successfully`). Vérifié sur le créneau `lodb-next-e1` :

- premier `up` : `migrate` applique les 2 migrations (726 ms) et se termine en code 0 ;
  `api` démarre 0,4 s plus tard ; `/readyz` répond 200 ;
- séquence du déploiement (`run --rm migrate`, puis `up -d --remove-orphans --wait`) : aucune
  migration appliquée, `--wait` accepte le service terminé, code 0 ;
- copie d'une base Doctrine : `Baseline` marquée puis lot 1 appliqué, 11 versions Doctrine
  conservées ; base au schéma inattendu : une ligne `db.baseline.refused`, base intacte,
  code 1, `up` échoue et `api` reste arrêtée ;
- sans chaîne de connexion : une ligne JSON `cli.command.failed`, code 1.

## Relancer la vérification

Après toute nouvelle migration Doctrine (tant que l'ancienne stack vit) :

1. écrire sa jumelle EF dans la migration du lot concerné ;
2. relancer, depuis la racine, ancienne stack démarrée :

   ```sh
   tools/next/schema/check.sh
   ```

3. committer les fichiers régénérés (`tests/fixtures/schema/*`,
   `src/LoDb.Infrastructure/Persistence/Baseline/doctrine-catalog.txt`) avec la migration.

Le script sort en code 1, avec le `diff`, au premier écart. Une table ajoutée par un lot
s'ajoute à `NEW_TABLES` dans le script et à `TestDatabase.NewTables`, et se justifie ici.

## Anonymisation d'un dump (`tools/next/db/`)

Pour la base de `next` et pour les essais locaux :

```sh
pg_dump -Fc … > prod.dump   # format pg_restore obligatoire
LODB_ANON_PASSWORD='…' tools/next/db/anonymize.sh prod.dump next.dump
```

Le dump est restauré dans un conteneur `postgres:17-alpine` jetable, sans réseau, anonymisé
par `anonymize.sql` en une transaction, puis réexporté (`-Fc`) ; le script affiche les
comptes de lignes avant et après. Tous les mots de passe deviennent le hash bcrypt de
`LODB_ANON_PASSWORD`, choisi par l'opérateur et partagé comme un secret : un mot de passe
connu du dépôt ouvrirait tous les comptes de `next`, qui est public.

Au-delà de la liste du plan (e-mails, `google_id`, identifiants Stripe, hash des clés
d'API, IP et contenus de contact, mots de passe), le script remplace aussi les pseudos, le
`riot_tagline`, le motif de bannissement, le nom et la description des builds et le nom des
clés d'API. Il **vide** `messenger_messages` (e-mails en attente, adresses comprises) et
`reset_password_request` (jetons de réinitialisation) : ce sont les seuls volumes non
conservés. Les identifiants Stripe deviennent des pseudonymes salés, si bien que deux clés
d'un même client partagent encore le leur.

Garde-fou : toute colonne pouvant contenir du texte doit figurer dans la liste revue du
script (anonymisée, vidée ou conservée) ; sinon il s'arrête avant toute modification. Les
lots 4, 6 et 7 complètent donc cette liste quand ils ajoutent des colonnes.
`AnonymizationTests` vérifie qu'aucune donnée marquée ne subsiste, que les comptes et le
schéma sont conservés, et que le garde-fou arrête le script.
