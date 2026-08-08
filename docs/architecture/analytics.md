# 📊 Analytics & panneau d'administration

Sous-système d'analytics (trafic, audience, stockage) et refonte Hextech du panneau
`/admin`. Sans base de données : le flux d'événements vit en NDJSON local, consolidé
en agrégats immuables sur MinIO ; les métriques stockage dérivent du bucket lui-même.

## Vue d'ensemble

| Brique | Rôle | Fichier(s) |
|---|---|---|
| Capture | 1 event/vue de page ressource, au `kernel.terminate` (zéro latence) | `EventListener/RecordRequestListener`, `Service/Analytics/RequestEventFactory` |
| Parsing | UA → navigateur/OS/appareil/bot ; referer → source ; IP → pays | `UserAgentParser`, `RefererClassifier`, `GeoLocator` |
| Journal | Append-only NDJSON, 1 fichier/jour UTC | `Service/Analytics/EventStore` → `var/state/analytics/events/{Y-m-d}.ndjson` |
| Agrégation | Events → agrégat journalier mergeable → rapport de plage | `AnalyticsAggregator`, `RangeReportBuilder`, `AnalyticsReportService` |
| Durabilité | Consolidation NDJSON local → objets immuables MinIO | `RollupService`, `DailyAggregateStore`, cmd `app:analytics:rollup` |
| Stockage | Analyse détaillée du bucket (familles, WebP, dédup, complétude) | `StorageAnalyticsService` |
| Rendu | Charts SVG serveur (aucune dépendance front) | `Service/Analytics/Chart/SvgChartRenderer`, `Twig/AdminChartExtension` |
| UI | Design system Hextech auto-contenu | `public/admin/admin.css`, `templates/admin/*` |

## Ingestion — pourquoi `kernel.terminate`

Le listener sœur de `FlushDeferredImagesListener` : il tourne **après**
`fastcgi_finish_request`, donc hors du chemin critique. La réponse est déjà envoyée
au visiteur, la capture n'ajoute aucune latence perçue. `TerminateEvent` ne se
déclenche que pour la requête principale ; `RequestEventFactory` restreint ensuite
aux routes ressources whitelistées (`app_home`, listes et détails), en `GET`, via le
nom de route (`_route`) — plus robuste qu'un matching de chemin, exclut nativement
`api_*`, `admin_*`, le setup et le SSE. La factory lit uniquement la requête et la
query, **jamais la session**, pour ne pas forcer son démarrage.

## Persistance — pas de read-merge-write S3

Le manifeste documente que le RMW S3 est non atomique (course loader SSE ↔ flush
terminate). Les vues de page sont haute fréquence : un objet mutable partagé
hériterait de cette course. D'où :

1. **Chemin chaud** — `EventStore::append()` = un `file_put_contents(FILE_APPEND | LOCK_EX)`
   local, atomique par ligne entre workers php-fpm, microsecondes, sans réseau.
2. **Durabilité** — `app:analytics:rollup` plie les journées **closes** en agrégats
   immuables `analytics/daily/{Y-m-d}.json` sur MinIO (écrits une seule fois, jamais
   mutés). La journée courante reste toujours lue en direct depuis le NDJSON.

Le rapport (`AnalyticsReportService`) sait lire indifféremment l'agrégat MinIO (jours
consolidés) ou agréger le NDJSON à la volée (jours non encore consolidés) → le
panneau est correct que le rollup ait tourné ou non.

### Commande de rollup

```bash
# Consolide les journées closes (idempotent). À planifier (cron/déploiement).
docker compose exec php php bin/console app:analytics:rollup
docker compose exec php php bin/console app:analytics:rollup --include-today   # + journée courante
docker compose exec php php bin/console app:analytics:rollup --prune           # purge le NDJSON consolidé
docker compose exec php php bin/console app:analytics:rollup --force           # réécrit les agrégats existants
```

Le bouton **Consolider** de la vue d'ensemble déclenche `--include-today` (CSRF protégé).

### Où vit le NDJSON — `var/state`, adossé à un volume

Un déploiement, c'est `compose pull` + `up -d` : le conteneur `php` est **recréé**,
et tout ce qui a été écrit dans sa couche writable disparaît. Le journal ne peut donc
pas vivre n'importe où sous `var/` :

| Chemin | Volume | Pourquoi |
|---|---|---|
| `var/state/**` | `app_state` | Durable — events analytics, journal d'audit, sessions, base GeoLite2 |
| `var/cache`, `var/log` | aucun | Jetable **volontairement** : persister le cache prod servirait le conteneur DI compilé d'une image antérieure après un déploiement |

Le point de montage `var/state` est créé **dans l'image** et `chown www-data`
(`docker/php/Dockerfile`) : Docker recopie les droits du point de montage lorsqu'il
peuple un volume nommé vide, et un dossier absent serait créé `root` — le pool FPM ne
pourrait plus écrire, et `EventStore::append()` avale cet échec en silence.

> Fenêtre de perte : le volume `app_state` couvre la recréation de conteneur (donc
> tout déploiement). Restent le `down -v` et la perte de l'hôte — c'est le rôle du
> rollup, exécuté à chaque déploiement sur les journées closes
> (`.github/workflows/_deploy.yml`). Pour rétrécir encore la fenêtre, planifier un
> rollup horaire `--include-today`.

## Vie privée & sécurité

- **Modèle retenu** : IP réelle + User-Agent complet stockés (analytics fines + géo).
  `trusted_proxies` (Caddy→nginx→php) fait remonter la vraie IP via `X-Forwarded-For`.
- `visitorId` = HMAC(IP|UA, APP_SECRET) tronqué — identifiant pseudonyme stable pour
  le comptage des uniques ; le couple brut n'est pas dérivable de l'id seul.
- **Les IP/UA bruts ne quittent jamais le NDJSON local.** Les agrégats journaliers
  MinIO ne contiennent que des compteurs + `visitorId` pseudonymes (pas de PII).
- **Défense en profondeur** : le bucket `ddragon` est en lecture anonyme (blobs =
  CDN). nginx **refuse** `^~ /cdn/analytics/` (404) pour qu'aucun agrégat ne soit
  atteignable depuis le web ; l'app les lit en interne (php→minio, hors nginx).
- Robots exclus des métriques humaines (comptés à part).
- ⚠️ Rétention : le NDJSON contient de la PII. Le purger (`--prune`) après rollup,
  ou planifier une purge des journées anciennes selon la politique de rétention.

## Géolocalisation (GeoLite2)

`GeoLocator` lit une base MaxMind/DB-IP `.mmdb` (format `GeoLite2-Country`) via
`geoip2/geoip2`. Tout dégrade proprement : sans package, sans fichier, ou pour une IP
privée/inconnue → `null`, la carte affiche « inconnu ». Provisionnement (étape ops,
la base ne peut pas être versionnée) :

```bash
# Déposer une base ip→pays au format .mmdb, p. ex. :
#  - MaxMind GeoLite2-Country (compte gratuit + clé de licence), ou
#  - DB-IP ip-to-country-lite (CC-BY, même format .mmdb)
mkdir -p app/var/state/geoip && cp GeoLite2-Country.mmdb app/var/state/geoip/
# ou pointer GEOIP_DB_PATH vers un fichier monté (voir compose.yaml / .env)
```

Variable `GEOIP_DB_PATH` (vide → `var/state/geoip/GeoLite2-Country.mmdb` par défaut).
Le fichier est déposé **une fois** : `var/state` est adossé au volume `app_state`
(cf. « Persistance » ci-dessous), il survit aux déploiements.

## Analytics stockage

`StorageAnalyticsService` fait **une** passe `listContents` deep (taille/date
renvoyées sans HEAD) + une lecture bornée des manifestes, puis calcule : poids par
famille (`blobs`/`data`/`manifest`/`analytics`), ventilation blobs par extension,
**couverture WebP** (siblings / sources), **ratio de déduplication** (références
logiques des manifestes → blobs physiques content-addressed), poids `data` par
version/langue/type, plus gros objets, timeline d'ingestion (`lastModified`), et
matrice de complétude version × langues. Résultat mémoïsé 120 s (`ddragon.cache`) ;
dégrade en `ok=false` si MinIO est injoignable (jamais de 500). `?refresh=1` force.

## Panneau `/admin`

`ROLE_ADMIN` via le firewall `admin` (inchangé : `AdminAuthenticator`, creds env).

| Route | Page |
|---|---|
| `admin_dashboard` `/admin` | Vue d'ensemble (KPIs cross-subsystème, série temporelle, donut ressources, top pages, stockage) |
| `admin_traffic` `/admin/traffic` | Pages les plus consultées, entités, ventilations, heatmap heure×jour |
| `admin_audience` `/admin/audience` | Uniques/récurrents/nouveaux, pays, appareils, navigateurs/OS/langues, sources & référents |
| `admin_storage` `/admin/storage` | Analyse stockage détaillée |
| `admin_analytics_rollup` `POST /admin/analytics/rollup` | Consolidation manuelle (CSRF) |

Filtre de période `?range=7d|30d|90d|all` sur les pages trafic/audience/overview.

### Design — auto-contenu par choix

L'admin **reste indépendant du pipeline Vite** (invariant : « l'ops panel doit
survivre à un build front cassé »). L'alignement Hextech est obtenu sans Vite :
`public/admin/admin.css` (statique, servi par nginx via `try_files`, cacheable) porte
les tokens/polices/framing identiques à `assets/styles/app.css` ; les charts sont du
**SVG rendu serveur** (`SvgChartRenderer`), avec `<title>` natifs comme couche hover.
Aucun îlot Vue, aucune lib de charts. Palette catégorielle = couleurs d'arbres de
runes (CVD-safe, validées), toujours doublées d'un label/légende.

## Garde-fous

```bash
docker compose exec -T php php vendor/bin/phpunit tests/Unit   # baseline + analytics
```

Le front (`npm test`/`typecheck`/`build`) n'est **pas** impacté : ce lot ne touche
ni `assets/` ni la config Vite (admin 100 % serveur).
