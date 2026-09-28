# Migration de *contract* (L8.3) — préparée, **non appliquée**

Dernière étape du schéma partagé en *expand/contract*
([`plan-migration.md`](../../docs/reecriture/plan-migration.md), « Bascule », point 6). Jusqu'ici
toutes les migrations EF sont additives, pour que l'ancienne stack puisse revenir sur la
base. Le *contract* supprime ce qu'elle seule utilisait : **après lui, le retour arrière vers
l'ancienne stack est impossible.**

| Fichier | Rôle |
|---|---|
| `contract.sql` | Le corps de la migration : SQL pur, sans `BEGIN`/`COMMIT`, idempotent, gardé |
| `check.sh` | Le vérifie sur une base jetable (`postgres:17-alpine`, port de boucle local aléatoire) |

Ce dossier est **hors** de `src/LoDb.Infrastructure/Persistence/Migrations` : `migrate` ne
le voit pas, aucun déploiement ne l'applique. Il n'est lu qu'au moment du *contract*, puis
supprimé avec lui.

## Ce que fait `contract.sql`

1. **Garde** : refuse (exception, rien de modifié) toute base dont l'historique EF ne contient
   pas `20260926185409_Lot6BillingAnalyticsApps`, dernière migration connue à sa rédaction.
2. **Tables de l'ancienne stack** supprimées : `messenger_messages` (file Symfony Messenger),
   `reset_password_request` (jetons du bundle SymfonyCasts ; ceux de la réécriture sont
   ceux d'Identity), `doctrine_migration_versions` (l'historique EF garde `Baseline`).
3. **Colonne morte** supprimée : `users.roles` (rôles Symfony en JSON ; les rôles de la
   réécriture sont dans `identity_user_roles`). Aucune autre colonne héritée n'est morte :
   toutes les autres sont lues ou écrites par la réécriture (relevé du 2026-09-27).
4. **Horodatages** : les sept colonnes de `LegacyUtcTimestamps.Columns` passent de
   `timestamp(0) without time zone` à `timestamp(0) with time zone`, converties
   explicitement `AT TIME ZONE 'UTC'` (jamais par le fuseau de la session). La précision 0
   reste, comme pour les colonnes héritées déjà en `timestamptz`. Le défaut typé de
   `contact_messages.handled_at` est redéclaré.

`SET LOCAL lock_timeout = '5s'` : la réécriture des tables `users` et `builds` prend un
verrou exclusif ; si le verrou n'est pas obtenu en 5 s, la migration échoue sans rien
changer (transaction) et se relance plus tard, au lieu de bloquer le site derrière elle.

## Quand

- **30 jours** après la bascule, **sans** retour arrière pendant cette période, et
  **après** la décision de ne plus y revenir (runbook, § *Contract*).
- Ancienne stack arrêtée et plus jamais redémarrée sur cette base (ses conteneurs peuvent
  encore exister ; ils ne doivent plus démarrer).
- Sauvegarde de la base faite juste avant, vérifiée par une restauration à blanc : c'est le
  seul retour possible après le *contract*.

## Appliquer (le jour venu, dans une PR dédiée)

Le *contract* ne s'applique **jamais à la main** en prod : il devient une migration EF
ordinaire, appliquée par `migrate` avant `up`, comme les autres (`staging` d'abord, puis
`prod` par la fusion de `test` dans `main`).

1. Changer le modèle, dans le même commit :
   - retirer `User.Roles`, son convertisseur et son comparateur (`UserConfiguration`), et
     les `Roles = []` d'`AdminGrant`, `RegisterEndpoint` et `GoogleProvisioner` ;
   - supprimer `LegacyUtcTimestamps` (et son appel dans `LoDbDbContext.OnModelCreating`) ;
     déclarer les sept propriétés concernées `HasPrecision(0)`, `handled_at` gardant
     `HasNullDefault()`. PostgreSQL **arrondit** à la seconde là où le convertisseur
     tronquait : un test qui compare un instant écrit puis relu le fait à la seconde, ou
     passe un instant déjà tronqué.
2. `dotnet ef migrations add Contract` (projet `LoDb.Infrastructure`, démarrage
   `LoDb.Api`), puis **remplacer** les opérations générées dans `Up` par
   `migrationBuilder.Sql(...)` avec le contenu de `contract.sql`. EF générerait un
   `ALTER COLUMN … TYPE timestamp with time zone` sans `USING … AT TIME ZONE 'UTC'`, donc
   dépendant du fuseau de la session, et ignore les trois tables créées en SQL brut par
   `Baseline`. `Down` lève `NotSupportedException` : on revient par la sauvegarde, pas par
   une migration.
3. Adapter les tests et outils qui décrivent le schéma hérité :
   - `BaselineSchemaTests` : `Baseline` seule reste égale au dump Doctrine ; le schéma
     complet se compare désormais au schéma après *contract* ;
   - `LegacyUtcTimestampsTests` : supprimé ; `EntityRoundTripTests` couvre l'aller-retour
     des instants en `timestamptz` ;
   - `tools/db/anonymize.sql` : retirer `messenger_messages.*`,
     `reset_password_request.*` et `users.roles` de la liste revue, et les deux `DELETE` ;
   - `tools/schema/check.sh` et `tools/cutover/` : sans objet une fois l'ancienne
     stack décommissionnée (runbook, § Décommission).
4. `tools/contract/check.sh` une dernière fois sur la branche, **avant** d'y retirer ce
   dossier : il prouve encore la conversion et la garde sur le modèle précédent.
5. Suites complètes, déploiement sur `staging` (sa base remplacée au préalable par un dump
   anonymisé de la prod, `tools/db/anonymize.sh`), contrôle des dates affichées (profil,
   builds, admin), puis promotion en `prod`.

`DatabaseMigrator` ne change pas : une base Doctrine qu'on migrerait encore serait marquée
à `Baseline`, puis recevrait toutes les migrations, *contract* compris.

## Vérifier

```bash
tools/contract/check.sh
```

Docker et le SDK .NET suffisent ; ni `lodb-dev`, ni un emplacement, ni l'ancienne stack ne
sont touchés. Le script :

1. démarre un `postgres:17-alpine` à lui, y lance `migrate` (toutes les migrations), puis écrit
   une ligne, comme l'ancienne stack, dans chaque table concernée ; l'horodatage choisi
   (2026-03-29 01:30 UTC) tombe la nuit où Paris saute 02:00-03:00 ;
2. applique `contract.sql` en une transaction, session en `Europe/Paris` ;
3. vérifie : tables et `users.roles` supprimées, sept colonnes en `timestamptz(0)`, mêmes
   instants, défaut typé ; une seconde application ne change pas le schéma ; `migrate`
   n'a plus rien à appliquer ;
4. vérifie la garde : sur une base sans historique EF, le script échoue et rien ne change.

Sortie attendue, code 0 :

```text
migrate on an empty database: 4 migration(s) applied
contract.sql applied.
Legacy tables and users.roles dropped; 7 timestamps in timestamptz(0), same instants.
Second run: schema unchanged. migrate afterwards: 0 migration(s) applied
Guard: refused without the EF history, nothing changed.
CONTRACT OK
```

Une migration EF ajoutée d'ici le *contract* ne change rien au script. Il faut seulement
mettre à jour, dans la garde, le nom de la dernière migration exigée.
