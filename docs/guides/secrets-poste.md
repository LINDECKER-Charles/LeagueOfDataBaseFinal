# Secrets du poste — chiffrés au repos avec sops et age

GitHub ne rend jamais la valeur d'un secret : les seules copies lisibles des `.env` de
`staging` et de `prod`, des certificats Data Protection et des sauvegardes rapatriées vivent
sur le poste du propriétaire, dans un dossier `lodb-secrets` hors de tout dépôt. Tout outil qui
lit le disque du poste les lit aussi. Cible : ce dossier ne contient plus que des fichiers
**chiffrés**, et la clé qui les ouvre vit ailleurs, **hors de tout dépôt et hors du dossier**.
Procédure du propriétaire : une migration, puis l'usage courant.

Où vont ces valeurs ensuite : [`configuration.md`](configuration.md) (secrets GitHub, `.env`)
et, une fois le déploiement par clé restreinte en place,
[`deploiement-cle-restreinte.md`](deploiement-cle-restreinte.md) § 7.

## 1. Ce qui est concerné

| Fichier | Format | Outil |
|---|---|---|
| `.env` de `staging` et de `prod` (contenu de `ENV_STAGING`, `ENV_PROD`) | dotenv | `sops` : chaque valeur chiffrée, les noms restent lisibles (ils sont publics dans `.env.*.example`) et un diff reste utile |
| certificats Data Protection (`.pfx`) | binaire | `age` |
| sauvegardes rapatriées de l'hôte (`.deploy/backups/` : `*.dump`, `*.env`) | binaire | `age` : un dump est une copie de la base, données personnelles comprises |
| clés privées SSH du propriétaire pour l'hôte | texte | leur phrase de passe ; `age` si elles sont copiées dans le dossier |
| copies en clair ailleurs : `.env.staging` et `.env.prod` à la racine d'un dépôt, `.env` réel d'un autre dépôt du poste (`infra-vps`) | dotenv | migrées dans le dossier, puis supprimées |

## 2. Principe

- **Deux destinataires `age`** pour chaque fichier : la clé **quotidienne**, dont la partie
  privée est dans `%AppData%\sops\age\keys.txt` (là où `sops` la cherche sous Windows), et la
  clé de **secours**, dont la partie privée ne vit que hors ligne (papier ou support amovible,
  au coffre). Perdre le poste ou sa clé ne perd pas les secrets : GitHub ne les rendrait pas.
- Le dossier ne contient que des fichiers chiffrés et des clés **publiques** :

```
lodb-secrets/
  .sops.yaml                      # règle : quels destinataires pour quels fichiers
  .age-recipients                 # les deux clés publiques, pour age
  staging/staging.sops.env
  staging/data-protection.pfx.age
  prod/prod.sops.env
  prod/data-protection.pfx.age
  prod/backups/<horodatage>.dump.age
  infra-vps/infra-vps.sops.env
```

- Il peut devenir un dépôt Git **privé et dédié** (historique des changements) : jamais le
  dépôt public de LoDb, jamais `infra-vps` (aucun secret dans ses fichiers, même chiffré).

## 3. Installer

Versions publiées depuis plus de deux semaines au moment d'écrire : **sops v3.13.3**,
**age v1.3.2**. Les commandes sont pour **Git Bash** : un tube de PowerShell 5.1 réencode
les octets et corrompt un binaire comme un texte non ASCII.

```bash
# age (inclut age-keygen) : winget, ou l'archive de la release vérifiée par sa preuve Sigsum.
# Version explicite : sans elle, winget installe la dernière, quelle que soit son ancienneté.
winget install --id FiloSottile.age --exact --version 1.3.2

# sops : l'exécutable de la release et la liste des empreintes, comparées avant usage
v=v3.13.3; base=https://github.com/getsops/sops/releases/download/$v
curl -fLO "$base/sops-$v.amd64.exe" -fLO "$base/sops-$v.checksums.txt"
sha256sum -c --ignore-missing "sops-$v.checksums.txt"     # sops-v3.13.3.amd64.exe: OK
mkdir -p "$LOCALAPPDATA/Programs/bin" && mv "sops-$v.amd64.exe" "$LOCALAPPDATA/Programs/bin/sops.exe"
```

Ajouter `%LOCALAPPDATA%\Programs\bin` au `PATH` de l'utilisateur ; `sops --version` et
`age --version` répondent.

## 4. Créer les clés

```bash
mkdir -p "$APPDATA/sops/age"
age-keygen -o "$APPDATA/sops/age/keys.txt"          # affiche « Public key: age1… » (quotidienne)
age-keygen -o /<support amovible>/lodb-secours.txt   # clé de secours, jamais sur le disque du poste
age-keygen -y /<support amovible>/lodb-secours.txt   # sa clé publique
```

- Le support de la clé de secours (et une impression, au besoin) va au coffre. Seule sa clé
  publique (`age1…`) reste sur le poste, dans les deux fichiers ci-dessous.
- Restreindre la clé quotidienne au seul compte (PowerShell) :
  `icacls "$env:APPDATA\sops\age\keys.txt" /inheritance:r /grant:r "${env:USERNAME}:F"`.
- Option : la chiffrer elle-même par phrase de passe (`age -p`) et la fournir à `sops` par
  `SOPS_AGE_KEY_CMD` ; une phrase de passe à chaque usage, contre une clé inerte au repos.

`lodb-secrets/.sops.yaml` et `lodb-secrets/.age-recipients` :

```yaml
creation_rules:
  - path_regex: \.sops\.env$
    age: >-
      age1…quotidienne,
      age1…secours
```

```
age1…quotidienne
age1…secours
```

## 5. Migrer, une fois

Depuis `lodb-secrets` (Git Bash), `export SOPS_AGE_KEY_FILE="$APPDATA/sops/age/keys.txt"` :

1. Chaque dotenv, renommé en `*.sops.env` puis chiffré **en place** (aucune copie en clair
   n'est écrite) :
   `sops encrypt -i --input-type dotenv --output-type dotenv prod/prod.sops.env`.
2. Chaque binaire, chiffré pour les deux destinataires, puis l'original supprimé :
   `age -R .age-recipients -o prod/data-protection.pfx.age prod/data-protection.pfx && rm prod/data-protection.pfx`.
3. Les copies en clair hors du dossier (racine des dépôts, `.env` d'`infra-vps`) : importées
   comme en 1, puis supprimées ; corbeille vidée. Un dépôt qui en a besoin pour une commande
   locale la reçoit sur l'entrée standard, jamais en fichier.
4. Vérifier qu'aucune sauvegarde du poste (historique de fichiers, synchronisation dans le
   nuage, points de restauration) n'a gardé les anciennes versions en clair ; le chiffrement
   du disque (BitLocker) couvre le vol du poste, pas un programme qui lit ses fichiers.
5. Au jugement du propriétaire, selon la durée d'exposition des copies en clair : rotation
   des secrets les plus sensibles (clé Stripe live, relais SMTP, OAuth, mot de passe de la
   base ; [`configuration.md`](configuration.md) § 6).

## 6. Usage courant

```bash
export SOPS_AGE_KEY_FILE="$APPDATA/sops/age/keys.txt"
EDITOR='code --wait' sops edit prod/prod.sops.env        # tout éditeur qui attend sa fermeture

# Vers GitHub, sans fichier en clair (configuration.md § 3.2) ; --env production si les
# secrets sont devenus des secrets d'environnement (§ 3.3, nom ENV_FILE)
sops decrypt prod/prod.sops.env | gh secret set ENV_PROD --repo <propriétaire>/<dépôt>
age -d -i "$SOPS_AGE_KEY_FILE" prod/data-protection.pfx.age | base64 -w0 \
  | gh secret set PROD_DATA_PROTECTION_PFX --repo <propriétaire>/<dépôt>

# Lire une valeur
sops decrypt prod/prod.sops.env | grep '^LODB_CANONICAL_HOST='

# Une sauvegarde rapatriée de l'hôte, chiffrée à l'arrivée
ssh <compte>@<hôte> 'cat <chemin>/.deploy/backups/<horodatage>.dump' \
  | age -R .age-recipients -o prod/backups/<horodatage>.dump.age
```

- `sops edit` déchiffre le temps de l'édition dans un fichier temporaire du système, qu'il
  supprime en sortant : ne pas interrompre l'éditeur brutalement.
- Ne jamais déchiffrer vers un fichier dans l'arbre d'un dépôt : `.gitignore` ne couvre pas
  tous les noms, et un `git add` de trop publie.
- Changer de clé quotidienne : nouvelle clé, `.sops.yaml` et `.age-recipients` mis à jour,
  `sops updatekeys <fichier>` pour chaque `*.sops.env`, `age` réchiffre les `*.age`
  (déchiffrés avec l'ancienne, chiffrés avec `-R .age-recipients`).

## 7. Reprise depuis la clé de secours

Sur un poste neuf : sops et age installés (§ 3), le support de la clé de secours branché et
`SOPS_AGE_KEY_FILE` pointé sur son fichier (ressaisi depuis l'impression sur ce support, au
besoin). Déchiffrer un fichier de chaque sorte, créer une nouvelle clé quotidienne (§ 4 et
§ 6, « Changer de clé »), puis rendre le support au coffre. À répéter une fois l'an pour savoir
que la reprise marche.

## 8. Preuves

```bash
cd lodb-secrets
# Aucun fichier en clair : chaque fichier est un dotenv chiffré par sops ou un fichier age
find . -path ./.git -prune -o -type f -print | while read -r f; do
  case "$f" in
    ./.sops.yaml | ./.age-recipients) ;;
    *.sops.env) grep -q '^sops_version=' "$f" || echo "en clair : $f" ;;
    *.age) [ "$(head -c 21 "$f")" = age-encryption.org/v1 ] || echo "en clair : $f" ;;
    *) echo "inattendu : $f" ;;
  esac
done                                                    # aucune ligne attendue
grep -c '^# public key' "$APPDATA/sops/age/keys.txt"   # 1 : la seule clé quotidienne
```

Puis : la reprise du § 7 réussie avec la clé de secours, et aucune copie `.env.prod` ou
`.env.staging` dans les dossiers de dépôts du poste.

## 9. Ce qu'on ne fait pas

| Action | Raison |
|---|---|
| La clé `age` dans le dossier, dans un dépôt ou dans un dossier synchronisé | elle ouvrirait tout ce qu'elle protège |
| Les fichiers chiffrés dans le dépôt public de LoDb ou dans `infra-vps` | un dépôt public est copié pour toujours ; `infra-vps` n'admet aucun secret |
| Une seule clé, sans secours | perdre le poste perdrait les seules copies lisibles |
| Un tube PowerShell 5.1 entre `sops`, `age`, `base64` et `gh` | il réencode les octets |
| Un secret collé dans un ticket, une conversation ou un outil tiers | il en sort du périmètre chiffré |
