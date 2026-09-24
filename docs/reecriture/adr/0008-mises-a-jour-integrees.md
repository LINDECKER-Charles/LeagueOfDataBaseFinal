# ADR 0008 — Mises à jour intégrées aux apps (desktop et Android)

- **Statut** : acceptée — 2026-09-24

## Contexte

Les apps doivent se mettre à jour **depuis l'app elle-même**, sans que l'utilisateur
aille chercher un installeur. Deux projets servent de référence :

- **GitHealth** : Velopack sur GitHub Releases. Un bouton « Mettre à jour » apparaît au
  démarrage, puis l'app télécharge, applique et redémarre. Les lacunes relevées, à ne
  pas reproduire :
  - aucune signature ni notarisation (dette « M3 » de son audit de sécurité) ;
  - deltas promis mais jamais produits en CI ;
  - mise à jour de bout en bout jamais testée ;
  - version du tag et `VersionPrefix` qui peuvent diverger sans alerte.
- **Bloodborne Legendary Run** : un `latest.json` auto-hébergé et un APK complet ouvert
  dans le navigateur système ; l'utilisateur confirme l'installation. Ce projet apporte
  des enseignements utiles :
  - clés de signature distinctes entre staging et prod ;
  - APK construit après le déploiement, sur le SHA exact ;
  - contrats d'API maintenus pour les anciens clients.

Contraintes Android (relevées le 2026-09-24) :

- **Play interdit à une app de se mettre à jour hors de Play**, sauf pour le code
  interprété dans une WebView (JavaScript, HTML, CSS), qui est explicitement autorisé.
- `REQUEST_INSTALL_PACKAGES` est interdit pour s'auto-mettre à jour depuis Play.
- L'API **Play In-App Updates** ne fonctionne que pour les installations venues de Play.
- **Ionic Appflow est en fin de vie** (arrêt annoncé le 2025-02-11, fin le 2027-12-31).
- La **vérification des développeurs Android** s'applique hors Play : dès le 2026-09-30
  dans 4 pays, partout en 2027.

## Décision

### Desktop : Velopack, en corrigeant les lacunes de GitHealth

| Aspect | Décision |
|---|---|
| Source | **GitHub Releases** du dépôt (public), lue par `GithubSource` |
| Canaux | un par RID (`win-x64`, `osx-arm64`, `osx-x64`, `linux-x64`) ; suffixe `-beta` alimenté par le staging |
| Vérification | au démarrage **et toutes les 6 h** en tâche de fond |
| Téléchargement | **en arrière-plan**, sans bloquer ; bandeau « Mise à jour prête — redémarrer » |
| Application | au clic, ou **automatiquement à la fermeture** (`WaitExitThenApplyUpdates`) |
| Deltas | la CI récupère la release N-1 (`vpk download`) **avant** `vpk pack` : les deltas sont réellement produits |
| Signature Windows | Authenticode via Azure Artifact Signing (`--azureTrustedSignFile`) |
| Signature macOS | Developer ID + **notarisation** (`--signAppIdentity`, `--notaryProfile`) |
| Version | une seule source : le tag. La CI échoue si le tag et `VersionPrefix` divergent |
| Publication | release publiée d'abord en *pre-release*, promue une fois tous les RID uploadés (sinon `/releases/latest` pointe un temps sur une release vide) |

**La signature est un prérequis de l'ouverture publique des mises à jour
automatiques** : un canal qui télécharge et exécute sans intervention ne se publie pas
non signé. Coût : Apple Developer Program (99 $ par an) et un abonnement Azure Artifact
Signing.

### Android : deux niveaux, sans sideload à terme

**Niveau 1 — le front se met à jour seul (live update).**

- Plugin **`@capawesome/capacitor-live-update`** (MIT), avec des bundles
  **auto-hébergés** : zip du build `shell`, en asset de GitHub Release.
- **Bundles signés RSA** et vérifiés par SHA-256. **Retour arrière automatique** si
  l'app n'appelle pas `ready()` dans le délai imparti.
- Chaque bundle déclare la **version native minimale** qu'il exige : jamais de bundle
  sur une coquille trop ancienne.
- Vérification au démarrage et au retour au premier plan, au plus toutes les 15 minutes
  (repris de Bloodborne). Téléchargement en arrière-plan, **application au prochain
  démarrage à froid**, jamais en pleine session.
- C'est ce niveau qui livre l'essentiel des évolutions, sans passer par le store.

**Niveau 2 — le binaire natif passe par Google Play.**

- **Play In-App Updates** via `@capawesome/capacitor-app-update` :
  - flux *flexible* par défaut ;
  - flux *immediate* quand la version native installée est sous le minimum publié par
    l'API (correctif de sécurité Capacitor, montée annuelle de `targetSdk`, nouveau
    plugin).
- Les testeurs du staging passent par la **piste de test interne** de Play : mises à
  jour gérées par Play, et plus de clés de signature séparées entre staging et prod.

**Canal transitoire (avant l'ouverture de la fiche Play).**

- Pattern Bloodborne : `latest.json` + APK ouvert dans le navigateur système, SHA-256
  vérifié, comparaison de `versionCode`.
- Signé avec **la clé qui deviendra la clé de signature Play** : à l'enrôlement dans
  Play App Signing, on importe cette clé plutôt que d'en laisser générer une. Sans
  cela, les utilisateurs devraient désinstaller pour passer sur Play.
- Enregistrer l'identité développeur avant 2027.
- Canal **désactivé** une fois Play en service.

### Commun aux deux plateformes : compatibilité et politique client

- Chaque requête d'une app porte `X-LoDb-Client: {plateforme}/{version}`.
- `GET /api/client-policy` renvoie, par plateforme : la version minimale, la dernière
  version, et, pour Android, le bundle courant (URL, signature, version native minimale).
  **C'est l'API qui décide** : un bundle défectueux se retire en republiant la politique,
  sans nouvelle release.
- En dessous du minimum, l'API répond **`426 Upgrade Required`**. L'app affiche alors un
  écran de mise à jour bloquant : Velopack sur desktop, flux *immediate* ou live update
  sur Android.
- L'API de l'app évolue **par ajouts**. Une rupture ouvre une nouvelle version de route
  et l'ancienne vit jusqu'à ce que la politique client ait fait monter tout le monde.
- Les apps sont construites **après le déploiement de l'API, sur le SHA exact**.
- **Clés de signature** : certificats, clé RSA des bundles, clé de téléversement Play.
  Elles vivent dans les environnements GitHub et **ont une sauvegarde hors ligne**. Perdre
  la clé des bundles ou la clé Play coupe les mises à jour des installations existantes.
- **Test de bout en bout en CI** avant toute publication : installer N-1, mettre à jour
  vers N, vérifier le démarrage. C'est ce que GitHealth n'a jamais fait.

## Alternatives écartées

| Alternative | Pourquoi non |
|---|---|
| APK auto-hébergé comme canal définitif | Pas de flux natif de Play ; friction de la vérification développeur hors Play ; installation toujours confirmée à la main |
| `REQUEST_INSTALL_PACKAGES` + installation dans l'app | Interdit pour les apps publiées sur Play |
| Ionic Appflow / `@capacitor/live-updates` | En fin de vie (31/12/2027) |
| Capgo | Alternative crédible (plugin MPL, backend AGPL auto-hébergeable), mais un backend de plus à exploiter pour un besoin que GitHub Releases + `/api/client-policy` couvrent |
| Updater Tauri | Desktop uniquement, sans deltas (cf. ADR 0007) |

## Conséquences

- **+** Les évolutions du front arrivent sur Android sans passer par le store, et les
  correctifs natifs sont imposés quand la sécurité l'exige.
- **+** Le desktop se met à jour en silence, signé, avec deltas.
- **+** Un seul point de contrôle (`/api/client-policy`) pour forcer, suspendre ou
  retirer une version.
- **−** Coûts récurrents : Apple Developer (99 $/an), signature Windows, compte Play
  Console (25 $ une fois).
- **−** Discipline de compatibilité d'API à tenir sur la durée de vie des installations.
