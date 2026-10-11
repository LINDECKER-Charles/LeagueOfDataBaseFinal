# Pièges connus (ne pas « corriger » par erreur)

Comportements voulus ou contraintes constatées pendant la construction de la stack .NET +
Angular. Chacun a coûté du temps une fois : le lire avant de toucher à la zone concernée.
Les règles de code et les invariants font foi dans [`AGENTS.md`](../../AGENTS.md).

## Build et outillage

- `dotnet test` compile en **Debug**, le build en **Release** : les deux coexistent. Il ne
  prend pas `-m` (code 5, aucun test) ; `--artifacts-path` fait échouer
  `PhotinoIsolationTests`. `bin/` et `obj/` ne sont ignorés que sous `/src` et `/tests`.
- OpenAPI : `dotnet build src/LoDb.Api -p:LoDbGenerateOpenApi=true` écrit
  `openapi/LoDb.Api_app.json` et `LoDb.Api_public-v1.json` (noms imposés par le SDK).
  `MapOpenApi` n'est actif qu'en Development. Sous Windows, normaliser les `\r\n` avant
  `api:check`.
- **npm 12** bloque les scripts d'installation (`allowScripts`) d'`esbuild`, `lmdb`,
  `@parcel/watcher` et `msgpackr-extract` ; les binaires viennent des paquets optionnels de
  plateforme. Si l'image `web-ssr` échoue au build, c'est la première piste.
- `npm install --prefix src/LoDb.Web` tire `@capacitor/ios` et `@capacitor/keyboard` par
  `@aparajita/capacitor-secure-storage` : attendu, il n'y a pas de plateforme iOS.
- **Images** : chaque image d'un Dockerfile est épinglée par digest
  (`tools/ci/check-pinned-images.mjs`). Les étages `runtime` de nginx et web-ssr font un
  `apk upgrade` / `apt-get upgrade` **voulu** : la CI les reconstruit sans cache pour qu'ils
  prennent les correctifs du jour avant le scan Trivy, qui bloquerait sinon sur des failles
  corrigées en amont. `web-ssr` n'a plus ni npm, ni corepack, ni yarn : seul `node` y tourne.
- Relever le budget `initial` de `build:web` est une décision du propriétaire.
- **CLI en `Development`** : l'hôte de `CliRunner` y valide tout le conteneur. Un service
  qui dépend de l'hôte web fait tomber **toutes** les sous-commandes. Après un ajout à un
  `Add<Module>` : `docker compose -p lodb-dev … exec -T api dotnet LoDb.Api.dll analytics
  import --source /x --dry-run` doit répondre `No such directory: /x`.

## Front, SSR et SEO

- **`autoCsp` est incompatible avec le SSR** ; `withFetch` et `withIncrementalHydration`
  sont omis (défauts en v22).
- Issues de navigation **rendues en place** : 301, 302, 404 et 5xx s'affichent à l'URL
  demandée (`PageResponse`), jamais d'`UrlTree` de redirection côté serveur.
  `/{locale}/account`, `/{locale}/u` et `/b` ont une route 404 explicite (`bareNotFound`).
  Les routes `RenderMode.Client` (compte, `/admin`) ne posent pas de statut.
- **Pré-routeur SSR** (`src/server/routing/`), avant l'admission : premier segment ni forme
  de locale, ni `b`, ni `admin` → 404 statique sans Angular ; segment décodé en `/` ou NUL →
  400 ; liste ou fiche du catalogue → 301 `no-store` vers son URL sans les paramètres
  qu'elle ne lit pas. Un nouveau paramètre de liste se déclare dans les clés de facettes
  (`*-facet-keys.ts`) ou dans `owned-params-of.ts`, sinon il disparaît au rechargement.
  Locale et version se lisent **décodées** (`/%66r/%31%35.13.1/` est épinglée). Un segment
  `.` ou `..`, ou qui contient `\`, `(`, `)` ou `;`, donne aussi un 400 : le rendu résout
  l'URL comme un navigateur et son routeur lit outlets et paramètres matriciels
  (`;version=` compris), il rendrait donc une autre page que celle classée.
- Une API qui ne répond pas (aucune réponse, 502, 504) donne un **502** ; seule une 503 de
  l'API (délestage, avec `Retry-After`) reste 503. Chaque appel API d'un rendu est borné
  par l'échéance du rendu (`LODB_RENDER_DEADLINE_SECONDS`, 20 s).
- La liaison des entrées de composant par le routeur reste coupée : une query forgée
  remplirait des entrées.
- `app.routes.server.spec.ts` passe par `ɵextractRoutesAndCreateRouteTree`, API privée
  qu'une montée de version peut déplacer.
- Points d'accroche de `app.config.ts` : chaque propriétaire remplit son dossier
  (`core/auth`, `core/analytics`, `core/update`) sans toucher `app.config.ts` ni `app.html`.
- **Historique des patchs** (`/{locale}/patches`) : quatre liens par version listée, des
  centaines de versions. Balisage nu et `ViewEncapsulation.None` voulus (l'attribut
  d'encapsulation pesait un tiers de la page) : ni attribut, ni classe, ni pipe par lien.
  L'index des sitemaps n'annonce que le dernier patch et les 12 versions suivantes
  (`LoDb__Seo__SitemapVersionWindow`) : un ancien sitemap absent de l'index reste servi, ce
  n'est pas une régression. Les anciens patchs se découvrent par cette page et le bloc
  « Dans les patchs précédents » des fiches, qui ne chargent aucun catalogue.
- Une page qui oublie `Seo.apply` n'a ni canonique ni hreflang. Une clé `about.*`, `api.*`
  ou `seo.*` n'est traduite que si un ancêtre fournit son scope (`provideTranslocoScope`) ;
  sinon la clé brute sort dans le HTML, sans erreur. Le chrome (en-tête, pied de page) n'en
  fournit aucun : le build prête au catalogue racine ses libellés d'autres scopes
  (`src/LoDb.Web/i18n/chrome-keys.json`, que `core/layout/chrome-keys.spec.ts` vérifie).
- **Catalogues construits** (`npm run i18n:build`, avant chaque build et `npm start`) :
  `public/i18n` est la source, `dist/i18n` ce que le site sert, chaque locale complétée par
  `en` et ses messages ICU compilés (`core/i18n/messages`, aucun `new Function` : la CSP
  refuse `'unsafe-eval'`). `useFallbackTranslation` est coupé : une clé ajoutée à `en` seul
  s'affiche en anglais partout après le build. Un message ICU n'accepte ni formateur
  (`{n, number}`, dates) ni paramètre `{{ }}` : le build échoue en le nommant. Un
  `npx ng serve` lancé à la main sert le dernier `dist/i18n` (`npm run i18n:build` d'abord) ;
  un spec qui fournit un message ICU le donne compilé (`provide-admin-catalogues.ts`).
- **Le SSR ne se requête jamais lui-même** : catalogues et changelog sont lus en mémoire au
  démarrage (un dossier absent l'arrête) et `SsrHttpBackend` répond aux fichiers de la page
  sans HTTP. `/api/meta` est gardée 60 s par processus (`LODB_API_META_TTL_SECONDS`), sa
  dernière copie servie quand l'API ne répond pas : un patch promu apparaît dans les pages
  rendues jusqu'à une minute après l'API.
- **Grilles de cartes en `div role=list`**, jamais `ul/li` ni `<p>` : le texte riche de
  Riot contient des `<li>` nus qui cassent l'hydratation.
- **Squelette à la hauteur de ce qu'il remplace** (forme `portrait` pour une grille de
  portraits), sinon CLS.
- **Dialogues CDK** : le focus initial est sur `cdk-dialog-container` ; écouter
  `DialogRef.keydownEvents`, pas `host: {'(keydown)'}`. Un dialogue plus haut que l'écran
  reste défilable jusqu'à son pied.
- L'en-tête n'a que 288 px utiles à 320 px : tout ajout au groupe d'actions se vérifie à
  320 px, `ar` compris. Un `white-space: nowrap` sur un titre doit pouvoir passer à la ligne.
- Variante store : `environment.payments` n'agit que là où le code le lit ; le paiement se
  retire par le routage et les liens. `cap sync` ne tourne que dans le conteneur
  (`tools/android/build-debug.sh`).
- **Liens des e-mails de compte** : le jeton est dans le **fragment**
  (`verify-email?user={id}#token=…`, `reset-password?user={id}#token=…`), que ni le serveur,
  ni l'edge, ni un `Referer` ne voient. La page le lit au démarrage, côté navigateur, puis le
  retire de l'URL (`forgetLinkToken`, navigation `replaceUrl`) : rechargée ensuite, la page
  n'a plus de lien (celle du mot de passe renvoie vers une nouvelle demande), c'est voulu.
  Les anciennes formes (`verify-email?token=`, `reset-password/{token}`) restent lues pour les
  liens déjà envoyés (valides une heure) ; ne pas remettre le jeton dans le chemin ni la query.

## API, données et comptes

- `/readyz` se lit sur l'API (18081 ou `lodb-api:8080`) et, par nginx, sur le sous-domaine `api.`
  seulement (1 requête/s par adresse, sonde externe) ; demandé au site, il part au SSR (404).
- En local, les URLs absolues perdent le port (nginx transmet `Host: $host`) : l'origine
  canonique se fixe par `LoDb__Seo__CanonicalOrigin` et `LoDb__Accounts__SiteOrigin`.
- **Pool Postgres** : `Maximum Pool Size=32;Timeout=5` dans la chaîne de connexion. Une
  requête `/api` qui n'obtient pas de connexion à temps reçoit un **503** `service-busy`
  avec `Retry-After` : délestage voulu, non compté. Une base injoignable donne un **500**,
  compté par `lodb_db_unavailable_total`. Npgsql ne donne au pool épuisé ni type ni code :
  son message est reconnu par son texte, et `DatabaseFailureTests` le revérifie sur un vrai
  pool à chaque montée de version.
- **Rôle Postgres de l'API** : l'API se connecte comme `lodb_app` (lignes seulement, 15 s par
  requête), `migrate` comme le superutilisateur ([`configuration.md`](../guides/configuration.md)
  § 4.3). Un `42501` (`permission denied`, `must be owner`) d'une commande lancée dans le
  conteneur `api` est voulu : ce qui change le schéma passe par `migrate`
  (`docker compose run --rm migrate …`), jamais par l'API ni par un droit accordé à la main,
  que le déploiement suivant retire. Les seules tables que l'API crée et supprime, les
  partitions d'`analytics_event`, passent par deux fonctions `SECURITY DEFINER`. Une tâche
  dont les requêtes grandissent avec le trafic relève son délai pour sa transaction
  (`StatementTimeouts.RaiseAsync`), jamais celui du rôle. En test, `TestDatabase.ConnectionString`
  est le superutilisateur (`migrate`, amorçage, assertions), `AppConnectionString` le rôle de
  l'API : un hôte de test se branche sur le second.
- **Corps de requête** : 128 Kio au plus (`LoDb:Hosting:MaxRequestBodySize`), 413 au-delà.
  Le plus gros corps légitime, un build maximal dont chaque caractère est échappé, dépasse
  64 Kio (`tests/LoDb.E2E/specs/builds/maximal-build.spec.ts`) : ne pas descendre à 64 Kio.
  Un endpoint qui doit lire plus pose son propre `RequestSizeLimit` (webhook Stripe, 1 Mio).
  Les tests xUnit de cette borne tournent sur un vrai Kestrel, en Production
  (`Hosting/Support/KestrelApi.cs`) : le serveur en mémoire n'applique aucune limite, et
  Development transforme un corps invalide en exception.
- `/api/pickers/*` exige `version` et `lang` (400 `invalid-version` sinon).
- **Porte réseau de l'API** : le port 8080 ne répond qu'au réseau de la stack
  (`LoDb:Hosting:KnownProxyNetworks`, le seul `LODB_STACK_SUBNET` sur un hôte, les trois
  plages privées en local) et à la loopback ; tout autre pair reçoit un 404 vide, compté par
  `lodb_network_gate_refusals_total`, sans log. Un conteneur d'un réseau partagé
  (`observability`) ne joint que le port 9464. Un nouveau service qui appelle l'API doit
  être sur le réseau `default` de la stack.
- **Postgres sur le seul réseau `data`**, avec `api` et `migrate` (interne sur un hôte : aucune
  route vers l'extérieur) : nginx et le SSR ne joignent pas la base, et l'API répond un 404 à
  un pair de `data` (ce n'est pas le réseau de `KnownProxyNetworks`). Un nouveau service qui
  lit la base rejoint `data` et appelle `lodb-postgres` ; un outil local passe par le port
  publié (`LODB_PG_PORT`) ou `docker compose exec postgres`, jamais par nginx.
- **Porte froide des catalogues** : un appel direct à l'API (18081, `lodb-api:8080`) depuis le
  réseau de la stack (ou la loopback) et sans `X-Forwarded-For` est compté comme le SSR
  (débit interne, attente de 2 s permise). Pour reproduire un client public, passer par nginx
  (18080). Un `503` « en attente » sous charge est un délestage voulu, pas une panne.
- **Warm-up à court d'images** : une trame `done` avec `settled` < `total` est voulue. Le
  budget d'images par minute est partagé (≈ 900 images par warm-up froid de l'accueil,
  1 500 par minute) : refusé, le flux ne demande plus d'images et finit `done`, le
  catalogue ouvert ; les images restantes restent en attente et les listes les confient à
  la file de fond. Un catalogue qui ne s'ouvre pas et un flux expiré finissent toujours
  `failed`, en gardant les comptes atteints.
- Une URL de l'ancien site sur une version non stockée répond `302` vers la liste de la
  version, sans rien ingérer : voulu (une entrée retirée du jeu ne devient pas une 404).
- **E-mails** : le modèle d'un `EmailMessage` s'écrit avec `EmailModelKeys`, jamais en
  littéral (sinon `dead` dès le premier essai). L'outbox n'envoie rien sans
  `LoDb:Mail:Host` : voulu. En local, Mailpit sur http://localhost:18025.
- **Plafond horaire de l'outbox** (`LoDb:Outbox:HourlyCaps`, 100 confirmations, 50
  réinitialisations, 20 messages de contact par heure glissante, pour tout le site) : au-delà,
  les messages restent `pending` avec 0 tentative jusqu'à ce que l'heure libère une place.
  Un e-mail « qui n'arrive pas » se cherche d'abord dans la jauge `lodb_outbox_deferred` et
  le journal `outbox.hourly_cap.reached` ; un lien retenu plus d'une heure arrive expiré.
- **Comptes non vérifiés effacés après 7 jours** (tâche `accounts.unverified_purge`, horaire),
  sauf build, clé API, don, rôle, bannissement, soutien ou connexion Google : un compte de
  test non vérifié d'une pile locale qui tourne depuis une semaine disparaît, c'est voulu.
  Le renvoi de l'e-mail de vérification est limité à 5 par compte, puis 1 par jour.
- **Hôtes de l'API** (`AllowedHosts`, posé par `compose.deploy.yaml`) : un `Host` absent de la
  liste reçoit un `400`. Un nouvel appelant interne (service, sonde) doit utiliser un nom
  listé (`lodb-api`, `localhost`, `127.0.0.1`) ; le port 9464 n'est pas filtré, la collecte
  y nomme le conteneur par son adresse. Le filtre du framework, qui passerait avant ce port,
  est retiré exprès (`Hosting/HostFilteringSetup.cs`) : ne pas le remettre. En local,
  `AllowedHosts` vaut `*`.
- **Jetons de rafraîchissement à usage unique** : chaque `/refresh` rend le suivant, que le
  client doit garder à la place du précédent. Rejouer un jeton déjà échangé plus de 30 s
  après (`LoDb:Accounts:RefreshTokens:ReuseGrace`) révoque toute la famille : un script de
  test qui réutilise le même jeton deux fois voit la session tomber, c'est voulu.
- **Codes TOTP** : un code n'est accepté qu'une fois par compte (même à l'enrôlement), avec
  un pas de 30 s de dérive. Un test qui enchaîne deux connexions prend le code du pas suivant
  (`specs/admin/admin-account.ts` attend ; les tests xUnit calculent le code à l'horloge de
  l'hôte, `Totp.Code(key, App.Clock)`, jamais à l'heure réelle). La clé est scellée par Data
  Protection : si le trousseau est perdu (certificat compris), les TOTP ne s'ouvrent plus et
  un administrateur se rétablit par `admin mfa-reset` puis `admin mfa-invite`. Même remède
  après un retour arrière de l'API vers une image antérieure au scellement : elle ne lit ni
  la clé scellée (`dp1:…`) ni les codes de secours hachés, et la connexion au second facteur
  y échoue.
- Bannir, c'est `IsBanned` plus `UpdateSecurityStampAsync`. Un hash argon2id **plus fort**
  que la cible est conservé (`Argon2Passwords.IsTarget`) ; un compte hérité arrive sans
  `security_stamp`, la connexion le remplit.
- Rapport de surveillance de l'admin : un rapport dont les lectures ont échoué reste 30 s en
  cache. Une carte absente se cherche dans les logs
  (`admin.monitoring.database_unreadable`), jamais par un `waitFor` plus long.
- **Webhook Stripe en local** : aucun secret en dev (503). Pour un rejeu, un secret de test
  dans une surcouche hors dépôt et une signature datée de moins de 5 minutes.

## Tests E2E

- **Quota d'inscriptions** : 5 par heure et par adresse, en mémoire, et toute la suite
  arrive sous une seule adresse. Seule `specs/account/register.spec.ts` passe par le
  formulaire ; les autres créent leur compte par la CLI (`support/member-account.ts`,
  `support/worker-account.ts`). Entre deux passages complets :
  `docker restart lodb-dev-api-1`. Ne jamais relever le quota pour les tests.
- **Bornes relevées pour l'E2E**, dans `compose.override.yaml` seulement (jamais
  `compose.deploy.yaml`) : la suite ouvre des patchs anciens et des langues, d'une seule
  adresse, bien plus vite qu'un hôte servi ne l'admet. La pile de dev relève l'admission
  des rendus du SSR (`LODB_*RENDER*`), la porte froide des catalogues
  (`LoDb__Catalog__ColdLoad*`, `PublicColdLoadsPerSecond`), les flux du warm-up
  (`LoDb__Catalog__WarmUp__*`) et le budget d'ingestion des visiteurs
  (`LoDb__Ingestion__ColdDatasetsPerMinute`, `ColdImagesPerMinute`,
  `MaxWaitingIngestions`, `SlotWait` à 20 s). Ces bornes se testent en xUnit avec leurs
  valeurs de production, jamais par l'E2E.
- **Limite de connexions** : 10 connexions par adresse et par 15 min
  (`LoDb:RateLimiting:SignInPerWindow`), et toute la suite se connecte par
  `POST /api/account/login` depuis une seule adresse (`support/member-account.ts`, les
  administrateurs des specs `admin`) : `compose.override.yaml` relève la borne pour la pile
  locale, jamais `compose.deploy.yaml`. Les 4 échecs par identifiant et par adresse ne se
  relèvent pas (ils restent sous le verrouillage à 5) ; les tests xUnit gardent les valeurs
  de production.
- **Âge minimal pour voter** : un compte vote 24 h après sa création
  (`LoDb:Builds:Votes:MinimumAccountAge`), et le compte de chaque worker est créé au début
  du passage : `compose.override.yaml` ramène l'âge à zéro pour la pile locale, jamais
  `compose.deploy.yaml`. L'e-mail vérifié reste exigé (le compte du worker l'est). Les
  limites horaires par compte (`LoDb:RateLimiting:CheckoutsPerHour` 10,
  `ApiKeyIssuesPerHour` 5, `BuildEditsPerHour` 60, `VotesPerHour` 120) ne sont pas
  relevées : la suite reste loin en dessous ; un `429 rate-limited` sur un build, une clé
  ou un vote se cherche d'abord là.
- Comparer le chemin d'une réponse (`new URL(url).pathname`), jamais `url.endsWith(...)`.
  Limiter les comptes de titres ou de boutons à `main` ou au composant visé. Une spec qui
  compte les POST exclut `/api/analytics/` ; `/u/api/send` ne part jamais d'une pile locale
  (`specs/public/audience.spec.ts`). Un parcours qui crée un compte le supprime aussi en cas
  d'échec.
- Angular laisse des espaces autour d'un texte entre balises : ancrer une regex sur le
  contenu, pas sur `^`/`$`.
- `disclosure.spec.ts` échoue parfois en suite complète, jamais seul : relancer n'est pas
  une correction.
- **Bornes du SSR relevées en dev** (`compose.override.yaml`, jamais
  `compose.deploy.yaml`) : toute la suite arrive d'une adresse et ouvre les anciens patchs
  bien plus vite qu'un hôte servi ne les admet. Admission : `LODB_RENDER_MAX_IN_FLIGHT`,
  `LODB_PINNED_*`, `LODB_CURRENT_*` et la part d'un client (`LODB_RENDER_PER_CLIENT_*`,
  `LODB_PINNED_RENDERS_PER_CLIENT_PER_MINUTE`, `LODB_CURRENT_RENDERS_PER_CLIENT_PER_MINUTE`,
  `LODB_CURRENT_RENDER_PER_CLIENT_BURST` : sans elles, la suite entière n'aurait qu'un rendu
  par seconde des pages du dernier patch). Attente de l'API par un rendu :
  `LODB_RENDER_DEADLINE_SECONDS` et `LODB_RENDER_API_TIMEOUT_SECONDS` à 30 s, car la
  première visite d'un patch attend son ingestion. Une 503 ou une 502 du SSR en E2E se
  cherche d'abord là.
- Les instantanés de `tests/LoDb.E2E/test-results/` sont écrasés au passage suivant.
- **Débits par adresse de nginx relevés** : toute la suite arrive sous une seule adresse (la
  passerelle Docker), plus vite qu'un visiteur. `compose.override.yaml` relève
  `LODB_PAGES_*`, `LODB_API_READS_*`, `LODB_LEGACY_*` et `LODB_SITEMAPS_*` (débit et rafale) ;
  jamais `compose.deploy.yaml`. Pour retrouver un défaut, retirer la clé (`!reset null`) :
  une valeur vide fait refuser la configuration de nginx.
- **Tout relèvement ajouté à `compose.override.yaml` s'annule aussi dans
  `tools/load/compose.bench.yaml`**, le calque du banc de charge, sinon le banc mesure une pile
  sans borne : `tools/load/test/compose-bench.test.mjs` échoue tant qu'une clé relevée garde sa
  valeur relevée. Compose n'applique pas le `!reset` par clé d'un troisième fichier à une clé
  que le deuxième a ajoutée : le calque redonne l'entrée de `compose.yaml` (SSR, nginx) ou
  nomme la clé sans valeur (API, comme `compose.deploy.yaml`). Le banc ne tourne jamais sur le
  staging, qui partage les vCPU de la prod ([`tools/load/`](../../tools/load/README.md)).

## Infra et déploiement

- Variables Compose préfixées **`LODB_`**. En dev, les images s'appellent
  `<projet>-<service>` : un emplacement n'écrase pas les images de la stack locale.
- **`!reset` et l'ordre des fichiers** : Compose ignore un `!reset` placé après un autre
  fichier qui touche déjà l'`environment` du même service. Les `!reset null` de
  `compose.deploy.yaml` (`KnownProxyNetworks__1`, `__2`) ne tiennent donc que s'il suit
  directement `compose.yaml` : un calque d'hôte (staging…) se place **après** lui. Sinon,
  l'API croirait encore les plages privées de `compose.yaml`, celles des réseaux partagés
  comprises ; le déploiement le vérifie sur la configuration résolue et s'arrête.
- **Promotion par digest** : `_promote.yml` refuse si `:staging` ne désigne plus
  `:staging-validated` (un build de `dev` en cours, un déploiement de staging en échec) ou si
  la révision servie n'a pas le code de `main`. Voulu : ne pas revenir à un retag de
  `:staging`. Une fois staging vert, relancer le run refusé (*Re-run failed jobs*) ; refusionner
  `test` dans `main` ne lance rien quand `main` le contient déjà.
- Sur l'hôte, la stack occupe le projet de l'ancienne (`lodb-staging`, `lodb-prod`) : elle
  reprend son volume `pgdata` et range ses blobs dans `ddragon`. Le volume `storage` de
  l'ancienne stack (propriété de `www-data`) n'est ni réutilisé ni supprimé : le nommer
  `storage` rendrait le stockage de l'API non inscriptible.
- nginx : sous-domaine reconnu par `server_name ~^api\.` ; CORP `same-origin` sauf
  `/cdn/blobs/` en `cross-origin` (coquille Android) ; `access_log off` voulu (l'edge
  journalise) ; tous les `.conf` de `sites/`, `server.d/` et `snippets/` passent par
  `envsubst`, filtré sur `^LODB_` (défauts dans le `Dockerfile`, jamais une valeur vide, sauf
  `LODB_EDGE_SECRET`). Rendus dans `/tmp/{sites,server.d,snippets}`, que
  `/etc/nginx/{sites,server.d,snippets}` désignent par un lien : les `include` gardent leurs
  chemins. Une variable `LODB_*` de nginx passe par `compose.yaml` avec son défaut
  (`${LODB_X:-défaut}`), **jamais** en clé sans valeur dans un calque comme les réglages de
  l'API : Docker effacerait le défaut `ENV` de l'image et nginx refuserait de démarrer.
  `server.d/legacy-redirects.conf` envoie à `/api/legacy` les premiers segments de l'ancien
  site : tout nouveau chemin hors locale se vérifie contre cette liste. `server.d/seo.conf`
  possède `/sitemap.xml`, `/sitemaps/`, `/robots.txt` et `/llms.txt`.
- nginx, bornes (`sites/site.conf`) : `limit_req` par adresse sur les pages, les lectures
  publiques de l'API, l'ancien site, `/sitemaps/` et `/readyz` ; **jamais** sur les autres
  `/api/` (actions d'un compte, ADR 0009), `/v1/`, `/webhooks/`, `/build/`, `/cdn/`. Une
  location qui vise une route de l'API se compare **sans casse** (`~*`) : l'API route
  `/api/Catalog/…` comme `/api/catalog/…`, que `location /api/` laisserait passer. Le 429
  vient de `snippets/server-guards.conf` : une location qui déclare son propre `error_page`
  doit répéter `error_page 429`. `/api/catalog` et `/api/meta` ne sont **jamais** mis en
  cache par nginx (un HIT contournerait le `426` des apps). Tous les caches sont des volumes :
  `lodb_pages`, `lodb_seo` et `lodb_legacy` sous une clé préfixée par la révision (un
  déploiement repart de zéro ; en local, la révision `dev` les garde d'un rebuild à l'autre :
  `down -v`), `lodb_archive` sous l'epoch, `lodb_assets` (bundles de `/build/`) sous `$uri`
  seul.
  `/cdn/blobs/` ne sert que `{sha256}.{png,jpg,jpeg,webp,gif,webm,mp4}` : une nouvelle
  extension de blob s'ajoute à la regex **et** à `BlobMediaTypes` (ingestion), qui refuse tout
  fichier hors de cette liste ou dont les premiers octets ne sont pas de son format
  (enregistré absent, une ligne `ingest.images.refused` par lot). La garde des extensions de scripts
  (`snippets/server-guards.conf`) épargne les chemins de profil (`/u/`, `profiles/`) : un
  nom d'utilisateur peut finir en `.bak` ou `.ini`. Vérification : `tools/nginx/`
  (`node --test`).
- **Secrets en fichiers** (`compose.deploy.yaml`, `tools/deploy/split-env.sh`) : sur un hôte,
  aucun secret sous `environment:` (une clé sans valeur l'y ramènerait, visible par
  `docker inspect`) ; un nouveau secret s'ajoute à `SECRET_KEYS` **et** aux `secrets` du
  calque (`tools/deploy` vérifie l'accord). Le job écrit un fichier par secret, vide s'il est
  absent : Docker monterait un fichier manquant comme un dossier, et l'API lit un fichier
  vide comme un réglage absent. Un secret tiré sur l'hôte (`GENERATED_KEYS`, le mot de passe
  de `lodb_app`) n'est jamais dans `ENV_*` : supprimer son fichier en tire un autre au
  déploiement suivant. Un conteneur garde le fichier monté à sa création et l'API
  ne lit ses fichiers qu'au démarrage : le label `LODB_SECRETS_REVISION` de `api` est ce qui
  la fait recréer quand un secret change, ne pas le retirer. `POSTGRES_PASSWORD_FILE` ne
  sert qu'à la création d'une base (volume vide).
- **Conteneurs durcis, en local comme sur un hôte** (`x-hardening` de `compose.yaml`) :
  système de fichiers racine en lecture seule, aucune capacité (`cap_drop: [ALL]`), pas
  d'élévation par un binaire setuid (`no-new-privileges`). Un service n'écrit que dans ses
  volumes et ses tmpfs (`/tmp`, et `/var/run/postgresql` pour Postgres) : un nouveau chemin
  écrit va dans l'un ou l'autre, sinon `EROFS` au premier essai. Un `docker compose exec`
  n'écrit pas ailleurs non plus. Postgres reprend `CHOWN`, `DAC_OVERRIDE`, `FOWNER`,
  `SETGID` et `SETUID` (volume neuf comme existant) : son entrée tourne en root le temps de
  donner ses dossiers à `postgres` ; sans `FOWNER`, son `chmod` du dossier des données
  échoue et écrit une ligne d'erreur à chaque démarrage. Jamais `DAC_READ_SEARCH` à la place
  de `DAC_OVERRIDE` : Docker la retire par défaut, et son profil seccomp ouvre alors
  `open_by_handle_at` au conteneur. Un tmpfs compte dans la limite mémoire du
  conteneur.
- **nginx sans privilège, sur le port 8080** (utilisateur `nginx` de l'image, maître compris) :
  sonde, smoke tests du déploiement, labels de l'edge (`{{upstreams 8080}}`), port publié en
  local (`LODB_HTTP_PORT` → 8080) et banc de charge (`http://nginx:8080`) le visent.
  `absolute_redirect off` : une redirection écrite par nginx garde un `Location` relatif, qui
  sinon porterait `http://` et le port 8080. Pid et configuration rendue dans `/tmp` ;
  fichiers temporaires (corps de requête, réponse qu'un client lent n'a pas encore lue) sur
  le volume `nginx-temp`, jamais dans le tmpfs : quelques lecteurs lents de grosses réponses
  le rempliraient, et toute réponse plus grande que les tampons échouerait ensuite.
- **En-tête de l'edge** (`X-LoDb-Edge`, `sites/site.conf`) : sur un hôte, nginx répond `403` à
  toute requête qui ne le porte pas avec la valeur de `LODB_EDGE_SECRET`, `/healthz` excepté,
  et ne le transmet jamais à l'amont. L'edge le pose (labels `caddy_*.reverse_proxy.header_up`),
  le job de déploiement génère la valeur une fois par hôte (`.deploy/secrets/LODB_EDGE_SECRET`)
  et l'écrit dans le `.env`. Vide (local, E2E, banc), rien n'est exigé. Un appel à la main
  dans la stack d'un hôte le prend dans l'environnement de nginx :
  `docker compose exec -T nginx sh -c 'wget -qO- --header "X-LoDb-Edge: $LODB_EDGE_SECRET" …'`.
  Un `403` venu de nginx sur tout le site : labels de l'edge pas encore relus après un `up`
  (quelques secondes), ou valeur changée d'un seul côté.
- **HTML compressé une fois, par le SSR** (`src/server/compression`, gzip `6`) : les caches
  de pages de nginx demandent `Accept-Encoding: gzip` à l'amont quel que soit le client,
  gardent cette seule copie et la décompressent (`gunzip`) pour un client sans gzip. Le
  `Vary` de l'amont est ignoré par le cache et masqué ; `Vary: Accept-Encoding` vient de
  nginx (`gzip_vary`), une fois : ni `add_header Vary`, ni retrait de `proxy_hide_header`.
  Le SSR compresse **après** le calcul de la CSP, qui hache le HTML rendu.
- `gzip_types` de `docker/nginx/nginx.conf` doit lister `text/javascript` et
  `application/manifest+json` (sinon ~1,7 s perdues en 4G lente). Un budget Lighthouse ne
  se relâche jamais ; Lighthouse en local est pessimiste (cache de transfert du SSR rangé
  sans le port).
- **Pages des anciens patchs** (`location ~ "^/{locale}/[0-9]"`, `snippets/archive-cache.conf`) :
  clé **sans révision**, préfixée par `LODB_ARCHIVE_EPOCH`. Elles survivent aux déploiements
  et gardent jusqu'à deux semaines le chrome, la CSP et les bundles du build qui les a
  rendues (une semaine de `s-maxage`, puis une dernière copie périmée servie pendant son
  rafraîchissement tant que la page n'a pas atteint ses 7 jours d'inactivité) : voulu. Monter l'epoch quand le chrome, la CSP ou le contrat `/api` changent de façon
  visible, et après tout correctif de sécurité du front (procédure :
  [`configuration.md`](../guides/configuration.md)). Gardées à la **2ᵉ** demande
  (`LODB_ARCHIVE_CACHE_MIN_USES`, `2`) : `MISS`, `MISS`, puis `HIT` ; une spec qui attend un
  `HIT` les demande trois fois. En deçà, chaque URL distincte tient quand même une clé
  jusqu'à son inactivité, même une réponse non gardée (`301`, `503`) : la zone des clés borne
  les URL qu'un balayage parcourt avant d'évincer, pas le disque. En local aussi, une page d'ancien patch survit à un rebuild : `down -v` ou monter
  l'epoch. `archive-cache.conf` sert la copie périmée pendant le rafraîchissement
  (`updating`) ; `page-cache.conf` **jamais** (elle primerait sur le
  `stale-while-revalidate` du dernier patch). Les regex de location contenant `{` sont entre
  guillemets, sinon nginx refuse sa configuration.
- **Bundles des builds précédents** : `web-ssr` copie ses bundles hachés dans le volume
  `front-builds` (`LODB_FRONT_BUILDS_DIR`) sans réécrire l'existant, au démarrage puis
  **toutes les heures**, et rafraîchit leur date à chaque fois ; `/build/` sert ceux de
  l'image, puis ceux du volume. Le job de déploiement supprime ce qu'aucun build en service
  n'a livré depuis `LODB_FRONT_BUILDS_RETENTION_DAYS` (30 j) : jamais sous la durée de vie
  d'une page en cache. La date d'un fichier est celle de la dernière heure où un build l'a
  servi, pas celle du démarrage : sans le rafraîchissement horaire, un build resté en service
  plus de 30 jours perdrait ses bundles au déploiement qui le remplace. Un chunk introuvable
  recharge la page une fois (`ChunkReload`, drapeau en `sessionStorage`), puis affiche la
  500 : un rechargement en boucle se cherche là. Le volume est écrit par le SSR et survit aux
  déploiements : nginx sert `/build/` sous `default-src 'none'; sandbox` (un fichier ouvert
  directement n'exécute rien ; la politique est ignorée pour un script ou un style qu'une
  page charge), et après une compromission du SSR on vide `front-builds` et `assets-cache`
  ([`configuration.md`](../guides/configuration.md)).
- SSR : `LODB_TRUST_PROXY_HEADERS` obligatoire derrière nginx, `LODB_ALLOWED_HOSTS` liste
  les hôtes acceptés ; `public/` ne contient ni `media/` ni script racine nommé comme un
  bundle (cache `immutable`). Ses métriques sont sur un second serveur HTTP, port 9464
  (`LODB_METRICS_PORT`), que nginx ne route pas et qu'aucun port ne publie ; `/metrics`
  demandé au site reste la 404 statique du pré-routeur.
- **Porte réseau du SSR** : comme l'API, le port 4000 ne répond qu'aux réseaux de
  `LODB_STACK_NETWORKS` (le seul `LODB_STACK_SUBNET` sur un hôte, les trois plages privées
  en local) et à la loopback ; tout autre pair reçoit un 404 vide, compté par
  `lodb_ssr_network_gate_refusals_total`, sans log. Vide ou absente, la variable laisse la
  seule loopback : un `node server.mjs` lancé hors de la stack ne sert que la machine.
- Collecte (`compose.deploy.yaml`) : `api` et `web-ssr` rejoignent le réseau partagé
  `observability` avec les labels `prometheus.scrape`/`prometheus.port`. Sur ce réseau, le
  nom court d'un service (`api`, `postgres`) désigne aussi ceux des autres projets : un
  conteneur de la stack n'appelle ses voisins que par les alias préfixés (`lodb-api`,
  `lodb-postgres`, `lodb-web-ssr`).
- Constats de logs (lignes hors JSON, erreurs multi-lignes) : **à corriger à la source,
  jamais à filtrer**.
- **Umami, en prod seulement** (`compose.umami.yaml`, ajouté par le job) : l'import vise
  `caddy_0` (le site), **jamais** `caddy.import` sans numéro (hors de tout site, l'edge
  refuserait sa configuration) ni `caddy_1` (le sous-domaine de l'API). Le front ne charge
  **jamais** `/u/script.js` : le script d'Umami s'exécuterait avec les droits du site ; il
  poste lui-même `/u/api/send` (`core/analytics`), et seulement depuis l'hôte canonique :
  le staging, les piles locales et l'E2E servent le même bundle et restent muets. L'UUID du
  site est un littéral, dans le calque et dans `audience-site.ts` : `tools/umami/test`
  vérifie leur accord. Aucune adresse de compte ou d'admin ne part (`audience: 'none'`), les
  autres pages partent sous leur gabarit, sauf listes et fiches (`audience: 'path'`).

## Transition (jusqu'à la décommission)

- L'ancienne stack (projet local `lodb`) tourne depuis `legacy/` du dépôt principal
  (`docker compose --project-directory legacy …`, son `.env` est `legacy/.env`), jamais
  recréée depuis un autre dossier (label `com.docker.compose.project.working_dir`). Ses
  commandes console gardent `-u www-data`.
- **Répétition locale** : [runbook](../reecriture/bascule.md) § 3.1 tel qu'écrit,
  emplacement `lodb-dev-e2`, jamais pendant un build Android. Derrière `| tee`, lire
  `$pipestatus[1]` (zsh) ou `${PIPESTATUS[0]}` (bash). Après elle, `lodb_rehearsal` ne doit
  plus exister dans le Postgres de l'ancienne stack.
- **Parité** : `tools/parity/` collecte, `tests/LoDb.Parity` compare ; un écart nouveau
  reçoit une règle argumentée de `ParityRules.cs`, jamais un filtre. Parité des builds et
  agrégats analytics : copies dans le Postgres de l'ancienne stack, jamais sa base `lodb`.
- **Rapports générés** (`diff-seo.md`, `lighthouse.md`…) : écrits par leurs outils seuls ;
  un écart SEO nouveau reçoit une règle dans `tools/seo-diff/lib/rules.mjs`.
- Les outils de comparaison (`seo-diff`, `lighthouse`, `builds-parity`) appellent encore
  `next` le côté « nouvelle stack » : ils disparaissent à la décommission avec `legacy/`.
- **Documents historiques** (`docs/reecriture/{plan-*,heritage,adr,implementation,
  rapports}`) : ils gardent les noms de leur époque (`lodb-next`, `tools/next/`…) ; on ne
  les réécrit pas.
