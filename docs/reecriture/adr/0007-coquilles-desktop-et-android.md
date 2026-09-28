# ADR 0007 — Coquilles desktop (Photino.NET) et Android (Capacitor 8)

- **Statut** : acceptée — 2026-09-24
- **Remplace** : l'APK TWA prévu (jamais finalisé : `assetlinks.json` contient encore des
  placeholders), cf. [`packaging-apk.md`](../../guides/packaging-apk.md)

## Contexte

Cibles : **Windows et macOS** (Linux en bonus) et **Android**. iOS est hors périmètre,
mais on ne doit pas s'en fermer la porte. Les deux coquilles embarquent le build
`shell` du front ([ADR 0006](0006-front-angular-multi-cibles.md)) et parlent à l'API
distante.

Chaque coquille doit aussi pouvoir **se mettre à jour elle-même**
([ADR 0008](0008-mises-a-jour-integrees.md)).

## Décision desktop : Photino.NET, derrière une abstraction

### Comparatif (relevé le 2026-09-24)

| Critère | **Photino.NET 4 + Velopack** | Tauri 2 | Avalonia 12 + NativeWebView |
|---|---|---|---|
| Langage de l'hôte | **C#, dans le processus** | Rust ; .NET en *sidecar* | C#, dans le processus |
| Réutilise le front Angular | ✅ | ✅ | ✅ |
| Mises à jour desktop | Velopack : deltas, signature Windows, notarisation macOS | plugin updater : **pas de deltas officiels**, clé minisign obligatoire | Velopack |
| Mobile | ❌ | ✅, mais **updater desktop seulement** et **sidecars impossibles sur mobile** | ✅ (WebView open source depuis mars 2026) |
| Déjà livré par nous | ✅ GitHealth | ❌ | ❌ |
| Maintenance | ⚠️ aucun commit de code depuis janvier 2025, issue « Is this project dead? » sans réponse | très active, **Tauri 3 en alpha** | active, WebView jeune (~130 ★) |
| Poids | runtime .NET + WebView système | minime… jusqu'à l'ajout du sidecar .NET | runtime .NET + Skia |

**Photino.NET 4 + Velopack l'emporte.** C'est la seule option qui garde .NET dans le
processus sans couche Rust, c'est déjà éprouvé dans GitHealth, et Velopack couvre
signature, notarisation et deltas.

Tauri est écarté pour plusieurs raisons :

- ses deux atouts ne servent pas ici : son updater ne marche pas sur mobile, et un
  sidecar .NET ne tourne pas sur Android, donc l'app Android serait de toute façon un
  client de l'API ;
- côté desktop, il faudrait superviser un processus .NET par triple de cible, et le
  signer et le notariser à part ;
- il n'y a pas de deltas officiels ;
- une migration vers Tauri 3 se profile.

### Architecture de `LoDb.Desktop` (modèle GitHealth)

- `VelopackApp.Build().Run()` en **toute première instruction** ; `Main` en
  `[STAThread]` (sans ça, WebView2 ne rend rien, sans erreur).
- **Kestrel sur `127.0.0.1`, port aléatoire**. Il sert :
  - le bundle `shell` ;
  - les endpoints locaux de mise à jour ;
  - un **proxy `/api` → API distante** (YARP). Ce proxy :
    - rend l'origine identique pour la WebView, donc pas de CORS sur un port aléatoire ;
    - ajoute le jeton d'accès, que l'hôte détient et que le JavaScript ne voit jamais ;
    - servira plus tard de point d'accroche pour un cache hors ligne.
- Photino charge l'URL loopback. **Pas de schéma personnalisé** : Photino #232 bloque
  `fetch`/XHR dessus.
- **Pont `DesktopBridge`** limité au strict nécessaire : ouvrir un lien externe
  (http(s) seulement), enregistrer un fichier (sans séparateur de chemin), état des mises
  à jour. Sous macOS, `target="_blank"` et `<a download>` ne font rien sous Photino :
  ils passent donc par le pont.
- Repli : si la WebView ne démarre pas, l'app ouvre le navigateur système sur l'URL
  loopback.
- Dossier d'installation ≠ dossier de données (`--packId` distinct) : une mise à jour ou
  une désinstallation ne touche jamais les données.

### Garde-fous contre le risque de maintenance de Photino

1. **`IDesktopShell`** (fenêtre, messages, dialogues) : Photino n'est référencé que
   par son implémentation.
2. Versions **épinglées** (Photino.NET 4.0.16, Photino.Native 4.0.22). Le code est sous
   Apache-2.0 et la couche native est petite : **forkable**.
3. Aucune dépendance aux menus natifs, à la zone de notification ni au multi-fenêtre
   (absents ou défaillants chez Photino).
4. **Plan B désigné : Avalonia 12 + NativeWebView (MIT).** Même modèle, .NET dans le
   processus, Velopack inchangé ; seule l'implémentation d'`IDesktopShell` change.
   Déclencheurs :
   - un bug bloquant sans correctif en amont ;
   - une incompatibilité avec une version de WebView2, WKWebView ou macOS ;
   - une faille de sécurité non corrigée.

   Une vérification de deux jours (spike) est **prévue au démarrage du lot desktop**
   pour reconfirmer le choix sur l'état du moment.
5. Linux : AppImage Velopack en best effort, non bloquant.

## Décision Android : Capacitor 8, sans Ionic UI

- **Bundle embarqué** dans l'APK/AAB, servi depuis `https://localhost`. Pas de
  `server.url` distant : l'app démarre sans réseau et ne dépend pas de la prod pour
  s'afficher.
- L'API est appelée en absolu (URL par environnement). `https://localhost` est dans les
  origines CORS de `LoDb.Api`. `allowNavigation` reste **vide** ; les liens externes
  passent par le navigateur système.
- Plugins :
  - `@capacitor/app` (cycle de vie, version native) ;
  - `@capacitor/browser` ;
  - `@capacitor/share` ;
  - stockage sécurisé du jeton de rafraîchissement (Android Keystore) ;
  - mises à jour (ADR 0008).
- **Capacitor ≥ 8.5.1** obligatoire : l'avis GHSA-rvm3-566m-v7fv (critique, 2026-08-31)
  permettait à du contenu distant de se charger avec l'origine de l'app.
- `minSdk 24`, **`targetSdk 36`** (exigé par Play depuis le 2026-08-31).
- **Paiements absents du build store.** Les crédits API sont des biens numériques, que
  Play oblige à vendre via Play Billing. Les dons et le checkout Stripe sont donc
  désactivés par un drapeau de build, comme dans Bloodborne.

### Ionic UI écarté (Capacitor conservé)

Ionic Framework apporte son propre langage visuel. Il faudrait le re-thématiser
entièrement en Hextech, alors que le design system couvre déjà les usages mobiles.
Capacitor ne dépend pas d'Ionic UI : la décision est **réversible** composant par
composant si un besoin natif précis apparaît (geste de retour, transitions de pages).

### TWA écartée

Une TWA affiche la prod en ligne :

- pas de hors ligne ;
- pas de bundle propre, ni de mise à jour contrôlée ;
- elle dépend de la disponibilité du site ;
- elle ne partage pas la coquille du desktop.

## Conséquences

- **+** Un front unique, deux coquilles minces ; l'hôte desktop réutilise les DTO et le
  client HTTP .NET.
- **+** Le desktop n'expose aucun jeton au JavaScript (proxy loopback).
- **−** Dépendance à un projet peu maintenu (Photino), contenue par `IDesktopShell` et le
  plan B.
- **−** L'API doit rester compatible avec les versions installées : les clients survivent
  aux déploiements (règles dans l'ADR 0008).
