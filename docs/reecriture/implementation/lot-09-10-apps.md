# Lots 9 et 10 — Apps desktop et Android

Objectif des lots : deux coquilles minces qui embarquent le build `shell` du front, parlent
à l'API distante et se mettent à jour elles-mêmes. Ils peuvent démarrer dès les fondations
du lot 3, L4.5 (socle d'authentification front) et L6.2 (table de la politique client) ;
leur publication attend la bascule (lot 8), car les apps ont besoin des comptes et des
builds.

Critères de sortie locaux :

- **desktop** : build macOS arm64 ; `--smoke` affiche la version et sort ; mise à jour
  N-1 → N observée en local, non signée, par `--smoke` avant et après ;
- **Android** : APK de debug construit en conteneur `linux/amd64` ; live update, retour
  arrière d'un bundle défectueux et politique `426` couverts par des tests.

Les releases signées (Windows, macOS, Play) exigent des comptes et des clés : ce sont des
opérations hôte (§11 du plan). Les **portes de CI** exigées par les ADR 0008 et 0010 (mise
à jour N-1 → N sur desktop, retour arrière d'un bundle défectueux sur Android) sont écrites
dans les workflows de release et s'exécutent avant toute publication.

## L9.0 — Politique client commune

- **Objectif** : un seul point de contrôle pour forcer, suspendre ou retirer une version
  d'app, sans nouvelle release.
- **À lire** : ADR [0008](../adr/0008-mises-a-jour-integrees.md) (compatibilité et politique
  client), ADR [0004](../adr/0004-stockage-etat-et-scaling.md) (état mutable en base).
- **Périmètre** : `src/LoDb.Api/Modules/ClientPolicy/**` (complète le squelette de L2.1),
  `src/LoDb.Api/Cli/ClientPolicy*`, `src/LoDb.Web/src/app/core/update/**`, tests associés.
- **Conception** :
  - Politique lue dans la table `client_policy` (L6.2), mise en cache une minute ; publiée
    par la sous-commande `client-policy publish` et par un endpoint admin (politique
    `Admin`) : par plateforme, version minimale et dernière ; pour Android, bundle courant
    (URL, signature, version native minimale).
  - `GET /api/client-policy` ; middleware qui lit `X-LoDb-Client: {plateforme}/{version}` et
    répond **`426 Upgrade Required`** (ProblemDetails typé) sous le minimum ; le web, sans
    en-tête, n'est jamais concerné ; compteur des réponses 426.
  - Front : l'intercepteur de L3.1 ajoute l'en-tête pour les apps ; `core/update/` affiche
    l'écran de mise à jour bloquant et le bandeau « Mise à jour prête — redémarrer »,
    alimentés par `PlatformService`.
- **Tests** : comparaison de versions, 426 sous le minimum, absence d'effet sans en-tête,
  publication et relecture de la politique, écran bloquant.
- **Dépend de** : L6.2, fondations du lot 3. **Taille** : M.

## L9.1 — Spike Photino / Avalonia

- **Objectif** : reconfirmer le choix de l'ADR 0007 sur l'état du moment, avant tout code
  desktop livré.
- **À lire** : ADR [0007](../adr/0007-coquilles-desktop-et-android.md), `src/App.GitHealth.*`
  du dépôt GitHealth (Photino + Velopack en service).
- **Périmètre** : `docs/reecriture/rapports/spike-desktop.md` ; le code d'essai n'est pas
  conservé.
- **Conception** : sur macOS arm64, Photino.NET 4.0.16 / Photino.Native 4.0.22 puis
  Avalonia 12 + NativeWebView, chacun chargeant le build `shell` servi par un Kestrel
  loopback : rendu, `fetch`/XHR, messages du pont, enregistrement de fichier, liens
  externes, cycle de vie de la fenêtre. Ce qui ne se vérifie pas ici (WebView2 sous
  Windows) est noté comme risque.
- **Acceptation** : rapport avec la décision (Photino confirmé, ou bascule vers le plan B et
  ses conséquences sur L9.2).
- **Dépend de** : fondations du lot 3. **Taille** : S.

## L9.2 — Hôte desktop

- **Objectif** : l'hôte `LoDb.Desktop`, qui sert le front en loopback, relaie l'API et garde
  les jetons hors de portée du JavaScript.
- **À lire** : ADR 0007 (architecture de `LoDb.Desktop`), ADR 0009 (jetons, OAuth desktop),
  §5.2 du plan (contrat de l'hôte desktop), le code desktop de GitHealth.
- **Périmètre** : `src/LoDb.Desktop/**` (hors `Updates/`), `tests/LoDb.Desktop.Tests/**`.
- **Conception** :
  - `Main` en `[STAThread]`, `VelopackApp.Build().Run()` en toute première instruction.
  - Drapeau `--smoke` : démarre l'hôte sans fenêtre, affiche la version et l'état du proxy,
    puis sort (vérifications automatiques, critère de sortie).
  - Kestrel sur `127.0.0.1`, port aléatoire : bundle `shell` (avec
    `window.__LODB_DESKTOP__` injecté dans l'`index.html` servi), endpoints locaux
    `/desktop/auth/*` du contrat, proxy YARP `/api/**` vers l'API distante (URL par canal)
    qui ajoute `Authorization` et `X-LoDb-Client: desktop/{version}`.
  - Jetons : `/desktop/auth/login` appelle `/api/account/token` et garde jeton d'accès et
    refresh côté hôte (refresh chiffré par Data Protection locale) ; le JavaScript ne voit
    jamais un jeton. Google : navigateur système + PKCE + redirection loopback (RFC 8252) +
    échange par l'API.
  - `IDesktopShell` (fenêtre, messages, dialogues) ; Photino n'est référencé que par son
    implémentation ; aucune dépendance aux menus natifs, à la zone de notification ni au
    multi-fenêtre. Pas de schéma d'URL personnalisé.
  - `DesktopBridge` selon le contrat du plan : `openExternal` (http(s) seulement,
    navigateur système), `saveFile` (nom sans séparateur de chemin, dialogue natif),
    `updateState`, `applyUpdate` ; toute entrée est validée.
  - Repli : si la WebView ne démarre pas, ouverture du navigateur système sur l'URL
    loopback. Dossier de données distinct du dossier d'installation.
- **Tests** : validation du pont (URLs, noms de fichier) ; en-têtes ajoutés par le proxy ;
  endpoints `/desktop/auth/*` ; chiffrement du refresh ; callback OAuth loopback ;
  `--smoke`.
- **Dépend de** : L9.1, L4.2. **Taille** : L.

## L9.3 — Plateforme desktop côté front

- **Objectif** : l'implémentation `desktop` de `PlatformService` et la stratégie
  d'authentification `host`.
- **À lire** : ADR [0006](../adr/0006-front-angular-multi-cibles.md) (couche plateforme),
  §5.2 du plan (contrat de l'hôte desktop), `core/auth/` (L4.5).
- **Périmètre** : `src/LoDb.Web/src/app/core/platform/desktop/**`.
- **Conception** : détection par `window.__LODB_DESKTOP__` ; origine de l'API = même
  origine (proxy loopback) ; client du pont (requêtes corrélées par id, délai, réponses
  traitées comme non fiables) ; liens externes, enregistrement de fichier (téléchargement →
  base64 → pont), partage par copie du lien, état des mises à jour vers `core/update/`,
  en-tête client ; stratégie `host` de `AuthStrategy` (connexion et déconnexion par
  `/desktop/auth/*`).
- **Tests** : corrélation et délai du pont, détection, stratégie `host`.
- **Dépend de** : L9.0, L9.1, L4.5. **Taille** : M.

## L9.4 — Mises à jour Velopack et release desktop

- **Objectif** : des mises à jour silencieuses, signées, avec deltas, testées de bout en
  bout avant toute publication — ce que GitHealth n'a jamais fait.
- **À lire** : ADR 0008 (desktop), le workflow de release de GitHealth.
- **Périmètre** : `src/LoDb.Desktop/Updates/**`, `.github/workflows/next-release-desktop.yml`,
  `tools/next/desktop-update-e2e/**`, `docs/guides/release-desktop.md`.
- **Conception** :
  - `GithubSource` sur le dépôt public ; canaux par RID (`win-x64`, `osx-arm64`, `osx-x64`,
    `linux-x64`), suffixe `-beta` pour le staging ; vérification au démarrage puis toutes les
    6 h ; téléchargement en arrière-plan ; application au clic ou à la fermeture
    (`WaitExitThenApplyUpdates`) ; drapeau caché `--apply-updates` pour l'E2E.
  - Workflow : déclenché après le déploiement de prod sur le SHA exact ; matrice des RID ;
    `vpk download` de N-1 **avant** `vpk pack` (deltas réels) ; signature Authenticode
    (Azure Artifact Signing) et Developer ID + notarisation ; **porte E2E** avant publication
    (installation de N-1, mise à jour vers N, `--smoke` qui doit afficher N) ; release en
    *pre-release* puis promue quand tous les RID sont en ligne ; échec si le tag et
    `VersionPrefix` divergent.
  - Le même E2E tourne en local, non signé, sur macOS arm64 (source locale Velopack).
  - Guide : clés, abonnements, sauvegarde hors ligne des certificats, procédure de rotation.
- **Tests** : `actionlint` ; E2E local de mise à jour.
- **Dépend de** : L9.2. **Taille** : L.

## L10.1 — Projet Capacitor Android

- **Objectif** : l'app Android qui embarque le build `shell` et démarre sans réseau.
- **À lire** : ADR 0007 (Android), le projet Capacitor de Bloodborne Legendary Run,
  `app/public/.well-known/assetlinks.json` (gabarit actuel),
  [`packaging-apk.md`](../../../legacy/docs/guides/packaging-apk.md) (remplacé).
- **Périmètre** : `src/LoDb.Web/android/**`, `src/LoDb.Web/capacitor.config.ts`,
  environnements `shell` (dont la variante store), `docker/next/android-build/**`,
  `tools/next/android/**`, `assetlinks.json` servi par nginx (fichier à part).
- **Conception** : Capacitor **≥ 8.5.1** (avis GHSA-rvm3-566m-v7fv) ; bundle servi depuis
  `https://localhost`, jamais de `server.url` distant ; `allowNavigation` vide ; `minSdk 24`,
  `targetSdk 36` ; plugins `@capacitor/app`, `@capacitor/browser`, `@capacitor/share`, un
  stockage sécurisé adossé à l'Android Keystore (maintenu, compatible Capacitor 8) ;
  variante store sans paiements par **drapeau de build du front** (l'API reste active pour
  le web) ; App Links vérifiés pour le retour OAuth, `assetlinks.json` avec l'empreinte du
  certificat en paramètre ; build de debug en conteneur **`linux/amd64`** (émulation
  Rosetta de Docker Desktop : `aapt2` n'existe qu'en x86-64 sous Linux), avec JDK 21, SDK
  Android 36 et Node 24.
- **Tests** : build de debug réussi ; `https://localhost` accepté par le CORS de l'API ;
  aucun écran de paiement dans la variante store.
- **Dépend de** : fondations du lot 3. **Taille** : M.

## L10.2 — Plateforme Android côté front

- **Objectif** : l'implémentation `android` de `PlatformService` et la stratégie
  d'authentification `bearer`.
- **Périmètre** : `src/LoDb.Web/src/app/core/platform/android/**` (hors `updates/`).
- **Conception** : origine de l'API = origine publique de l'environnement `shell` ; liens
  externes par le navigateur système, partage natif, cycle de vie (reprise au premier
  plan), bouton retour ; stratégie `bearer` de `AuthStrategy` (`/api/account/token`,
  refresh dans le stockage sécurisé, jeton d'accès en mémoire, en-tête `Authorization`) ;
  OAuth par le navigateur système + PKCE + App Link.
- **Tests** : service de plateforme avec plugins simulés ; renouvellement des jetons.
- **Dépend de** : L10.1, L4.5. **Taille** : M.

## L10.3 — Live update signé et Play In-App Updates

- **Objectif** : le front se met à jour seul, sans passer par le store, avec retour arrière
  automatique ; le binaire natif passe par Play.
- **À lire** : ADR 0008 (Android, niveaux 1 et 2, canal transitoire).
- **Périmètre** : `src/LoDb.Web/src/app/core/platform/android/updates/**`,
  `tools/next/live-update/**` (signature des bundles), tests associés.
- **Conception** :
  - `@capawesome/capacitor-live-update` avec bundles auto-hébergés (zip du build `shell`
    en asset de GitHub Release), **signés RSA** et vérifiés par SHA-256 ; `ready()` appelé
    après un démarrage réussi, sinon retour arrière automatique ; version native minimale
    par bundle ; vérification au démarrage et au retour au premier plan, au plus toutes les
    15 minutes ; téléchargement en arrière-plan, application au prochain démarrage à froid.
  - `@capawesome/capacitor-app-update` : flux *flexible* par défaut, *immediate* sous le
    minimum de la politique client.
  - Canal transitoire derrière un drapeau (désactivé une fois Play en service) :
    `latest.json` + APK ouvert dans le navigateur système, SHA-256 vérifié, comparaison de
    `versionCode`.
  - Outil de signature des bundles (clé depuis l'environnement, jamais dans le dépôt).
- **Tests** : décision de mise à jour en fonction pure (version native, bundle, politique) ;
  vérification de signature avec une clé de test ; scénario de bundle défectueux → retour
  arrière, au niveau de la logique (le test sur émulateur est la porte de L10.4).
- **Dépend de** : L9.0, L10.2. **Taille** : L.

## L10.4 — Build et release Android

- **Objectif** : AAB et bundle de live update produits par la CI après le déploiement de
  prod, sur le SHA exact, après une porte E2E sur émulateur.
- **Périmètre** : `.github/workflows/next-release-android.yml`, `docs/guides/release-android.md`.
- **Conception** : build `shell` store, `cap sync`, AAB signé avec la clé d'envoi (secret),
  bundle de live update signé ; **porte E2E sur émulateur** avant publication (application
  installée, bundle défectueux appliqué, retour arrière vérifié, puis bundle sain) ; assets
  de release ; envoi sur la piste de test interne de Play ; guide : clé d'envoi, Play App
  Signing avec import de la clé du canal transitoire, clé RSA des bundles, sauvegarde hors
  ligne, vérification développeur avant 2027.
- **Tests** : `actionlint` ; signature de release vérifiée en local avec un keystore jetable.
- **Dépend de** : L10.3. **Taille** : M.
