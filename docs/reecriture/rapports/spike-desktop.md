# Spike desktop : Photino.NET ou Avalonia (L9.1)

- **Date** : 2026-09-26.
- **Objet** : reconfirmer le choix de l'[ADR 0007](../adr/0007-coquilles-desktop-et-android.md)
  (Photino.NET derrière `IDesktopShell`, plan B Avalonia 12 + NativeWebView) sur l'état du
  moment, avant tout code desktop livré
  ([L9.1](../implementation/lot-09-10-apps.md#l91--spike-photino--avalonia)).
- **Décision** : **Photino confirmé** (Photino.NET 4.0.16 / Photino.Native 4.0.22). Aucun
  déclencheur du plan B n'est atteint. Le plan B reste praticable : il a passé le même
  scénario sur macOS arm64. Six règles de conception en découlent pour L9.2 (§ 6).

## 1. Environnement

| Élément | Valeur |
|---|---|
| Poste | macOS 27.0 (26A428), arm64 |
| .NET | SDK 10.0.400, cible `net10.0` |
| Front | build `shell` du worktree (commit `3f953e1`) : `npm ci --prefix src/LoDb.Web`, puis `npm --prefix src/LoDb.Web run build:shell` (exit 0 ; avertissement de budget déjà connu, 575 kB / 500 kB) |
| Photino | Photino.NET 4.0.16 (lib `net9.0`), Photino.Native 4.0.22 (`runtimes/osx-arm64/native/Photino.Native.dylib`) |
| Plan B | Avalonia 12.1.3, Avalonia.Desktop 12.1.3, Avalonia.Themes.Fluent 12.1.3, Avalonia.Controls.WebView 12.1.0 (MIT) |

Le code desktop de GitHealth (`App.GitHealth/src/App.GitHealth.*`) n'a pas pu être lu : le
système refuse l'accès au dossier (`Operation not permitted`). Le spike s'appuie donc sur
l'ADR 0007 et sur les paquets eux-mêmes (API relevée par réflexion, plus une décompilation
d'`Avalonia.Controls.WebView` pour trouver son pont JavaScript).

## 2. Protocole

Les deux essais partagent le même hôte jetable :

- un Kestrel sur `127.0.0.1`, port aléatoire ;
- il sert le build `shell` (fichiers statiques et repli SPA vers `index.html`) ;
- il répond `503` sur `/api/**` (aucune API distante), et expose des points de sonde
  `/spike/*` ;
- il injecte dans le `<head>` de l'`index.html` servi `window.__LODB_DESKTOP__` et une sonde
  JavaScript.

La fenêtre charge `http://127.0.0.1:{port}/en/`, puis un scénario scripté s'exécute sans
intervention :

1. rendu ;
2. `fetch` GET et POST, XHR ;
3. messages du pont dans les deux sens, dont un message de 1 Mo (taille d'un `saveFile`) ;
4. `window.open`, `target="_blank"`, `<a download>` ;
5. navigateur système ;
6. dialogue d'enregistrement ;
7. réduction, restauration, agrandissement ;
8. fermeture annulée puis acceptée ;
9. fin du processus.

Un chien de garde tue le processus à 150 s.

Le rendu est prouvé par le DOM, pas par l'image :

- `lodb-root` porte l'en-tête et le pied de page d'Angular ;
- le titre, `lang` et `data-theme` sont posés ;
- polices, chunks paresseux et catalogues i18n sont demandés à Kestrel.

Une capture d'écran a été tentée puis abandonnée et supprimée : elle saisissait tout le
bureau de l'hôte, sans garantie que la fenêtre soit au premier plan.

Le dialogue natif est prouvé ouvert en lisant la session modale de `NSApplication` (Photino)
ou la feuille attachée à la fenêtre (Avalonia), puis il est refermé par code (`abortModal`,
`cancel:`). La frappe clavier par `osascript` est refusée faute de droit d'accessibilité
(`-1743`).

Le code d'essai a vécu dans `/tmp/lodb-desktop-spike/` et n'est pas conservé.

## 3. Résultats

| Critère | Photino 4.0.16 / 4.0.22 | Avalonia 12.1 + NativeWebView |
|---|---|---|
| Démarrage de la WebView | fenêtre créée en ~0,4 s, page servie en ~0,6 s | fenêtre ouverte en ~0,5 s, adaptateur `WkWebView` |
| Rendu du bundle `shell` | ✅ Angular amorcé : en-tête, navigation, pied de page ; `lang=en`, `data-theme=hextech` | ✅ identique |
| `fetch` GET / POST, XHR | ✅ 200, corps POST reçu intact | ✅ idem |
| `fetch` vers `/api/**` | ✅ même origine, réponse `503` relayée | ✅ idem |
| Cookies, `localStorage` | ✅ en session | ✅ |
| `window.__LODB_DESKTOP__` injecté | ✅ lu avant l'amorçage d'Angular | ✅ |
| Pont JS → hôte | `window.external.sendMessage(string)`, posé dès le début du document | fonction globale `invokeCSharpAction` posée **après** la fin de navigation, ou `window.webkit.messageHandlers.postAvWebViewMessage.postMessage` (macOS seulement) |
| Pont hôte → JS | `SendWebMessage` → `window.external.receiveMessage(cb)` | pas d'API de message : `InvokeScript("…")` |
| Aller-retour du pont | 2,1 ms ; 1 Mo en 29 ms | 2,0 ms ; 1 Mo en 7–8 ms |
| `window.open` | ne fait rien (`null`) | ne fait rien (`null`), sans événement |
| `target="_blank"` | ne fait rien, aucun événement | événement `NewWindowRequested`, interceptable |
| `<a download>` (blob) | ne fait rien | **la WebView navigue vers l'URL `blob:`** : l'app est remplacée ; à annuler dans `NavigationStarted` |
| Navigateur système (`Process.Start`, `UseShellExecute`) | ✅ le navigateur par défaut a chargé l'URL loopback | ✅ même mécanisme, indépendant de la coquille |
| Dialogue d'enregistrement | ✅ `NSSavePanel` en session modale ; `ShowSaveFile` rend `null` à l'annulation | ✅ `NSSavePanel` en feuille ; `SaveFilePickerAsync` rend `null` à l'annulation |
| Réduire / restaurer | ✅ événements `Minimized`, `Restored` | ✅ `WindowState` |
| Agrandir | ⚠️ `SetMaximized(true)` sans événement `Maximized` | ✅ |
| Annuler la fermeture | ❌ ignorée (voir § 4.1) | ✅ `Closing.Cancel` respecté |
| Fin du processus | ⚠️ `exit(0)` natif à la fermeture : `WaitForClose` ne rend jamais la main, `ProcessExit` n'est pas levé | ✅ `StartWithClassicDesktopLifetime` rend 0 ; l'arrêt de Kestrel doit être borné (§ 4.3) |
| Publication autonome `osx-arm64` (hôte web compris, sans trimming) | 111 Mo | 140 Mo |

Aucune vérification bloquante n'échoue pour Photino.

## 4. Constats

### 4.1 Photino : la fermeture n'est pas annulable sous macOS

Deux fermetures ont été testées : `Close()` et `performClose:`, le chemin du bouton rouge.
Dans les deux cas, le gestionnaire `WindowClosing` est appelé, mais sa valeur de retour
(`true` = annuler) est ignorée. Le processus se termine alors aussitôt par l'application
Cocoa :

- le code qui suit `WaitForClose()` ne s'exécute pas ;
- `AppDomain.ProcessExit` n'est pas levé ;
- le code de sortie est 0.

Notre conception ne demande pas d'annuler une fermeture : pas de « Voulez-vous vraiment
quitter ? ». Le constat n'est donc pas bloquant. En revanche, **tout le travail de fin**
(arrêt de Kestrel, écriture de l'état, et plus tard
`WaitExitThenApplyUpdates` de L9.4) doit se faire **dans le gestionnaire de fermeture**,
jamais après `WaitForClose`.

### 4.2 Port aléatoire : les cookies survivent, `localStorage` non

Deux lancements successifs ont tourné sur deux ports différents :

- un cookie persistant (`max-age`) posé au premier est relu au second : les cookies sont
  liés à l'hôte, pas au port ;
- `localStorage` est vide au second : il est lié à l'origine, port compris.

Le front range déjà le thème (`lod_theme`) et les préférences (`lod_prefs`) en cookies :
le port aléatoire de l'ADR 0007 tient. Le front desktop (L9.3) ne doit rien persister dans
`localStorage` ni IndexedDB.

### 4.3 Arrêt de Kestrel

Après la fermeture de la fenêtre Avalonia, un `StopAsync()` sans délai reste bloqué : les
connexions keep-alive de la WebView le retiennent jusqu'au délai d'arrêt de l'hôte. Un arrêt
borné (5 s dans l'essai) rend la main. L'hôte réel borne l'arrêt à quelques secondes, quelle
que soit la coquille.

### 4.4 Divers

- Sous macOS, `ShowSaveFile` de Photino ne pré-remplit pas le nom : `defaultPath` ne fixe
  que le dossier, et le champ garde `Untitled`. L'hôte passe un filtre tiré de l'extension
  du nom demandé, pour que le type du fichier survive au nom tapé par l'utilisateur.
- Photino journalise par défaut chaque appel sur la sortie standard, y compris le contenu
  de `SendWebMessage`. L'hôte le fait taire (`SetLogVerbosity(0)`).
- Au démarrage, Photino écrit sur la sortie d'erreur :
  `NSMapGet: map table argument is NULL`. C'est sans effet observé.
- L'agent utilisateur de Photino est `Photino WebView`. Celui d'Avalonia est celui de
  WebKit.
- Le code d'essai a d'abord renvoyé un corps POST vide. C'était une erreur de l'essai, pas
  de WKWebView : `curl` donnait le même résultat, et la version corrigée reçoit le corps.
- Le pont d'Avalonia n'est pas le même selon la plateforme : `invokeCSharpAction` injectée
  après la navigation, `window.chrome.webview` sous Windows. Une bascule vers le plan B
  changerait donc aussi le transport JavaScript. C'est pourquoi L9.2 injecte son propre
  transport (§ 6, règle 2).

## 5. État amont (relevé le 2026-09-26)

| Projet | Dernière version | Dernier commit de code | Autres signaux |
|---|---|---|---|
| Photino.NET | 4.0.16 (2025-01-23) | 2025-01-23 | 44 tickets ouverts ; README mis à jour le 2026-03-26 pour annoncer une maintenance « assistée par agents IA » (tri et relecture des PR) ; aucun avis de sécurité dans la base GitHub |
| Photino.Native | 4.0.22 (2025-01-23) | 2025-01-23 | 28 tickets ouverts ; ticket #197 « Retrieve current Url » toujours ouvert |
| Avalonia.Controls.WebView | 12.1.0 (2026-08-15) | 2026-09-17 | ~130 étoiles, 27 tickets ouverts, développement actif ; aucun avis de sécurité |

Le risque de maintenance décrit par l'ADR est inchangé : aucune nouvelle version en 20 mois.
Aucun des déclencheurs du plan B n'est atteint :

- pas de bug bloquant (§ 4.1 se contourne) ;
- pas d'incompatibilité avec macOS 27 ni avec le WKWebView du moment ;
- pas de faille connue.

Photino reste épinglé (4.0.16 / 4.0.22, déjà dans `Directory.Packages.props`).

## 6. Décision et conséquences pour L9.2

**Photino.NET est confirmé.** Les garde-fous de l'ADR sont conservés :

- `IDesktopShell` ;
- versions épinglées ;
- aucun menu natif, aucune zone de notification, aucun multi-fenêtre.

Le spike ajoute six règles à la conception de L9.2 :

1. **Fin de vie dans le gestionnaire de fermeture.** `IDesktopShell` rappelle l'hôte à la
   fermeture ; l'hôte y fait tout son travail de fin, borné dans le temps. Aucun code
   utile après la boucle de la fenêtre (§ 4.1).
2. **Transport du pont injecté par l'hôte.** L'`index.html` servi définit
   `window.__LODB_DESKTOP__ = {version, bridge?}`. `bridge` expose `send(message)` et
   `listen(listener)` au-dessus de l'API de la coquille : `window.external` pour Photino.
   Le front (L9.3) ne connaît que ce transport, donc une bascule vers Avalonia ne change que
   l'implémentation d'`IDesktopShell` et la ligne de transport qu'elle fournit.
3. **Liens externes et téléchargements par le pont.** `window.open`, `target="_blank"` et
   `<a download>` ne font rien sous Photino (et `<a download>` remplacerait l'app sous
   Avalonia) : `openExternal` et `saveFile` sont les seuls chemins.
4. **Aucune persistance dans `localStorage` côté desktop.** L'origine change à chaque
   lancement (§ 4.2) ; les cookies persistants restent le support des préférences.
5. **Arrêt de Kestrel borné** à quelques secondes (§ 4.3).
6. **Dossier de données explicite.** Le dossier de données de WebView2 est passé par
   `SetTemporaryFilesPath` : il vit dans le dossier de données de l'app, jamais dans le
   dossier d'installation que Velopack remplace, ni dans un dossier partagé entre apps
   Photino.

Si le plan B devait être activé plus tard, voici ce qu'il coûterait à L9.2, relevé par
l'essai :

- une implémentation `AvaloniaShell` d'`IDesktopShell` :
  - boucle `StartWithClassicDesktopLifetime` ;
  - transport JavaScript sur `invokeCSharpAction` et `InvokeScript` ;
  - annulation des navigations `blob:` ;
  - interception de `NewWindowRequested` ;
- environ 30 Mo de plus à la publication ;
- rien d'autre : Kestrel, proxy, jetons, pont, Velopack et contrat du front sont inchangés.

## 7. Risques non vérifiés ici

| Risque | Pourquoi il reste ouvert | Parade prévue |
|---|---|---|
| WebView2 sous Windows : runtime absent (Windows 10 non à jour), rendu vide sans `[STAThread]` | aucun poste Windows | `Main` en `[STAThread]` ; repli sur le navigateur système si la fenêtre ne démarre pas (L9.2) ; à vérifier par la porte E2E de L9.4 sur `win-x64` |
| Dossier de données de WebView2 : par défaut, Photino en choisit un partagé ou proche de l'exécutable | non observable sous macOS | `SetTemporaryFilesPath` vers le dossier de données de l'app (règle 6) |
| Fermeture sous Windows et Linux : annulation et fin du processus peut-être différentes de macOS | non testé | le travail de fin ne dépend d'aucune des deux (règle 1) |
| Linux (WebKitGTK, AppImage) | hors poste ; best effort selon l'ADR | non bloquant |
| Pages externes chargées dans la WebView : un lien sans `target` fait naviguer la fenêtre hors de l'app, et la page étrangère hérite du pont | Photino n'expose ni l'URL courante (ticket #197) ni de filtre de navigation | surface du pont minimale et validée côté hôte (L9.2) ; le front intercepte les liens externes vers `openExternal` (L9.3) ; endpoints locaux et proxy refusent toute requête d'une autre origine (L9.2) |
| Signature, notarisation, SmartScreen | exigent comptes et certificats (§ 11 du plan) | L9.4 |
| Dialogue d'enregistrement validé par un humain (chemin choisi, fichier écrit) | l'essai ne peut que l'ouvrir et l'annuler sans droit d'accessibilité | vérification manuelle au jalon du lot 9 |
