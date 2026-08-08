# ⚡ Audit de performance & de cache

> **Date** : 16 juillet 2026 · **Périmètre** : chaîne de chargement DDragon (Symfony ↔ go-fetcher ↔ MinIO) + loader SSE
> **Méthode** : lecture de code uniquement — **aucune mesure n'a été exécutée** (pas de Docker dans l'environnement d'audit).
> Chaque constat porte donc un niveau de confiance explicite. Les points marqués 🟡 sont à **mesurer avant** d'être corrigés.

---

## 📋 TL;DR

Deux symptômes, **des causes distinctes** :

| Symptôme | Cause dominante |
|---|---|
| « ça rame » | Le bind-mount Windows en dev (**A1**) + le manifeste qui se perd, donc le cache ne converge jamais (**A2**) |
| « le loader n'affiche pas les ressources » | Le lot entier est téléchargé **avant** le premier événement SSE (**B1**), et le watchdog à 15 s coupe souvent avant (**B2**) |

Le point le plus important : **A2 explique pourquoi recréer les conteneurs ne change rien.** Ce n'est pas un cache froid, c'est un cache qui **se vide lui-même** sous concurrence.

---

## 🐌 Partie A — Pourquoi ça rame

### A1 — Le bind-mount `./app` sur un disque Windows 🔴 CRITIQUE · confiance : haute

`compose.override.yaml:14` (auto-mergé, donc actif dès `docker compose up`) :

```yaml
volumes:
  - ./app:/var/www/html
```

Tout `./app` traverse la frontière Windows → Linux. Le décompte réel du dossier :

| Contenu | Fichiers |
|---|---|
| `app/vendor` | **9 049** |
| `app/var` (cache Symfony + pools) | **3 363** |

Chaque `stat()` / `open()` passe par 9p / gRPC-FUSE. C'est **la** régression « depuis que j'ai dockerisé » : le code n'a pas changé, le système de fichiers si.

Quatre amplificateurs se cumulent dessus :

1. `docker/php/php-dev.ini` → `opcache.validate_timestamps = 1` + `revalidate_freq = 0` → PHP re-`stat()` **chaque fichier inclus, à chaque requête**, sur le mount lent.
2. `config/packages/cache.yaml:26` → le pool `ddragon.cache` est `cache.adapter.filesystem` → **chaque lecture de manifeste ou de dataset = I/O sur le mount lent**. Le cache censé accélérer est posé sur le support le plus lent de la stack.
3. `compose.override.yaml:12` → `APP_ENV=dev` + `APP_DEBUG=1` → le profiler écrit un dossier par requête dans `var/cache/dev/profiler`.
4. `config/packages/monolog.yaml` → en dev, handler `stream` niveau `debug` → `var/log/dev.log` écrit sur le mount.

**Pistes** (par ordre de gain/effort) : déplacer le repo dans WSL2 (`~/…` et non `F:\`) · volumes nommés pour `vendor/` et `var/` · basculer `ddragon.cache` sur APCu ou Redis.

---

### A2 — Le manifeste se perd : le warm ne converge jamais 🔴 CRITIQUE · confiance : haute

`app/src/Service/API/AbstractManager.php:181-199` — séquence read-modify-write **nue** :

```php
$manifest = $this->loadManifest($version);   // L.181 — lecture memoïsée
// … plusieurs secondes de fetch + store …
$manifest[$name] = $cdn;                      // L.190
$this->saveManifest($version, $manifest);     // L.198 → PUT du fichier ENTIER
```

Vérifié : **aucun** lock (`symfony/lock` n'est pas installé), aucun merge, aucun re-read avant écriture, aucun `If-Match`/ETag côté Flysystem. Avec `pm.max_children = 20` (`docker/php/php-fpm.conf:9`), deux ingestions concurrentes sur la même version s'écrasent mutuellement : **last-write-wins**.

Ce n'est pas théorique — la collision est **structurellement provoquée** par l'architecture actuelle :

- le loader SSE ingère `/champions?numpage=1`,
- pendant que le flush différé de `kernel.terminate` ingère autre chose sur la même version,
- les deux réécrivent `manifest/{version}/champion.json` en entier.

**Deux facteurs aggravants** trouvés en relecture :

- **Le memo in-request élargit la fenêtre de perte.** Sur le chemin différé (`AbstractManager.php:162-163` → `DeferredImageIngestor` → `FlushDeferredImagesListener:22`), le manifeste est lu pendant le *render* mais écrit après la réponse **plus** toute la durée du `fetchMany`. La fenêtre n'est pas l'instant du write : c'est **la requête entière + le batch**.
- **En multi-conteneurs, ça devient permanent.** Le write-through `AbstractManager.php:336` (`$this->ddragonCache->delete(...)`) n'invalide que le **filesystem local du conteneur**. Le commentaire L.334-335 (« other workers repopulate ») n'est vrai qu'en mono-conteneur. Dès qu'on scale `php`, les autres conteneurs servent un manifeste périmé **jusqu'à 7 jours** (`default_lifetime: 604800`) puis le réécrivent intégralement.

Les blobs étant adressés par contenu, il n'y a **pas de corruption** — juste du travail refait à l'infini. C'est exactement le « le cache est toujours pas bon » : chaque entrée perdue = une image re-téléchargée à la visite suivante.

---

### A3 — BlobStore : jusqu'à 4 allers-retours MinIO + un transcodage GD par image, en série 🟠 MAJEUR · confiance : haute

`app/src/Service/Storage/BlobStore.php::store()` :

| # | Ligne | Opération | Appel S3 |
|---|---|---|---|
| 1 | L.36 | `fileExists($key)` | HeadObject |
| 2 | L.37 | `write($key, $bytes)` | PutObject |
| 3 | L.53 | `fileExists($webpKey)` | HeadObject |
| — | L.56 | `toWebp($bytes)` | CPU (GD) |
| 4 | L.58 | `write($webpKey, $webp)` | PutObject |

4 A/R est un **majorant exact** (2 sur chemin chaud, 3 si le transcodage renvoie `null`). Malgré son nom, `flysystem-async-aws-s3` résout **chaque appel de façon bloquante** (`AsyncAwsS3Adapter.php:128-131`, `142-144`).

Appelé depuis la boucle `foreach` de `AbstractManager.php:185-195`, un `store()` par itération, **zéro parallélisme**. Ordre de grandeur pour une page d'items froide (200 images) : **~800 A/R MinIO + 200 transcodages GD sérialisés dans un seul process PHP** — après un `fetchMany` déjà bloquant.

Le `fileExists` de la L.36 est par ailleurs **inutile** : la clé *est* le SHA-256 du contenu, donc un PUT est idempotent. Le supprimer économise 25 % des A/R gratuitement.

---

### A4 — Le lot entier est bufferisé en base64 des deux côtés 🟠 MAJEUR · confiance : haute

- **Go** : `go-workers/internal/api/handlers.go:95-96` → `wg.Wait()` **puis** `writeJSON(...)`. Rien n'est écrit avant que les N URLs soient **toutes** terminées.
- **PHP** : `GoFetcherClient.php:77` → `->toArray()` décode l'intégralité du JSON.

Un lot de 200 images en base64 (+33 %) tient intégralement en RAM des deux côtés, contre `memory_limit = 256M`. Et surtout : **rien ne peut être traité avant que tout le lot soit arrivé** — c'est la cause racine de **B1**.

---

### A5 — L'ingestion différée squatte un worker FPM après la réponse 🟡 MOYEN · confiance : moyenne

`FlushDeferredImagesListener` s'exécute à `kernel.terminate`. La réponse est bien envoyée (`fastcgi_finish_request`), mais **le worker reste occupé** plusieurs secondes. Avec `pm.max_children = 20`, quelques visites froides simultanées saturent le pool et les requêtes suivantes attendent dans la file nginx.

C'est un vrai job de queue. **`symfony/messenger` est déjà installé** (`config/packages/messenger.yaml`) mais n'est pas utilisé pour ça.

> ⚠️ Nuance : j'avais d'abord suspecté que le verrou de session restait tenu pendant `kernel.terminate`. **C'est faux** — vérifié dans `vendor/symfony/http-kernel/EventListener/AbstractSessionListener.php` : `$session->save()` est appelé à `kernel.response`, donc le verrou est relâché avant. (Ce qui rend au passage le `session_write_close()` de `LoaderController.php:65-67` redondant, mais inoffensif.)

---

### A6 — Aucun cache HTTP sur les réponses HTML 🟡 MOYEN · confiance : haute

Le docblock de `app/src/Service/Client/PageContextResolver.php:18` affirme :

> *« …which is what makes those responses safe to HTTP-cache (see the cache layer). »*

**« see the cache layer » ne référence rien.** Un grep sur `app/src`, `app/config`, `docs`, `docker` ne remonte que cette ligne elle-même. Vérification exhaustive :

- `public/index.php` instancie un `Kernel` nu — **aucun wrap `HttpCache`**.
- `framework.yaml:37-38` → `#esi: true` / `#fragments: true` — **commentés**.
- Aucun `setPublic` / `setSharedMaxAge` / `setEtag` / `#[Cache]` dans `app/src` (seule occurrence : `LoaderController.php:126` → `no-cache`, soit l'inverse).
- Aucun `fastcgi_cache` / `proxy_cache` dans `docker/nginx/`. nginx ne cache que `/cdn/`, `/build/`, `/fonts/` — jamais le HTML.

**Le docblock se trompe deux fois.** Même en ajoutant `setPublic()`, le cache partagé ne marcherait pas en l'état : `framework.yaml:6` → `session: true`, et `LocaleSubscriber:30` lit la session à `kernel.request` pour résoudre la locale UI. Symfony force alors `Cache-Control: private, no-cache`. La prémisse « each list/detail URL is a pure function of its query » est vraie pour l'URL mais **fausse pour le rendu** : la locale UI vient d'un état hors URL.

→ Corriger le commentaire, **ou** implémenter la couche : porter la locale dans l'URL, puis `s-maxage` + ETag.

---

### A7 — go-fetcher : transport HTTP par défaut 🟡 À MESURER · confiance : faible

Faits établis statiquement :

- `go-workers/internal/fetcher/fetcher.go:31` → `&http.Client{Timeout: timeout}`, `Transport` nil → `http.DefaultTransport`.
- Aucun Transport custom nulle part (grep sur les 8 fichiers `.go`).
- `http.DefaultTransport` ne fixe pas `MaxIdleConnsPerHost` → fallback à **2**, alors que `MaxConcurrency = 16` (`config.go:27`).

**Mais la conclusion « un handshake TLS par image » n'est PAS établie.** `http.DefaultTransport` porte aussi `ForceAttemptHTTP2: true`, et `go.mod` cible Go 1.25. Si `ddragon.leagueoflegends.com` négocie **h2** en ALPN (probable pour un CDN), Go bascule sur `http2.Transport` : **une seule connexion TCP/TLS multiplexée**, et `MaxIdleConnsPerHost` — qui ne s'applique qu'au pool HTTP/1.1 — devient **sans objet**. L'impact tomberait à zéro.

> 🔬 **À vérifier avant de toucher au code** : `GODEBUG=http2debug=1` sur le conteneur `go-fetcher`, ou compter les handshakes. Si HTTP/1.1 est effectivement utilisé, alors un batch de 200 URLs à concurrence 16 (~12,5 vagues) ferait ~175 handshakes au lieu de ~16, et le fix est un `Transport` dédié avec `MaxIdleConnsPerHost` ≥ `MaxConcurrency`.

---

## 🔄 Partie B — Pourquoi le loader n'affiche pas les ressources

### B1 — Aucun événement `item` ne peut partir avant la fin du téléchargement 🔴 CRITIQUE · confiance : haute

Chaîne complète vérifiée :

1. `LoaderController.php:108` → `$manager->ingest(...)`
2. `AbstractManager.php:253` → `ingestMissing($version, $missing, $onStored)`
3. `AbstractManager.php:184` → **`$bytesByUrl = $this->goFetcher->fetchMany(...)` — bloquant, avant toute itération**
4. `GoFetcherClient.php:63-67` → boucle sur les chunks, ne `return` qu'après **tous**
5. `handlers.go:95-96` → `wg.Wait()` puis `writeJSON` — pas de streaming côté Go
6. **Seulement ensuite** : `AbstractManager.php:185-195` → boucle → `store()` → L.193 `$onStored($name)`

**Aucun chemin ne permet un `$onStored` anticipé.**

Le symptôme exact est donc : **silence total pendant toute la phase réseau** (le code en est conscient — `LoaderController.php:105` émet un keepalive `": warming …"` juste avant « the blocking batch fetch »), **puis** progression cadencée par la vitesse de `store()` (cf. A3). La barre est déterminate mais **reste figée à 0 %** pendant tout le fetch, et la liste `hx-log` reste vide.

À noter : le nom affiché n'est pas « l'image qu'on télécharge » mais « l'image qu'on écrit dans MinIO depuis un lot déjà en RAM ». L'honnêteté revendiquée dans le docblock du composant (« no fabricated percentage ») est respectée sur le fond, mais l'UX promise — « name each resource as it lands » — ne peut pas se produire pendant la phase la plus longue.

**Fix** : émettre au fil de l'eau. Par ordre de coût croissant — chunker `fetchMany` en lots de 8-16 (2 lignes, gain immédiat) · faire streamer le Go en NDJSON · pousser le fetch et le store en pipeline.

---

### B2 — Le watchdog à 15 s coupe avant le premier événement 🔴 CRITIQUE · confiance : haute

`assets/vue/components/ResourceLoader.vue:58` :

```ts
const WATCHDOG = 15000 // no `done` within this window → give up and visit anyway
```

Une page froide met largement plus de 15 s (A3 : ~800 A/R MinIO + 200 transcodages, en série, **après** le fetch). Le watchdog déclenche `finishRun()` → navigation vers une page **pas encore chaude** → placeholders.

**Combiné à B1, c'est décisif** : puisque tout arrive à la fin, le watchdog tire quasi systématiquement **avant le premier `item`**. L'utilisateur voit un loader qui tourne, 0 %, aucun nom, puis une page à moitié vide. C'est très exactement le symptôme décrit.

> Corriger B1 seul suffit probablement à rendre le watchdog inoffensif (les `item` arrivent tôt et peuvent le réarmer). L'inverse n'est pas vrai : allonger le watchdog sans corriger B1 laisserait juste l'utilisateur devant 0 % plus longtemps.

---

### B3 — Le stream n'est pas protégé contre `fastcgi_read_timeout` 🟡 À VÉRIFIER · confiance : moyenne

`docker/nginx/default.conf` ne fixe pas `fastcgi_read_timeout` → **défaut nginx : 60 s**. Or le keepalive `": warming …"` est émis **avant** le batch bloquant (`LoaderController.php:105`), pas pendant. Si un batch dépasse 60 s sans qu'un seul octet ne parte, nginx coupe la connexion FastCGI → `EventSource` voit une erreur → **se reconnecte automatiquement** → **relance tout le warm depuis zéro**.

Un `/home` froid enchaîne 4 catégories, avec un keepalive entre chaque — donc le dépassement n'est pas certain, mais il est plausible sur `/objects`. Corriger B1 (chunks) fait disparaître le risque au passage, puisque des octets partent en continu.

Durcissement recommandé quoi qu'il arrive : `X-Accel-Buffering: no` est bien posé (`LoaderController.php:129`) et nginx l'honore, et `text/event-stream` n'est pas dans `gzip_types` — donc **le buffering n'est probablement pas en cause aujourd'hui**. Mais rien ne le verrouille : le stream dépend d'un header applicatif. Le fixer dans la conf (`fastcgi_read_timeout`, `fastcgi_buffering off`, `gzip off`) le rend indépendant du code.

---

### B4 — Couplage caché dans `loaderSteps()` 🟢 MINEUR · confiance : haute

`prepareUrl()` (`ResourceLoader.vue:103-111`) envoie `numpage` / `itemperpage`, mais `LoaderController::prepare()` ne lit que `path`, `version`, `lang`. Ça fonctionne quand même : `PageContextResolver::loaderSteps()` les récupère via `requestStack->getCurrentRequest()->query`, qui est la requête SSE elle-même.

**Ce n'est donc pas un bug** — mais le docblock affirme « read purely from the request query » alors que la méthode dépend du `RequestStack` ambiant tout en recevant `$path` en argument. Passer `page` / `perPage` explicitement supprimerait le piège.

---

## 🗺️ Plan de correction proposé

L'ordre compte : **A2 et B1 d'abord**. Sans A2, tout gain de perf est reperdu à la visite suivante. Sans B1, le loader reste muet quoi qu'on fasse ailleurs.

| # | Action | Effort | Gain attendu |
|---|---|---|---|
| **1** | **A2** — merge du manifeste : relire juste avant le write et fusionner (`$fresh + $mine`), ou passer à un fichier par entrée (`manifest/{v}/{type}/{name}.json`), ou verrou `symfony/lock` | M | 🔥 le cache converge enfin |
| **2** | **B1** — chunker `fetchMany` en lots de 8-16 dans `ingestMissing` | **S** | 🔥 progression visible immédiatement |
| **3** | **A1** — sortir `vendor/` et `var/` du bind-mount (volumes nommés) + repo dans WSL2 | M | 🔥 dev fluide |
| **4** | **A3** — supprimer le `fileExists` L.36 (PUT idempotent, clé = SHA-256) | **S** | −25 % d'A/R MinIO |
| **5** | **B2** — watchdog réarmé à chaque `item` reçu, plutôt qu'un timeout absolu | **S** | plus de navigation prématurée |
| **6** | **A1bis** — `ddragon.cache` sur APCu (`cache.adapter.apcu`) | **S** | supprime l'I/O disque du cache chaud |
| **7** | **A5** — déporter l'ingestion différée sur Messenger | L | libère les workers FPM |
| **8** | **B3** — `fastcgi_read_timeout` + `fastcgi_buffering off` sur la route loader | **S** | durcissement |
| **9** | **A6** — corriger le docblock mensonger (et décider si on implémente la couche) | **S** | honnêteté du code |
| **10** | **A7** — mesurer l'ALPN **avant** de toucher au Transport Go | **S** | ? (à établir) |

---

## 📊 Avant de coder : mesurer

Ces constats sont **statiques**. Trois mesures valident (ou invalident) l'essentiel en 10 minutes :

```bash
# 1. Isoler le coût du bind-mount : lancer SANS l'override (prod, pas de mount)
docker compose -f compose.yaml up -d --build
#    → si c'est nettement plus rapide, A1 est confirmé et dimensionné

# 2. Prouver A2 (perte d'entrées) : compter les entrées du manifeste
#    avant / après deux visites concurrentes sur la même version
docker compose exec minio sh -c 'cat /data/ddragon/manifest/*/champion.json' | jq 'length'

# 3. Trancher A7 : HTTP/2 ou pas ?
docker compose exec go-fetcher sh -c 'GODEBUG=http2debug=1 ...'
```

Le profiler Symfony (`/_profiler`, dev) donne le détail par requête — mais attention, **il est lui-même une partie du problème en dev** (A1.3).
