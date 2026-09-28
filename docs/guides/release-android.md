# Release d'Android (Play, live update, canal transitoire)

Ce guide couvre la publication de l'app Android et ses mises à jour : versions, workflow,
porte E2E, clés de signature, Play, sauvegarde et rotation. Les décisions sont dans
l'[ADR 0008](../reecriture/adr/0008-mises-a-jour-integrees.md), les chantiers dans le
[lot 10](../reecriture/implementation/lot-09-10-apps.md) (L10.3, L10.4).

| Élément | Emplacement |
|---|---|
| Mises à jour dans l'app | `src/LoDb.Web/src/app/core/platform/android/updates/` |
| Signature des bundles | `tools/next/live-update/` |
| Workflow de release | `.github/workflows/next-release-android.yml` |
| Scripts (version, signature, porte, publication, vérification locale) | `tools/next/android-release/` |

## Côté app

Deux niveaux, décidés par l'API (`GET /api/client-policy`, plateforme `android`) :

- **Live update** (`@capawesome/capacitor-live-update`) : le front, zip du build
  `shell-store`, en asset de GitHub Release. Le plugin refuse un zip dont la signature RSA
  ne tient pas contre la clé publique embarquée. Téléchargé en tâche de fond, appliqué au
  **prochain démarrage à froid**. Si l'app n'appelle pas `ready()` avant `readyTimeout`, le
  plugin revient au bundle embarqué et l'app écarte le bundle fautif.
- **Binaire natif** (`@capawesome/capacitor-app-update`) : Play In-App Updates, flux
  *flexible* sous `latest`, *immediate* sous `minimum`.
- **Canal transitoire**, tant que la fiche Play n'est pas ouverte : `latest.json` lu sur le
  site, APK ouvert dans le navigateur système, SHA-256 et `versionCode` vérifiés.
- **Rythme** : au démarrage et au retour au premier plan, au plus toutes les 15 minutes.

Publier la politique sans bundle ramène les appareils au bundle embarqué dans leur APK : c'est
le retrait d'un bundle fautif, sans nouvelle release.

La config Capacitor attendue (branchement hors L10.3/L10.4, voir [Limites
connues](#limites-connues)) :

```ts
plugins: {
  LiveUpdate: {
    publicKey: process.env['LODB_LIVE_UPDATE_PUBLIC_KEY'],
    readyTimeout: 20_000,
  },
},
```

Le workflow lit `assets/capacitor.config.json` dans l'APK
(`build/check-embedded-config.mjs`) et s'arrête si la clé n'est pas la clé publique du
signataire, si c'est une clé privée, ou si `readyTimeout` n'est pas positif.

## Versions

- Le tag `android-vX.Y.Z` posé sur le commit donne la version. Sans tag, pas de release.
- `versionCode` = `X × 1 000 000 + Y × 1 000 + Z` : mineure et patch sous 1000, au plus
  2 100 000 000, le plafond de Play. `2.10.0` passe donc bien après `2.9.99`.
- Les releases Android ne sont **jamais** la *latest* du dépôt : `/releases/latest` reste
  le flux du desktop.

### Version native minimale des bundles

Chaque bundle déclare la plus ancienne coquille qui le supporte (`minimumNativeVersion`).
`tools/next/android-release/native-baseline.json` la fixe, avec l'empreinte de la couche
native : fichiers suivis de `src/LoDb.Web/android/` et `capacitor.config.ts`, puis nom et
version de chaque plugin inclus dans `capacitor.settings.gradle`.

- Couche native inchangée : les bundles gardent le minimum de la base.
- Couche native changée (nouveau plugin, montée de Capacitor, `targetSdk`…) : le workflow
  s'arrête. La PR de ce changement monte la base à la version qui le livre :

  ```sh
  git add <fichiers natifs>   # l'empreinte lit l'index
  node tools/next/android-release/build/native-fingerprint.mjs --update X.Y.Z
  git add tools/next/android-release/native-baseline.json
  ```

  Ses bundles ne vont alors qu'aux coquilles X.Y.Z et plus. Les autres passent d'abord par
  Play : publier `--minimum X.Y.Z` si le front ne tourne plus sur les anciennes.

## Publier une version

1. Poser le tag sur le commit qui partira en prod :
   `git tag android-vX.Y.Z <sha>`, puis `git push origin android-vX.Y.Z`.
2. Déployer la prod. Le déploiement appelle le workflow avec le SHA déployé
   (voir [Contrat d'appel](#contrat-dappel)). À défaut, on le lance à la main :
   `gh workflow run next-release-android.yml -f sha=<sha>`.
3. Suivre le run : `build`, `stage`, `gate`, `cleanup`, `publish`, puis `play` quand Play
   est activé.
4. Publier la politique client : le résumé du job `publish` donne la commande (voir
   [Politique client](#politique-client)).

Relancer un run est sans risque :

- une release déjà promue n'est pas republiée (`prepare` la saute) ;
- une *pre-release* laissée par une porte en échec est reprise, ses assets remplacés ;
- `play` se relance seul, après la promotion.

### Contrat d'appel

Le déploiement de prod (L8.1) appelle le workflow une fois le déploiement réussi, sur le
SHA exact :

```yaml
  android:
    needs: deploy
    uses: ./.github/workflows/next-release-android.yml
    with:
      sha: ${{ github.sha }}
    permissions:
      contents: write   # la pre-release, ses assets et sa promotion
    secrets: inherit
```

Les testeurs du staging passent par la piste de test interne de Play, alimentée par ce même
workflow : pas de clés de signature propres au staging.

### Politique client

C'est l'API qui sert le bundle. Après la release, un opérateur publie la politique sur
l'hôte, avec la sous-commande de l'API (ou `PUT /api/admin/client-policy/android`) :

```sh
client-policy publish --platform android --minimum <minimum en vigueur> --latest X.Y.Z \
  --bundle-id X.Y.Z --bundle-url <url> --bundle-checksum <sha-256> \
  --bundle-signature <base64> --bundle-minimum-native A.B.C
```

- La commande **remplace toute la politique** de l'app : une option omise efface sa valeur.
  Toujours reprendre le `--minimum` en vigueur.
- Les valeurs `--bundle-*` sont celles du descripteur `lodb-bundle-X.Y.Z.json`, publié
  avec la release et recopié dans le résumé du job `publish`.
- Tant que la fiche Play n'est pas ouverte, `--latest` ne sert qu'au canal transitoire.

## Ce que fait le workflow

| Job | Rôle |
|---|---|
| `prepare` | Valide le SHA, résout la version (`release-version.mjs`), saute une release déjà promue, calcule le minimum natif (`native-fingerprint.mjs --check`). |
| `build` | `npm ci`, tests des outils, `build:shell:store`, bundles signés (`bundles.sh`), `cap sync android`, `gradlew bundleRelease assembleRelease assembleDebug`, contrôle de la config embarquée, signature (`sign-release.sh`). Environnement `android-release`. |
| `stage` | `stage.sh` : *pre-release* `android-vX.Y.Z`, upload des bundles (release et porte), relecture du bundle depuis son URL publique. |
| `gate` | `gate.mjs` sur un émulateur API 35 x86_64 (voir ci-dessous). Le logcat est en artefact `android-gate-logcat`. |
| `cleanup` | Retire les bundles de porte de la release, quel que soit le verdict. |
| `publish` | `publish.sh` : APK et `lodb-android-latest.json` si le canal transitoire est actif, relecture de l'APK, promotion. Résumé avec la commande de politique. |
| `play` | AAB sur la piste **interne** de Play, si `ANDROID_PLAY_ENABLED` vaut `true`. Environnement `android-release`. |

Artefacts du run : `android-release` (AAB, APK, `signing.txt`, 30 jours : c'est l'AAB à
déposer à la main avant l'automatisation de Play), `android-bundles` et `android-gate-apk`
(7 jours).

### Porte E2E

Rien n'est publié sans elle. Elle tourne sur l'émulateur, bundles servis par leurs URL de
*pre-release*, réseau coupé hors téléchargements : les vérifications propres de l'app ne
lisent donc aucune politique et ne déplacent aucun bundle dans son dos.

1. L'APK de release signé s'installe, démarre et tourne encore 10 s après.
2. L'APK debug du même build démarre sur son bundle embarqué. Il porte le même front et les
   mêmes réglages `LiveUpdate`, et sa WebView est ouverte à DevTools : la porte pilote le
   plugin par le pont Capacitor.
3. Un bundle **altéré** (zip de release plus un fichier, signature de release) est refusé.
4. Un bundle **fautif**, signé mais dont la page ne démarre jamais l'app, tourne au démarrage
   à froid suivant, puis le plugin revient au bundle embarqué après `readyTimeout`, et l'app
   supprime le fautif.
5. Le bundle de la **release** tourne au démarrage suivant, et tourne encore après
   `readyTimeout` : l'app a confirmé son démarrage.

Les bundles de porte sont signés avec la vraie clé : `cleanup` les retire toujours de la
release.

## Clés

| Clé | Rôle | Où | Perte |
|---|---|---|---|
| Clé d'envoi (*upload key*) | signe l'AAB déposé sur Play | environnement `android-release` | Play la réinitialise sur demande (Play App Signing) |
| Clé de signature de l'app | signe l'APK du canal transitoire, puis, importée, tout ce que Play distribue | environnement `android-release`, puis Play | les installations hors Play ne se mettent plus à jour sans désinstallation |
| Clé RSA des bundles | signe les live updates ; la clé publique est dans chaque APK | environnement `android-release` (privée), variable (publique) | plus aucun bundle jusqu'à une release native portant une nouvelle clé |

### Clé d'envoi

Générée une fois, hors du checkout, sur un poste chiffré :

```sh
keytool -genkeypair -v -storetype PKCS12 -keystore /secure/lodb-upload.p12 -alias upload \
  -keyalg RSA -keysize 4096 -validity 10000 -dname "CN=League of Data Base, O=LoDb"
```

- PKCS12 : un seul mot de passe pour le magasin et la clé (les deux secrets ont la même
  valeur).
- Validité ≥ 25 ans, comme Play l'exige.

### Clé de signature de l'app et Play App Signing

C'est la clé du canal transitoire. Elle doit devenir la clé de signature Play : sinon les
installations transitoires ne pourraient pas passer sur Play sans désinstaller.

1. La générer comme la clé d'envoi (`/secure/lodb-app-signing.p12`, alias `lodb`), en clé
   **distincte**.
2. À la création de l'app dans la Play Console, à l'étape Play App Signing, choisir
   d'**importer** une clé existante (« utiliser une autre clé de signature », export depuis
   un keystore Java), jamais la clé générée par Google.
3. La Console fournit l'outil PEPK et sa clé de chiffrement :

   ```sh
   java -jar pepk.jar --keystore=/secure/lodb-app-signing.p12 --alias=lodb \
     --output=/secure/lodb-app-signing-encrypted.zip --include-cert \
     --rsa-aes-encryption --encryption-key-path=/secure/encryption_public_key.pem
   ```

4. Déposer le zip chiffré, puis le certificat de la clé d'envoi.
5. Vérifier dans la Console (Intégrité de l'app) que l'empreinte SHA-256 de la clé de
   signature est celle de `apk_sha256` dans `signing.txt`.

Sans la clé de signature de l'app, le workflow signe l'APK avec la clé d'envoi, pour la porte
seulement : il ne publie pas cet APK, et s'arrête si `ANDROID_TRANSITIONAL_CHANNEL` vaut
`true`.

Les empreintes des deux clés (`signing.txt`) vont dans `assetlinks.json`
(`tools/next/android/assetlinks.mjs`) : sans elles, l'App Link de l'OAuth n'est pas vérifié.

### Clé RSA des bundles

```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out /secure/lodb-live-update.pem
LODB_LIVE_UPDATE_PRIVATE_KEY="$(cat /secure/lodb-live-update.pem)" \
  node tools/next/live-update/public-key.mjs > lodb-live-update.pub.pem
```

- 4096 bits au plus : la signature (684 caractères) doit tenir dans les 1024 de l'API.
- La clé publique entre dans chaque APK : en changer demande une release native, et les
  bundles signés avec la nouvelle ne vont qu'aux coquilles qui l'embarquent.

## Secrets et variables

Les secrets vivent dans l'environnement GitHub **`android-release`** (`Settings` →
`Environments`), limité à la branche `main`. Les variables sont celles du dépôt : `stage`
lit la clé publique hors de l'environnement.

| Nom | Type | Valeur |
|---|---|---|
| `ANDROID_UPLOAD_KEYSTORE_BASE64` | secret | `base64 -i /secure/lodb-upload.p12` |
| `ANDROID_UPLOAD_STORE_PASSWORD` | secret | mot de passe du magasin |
| `ANDROID_UPLOAD_KEY_PASSWORD` | secret | mot de passe de la clé (le même en PKCS12) |
| `ANDROID_UPLOAD_KEY_ALIAS` | variable | `upload` |
| `ANDROID_APP_SIGNING_KEYSTORE_BASE64` | secret | `base64 -i /secure/lodb-app-signing.p12` |
| `ANDROID_APP_SIGNING_STORE_PASSWORD` | secret | mot de passe du magasin |
| `ANDROID_APP_SIGNING_KEY_PASSWORD` | secret | mot de passe de la clé |
| `ANDROID_APP_SIGNING_KEY_ALIAS` | variable | `lodb` |
| `LODB_LIVE_UPDATE_PRIVATE_KEY` | secret | le PEM de `/secure/lodb-live-update.pem`, ou ce PEM en base64 |
| `LODB_LIVE_UPDATE_PUBLIC_KEY` | variable | le PEM de `lodb-live-update.pub.pem` |
| `PLAY_SERVICE_ACCOUNT_JSON` | secret | clé JSON du compte de service Play |
| `ANDROID_TRANSITIONAL_CHANNEL` | variable | `true` tant que la fiche Play n'est pas ouverte |
| `ANDROID_PLAY_ENABLED` | variable | `true` une fois l'envoi à Play prêt |
| `ANDROID_APP_LINK_HOST` | variable | hôte de l'App Link, `league-of-data-base.com` par défaut |

Le workflow décode les keystores dans le dossier temporaire du runner, le temps de l'étape
de signature ; les mots de passe passent par l'environnement, jamais par une ligne de
commande.

## Play

1. Créer l'app `com.leagueofdatabase.app` dans la Play Console, avec Play App Signing et la
   clé importée (voir plus haut).
2. Déposer **à la main** le premier AAB (artefact `android-release`) sur la piste interne :
   l'API de Play ne publie rien pour une app qui n'a encore aucune release.
3. Google Cloud : activer la *Google Play Android Developer API*, créer un compte de service
   et sa clé JSON (secret `PLAY_SERVICE_ACCOUNT_JSON`).
4. Play Console → Utilisateurs et autorisations : inviter le compte de service, limité à
   l'app, avec le droit de publier sur les pistes de test.
5. Passer `ANDROID_PLAY_ENABLED` à `true`. Le job `play` envoie ensuite chaque AAB sur la
   piste interne ; la promotion vers la production reste un geste manuel dans la Console.

Une fois la fiche ouverte et Play en service : passer `ANDROID_TRANSITIONAL_CHANNEL` à
`false`, et `TRANSITIONAL_CHANNEL_ENABLED` à `false` dans l'app
(`transitional/transitional-channel-token.ts`).

## Canal transitoire : `latest.json`

- `publish.sh` publie `lodb-android-latest.json` en asset de la release : `versionCode`,
  `versionName`, URL de l'APK (github.com) et son SHA-256.
- L'app le lit sur `<origine de l'API>/android/latest.json` : un asset de GitHub Release n'a
  pas d'en-têtes CORS, la WebView (`https://localhost`) ne peut donc pas le lire là.
- Le site doit servir ce fichier, avec `Access-Control-Allow-Origin: https://localhost` : à
  copier depuis la release à chaque publication, tant que nginx ne le relaie pas.

## Vérification des développeurs

Google exige une identité de développeur vérifiée pour les apps installées **hors Play** :
dès le 2026-09-30 dans 4 pays, partout en 2027. Sans elle, les appareils refuseront l'APK
du canal transitoire.

- Enregistrer l'identité (compte Play Console ou Android Developer Console) **avant 2027**,
  avec le nom de package `com.leagueofdatabase.app`.
- Y déclarer la clé de signature de l'app (empreinte `apk_sha256`), la même que celle
  importée dans Play App Signing.
- Les installations par Play ne sont pas concernées.

## Vérification locale de la signature

C'est le critère de sortie de L10.4, rejoué au jalon des lots 9-10. Aucune vraie clé n'est
lue : deux keystores jetables (RSA 4096, valides un jour, `CN=LoDb throwaway …`) et une clé
de bundle jetable naissent et meurent dans le conteneur.

```sh
tools/next/android-release/verify-local.sh               # version 1.0.0
tools/next/android-release/verify-local.sh --version 2.4.0
```

1. Build de `docker/next/android-build` (`lodb-android-build:local`, `linux/amd64`).
2. Dans le conteneur (6 Gio, 6 CPU, checkout en lecture seule) : `npm ci`,
   `build:shell:store`, `cap sync android`, `gradlew bundleRelease assembleRelease` avec le
   `versionCode` de la version.
3. Signature par `sign-release.sh`, le script du workflow : `jarsigner` puis
   `jarsigner -verify` pour l'AAB ; `zipalign`, `apksigner sign` puis `apksigner verify`
   (schéma v2) pour l'APK ; chaque certificat comparé à celui de son keystore.
4. Sortie dans `src/LoDb.Web/dist/android-release/` : `lodb-X.Y.Z.aab`, `lodb-X.Y.Z.apk`,
   `signing.txt`, puis les certificats et la ligne `package:` de l'APK
   (`versionCode`, `versionName`).

Succès : le script finit sur `verify-local.sh: release signature verified`. Tâche lourde :
jamais à côté de la stack legacy, d'un autre build Android ou de l'E2E. Ne jamais publier
ces fichiers.

## Sauvegarde hors ligne

| Élément | À sauvegarder |
|---|---|
| Clé d'envoi | le `.p12`, son alias et son mot de passe |
| Clé de signature de l'app | le `.p12`, son alias et son mot de passe, le zip PEPK déposé |
| Clé RSA des bundles | le PEM privé, et la clé publique |
| Play | l'accès au compte de développeur, la clé JSON du compte de service (révocable) |

- **Deux copies chiffrées** hors ligne, dans deux lieux distincts : coffre exporté d'un
  gestionnaire de mots de passe, ou clés USB chiffrées. Jamais dans le dépôt, ni sur un
  poste non chiffré.
- La perte de la clé des bundles ou de la clé de signature coupe les mises à jour (ADR 0008) :
  vérifier la restauration des copies une fois par an, par exemple avec
  `keytool -list -keystore <copie>` et `public-key.mjs`.

## Rotation

1. **Clé d'envoi** (perte ou compromission) : Play Console → Intégrité de l'app → demander
   la réinitialisation de la clé d'envoi, avec le certificat d'une nouvelle clé. Mettre à
   jour les secrets `ANDROID_UPLOAD_*`, puis `assetlinks.json`.
2. **Clé de signature de l'app** : pas de rotation de routine.
   - Sur Play, la mise à niveau de clé de Play App Signing vaut pour les nouvelles
     installations ; les autres gardent l'ancienne.
   - Sur le canal transitoire, une nouvelle clé impose la désinstallation.
3. **Clé RSA des bundles** (compromission) :
   1. publier aussitôt la politique **sans bundle** : les appareils reviennent au bundle
      embarqué ;
   2. générer une nouvelle paire, mettre à jour `LODB_LIVE_UPDATE_PRIVATE_KEY` et
      `LODB_LIVE_UPDATE_PUBLIC_KEY` ;
   3. livrer une release native (la couche native change : `--update`), et monter
      `--minimum` si les anciennes coquilles ne doivent plus tourner.
4. **Compte de service Play** : créer une nouvelle clé JSON, mettre à jour le secret,
   supprimer l'ancienne.

## Dépannage

| Message | Cause et remède |
|---|---|
| `No Android release: no android-vX.Y.Z tag on …` | poser le tag sur le SHA déployé, relancer |
| `several release tags: …` | un seul tag `android-vX.Y.Z` par commit |
| `… is not a release version X.Y.Z` | mineure et patch sous 1000, sans zéro en tête |
| `the native layer changed since the baseline … Run native-fingerprint.mjs --update X.Y.Z` | monter la base native dans une PR (voir [Version native minimale](#version-native-minimale-des-bundles)) |
| `… the app would take unsigned bundles` / `… is not the public key of the bundle signer` / `… readyTimeout is not set` | config `LiveUpdate` de `capacitor.config.ts`, ou `LODB_LIVE_UPDATE_PUBLIC_KEY` |
| `the app embeds a private key` | une clé privée a fui dans le build : ne rien publier, faire tourner la clé des bundles |
| `the debug APK, which the gate drives, differs from the release one` | les deux variantes doivent embarquer la même config |
| `LODB_… is not set` | compléter l'environnement `android-release` |
| `the transitional channel needs ANDROID_APP_SIGNING_KEYSTORE_BASE64` | clé de signature de l'app absente |
| `… is already published` | la release est promue : nouvelle version |
| porte : refus du bundle altéré absent | la clé publique embarquée n'est pas vérifiée : ne rien publier |
| porte : pas de retour arrière | `readyTimeout` du plugin, logcat en artefact |
| `play` : `Package not found` | premier AAB à déposer à la main, droits du compte de service |

## Limites connues

- **Branchements hors périmètre de L10.3/L10.4**, à faire avant la première release :
  - `AndroidPlatform` doit démarrer `AndroidUpdates` et lui confier `updateState` et
    `applyUpdate()` ;
  - `capacitor.config.ts` doit déclarer le plugin `LiveUpdate` (voir [Côté app](#côté-app)),
    puis `cap sync` régénérer `capacitor.settings.gradle`, et `package-lock.json` être
    régénéré ;
  - la base native doit être remontée ensuite (`native-fingerprint.mjs --update`) ;
  - nginx doit servir `/android/latest.json` avec son en-tête CORS ;
  - le déploiement de prod doit appeler ce workflow ([Contrat d'appel](#contrat-dappel)) ;
  - les secrets sont à ajouter au guide des secrets GitHub Actions.
- La porte et le workflow n'ont tourné qu'à blanc (`actionlint`) : le premier run réel est à
  suivre de près.
- La porte pilote le plugin par l'APK debug : même front et mêmes réglages que la release,
  vérifiés, mais pas le même binaire. L'APK de release n'est testé qu'au démarrage.
- Chaque release Android ajoute une release au dépôt, lue aussi par `GithubSource` du
  desktop, qui ne lit qu'une page de l'API des releases.
