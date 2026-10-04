# Release du desktop (Velopack)

Ce guide couvre la publication de l'app desktop `LoDb.Desktop` et ses mises à jour
intégrées : versions et canaux, workflow, porte E2E, signature, clés et rotation. Les
décisions sont dans l'[ADR 0008](../reecriture/adr/0008-mises-a-jour-integrees.md) et le
chantier dans le [lot 9](../reecriture/implementation/lot-09-10-apps.md) (L9.4).

| Élément | Emplacement |
|---|---|
| Mises à jour dans l'app | `src/LoDb.Desktop/Updates/` |
| Workflow de release | `.github/workflows/release-desktop.yml` |
| Scripts (pack, porte, publication, E2E local) | `tools/desktop-update-e2e/` |

## Côté app

- **Source** : les GitHub Releases du dépôt public (`GithubSource`). Le dépôt est une
  constante de `Updates/Engine/UpdateFeeds.cs` : un fork doit la changer.
- **Canal** : fixé au build (`-p:LoDbDesktopChannel=stable|beta`). Une installation
  `stable` ne lit que les releases publiées ; une installation `beta` lit aussi les
  pre-releases.
- **Rythme** : vérification au démarrage puis toutes les 6 h, téléchargement en tâche de
  fond, état exposé au front par le pont (`updateState`).
- **Application** : au clic (« redémarrer »), avec redémarrage, ou en silence à la
  fermeture (`WaitExitThenApplyUpdates`).
- **Hors installation Velopack** (développement, `dotnet run`) : rien n'est vérifié,
  l'état reste « aucune mise à jour ».
- **Journaux** : événements `desktop.update.*` (`check_failed`, `download_failed`,
  `downloaded`, `handed_over`, `handover_failed`, `feed_invalid`, `check_timed_out`).

Deux entrées cachées servent aux tests de bout en bout et n'ont aucun effet sans `--smoke` :

| Entrée | Effet |
|---|---|
| `--smoke --apply-updates` | vérifie et télécharge la mise à jour avant le rapport (`updates: ready N`), puis la confie à l'updater à la sortie |
| `LODB_DESKTOP_UPDATE_SOURCE` | dossier local de releases (chemin absolu) à la place de GitHub ; une valeur invalide coupe les mises à jour (`feed_invalid`) |

## Versions et canaux

- **Stable** : le tag `desktop-vX.Y.Z` posé sur le commit donne la version. `VersionPrefix`
  doit valoir `X.Y.Z`, sinon le workflow échoue. Sans tag sur le commit, il n'y a pas de
  release.
- **Beta** : `VersionPrefix-beta.N`, où `N` suit le plus grand tag beta existant pour ce
  préfixe. Le workflow crée le tag.
- `VersionPrefix` vit aujourd'hui dans `Directory.Build.props`, partagé par tous les
  projets. Une version propre au desktop irait dans `LoDb.Desktop.csproj`.

| RID | Canal stable | Canal beta | Build installable |
|---|---|---|---|
| `win-x64` | `win-x64` | `win-x64-beta` | `Setup.exe`, `Portable.zip` |
| `osx-arm64` | `osx-arm64` | `osx-arm64-beta` | `Setup.pkg`, `Portable.zip` |
| `osx-x64` | `osx-x64` | `osx-x64-beta` | `Setup.pkg`, `Portable.zip` |
| `linux-x64` (best effort) | `linux-x64` | `linux-x64-beta` | `.AppImage` |

Noms des assets : `LoDb.Desktop-<version>-<canal>-full.nupkg`, `…-delta.nupkg`,
`LoDb.Desktop-<canal>-Setup.pkg` (ou `-Setup.exe`), `LoDb.Desktop-<canal>-Portable.zip`,
`releases.<canal>.json`. Une release GitHub porte tous les RID d'une même version.

## Publier une version stable

1. Monter `VersionPrefix` dans une PR et la fusionner.
2. Poser le tag sur le commit qui partira en prod :
   `git tag desktop-vX.Y.Z <sha>`, puis `git push origin desktop-vX.Y.Z`.
3. Déployer la prod. Le déploiement appelle le workflow avec le SHA déployé
   (voir [Contrat d'appel](#contrat-dappel)). À défaut, on le lance à la main :
   `gh workflow run release-desktop.yml -f sha=<sha> -f channel=stable`.
4. Suivre le run : un job `pack` par RID, puis `publish`. La release apparaît d'abord en
   *pre-release*, puis elle est promue (`--latest`) quand les trois RID requis sont en ligne.

Relancer un run est sans risque :

- une release déjà promue n'est pas republiée ;
- une pre-release incomplète est complétée (`vpk upload --merge`).

La beta suit le même chemin après le déploiement de staging, sans tag préalable. Elle reste
en *pre-release*, et seules les 5 dernières betas sont gardées.

### Contrat d'appel

Le déploiement de prod (L8.1) appelle le workflow une fois le déploiement réussi, sur le
SHA exact ; le staging fait de même avec `channel: beta`.

```yaml
  desktop:
    needs: deploy
    uses: ./.github/workflows/release-desktop.yml
    with:
      sha: ${{ github.sha }}
      channel: stable
    permissions:
      contents: write   # la release et ses tags
      id-token: write   # OIDC vers Azure Artifact Signing
    secrets: inherit
```

## Ce que fait le workflow

| Job | Rôle |
|---|---|
| `prepare` | Valide les entrées. Résout la version (`release-version.sh`) et celle de `vpk`, qui est celle du package `Velopack`. Saute une release déjà promue. |
| `shell` | `npm ci` puis `npm run build:shell` : le front embarqué. |
| `pack` (×4) | N-1 (`previous.sh`), signature (`signing.sh`), `pack.sh`, présence du delta, porte (`gate.sh`). |
| `publish` | `publish.sh` : RID requis présents, upload en *pre-release*, puis promotion (stable) ou purge des vieilles betas. |

- **Deltas** : `previous.sh` lance `vpk download github` sur N-1 **avant** `vpk pack`. vpk
  écrit donc le delta N-1 → N, et le job échoue si N-1 existe sans delta produit.
- **Porte E2E** : `gate.sh` installe le build portable de N-1, pris sur sa release. Il
  vérifie ensuite trois rapports `--smoke` : N-1 avant, `updates: ready N` avec
  `--apply-updates` et le dossier de releases local, puis N après le passage de l'updater.
  Sur la première release d'un canal, sans N-1, il vérifie seulement que N démarre. Les
  rapports sont en artefact `desktop-gate-<rid>`.
- **Rien n'est publié** si un RID requis (`win-x64`, `osx-arm64`, `osx-x64`) a échoué.
  `linux-x64` est publié quand il passe, sans jamais bloquer.

Première mise à jour après une installation : le postinstall du `.pkg` vide le cache de
l'updater, et un portable n'en a pas encore. Ce premier téléchargement est donc complet ;
les suivants passent par les deltas.

## E2E local (macOS arm64, non signé)

C'est le critère de sortie du lot 9. Il se rejoue sur l'hôte, sans Docker ni réseau autre
que NuGet, avec le même `pack.sh` et le même `gate.sh` que la CI.

```bash
npm ci --prefix src/LoDb.Web
npm --prefix src/LoDb.Web run build:shell
dotnet tool install -g vpk --version 1.2.0   # la version du package Velopack
tools/desktop-update-e2e/local.sh        # --keep pour garder les rapports
```

Déroulé, en une minute environ :

1. pack de 1.0.0 dans un flux local ;
2. `vpk download local`, puis pack de 1.0.1 par-dessus, avec delta ;
3. installation du portable 1.0.0, puis porte 1.0.0 → 1.0.1 ;
4. saut 1.0.1 → 1.0.2, qui doit passer par le delta (`--no-delta-hop` pour l'omettre).

Le script retire le dossier de travail (sauf `--keep`), ainsi que le cache et le journal
de l'updater (`~/Library/Caches/velopack/LoDb.Desktop`,
`~/Library/Logs/velopack_LoDb.Desktop.log`) quand c'est lui qui les a créés.

## Signature

L'ADR 0008 exige qu'un canal qui télécharge et exécute sans intervention ne soit jamais
publié non signé. `signing.sh` échoue donc si un secret ou une variable manque. Tout vit
dans l'environnement GitHub **`desktop-release`** (`Settings` → `Environments`), que seuls
les jobs `pack` utilisent.

### Windows : Azure Artifact Signing (Authenticode)

- **Abonnements** : un abonnement Azure, et un compte Artifact Signing avec une
  validation d'identité acceptée et un profil de certificat *Public Trust*.
- **Accès sans secret longue durée** :
  - une inscription d'application Entra ID, avec un identifiant fédéré GitHub (sujet
    `repo:<owner>/<repo>:environment:desktop-release`) ;
  - le rôle *Artifact Signing Certificate Profile Signer* sur le profil.
- Microsoft garde la clé dans un HSM et renouvelle les certificats chaque jour. Les
  signatures horodatées restent valides après expiration.

| Nom | Type | Valeur |
|---|---|---|
| `AZURE_CLIENT_ID` | secret | id de l'application Entra ID |
| `AZURE_TENANT_ID` | secret | id du tenant |
| `AZURE_SUBSCRIPTION_ID` | secret | id de l'abonnement |
| `AZURE_SIGNING_ENDPOINT` | variable | ex. `https://weu.codesigning.azure.net` |
| `AZURE_SIGNING_ACCOUNT` | variable | nom du compte Artifact Signing |
| `AZURE_SIGNING_PROFILE` | variable | nom du profil de certificat |

`signing.sh windows` écrit le fichier de métadonnées passé à `vpk --azureTrustedSignFile`.
signtool signe ensuite avec la session Azure CLI ouverte par `azure/login` (OIDC).

### macOS : Developer ID et notarisation

- **Abonnement** : Apple Developer Program, 99 $ par an.
- **Certificats** : *Developer ID Application*, pour l'app, et *Developer ID Installer*,
  pour le `.pkg`. On les exporte tous les deux, avec leur clé privée, dans un seul `.p12`
  protégé par un mot de passe fort.
- **Notarisation** : une clé d'API App Store Connect (`Users and Access` →
  `Integrations`, rôle *Developer*). Le `.p8` ne se télécharge **qu'une fois**.

| Nom | Type | Valeur |
|---|---|---|
| `APPLE_DEVELOPER_ID_P12` | secret | `base64 -i developer-id.p12` |
| `APPLE_DEVELOPER_ID_P12_PASSWORD` | secret | mot de passe du `.p12` |
| `APPLE_NOTARY_KEY_P8` | secret | `base64 -i AuthKey_<id>.p8` |
| `APPLE_NOTARY_KEY_ID` | secret | id de la clé d'API |
| `APPLE_NOTARY_ISSUER_ID` | secret | issuer id App Store Connect |
| `APPLE_APP_IDENTITY` | variable | `Developer ID Application: <Nom> (<TEAMID>)` |
| `APPLE_INSTALL_IDENTITY` | variable | `Developer ID Installer: <Nom> (<TEAMID>)` |

`signing.sh macos` importe les certificats dans un trousseau propre au job, que le job
supprime en fin de course. Il y enregistre aussi le profil `notarytool`, puis passe à vpk
`--signAppIdentity`, `--signInstallIdentity`, `--notaryProfile` et `--keychain`. Les
entitlements par défaut de vpk (hardened runtime, JIT de .NET) suffisent.

### Linux

L'AppImage n'a pas de signature de plateforme. Le RID est en best effort, et son intégrité
repose sur HTTPS et sur les empreintes du flux `releases.linux-x64.json`. **À trancher**
avant l'ouverture publique : le publier ainsi, ou le retirer de la matrice.

## Sauvegarde hors ligne

| Élément | À sauvegarder | Perte |
|---|---|---|
| Developer ID (Application et Installer) | le `.p12` et son mot de passe | nouveau certificat sous le même Team ID : les mises à jour continuent |
| Clé d'API de notarisation | le `.p8`, son id et l'issuer id | nouvelle clé, sans effet sur les versions publiées |
| Artifact Signing | nom du compte, profil, tenant, pièces de la validation d'identité | aucune clé à perdre ; revalider l'identité si le compte disparaît |

- **Deux copies chiffrées** hors ligne, dans deux lieux distincts : coffre d'un
  gestionnaire de mots de passe exporté, ou clés USB chiffrées. Jamais dans le dépôt, ni
  sur un poste non chiffré.
- Noter à chaque sauvegarde les dates d'expiration. Un Developer ID vaut 5 ans.
- Velopack n'épingle aucun certificat : changer de certificat ne coupe pas les mises à jour
  des installations existantes. Seule la confiance de l'OS compte (Gatekeeper,
  SmartScreen).

## Rotation

1. **Developer ID** (avant expiration, ou si compromis) :
   1. créer le nouveau certificat dans le compte Apple Developer ;
   2. exporter le `.p12` et mettre à jour `APPLE_DEVELOPER_ID_P12` et son mot de passe ;
   3. changer `APPLE_APP_IDENTITY` et `APPLE_INSTALL_IDENTITY` si le nom change.

   Ne révoquer l'ancien qu'en cas de compromission : la révocation peut empêcher le
   lancement des versions déjà distribuées.
2. **Clé de notarisation** : créer une nouvelle clé, mettre à jour les trois secrets
   `APPLE_NOTARY_*`, puis révoquer l'ancienne.
3. **Azure** : les certificats tournent seuls.
   - Il faut renouveler la validation d'identité quand le portail le demande.
   - Pour faire tourner l'accès, on remplace l'identifiant fédéré ou l'application :
     secrets `AZURE_*`.
   - Un nouveau profil de certificat change l'éditeur affiché, et la réputation
     SmartScreen repart de zéro.
4. **Vérifier** :
   1. lancer une beta à la main
      (`gh workflow run release-desktop.yml -f sha=<sha> -f channel=beta`) ;
   2. contrôler les jobs `pack`, avec la signature, la notarisation et la porte ;
   3. sauvegarder les nouveaux éléments hors ligne.

## Dépannage

| Message | Cause et remède |
|---|---|
| `tag desktop-v… and VersionPrefix … diverge` | aligner `VersionPrefix` et le tag, puis relancer |
| `several release tags on <sha>` | un seul tag `desktop-vX.Y.Z` par commit |
| `an unsigned build is never published; missing: …` | compléter l'environnement `desktop-release` |
| `vpk wrote no delta` | N-1 ≥ N, ou `vpk download` n'a rien trouvé |
| `<rid> has no release folder: nothing is published` | un RID requis a échoué : relancer les jobs en échec |
| `… has no feed for <rid>: it stays a pre-release` | upload incomplet : relancer `publish` |
| l'app ne voit pas la mise à jour | canal du build, `LODB_DESKTOP_UPDATE_SOURCE` vide, événements `desktop.update.*` |

## Limites connues

- Les chemins Windows et Linux, et les chemins signés, n'ont tourné qu'en CI. Le premier
  run réel est à suivre de près.
- `GithubSource` ne lit qu'une page de l'API des releases, les plus récentes toutes
  confondues. D'où la purge des betas. D'autres releases du dépôt, comme les bundles
  Android, pèsent aussi.
- La porte d'une première release de canal ne teste que le démarrage de N.
