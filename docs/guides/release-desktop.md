# Release du desktop (Velopack)

Ce guide couvre la publication de l'app desktop `LoDb.Desktop` et ses mises à jour
intégrées : versions et canaux, workflow, porte E2E, signature, clés et rotation. Les
décisions sont dans l'[ADR 0008](../reecriture/adr/0008-mises-a-jour-integrees.md) et le
chantier dans le [lot 9](../reecriture/implementation/lot-09-10-apps.md) (L9.4).

| Élément | Emplacement |
|---|---|
| Mises à jour dans l'app | `src/LoDb.Desktop/Updates/` |
| Vérification des flux signés | `src/LoDb.Desktop/Updates/Engine/Signing/` |
| Workflow de release | `.github/workflows/release-desktop.yml` |
| Scripts (build, pack, porte, publication, E2E local) | `tools/desktop-update-e2e/` |
| Signature des flux (clé, signature, vérification) | `tools/desktop-update-signing/` |

## Côté app

- **Source** : les GitHub Releases du dépôt public, lues par `SignedGithubSource`. Le dépôt
  et les clés publiques des flux sont des constantes de `Updates/Engine/UpdateFeeds.cs` : un
  fork doit changer les deux.
- **Flux signés** : l'app ne lit que les flux `releases.<canal>.json` dont la signature
  Ed25519 tient, et n'accepte que les paquets conformes à ces flux (voir
  [Flux signés](#flux-signés)).
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
  `downloaded`, `handed_over`, `handover_failed`, `feed_invalid`, `check_timed_out`,
  `feed_refused`, `package_refused`).
- **Rapport `--smoke`** : la ligne `feed-key: ed25519 <clés>` donne les clés de confiance du
  build ; la porte E2E les compare à celles de `UpdateFeeds.cs`.

Deux entrées cachées servent aux tests de bout en bout :

| Entrée | Effet |
|---|---|
| `--smoke --apply-updates` | vérifie et télécharge la mise à jour avant le rapport (`updates: ready N`), puis la confie à l'updater à la sortie |
| `LODB_DESKTOP_UPDATE_SOURCE` | dossier local de releases (chemin absolu) à la place de GitHub, lu à chaque lancement et pas seulement avec `--smoke` ; tenu aux mêmes flux signés que GitHub (`SignedFolderSource`), jamais reconstruit depuis les paquets du dossier ; une valeur invalide coupe les mises à jour (`feed_invalid`) |

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
`releases.<canal>.json` et sa signature `releases.<canal>.json.sig`. Une release GitHub
porte tous les RID d'une même version.

## Publier une version stable

1. Monter `VersionPrefix` dans une PR et la fusionner.
2. Poser le tag sur le commit qui partira en prod :
   `git tag desktop-vX.Y.Z <sha>`, puis `git push origin desktop-vX.Y.Z`.
3. Déployer la prod. Le déploiement appelle le workflow avec le SHA déployé
   (voir [Contrat d'appel](#contrat-dappel)). À défaut, on le lance à la main :
   `gh workflow run release-desktop.yml -f sha=<sha> -f channel=stable`.
4. Suivre le run : les jobs `build`, `pack` et `gate` de chaque RID, puis `publish`. La
   release apparaît d'abord en *pre-release*, puis elle est promue (`--latest`) quand les
   trois RID requis sont en ligne.

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
| `prepare` | Valide les entrées. Teste `tools/desktop-update-signing`. Résout la version (`release-version.sh`) et celle de `vpk`, qui est celle du package `Velopack`. Saute une release déjà promue. |
| `shell` | `npm ci` puis `npm run build:shell` : le front embarqué. |
| `build` (×4) | `build.sh` : `dotnet publish` du RID avec le front embarqué. **Sans environnement ni secret.** Le dossier publié passe à `pack` en tar (artefact `desktop-publish-<rid>`, 1 jour), qui garde les bits d'exécution. |
| `pack` (×4) | N-1 (`previous.sh`), signature de plateforme (`signing.sh`), `pack.sh` (`vpk pack` seul, sans build), présence du delta. Environnement `desktop-release`. Artefacts `desktop-packed-<rid>` et, flux seul, `desktop-feed-<rid>` (1 jour). |
| `sign` | Signe le flux de chaque RID (`sign-feed.mjs`), après avoir vérifié que la clé est la paire d'une clé compilée dans l'app. Environnement `desktop-release`, Node seul, aucun paquet. Artefact `desktop-signed-feeds` (7 jours). |
| `gate` (×4) | Porte (`gate.sh`) sur les paquets signés et le flux signé, **sans secret**. Seul un RID qui la passe est transmis à `publish` (`desktop-releases-<rid>`). |
| `publish` | `publish.sh` : RID requis présents, paquets conformes au flux signé, upload en *pre-release*, flux signé et sa signature à la place de celui de vpk, relus et vérifiés, puis promotion (stable) ou purge des vieilles betas. |

- **Deltas** : `previous.sh` lance `vpk download github` sur N-1 **avant** `vpk pack`. vpk
  écrit donc le delta N-1 → N, et le job échoue si N-1 existe sans delta produit.
- **Porte E2E** : `gate.sh` vérifie d'abord le flux signé et les paquets du dossier
  (`verify-feed.mjs --packages`). Il installe ensuite le build portable de N-1, pris sur sa
  release, et vérifie trois rapports `--smoke` : N-1 avant, `updates: ready N` avec
  `--apply-updates` et le dossier de releases local, puis N après le passage de l'updater.
  Sur la première release d'un canal, sans N-1, il vérifie seulement que N démarre. Dans les
  deux cas, N doit montrer les clés de `UpdateFeeds.cs` (`feed-key: ed25519 …`), puis ignorer
  son propre flux une fois altéré d'un octet : `updates: none` et
  `"Refusal":"InvalidSignature"` dans ses journaux. Les rapports sont en artefact
  `desktop-gate-<rid>`.
- **Rien n'est publié** si un RID requis (`win-x64`, `osx-arm64`, `osx-x64`) a échoué.
  `linux-x64` est publié quand il passe, sans jamais bloquer.
- **Isolation de la signature** : le build .NET exécute les cibles MSBuild de ses paquets
  NuGet, et la porte exécute l'app construite. Les deux tournent donc dans des jobs sans
  secret. `pack`, seul à recevoir les certificats et le jeton OIDC d'Azure, n'extrait du
  commit que `tools/desktop-update-e2e`, ne restaure aucun cache et ne lance que `vpk` (le
  paquet `Velopack` de l'app), qui signe les fichiers du dossier publié en les empaquetant.
  Le trousseau macOS est supprimé dès `vpk pack` terminé. `sign`, seul à recevoir la clé
  des flux, n'extrait que les scripts de signature et `UpdateFeeds.cs`, et ne lance que
  Node sur des fichiers JSON. La séparation protège les clés, pas le contenu : `pack` signe
  ce que `build` a produit, et la porte reste le contrôle de ce qui est publié.

Première mise à jour après une installation : le postinstall du `.pkg` vide le cache de
l'updater, et un portable n'en a pas encore. Ce premier téléchargement est donc complet ;
les suivants passent par les deltas.

## E2E local (macOS arm64, sans signature de plateforme)

C'est le critère de sortie du lot 9. Il se rejoue sur l'hôte, sans Docker ni réseau autre
que NuGet, avec les mêmes `build.sh`, `pack.sh`, `sign-feed.mjs` et `gate.sh` que la CI. Il
demande Node, mais aucun secret : il génère une clé de flux jetable dans son dossier de
travail, que ses builds approuvent (`build.sh --feed-key`, propriété MSBuild
`LoDbDesktopFeedKey`) et qui signe ses flux. Une release ne passe jamais cette option : sa
porte vérifie que N approuve les clés de `UpdateFeeds.cs`.

```bash
npm ci --prefix src/LoDb.Web
npm --prefix src/LoDb.Web run build:shell
dotnet tool install -g vpk --version 1.2.0   # la version du package Velopack
tools/desktop-update-e2e/local.sh        # --keep pour garder les rapports
```

Déroulé, en une minute environ :

1. clé de flux jetable, pack de 1.0.0 dans un flux local ;
2. `vpk download local`, puis pack de 1.0.1 par-dessus, avec delta, et signature du flux ;
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
supprime dès `vpk pack` terminé. Il y enregistre aussi le profil `notarytool`, puis passe à
vpk `--signAppIdentity`, `--signInstallIdentity`, `--notaryProfile` et `--keychain`. Les
entitlements par défaut de vpk (hardened runtime, JIT de .NET) suffisent.

### Linux

L'AppImage n'a pas de signature de plateforme. Le RID est en best effort. Ses mises à jour
sont authentifiées comme les autres par le flux signé `releases.linux-x64.json`, mais la
première installation ne repose que sur HTTPS. **À trancher** avant l'ouverture publique : le
publier ainsi, ou le retirer de la matrice.

## Flux signés

Velopack compare chaque paquet téléchargé à la taille et au SHA-256 que donne le flux
`releases.<canal>.json` de la même release. C'est un contrôle d'intégrité : il ne dit pas
qui a publié le flux. L'app ne lit donc un flux que si sa signature Ed25519 tient contre une
clé compilée dans l'app. Les empreintes signées authentifient alors chaque paquet, quel que
soit l'hébergement des releases.

| Élément | Valeur |
|---|---|
| Signature | Ed25519 (RFC 8032) de `releases.<canal>.json`, en base64 sur une ligne dans `releases.<canal>.json.sig` |
| Message signé | `lodb-desktop-feed-v1\n` + nom du flux + `\n` + octets du flux publié : une signature ne vaut que pour son RID et son canal |
| Clés publiques | `UpdateFeeds.FeedPublicKeys` (32 octets en base64), une seule hors rotation |
| Clé privée | secret `LODB_DESKTOP_UPDATE_PRIVATE_KEY` (PEM PKCS#8), lu par le seul job `sign` |
| Entrées du flux | chacune avec sa taille et son SHA-256 ; sans eux, l'app ignore le flux |
| Outils | `tools/desktop-update-signing` (Node seul) : `generate-key`, `sign-feed`, `verify-feed`, `app-key` |

### Dans l'app

- `SignedGithubSource` lit les releases comme Velopack, mais n'en garde que les flux dont la
  signature tient. Un flux refusé compte comme absent : l'app reste sur sa version et
  journalise `desktop.update.feed_refused` avec la raison (`MissingSignature`,
  `InvalidSignature`, `Unreadable`, `IncompleteEntry`).
- Chaque paquet téléchargé, complet ou delta, doit avoir la taille et le SHA-256 de son entrée
  signée, sinon il est refusé (`desktop.update.package_refused`) et jamais appliqué. Velopack
  refait ensuite son propre contrôle.
- Le dossier local de la porte (`SignedFolderSource`) suit la même règle. Sans flux signé, il
  n'offre rien : il ne reconstruit jamais un flux depuis les paquets du dossier.

### Dans la release

1. `pack` publie le flux de chaque RID à part (`desktop-feed-<rid>`).
2. `sign` réécrit chaque flux tel que la release GitHub le publiera : les seuls paquets de la
   version, sans le N-1 que `vpk pack` liste aussi. Puis il le signe. Il refuse une clé
   privée dont la moitié publique n'est pas dans `UpdateFeeds.cs` : une release que les
   installations refuseraient ne part pas.
3. `gate` remplace le flux de vpk par le flux signé et vérifie les paquets. N doit montrer
   les clés attendues et ignorer son flux altéré.
4. `publish` revérifie les paquets contre le flux signé. Après `vpk upload`, qui écrit son
   propre flux, il le remplace par le flux signé et sa signature (`gh release upload
   --clobber`). Il relit enfin les deux depuis la release, les compare et les vérifie avant la
   promotion.

### Signature de plateforme avant application

Aucune release desktop n'est encore signée Authenticode ni Developer ID : l'environnement
`desktop-release` et ses certificats ([Signature](#signature)) restent à créer par le
propriétaire. L'app ne vérifie donc pas de signature de plateforme avant d'appliquer un
paquet. Elle vérifie le SHA-256 que le flux signé donne pour ce paquet, ce qui l'authentifie
déjà. Ce contrôle épingle notre clé, là où Authenticode accepterait tout éditeur reconnu par
l'OS. La signature de plateforme reste nécessaire à la confiance de l'OS à l'installation
(SmartScreen, Gatekeeper). Un contrôle WinVerifyTrust ou `codesign --verify` du paquet avant
application pourra s'ajouter quand les releases seront signées.

### La clé

- Paire générée par `generate-key.mjs`, qui refuse un chemin dans un dépôt git et n'écrase
  jamais un fichier. La clé privée vit dans le dossier de secrets local du propriétaire,
  hors de tout dépôt, et dans le secret `LODB_DESKTOP_UPDATE_PRIVATE_KEY` :
  `gh secret set LODB_DESKTOP_UPDATE_PRIVATE_KEY < <fichier>`. Secret de dépôt pour
  commencer ; le déplacer dans l'environnement `desktop-release` une fois celui-ci créé, pour
  profiter de ses règles de protection.
- Seule la clé publique entre dans le dépôt, dans `UpdateFeeds.FeedPublicKeys`.

### Bascule vers les flux signés

- Une installation qui ne vérifie rien (antérieure aux flux signés) lit le flux comme avant
  et ignore le `.sig` : la première release signée s'installe chez elle normalement.
- Toute version qui vérifie refuse ensuite les releases non signées encore en ligne. Ce
  n'est pas gênant : elles sont plus anciennes qu'elle.
- La porte de la première release signée part d'un N-1 qui ne vérifie pas. Elle ne prouve
  donc la vérification que sur N : clés attendues et refus du flux altéré. Dès la release
  suivante, N-1 vérifie lui-même le flux de N.
- Au 2026-10-11, aucune release desktop n'est publiée : la première release publique sera
  déjà signée.

## Sauvegarde hors ligne

| Élément | À sauvegarder | Perte |
|---|---|---|
| Developer ID (Application et Installer) | le `.p12` et son mot de passe | nouveau certificat sous le même Team ID : les mises à jour continuent |
| Clé d'API de notarisation | le `.p8`, son id et l'issuer id | nouvelle clé, sans effet sur les versions publiées |
| Artifact Signing | nom du compte, profil, tenant, pièces de la validation d'identité | aucune clé à perdre ; revalider l'identité si le compte disparaît |
| Clé des flux (Ed25519) | le PEM de `LODB_DESKTOP_UPDATE_PRIVATE_KEY` | **plus aucune mise à jour possible** pour les installations existantes : il faudra réinstaller l'app |

- **Deux copies chiffrées** hors ligne, dans deux lieux distincts : coffre d'un
  gestionnaire de mots de passe exporté, ou clés USB chiffrées. Jamais dans le dépôt, ni
  sur un poste non chiffré.
- Noter à chaque sauvegarde les dates d'expiration. Un Developer ID vaut 5 ans.
- Velopack n'épingle aucun certificat : changer de certificat ne coupe pas les mises à jour
  des installations existantes. Seule la confiance de l'OS compte (Gatekeeper,
  SmartScreen).
- La clé des flux, elle, est épinglée par chaque installation : sa perte coupe les mises à
  jour, sa fuite permet de signer des flux acceptés. C'est l'élément le plus précieux de
  cette table.

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
4. **Clé des flux** (par hygiène, ou si elle a fuité) : les installations ne connaissent
   que les clés compilées, d'où une release relais.
   1. générer la nouvelle paire (`generate-key.mjs --out <nouveau fichier>`) ;
   2. release relais R : ajouter la nouvelle clé publique à `UpdateFeeds.FeedPublicKeys`,
      à côté de l'ancienne, et la publier **signée par l'ancienne** (secret inchangé) ;
   3. remplacer `LODB_DESKTOP_UPDATE_PRIVATE_KEY` par la nouvelle clé privée ;
   4. release suivante : retirer l'ancienne clé de la liste.

   Une installation antérieure à R passe d'abord par R, puis par la suivante. R doit donc
   rester parmi les dix releases les plus récentes du dépôt, celles que lit l'app. En cas de
   fuite, aller vite : une installation qui fait encore confiance à l'ancienne clé accepte
   les flux qu'elle signe, jusqu'à la release qui la retire.
5. **Vérifier** :
   1. lancer une beta à la main
      (`gh workflow run release-desktop.yml -f sha=<sha> -f channel=beta`) ;
   2. contrôler les jobs `pack` (signature, notarisation), `sign` et `gate` (porte) ;
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
| `LODB_DESKTOP_UPDATE_PRIVATE_KEY is not set` | poser le secret (§ Flux signés, La clé) |
| `… is not the pair of a key the app trusts` | le secret ne correspond à aucune clé de `UpdateFeeds.FeedPublicKeys` : corriger le secret, ou la liste lors d'une rotation |
| `the release folder of <rid> does not match its signed feed` | un paquet a changé entre `pack` et `publish` : relancer depuis `pack` |
| `the published releases.<canal>.json … is not the signed one` | upload interrompu ou concurrent : relancer `publish` |
| `… does not trust the feed key …` | N n'approuve pas les clés de `UpdateFeeds.cs` : build fait avec `--feed-key`, à refaire sans |
| `… did not refuse a feed whose signature no longer holds` | N ne vérifie pas ses flux : régression à corriger avant toute release |
| l'app ne voit pas la mise à jour | canal du build, `LODB_DESKTOP_UPDATE_SOURCE` vide, événements `desktop.update.*` (dont `feed_refused`) |

## Limites connues

- Les chemins Windows et Linux, et les chemins signés, n'ont tourné qu'en CI. Le premier
  run réel est à suivre de près, en particulier le passage `build` → `pack` → `gate` sur
  trois runners distincts par RID (tar du dossier publié, paquets transmis en artefact).
- `GithubSource` ne lit qu'une page de l'API des releases, les plus récentes toutes
  confondues. D'où la purge des betas. D'autres releases du dépôt, comme les bundles
  Android, pèsent aussi.
- La porte d'une première release de canal ne teste que le démarrage de N, ses clés et son
  refus d'un flux altéré.
- Les flux signés n'ont tourné qu'en tests (xUnit, `node --test`) : le premier run réel de
  `sign` → `gate` → `publish` est à suivre de près, en particulier le remplacement du flux
  de vpk après `vpk upload`.
- `previous.sh` récupère N-1 par `vpk download` sans le comparer au flux signé de sa
  release, et `vpk pack` calcule le delta N-1 → N sur ce paquet. Reste à faire : vérifier ce
  paquet contre le flux signé de N-1 avant `vpk pack`, avec les clés que N-1 approuve (une
  rotation les change).
