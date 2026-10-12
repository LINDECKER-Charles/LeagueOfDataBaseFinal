# Sauvegardes hors hôte de la base de prod

Chaque nuit, la pile `sauvegardes` d'`infra-vps` fait un `pg_dump` de chaque base Postgres de
l'hôte et l'envoie, chiffré par restic, vers un stockage objet S3 à verrouillage d'objet
(Scaleway) ; une fois par mois, elle en restaure une pour de vrai. Elle ne joint une base que
par un réseau dédié et un rôle qui ne peut que lire. Ce guide décrit la part de LoDb ; celle
de l'hôte (bucket, `RESTIC_PASSWORD`, restauration) est dans `infra-vps` :
`sauvegardes/README.md` et `docs/APPLICATIONS.md` § 7.

**En prod seulement.** Le staging n'est pas sauvegardé hors de l'hôte : son rôle
`lodb_sauvegarde` existe, sans mot de passe, et personne ne s'y connecte. Les dumps que le
job de déploiement prend avant `migrate` (`.deploy/backups/`,
[`configuration.md`](configuration.md) § 5) restent ceux du retour arrière.

## Ce que LoDb porte

| Pièce | Où | Rôle |
|---|---|---|
| Réseau `sauvegardes` | [`compose.sauvegarde.yaml`](../../compose.sauvegarde.yaml), ajouté par le job en prod | Postgres le rejoint sous l'alias `lodb-prod-postgres`, préfixé par sa pile (le staging partage l'hôte). Interne, créé par `infra-vps` même quand ses sauvegardes dorment : le job vérifie qu'il existe, sans le créer, et s'arrête sinon avant toute modification. |
| Rôle `lodb_sauvegarde` | tenu par `migrate` à chaque déploiement (`Persistence/Roles/`) | `LOGIN`, `CONNECTION LIMIT 2`, membre de `pg_read_all_data` : il lit toute la base, de quoi faire un `pg_dump`, et n'écrit que dans `backup_run`. |
| `LoDb__Database__BackupRole__Password` 🔒 | `ENV_PROD`, puis un fichier de `.deploy/secrets/` monté dans `migrate` seul | Le mot de passe que `migrate` pose sur le rôle ; le même que `SAUVEGARDE_LODB_MOT_DE_PASSE` du secret `SAUVEGARDES_ENV_FILE` d'`infra-vps`. L'API ne le reçoit pas. |
| Table `backup_run` | migration de la base | Le journal des passes : le conteneur de sauvegarde y ajoute une ligne par étape (sauvegarde, rétention, test de restauration), réussie ou non ; la page `/admin/backups` l'affiche. |

Absent ou vide, le secret laisse le rôle tel qu'il est : un rôle neuf n'a pas de mot de passe,
un rôle existant garde le sien. Posé, le job exige le format qu'`infra-vps` accepte (16
caractères au moins parmi lettres, chiffres, `-` et `_`) et refuse un `ENV_PROD` qui s'en
écarte avant toute modification, sans afficher la valeur.

Le calque est ajouté à chaque déploiement de la prod, sauvegardes activées ou non. Le réseau
doit donc exister **avant** le premier déploiement qui le porte : `infra-vps` déployé avec sa
pile `sauvegardes`, qui le crée même en sommeil ; sinon le job s'arrête sur *Docker network
'sauvegardes' is missing*. Ce premier déploiement recrée le conteneur Postgres (un réseau de
plus) : quelques secondes sans base, volume intact.

## Activer

Tant que rien n'est posé, les sauvegardes dorment : les deux déploiements restent verts, et
l'alerte `SauvegardeEnRetard` d'`infra-vps` sonne pour `lodb`. Dans l'ordre :

1. Côté `infra-vps` : le bucket et sa clé, `RESTIC_PASSWORD` gardé hors de GitHub
   (`sauvegardes/README.md`, « Le bucket » et « Activer »).
2. Un mot de passe, tiré une fois : `openssl rand -hex 32`.
3. Côté LoDb : ce mot de passe en `LoDb__Database__BackupRole__Password` dans `ENV_PROD`
   (*Settings ▸ Secrets*), puis un déploiement de la prod : `migrate` le pose sur le rôle.
4. Côté `infra-vps` : le même mot de passe en `SAUVEGARDE_LODB_MOT_DE_PASSE` dans
   `SAUVEGARDES_ENV_FILE`, `lodb` dans `SAUVEGARDE_BASES`, `SAUVEGARDE_JOURNAL=lodb` pour
   que chaque passe écrive son journal dans `backup_run`, puis son déploiement : son
   `verify.sh` prouve que la base répond au rôle.

LoDb d'abord : déployé avant, `infra-vps` trouverait un rôle sans mot de passe et son
déploiement échouerait. La première passe part après 2 h UTC (`infra-vps` documente comment la
lancer sans attendre) ; `/admin/backups` en montre alors les étapes.

## Changer le mot de passe

Les deux secrets ensemble, puis le déploiement de la prod, puis celui d'`infra-vps`. Entre les
deux, une passe échoue à la connexion : rien n'est perdu, la suivante reprend, et
`SauvegardeEnRetard` ne sonne qu'au-delà de 26 h sans réussite.

## Retirer

Retirer `lodb` de `SAUVEGARDE_BASES` côté `infra-vps` (son alerte sonne alors pour `lodb`
jusqu'à ce qu'on la fasse taire) et vider la ligne de `ENV_PROD`. Cela ne coupe pas l'accès :
sans valeur, `migrate` laisse le mot de passe en place. L'ôter au rôle, depuis le dossier de
la prod sur l'hôte (`PROD_PATH`) :

```bash
docker compose exec -T postgres sh -c \
  'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "ALTER ROLE lodb_sauvegarde PASSWORD NULL"'
```

Le calque reste : le réseau ne donne rien sans un rôle qui accepte une connexion.
