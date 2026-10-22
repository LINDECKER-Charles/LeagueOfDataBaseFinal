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
- **CSP : seuls les scripts inline du build sont hachés** (`src/server/security/csp`) : ceux des
  gabarits `index.server.html` et `index.csr.html`, lus au démarrage (un gabarit absent arrête
  le serveur), plus l'appel `window.__jsaction_bootstrap(…)` du rejeu d'événements quand il
  suit **immédiatement** le contrat `ng-event-dispatch-contract`, sous sa forme exacte. Tout
  autre script inline n'est pas exécuté par le navigateur et se compte
  (`lodb_ssr_csp_unexpected_scripts_total`, ligne `ssr.csp.unexpected_scripts`). À chaque
  montée d'Angular ou de `beasties`, la spec E2E `pwa/content-security-policy.spec.ts` revérifie
  qu'aucune page ne viole sa politique : un rejeu refusé perd les clics d'avant l'hydratation,
  un chargeur refusé laisse les feuilles différées en `media="print"`. Un script inline ajouté
  à `src/index.html` est admis de lui-même ; un script ajouté au rendu ne l'est jamais.
- **Trusted Types en Report-Only** (`Content-Security-Policy-Report-Only`) : politiques
  `angular`, `angular#unsafe-bypass`, `lodb#worker` (enregistrement du service worker) et
  `lodb#json-ld` (texte des données structurées que `HeadWriter` écrit : affecter le texte d'un
  `<script>` est un puits, même `application/ld+json`) ; tout autre `innerHTML`, `eval`, texte
  ou URL de script en chaîne est signalé, rien n'est bloqué. Un
  nouveau puits (une bibliothèque, un `innerHTML` direct) fait échouer la spec E2E de la CSP :
  passer par le sanitizer d'Angular ou déclarer la politique dans `trusted-types-policy.ts`.
- **Rapports CSP** : `report-to` et `report-uri` visent `POST /api/csp-reports` (API, même
  origine, sans `AllowAnonymous` : le garde vérifie l'`Origin`). Un rapport envoyé avec le
  cookie de session sans jeton XSRF est refusé comme toute requête de ce genre : celui d'un
  visiteur connecté dont le navigateur joint ses cookies (`report-uri`) est perdu, voulu.
  WebKit (Safari, et tout navigateur sur iOS) poste chaque violation avec `Origin: null` et
  les cookies de la page : le garde la refuse (403), aucun rapport de WebKit n'arrive tant que
  ce choix tient. L'API lit pourtant son format (un rapport seul, sous `application/csp-report`).
  Rien de la page ni du client n'est gardé ; une ligne `web.csp.violated` par minute. Sur la
  pile locale (http), Chromium n'enregistre pas l'endpoint de `Reporting-Endpoints` (il le veut
  en https) : ses rapports restent en file et n'arrivent jamais, voulu. L'E2E vérifie l'absence
  de violation par l'événement `securitypolicyviolation`, qui précède tout rapport, les tests
  xUnit l'endpoint (`CspReportEndpointTests`) ; la livraison réelle se vérifie sur le staging.
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
- **Historique des patchs** (`/{locale}/patches`) : par version listée, sa page de
  changements quand elle en a une, puis ses quatre listes, des centaines de versions.
  Balisage nu et `ViewEncapsulation.None` voulus (l'attribut d'encapsulation pesait un tiers
  de la page) : ni attribut, ni classe, ni pipe par lien. Une fiche d'ancien patch est
  indexée une fois par état (ADR 0005, amendement du 2026-10-22) : l'ouvreur d'un état clos
  est sa propre canonique, les autres versions de l'état désignent l'ouvreur dans la même
  langue, sans hreflang ni JSON-LD, et une version de l'état ouvert désigne l'URL courte :
  voulu, « Autre page avec balise canonique correcte » monte dans la Search Console.
  L'API décide la canonique (`StateView`) depuis l'index des états et le dernier patch du
  lecteur de catalogues, jamais celui de l'index : une page à ce patch est toujours sa
  propre canonique. Une version que l'index ne couvre pas encore (index en retard sur une
  promotion) compte comme l'état ouvert ; hors de l'index (plus ancienne que sa couverture,
  listée après le dernier patch, avant toute promotion), la règle résiduelle donne l'URL
  courte si le dernier patch tient l'entité, sinon la page elle-même. Index illisible et pas
  de copie en mémoire : la fiche et `…/changes` répondent 503 avec `Retry-After`. Le
  `{version}.xml` d'un ancien patch ne liste que sa page de changements et les ouvreurs de
  ses états clos : un `urlset` vide reste servi, mais l'index des sitemaps ne l'annonce
  pas. Le bloc « Historique » se lit dans l'index seul ; l'ouvreur lit en plus le catalogue
  de la dernière version de l'état précédent, dans sa langue : épinglé, il répond 503 tant
  que ce catalogue n'est pas stocké, jamais rendu sans son bloc ; l'URL courte sort alors
  sans le bloc. Le bloc liste toujours les champs que l'index marque changés, même sans
  valeur dans la langue de la page. Si Data Dragon ne livrait pas encore cette langue à la
  version comparée, son catalogue tient l'en_US : seuls les nombres, codes, ids et chaînes par
  rang des sorts y gardent leur valeur avant, un texte ou un nom s'affiche avec sa seule
  valeur après (voulu, `ComparedProjection`). Une ancienne URL du site qui nomme une version
  mène à la canonique de la page qu'elle nommait : l'ouvreur de l'état clos, avec le slug de
  cette version, ou l'URL courte de l'état ouvert ; sans version, un champion, un objet ou
  un sort absent du dernier patch mène à l'ouvreur de son dernier état. Une voie de runes
  absente du dernier patch garde la règle du 2026-10-16 (sa page épinglée de la version
  nommée, 404 sans version) : l'ancien site la nommait par sa clé, que seul le catalogue du
  dernier patch traduit en id pour l'index (amendements du 2026-10-16 et du 2026-10-22).
  L'index des sitemaps n'annonce, après les `latest.xml`, que les sitemaps des versions
  couvertes passées dont le fichier n'est pas vide (~8 440 entrées au plus), lus dans la copie
  de l'index sans catalogue ; une version pas encore couverte (index en retard), plus récente
  que le dernier patch ou d'avant toute promotion répond 404, et `latest.xml` n'ajoute
  `/{locale}/changes` que si l'index couvre le dernier patch et qu'il a changé quelque chose.
  Une page figée (état clos) ne lie que des URL versionnées, sauf « Dans le dernier patch »,
  qui devient une 404 si l'entité est retirée ensuite, et « Changé au patch X » mène à une 301
  tant que X est le dernier patch : voulu. Une page révisable (état ouvert) ne nomme aucune
  version qu'une promotion change (« depuis le patch X »). Le premier état d'une entité au
  début de la couverture est `baseline` : ni résumé, ni pastille, ni bloc. Les blocs ne lisent
  que la réponse de la fiche, jamais `/api/meta` (les specs des fiches ne la fournissent
  plus). Une page de patch (`/{locale}/[{version}/]changes`) n'existe pas pour la
  première version couverte ni hors de l'index : sur elle, la version mémorisée ou choisie dans
  le sélecteur ne s'applique qu'entre `oldestChangesVersion` et `newestChangesVersion` (la fin
  de la couverture : le dernier patch, sauf tant que l'index est en retard sur sa promotion ;
  `core/api/meta/has-changes-page.ts`, la règle de `/patches`), sinon la page reste sur le
  dernier patch.
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
- **Hydratation incrémentale** : vitrine des sorts, stats, pager des fiches, pied de page et
  console de filtres sont rendus en entier par le serveur mais hydratés plus tard
  (`@defer (on immediate; hydrate on …)`). D'ici là, leurs éléments gardent leur `jsaction`
  et un clic n'agit qu'une fois rejoué, après l'hydratation : voulu. **Mais** Angular (22.2)
  ne rejoue qu'à la fin d'une hydratation lancée par un événement : un clic qui tombe pendant
  une hydratation lancée par `viewport`, `idle` ou `when` reste en file, perdu
  (`triggerHydrationFromBlockName` sort sans rejouer quand le bloc s'hydrate déjà). Le pager
  et le pied de page, qu'on clique à peine atteints, s'hydratent donc au repos
  (`hydrate on idle`), hors de la vue et avant d'être atteints : jamais `viewport`, même avec
  une marge (un saut en bas de page ou le défilement d'un clic Playwright y tombent). Ils se
  mesurent une fois hydratés : en vue, leur ajustement deviendrait un décalage visible (CLS). Un bloc déshydraté n'est jamais mis à
  jour : la console s'hydrate donc aussi dès que sa copie serveur est périmée (liste entière
  lue, état changé hors d'elle : filtre retiré des résultats, autre langue), sinon elle
  garderait les comptes de la première page. La console se lit par sa référence
  (`#console`), jamais par sa classe, qui la chargerait avec la liste.
- **Liste complète à l'intention** : une liste ne lit sa liste entière qu'à l'intention du
  lecteur (recherche, facette, page, taille, feuille des filtres, URL filtrée) ou au repos
  après `load`, jamais d'office sous `Save-Data` ou en 2g/3g : une liste encore réduite à sa
  première page juste après le chargement n'est pas une régression. Même règle pour la vidéo
  d'un sort (poster seul) et le préchargement des chunks du catalogue.
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
- **Les dialogues de l'admin vivent hors de sa page.** Le CDK les monte sous `<body>` :
  les règles de `admin-page.css` et `admin-frame.css` ne les atteignent que par `.admin-overlay`,
  que chaque composant de dialogue de l'admin porte sur son hôte. Un nouveau sélecteur de ces
  feuilles s'écrit `:is(lodb-admin-page, .admin-overlay) …`.

## API, données et comptes

- `/readyz` se lit sur l'API (18081 ou `lodb-api:8080`) et, par nginx, sur le sous-domaine `api.`
  seulement (1 requête/s par adresse, sonde externe) ; demandé au site, il part au SSR (404).
- **Deux captures sans autorisation, voulues** : la balise (`POST /api/analytics/view`,
  `BeaconEndpoint.cs`) et le point d'accès du miroir de nginx (`MirrorEndpoint.cs`) portent
  `.AllowAnonymous()`. `sendBeacon` ne peut pas envoyer le jeton XSRF : la balise vérifie
  l'`Origin` elle-même, et le miroir n'est joignable que par nginx. Ce sont les seules
  exceptions à « jamais `AllowAnonymous` » d'`AGENTS.md`, tenues fermées par
  `tests/LoDb.Api.Tests/Hosting/AnonymousEndpointsTests.cs` ; elles partent avec la mesure
  maison ([ADR 0012](../reecriture/adr/0012-audience-umami.md)).
- **`UsageFlusher` tourne même avec `LoDb:Workers:Enabled=false`** : le métrage de `/v1`
  (`Modules/PublicApi/Metering/`) est enregistré par le module, hors de `Workers/`, sinon
  `api_usage` ne serait jamais alimenté.
- En local, les URLs absolues perdent le port (nginx transmet `Host: $host`) : l'origine
  canonique se fixe par `LoDb__Seo__CanonicalOrigin` et `LoDb__Accounts__SiteOrigin`.
- **Pool Postgres** : `Maximum Pool Size=32;Timeout=5` dans la chaîne de connexion. Une
  requête `/api` qui n'obtient pas de connexion à temps reçoit un **503** `service-busy`
  avec `Retry-After` : délestage voulu, non compté. Une base injoignable donne un **500**,
  compté par `lodb_db_unavailable_total`. Npgsql ne donne au pool épuisé ni type ni code :
  son message est reconnu par son texte, et `DatabaseFailureTests` le revérifie sur un vrai
  pool à chaque montée de version.
- **Rôle Postgres de l'API** : l'API et le one-shot `states` (index des états) se connectent
  comme `lodb_app` (lignes seulement, 15 s par requête), `migrate` comme le superutilisateur ([`configuration.md`](../guides/configuration.md)
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
  64 Kio (≈ 65 Kio : 2 080 caractères de nom et de description, dix étapes de 40 + 300, chacun
  hors du plan de base pesant 12 octets en `\u` échappés) : ne pas descendre à 64 Kio.
  Un endpoint qui doit lire plus pose son propre `RequestSizeLimit` (webhook Stripe, 1 Mio).
  La borne se teste dans nginx (`tools/nginx/test`) et en xUnit, sur un vrai Kestrel, en
  Production (`Hosting/Support/KestrelApi.cs`) : le serveur en mémoire n'applique aucune
  limite, et Development transforme un corps invalide en exception.
- `/api/pickers/*` exige `version` et `lang` (400 `invalid-version` sinon).
- **Porte réseau de l'API** : le port 8080 ne répond qu'au réseau de la stack
  (`LoDb:Hosting:KnownProxyNetworks`, le seul `LODB_STACK_SUBNET` sur un hôte, les trois
  plages privées en local) et à la loopback ; tout autre pair reçoit un 404 vide, compté par
  `lodb_network_gate_refusals_total`, sans log. Un conteneur d'un réseau partagé
  (`observability`) ne joint que le port 9464. Un nouveau service qui appelle l'API doit
  être sur le réseau `default` de la stack.
- **Postgres sur le seul réseau `data`**, avec `api` et les one-shots `migrate` et `states`
  (interne sur un hôte : aucune
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
- Une URL de l'ancien site qui nomme une version où l'index des états n'a pas l'entité, pour
  une entité absente du dernier patch et une version non stockée, répond `302` vers la liste
  de la version, sans rien ingérer : voulu (une entrée retirée du jeu ne devient pas une
  404). Une entité que l'index connaît à cette version mène à sa canonique sans ouvrir aucun
  catalogue que celui du dernier patch.
- **E-mails** : le modèle d'un `EmailMessage` s'écrit avec `EmailModelKeys`, jamais en
  littéral (sinon `dead` dès le premier essai). L'outbox n'envoie rien sans
  `LoDb:Mail:Host` : voulu. En local, Mailpit sur http://localhost:18025.
- **Plafond horaire de l'outbox** (`LoDb:Outbox:HourlyCaps`, 100 confirmations, 50
  réinitialisations, 20 messages de contact par heure glissante, pour tout le site) : au-delà,
  les messages restent `pending` avec 0 tentative jusqu'à ce que l'heure libère une place.
  Un e-mail « qui n'arrive pas » se cherche d'abord dans la jauge `lodb_outbox_deferred` et
  le journal `outbox.hourly_cap.reached` ; un lien retenu plus d'une heure arrive expiré.
- **Renvoi de l'e-mail de vérification** limité à 5 par compte, puis 1 par jour : un compte
  non vérifié n'est jamais effacé d'office (purge retirée le 2026-10-22).
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
  (`core/analytics/audience-tracker.spec.ts`). Un parcours qui crée un compte le supprime aussi
  en cas d'échec.
- Angular laisse des espaces autour d'un texte entre balises : ancrer une regex sur le
  contenu, pas sur `^`/`$`.
- Les blocs hydratés plus tard gardent leur `jsaction` jusqu'à leur tour : `hydrated()` vise
  un composant hydraté d'emblée, jamais la page entière. La vidéo d'un sort n'apparaît qu'une
  fois sa scène à moitié à l'écran (`scrollIntoViewIfNeeded`). Chaque page part de
  `fastConnection` (`support/network.ts`, posé par `support/test.ts`) : sans lui, Chromium
  déduit `effectiveType` des allers-retours qu'il mesure, qui varient d'une machine à l'autre
  (sous la 4g : poster sans vidéo, liste différée). Un clic sur un onglet ne fait défiler que
  l'onglet : la scène du clip peut rester sous l'écran, d'où un `scrollIntoViewIfNeeded` sur
  elle avant d'attendre la vidéo.
- `disclosure.spec.ts` (Vitest, `core/layout/disclosure/`, pas un E2E) échoue parfois en
  suite complète, jamais seul : relancer n'est pas une correction.
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
- **L'index des états d'une stack neuve ne couvre que son dernier patch.** Le service `states`
  tourne avant toute promotion, la première promotion indexe ce qu'elle trouve, et un ancien
  patch ingéré ensuite à la demande n'y entre pas : seule la commande `catalog states build`
  reconstruit quand le début de la couverture recule. Les specs qui choisissent leurs pages par
  l'état (`support/states.ts`) échouent alors en le disant, et `check-urls.sh` saute ses lignes
  d'états : `tools/states/index-past-patches.sh` d'abord, comme la CI
  ([`developpement.md`](../guides/developpement.md)).
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

- Toute nouvelle variable Compose est préfixée **`LODB_`** ; les plus anciennes
  (`IMAGE_TAG`, `REGISTRY`, `APP_REVISION`, `CONTACT_RECIPIENT`…) gardent leur nom, comme
  toutes les conventions devops de la bascule. En dev, les images s'appellent
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
  `/cdn/blobs/` en `cross-origin` (coquille Android) ; COOP `same-origin` et
  `Permissions-Policy` (caméra, micro, position, paiement… refusés ; plein écran, lecture
  automatique, partage et presse-papiers laissés) sur toute réponse : une fonction nouvelle du
  navigateur se vérifie contre cette liste (`snippets/security-headers.conf`). Chaque requête
  relayée part avec un `traceparent` neuf tiré de `$request_id` (`snippets/proxy-headers.conf`),
  celui d'un client est **remplacé**, jamais continué, et `tracestate`/`baggage` retirés : le
  SSR le relaie à l'API, dont l'échantillonnage ne lit que l'identifiant ; `access_log off`
  voulu (l'edge journalise) ; tous les `.conf` de `sites/`, `server.d/` et `snippets/` passent
  par `envsubst`, filtré sur `^LODB_` (défauts dans le `Dockerfile`, jamais une valeur vide,
  sauf `LODB_EDGE_SECRET`). Rendus dans `/tmp/{sites,server.d,snippets}`, que
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
- **Pages des anciens patchs** (`location ~ "^/{locale}/[0-9]"`,
  `snippets/pages-store/pages.conf`, `snippets/archive-cache.conf`) : le cache chaud
  `lodb_archive` d'abord, puis le stockage objet (ADR 0011), puis le SSR. Clé **sans
  révision**, préfixée par les deux epochs (`LODB_ARCHIVE_EPOCH.LODB_ARCHIVE_SERVE_EPOCH`,
  § « Catalogue pré-rendu » ci-dessous). Elles survivent aux déploiements et gardent le
  chrome, la CSP et les bundles du build qui les a rendues aussi longtemps que leur
  `Cache-Control` le dit (`snippets/pages-store/pages.conf` suit l'objet du stockage) : un an
  pour une page d'état clos (`archived`), une heure pour une page de l'état ouvert
  (`revisable`), tant que la page est demandée avant ses 90 jours d'inactivité, puis une
  dernière copie périmée servie pendant son rafraîchissement : voulu. `proxy_cache_valid 200
  365d` ne vaut plus qu'en repli, pour un objet sans `Cache-Control` ; les bundles
  (`bundles.conf`) gardent l'année quoi que dise leur objet. Monter
  `LODB_ARCHIVE_EPOCH` quand le chrome, la CSP ou le contrat `/api` changent de façon visible,
  après tout correctif de sécurité du front, et après des pages recalculées en place dans
  l'epoch servie (nouveau schéma d'empreinte). Elle ne vide que le cache : une fois que nginx
  lit le stockage (`LODB_PAGES_STORE_ENABLED=1`), la requête suivante y reprend la page d'avant,
  chrome, CSP et bundles compris. Le changement passe alors par une passe sur une epoch neuve
  et la bascule de `LODB_ARCHIVE_SERVE_EPOCH`, tout de suite pour un correctif de sécurité
  (procédure : [`configuration.md`](../guides/configuration.md), « Monter l'epoch des anciens
  patchs »). Gardées dès la **1ʳᵉ** demande
  (`LODB_ARCHIVE_CACHE_MIN_USES`, `1`) : `MISS`, puis `HIT`, la copie venant du stockage
  plutôt que d'un rendu. Chacune des deux locations cherche la page dans le cache : une page
  absente du stockage compte deux recherches par requête, à retenir avant de remonter le seuil
  (à `2`, un rendu du SSR serait gardé à la 1ʳᵉ demande, une page du stockage à la 2ᵉ).
  Au-dessus de `1`, chaque URL distincte tient une clé jusqu'à son inactivité, même une réponse
  non gardée (`301`, `503`) : la zone des clés borne les URL qu'un balayage parcourt avant
  d'évincer, pas le disque. En local aussi, une page d'ancien patch survit à un rebuild et au
  vidage du stockage simulé : `down -v` ou monter l'epoch. `archive-cache.conf` sert la copie
  périmée pendant le rafraîchissement (`updating`) ; `page-cache.conf` **jamais** (elle
  primerait sur le `stale-while-revalidate` du dernier patch). Les regex de location contenant
  `{` sont entre guillemets, sinon nginx refuse sa configuration.
- **Bundles des builds précédents** : `web-ssr` copie ses bundles hachés dans le volume
  `front-builds` (`LODB_FRONT_BUILDS_DIR`) sans réécrire l'existant, au démarrage puis
  **toutes les heures**, et rafraîchit leur date à chaque fois ; `/build/` sert ceux de
  l'image, puis ceux du volume. Le job de déploiement supprime ce qu'aucun build en service
  n'a livré depuis `LODB_FRONT_BUILDS_RETENTION_DAYS` (400 j, 395 au moins) : jamais sous la
  durée de vie d'une page en cache (un an). La copie des builds sous `build/` du stockage
  (ADR 0011) ne la remplace pas : elle ne couvre que les builds dont la passe a écrit une page,
  pas ceux des pages que le SSR rend sur un raté et que `lodb_archive` garde. La date d'un
  fichier est celle de la dernière heure où un build l'a
  servi, pas celle du démarrage : sans le rafraîchissement horaire, un build resté en service
  plus de 400 jours perdrait ses bundles au déploiement qui le remplace. Un chunk introuvable
  recharge la page une fois (`ChunkReload`, drapeau en `sessionStorage`), puis affiche la
  500 : un rechargement en boucle se cherche là. Le volume est écrit par le SSR et survit aux
  déploiements : nginx sert `/build/` sous `default-src 'none'; sandbox` (un fichier ouvert
  directement n'exécute rien ; la politique est ignorée pour un script ou un style qu'une
  page charge). Un bundle retiré du volume et du cache de nginx **revient du stockage**
  (`@lodb_build_store`, sur la 404 du SSR) tant qu'il reste sous `build/`, qu'aucune règle de
  cycle de vie n'efface et que la clé d'écriture ne supprime pas : un correctif de sécurité l'y
  retire d'abord, avec la clé de l'opérateur, puis purge le cache. Après une compromission du
  SSR, on vide `front-builds`, `build/` du stockage puis `assets-cache`, on oublie
  `prerender_build` (en base, et dans l'API par un redémarrage) et on bascule sur une epoch
  neuve ([`configuration.md`](../guides/configuration.md)).
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
- Collecte (`compose.deploy.yaml`) : `api`, `web-ssr` et `web-ssr-prerender` rejoignent le réseau partagé
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
- **Sauvegardes hors hôte, en prod seulement** (`compose.sauvegarde.yaml`, ajouté par le
  job) : le réseau `sauvegardes` appartient à `infra-vps`, qui le crée même en sommeil ; le
  job s'arrête s'il manque, **ne pas** le créer depuis LoDb. Le rôle `lodb_sauvegarde` lit
  tout (`pg_read_all_data`) et n'écrit que dans `backup_run` : ce droit vient de
  `schema-privileges.sql`, **jamais** d'un `GRANT` à la main, que le `migrate` suivant
  reprend. Un secret vide ne retire pas le mot de passe du rôle
  ([`sauvegardes.md`](../guides/sauvegardes.md), « Retirer »).
- **Page de maintenance** (`server.d/maintenance.conf`, `docker/nginx/maintenance/`) : tant que
  `.deploy/maintenance/active.json` existe sur l'hôte (monté en lecture seule sur
  `/srv/maintenance`), nginx répond **503** à chaque page — `/`, une locale, `/b/`, `/admin` —
  par la page de son image ; l'API, `/v1/`, les webhooks, les blobs, `/build/`, les fichiers
  des robots et les 301 de l'ancien site gardent leur réponse, voulu. Le job le pose juste avant
  d'arrêter un conteneur et le retire après les sondes (`tools/deploy/maintenance.sh`) ; un
  échec entre les deux **laisse la page**, qu'on retire à la main une fois la stack rétablie.
  Sans le dossier (pile locale, E2E), l'interrupteur n'existe pas. Le premier déploiement qui
  apporte la page ne l'affiche pas (l'ancien nginx ne la connaît pas). Pendant la recréation de
  nginx lui-même, quelques secondes, c'est l'edge qui répond. Ses textes vivent dans
  `assets/messages.js` (21 locales), ses couleurs et ses faces recopient `src/styles`
  (`tools/nginx/test/maintenance-sources.test.mjs` tient l'accord) : un jeton de thème changé
  côté site se recopie là, sinon le test échoue.
- **`states` passe avant la page de maintenance.** Le job lance `docker compose run --rm
  migrate`, puis `docker compose run --rm --no-deps states` (`catalog states build`), puis
  seulement `maintenance_on` et `up` (`tools/deploy/test/deploy-checks.test.mjs` tient cet
  ordre). L'ancienne pile sert pendant la construction (les tables de l'index sont additives,
  l'image en place les ignore), un échec arrête le job pendant qu'elle sert encore, et le
  passage de `states` dans `up` ne fait plus rien. `--no-deps` : `migrate` vient de tourner.
  Jamais `--ingest-missing` au déploiement : `states` monte `ddragon` en lecture seule.
- **Data Dragon injoignable au déploiement** : `catalog states build` lit la liste des versions
  de Data Dragon (le cache de l'hôte n'est qu'en mémoire), sauf tant qu'aucune version n'est
  promue. Une génération de l'index est courante : il ne l'attend pas (code 0, ligne
  `states.build.deferred`) et l'API sert l'index en retard jusqu'à sa réconciliation. **Aucune**
  n'est courante (premier déploiement de la règle par état, nouveau schéma d'empreinte) : code 1
  (`states.build.failed`), le déploiement s'arrête avant la page de maintenance. Voulu : une API
  qui démarrerait sur un index vide servirait la règle résiduelle partout, sans historique dans
  les sitemaps ; ne pas « corriger » en différant.

## Index des états d'entité

L'index qui décide la page indexée de chaque fiche d'ancien patch
([spécification](../produit/seo-une-page-par-etat.md), ADR 0005 et ADR 0003, amendements du
2026-10-22) : tables `catalog_state_*`, écrites par `LoDb.Ingestion/Catalog/States/`.

- **Ce qui change l'empreinte change l'index.** L'empreinte d'une entité
  (`EntityFingerprint`, schéma `FingerprintScheme.Current`) se calcule sur les entrées du
  domaine du catalogue en_US **stocké**, vues par les projections de
  `LoDb.Domain/States/Projections/`. Changer une projection, un gabarit de `ChangeField`,
  `DdragonText.PlainText` ou la sérialisation, c'est monter `FingerprintScheme.Current` : une
  génération neuve, puis les pages des états qui changent pré-calculées de nouveau
  (spécification, § 3.7). Un **mapper d'ingestion** qui change les entrées stockées change
  l'entrée de l'empreinte **sans** changer de schéma : réingérer l'en_US de toutes les versions
  (ou neutraliser l'écart dans la projection), puis `catalog states build --rebuild`. Sinon les
  versions ingérées avant et après le changement s'opposent, et la frontière ouvre de faux
  états.
- **Balisage, traduction, icône : aucun état.** `PlainText` lit un texte comme la page :
  `<br>`, `<hr>` et les deux bouts de `li`, `p`, `ul`, `ol`, `maintext`, `stats`, `rules`,
  `flavortext` font un saut de ligne, les autres balises et les commentaires disparaissent, un
  `<` qui n'ouvre pas de balise reste du texte, les entités sont décodées, les blancs réduits et
  les sauts répétés repliés : un `<br>` de plus ou de moins n'ouvre rien. L'empreinte ne lit que
  l'en_US (une traduction corrigée seule n'ouvre rien) et aucune image (une icône redessinée
  non plus). Voulu.
- **Chromas comptées comme skins** (limite acceptée le 2026-10-22). Data Dragon liste les
  chromas parmi les skins ; l'ingestion les retire d'après le catalogue des chromas de
  CommunityDragon (celui du patch, sinon `latest`, sinon aucun : `ChromaSkins.Without`). Un patch
  ingéré avant que CommunityDragon connaisse une nouvelle chroma ouvre donc un état pour elle
  seule (`skins`, `skin.<id>.name`), refermé par le patch suivant qui la filtre ; CommunityDragon
  injoignable, toute chroma compte comme un skin. `ChromaSourcePairTests` épingle ce
  comportement. Correctif prévu, pas fait : lire `parentSkin` de Data Dragon dans le mapper des
  skins, réingérer l'en_US, puis `catalog states build --rebuild` (point précédent).
- **Champions réduits à leur résumé** (vieilles versions sans `championFull`, fichier en 403) :
  sorts, passif, lore et skins vides ; l'empreinte voit tout disparaître puis revenir, deux
  états de plus. Accepté.
- **Premier état au début de la couverture : `baseline`.** Il n'a été comparé à rien : l'API
  le dit `baseline` (`EntityStateSnapshot.KindOf`), sans `changes`, et la fiche n'affiche ni
  résumé, ni pastille, ni bloc ; la version de début n'a pas de page de patch (404). L'index,
  lui, le range comme un état nouveau (`previous_opener_version` nul). Les runes n'ont aucun
  état avant 7.22.1 : un jeu définitivement absent n'étend ni n'interrompt un état.
- **Clé repliée, chemin de l'ouvreur.** La clé d'une entité est son id en minuscules
  (`EntityKey` : `FiddleSticks` et `Fiddlesticks` sont une seule entité) ; le chemin stocké
  garde la casse et le slug de la version ouvreuse. Toute lecture replie de même ; la file des
  états clos rend chaque version sous la graphie de son propre catalogue.
- **Écrit avec le rôle de l'API** (`lodb_app` : lignes seulement, 15 s par requête), une
  transaction par version, sous le verrou consultatif `catalog:states`, essayé toutes les
  0,5 s : 1 minute pour la commande (puis code 1), 5 s pour la promotion et la réconciliation,
  qui rendent la main sans échec (`states.update.locked`). Ne pas relever le délai du rôle pour
  une reconstruction : elle avance version par version et reprend où elle s'est arrêtée.
- **Générations.** Une reconstruction écrit une génération neuve à côté de la courante, la rend
  courante en une transaction, puis supprime l'ancienne ; l'API ne lit que la génération
  courante du schéma qu'elle compile, et celles d'un autre schéma ne sont jamais touchées (une
  image précédente lit la sienne). Une génération en attente au même début de couverture
  reprend, les autres sont supprimées, `--rebuild` les supprime toutes. **Seule la commande**
  reconstruit quand le début de la couverture recule : la promotion et la réconciliation ne
  construisent que si aucune génération n'est courante, et ne font sinon qu'étendre. Un début
  qui avance n'est jamais un historique plus court : une version listée après la fin de la
  génération courante sans son en_US (la promotion qui l'a sautée n'a pas pu l'ingérer) la
  laisse en retard (`states.coverage.interrupted`), jusqu'à ce que la promotion suivante ou la
  réconciliation l'ingère et l'étende ; ne pas « corriger » en reconstruisant depuis la version
  d'après, ce qui jetterait tout l'historique sans retour.
- **Couverture** : la plus longue suite de versions listées (liste de Data Dragon, plus le
  dernier patch promu) dont les jeux en_US sont stockés, jusqu'au dernier patch promu. Une
  version listée sans son en_US coupe la couverture : les plus anciennes sortent de l'index et
  passent à la règle résiduelle (canonique sur l'URL courte, aucun historique), sans erreur. Le
  remède : `ingest --version <v> --languages en_US`, ou `catalog states build --ingest-missing`
  dans un conteneur qui écrit `ddragon` (`docker compose run --rm --no-deps api catalog states
  build --ingest-missing`). Une version dont les quatre jeux sont définitivement absents est
  sautée, comme non listée.
- **Copie en mémoire, une par processus**, chargée à la première lecture ; son repère
  (génération, fin de couverture) est relu au plus toutes les `LoDb:Catalog:VersionsLifetime`
  (1 min) : un autre processus voit une promotion jusqu'à une minute après celui qui a écrit.
  Base injoignable : la copie tenue reste servie, revérifiée une durée plus tard. Aucune copie :
  503 `state-index-unavailable` avec `Retry-After` (fiche, `…/changes`, anciennes URL,
  `/sitemap.xml`, `latest.xml` et `{version}.xml`), ligne `states.copy.failed` et compteur
  `lodb_states_unavailable_total` ; `/api/meta` répond quand même, sans `oldestChangesVersion`
  ni `newestChangesVersion` (`catalog.meta.degraded`).
- **Première construction dans l'API** : sans aucune génération (pile locale neuve, E2E), la
  première promotion construit tout l'index dans le processus de l'API, sous ses limites. Sur
  un hôte, `states` passe à chaque déploiement : n'arrive pas.
- **Ordre de la promotion** : les écouteurs s'exécutent par étape (`PromotionStage` :
  `StateIndex`, `Prerender`, `Announce`), triés de façon stable par `VersionIngestion`, quel que
  soit leur ordre d'enregistrement ; un nouvel écouteur déclare son étape
  (`PromotionListenerTests`). Le précalcul et IndexNow lisent dans l'index ce que la promotion a
  changé ; s'il ne couvre pas encore la version, ils journalisent et s'arrêtent
  (`prerender.promotion.index_behind`, `seo.indexnow.unindexed`). La réconciliation de l'index
  (`catalog.states.reconcile`, 10 min) puis celle du précalcul (`prerender.reconcile`, 30 min)
  rattrapent ; IndexNow, jamais (les sitemaps datés prennent le relais).
- **Ouvreur épinglé et budget à froid** : l'ouvreur d'un ancien patch lit aussi le catalogue
  de la dernière version de l'état précédent, dans sa langue. Non stocké, ce catalogue consomme
  le budget des visiteurs (`LoDb:Ingestion:ColdDatasetsPerMinute`, 6 par minute) ; au-delà,
  la fiche répond 503 `catalog-pending` avec `Retry-After`, jamais rendue ni stockée sans son
  bloc. Une passe d'ouvreurs sur des versions dont l'en_US seul est stocké se voit en 503
  retentées : voulu. L'URL courte, elle, sort sans le bloc.

## Catalogue pré-rendu et CDN (ADR 0011)

Les pages des anciens patchs sont rendues une fois par la passe de l'API, rangées dans un
stockage objet, puis lues par nginx
([spécification](../produit/pre-rendu-catalogue-cdn.md),
[ADR 0011](../reecriture/adr/0011-catalogue-pre-rendu-et-cdn.md)).

- **Deux epochs du stockage, et un compteur du cache à côté.** L'epoch **servie**
  (`LODB_ARCHIVE_SERVE_EPOCH`, le dossier `pages/<N>/` que nginx lit, transmise à l'API comme
  `LoDb__Prerender__ServedEpoch` pour le panneau) ; l'epoch **d'écriture**, celle de la passe en
  cours, en base ; et `LODB_ARCHIVE_EPOCH` (défaut `3`), qui ne numérote que les clés du cache
  de nginx. La clé de `lodb_archive` porte les deux variables (`3.1|http://…`) : changer d'epoch
  servie vide aussi le cache chaud, monter `LODB_ARCHIVE_EPOCH` ne change rien au stockage.
  Monter la mauvaise laisse servir des copies périmées. nginx lit l'epoch servie au démarrage :
  la bascule est une variable d'`ENV_*` et un redéploiement, aucun point d'accès ne la fait
  (le panneau donne la consigne). L'epoch `0` est celle des sondes de `scaleway-bucket.sh
  verifier`, jamais d'une passe ; la première passe de prod écrit l'epoch `1`, servie pendant
  qu'elle se remplit. La règle par état se sert dans l'epoch `2` : à son déploiement, seule
  `LODB_ARCHIVE_SERVE_EPOCH` passe à `2` (ce qui purge déjà le cache chaud), et
  `LODB_ARCHIVE_EPOCH` reste à `3` ; l'epoch `2` part vide et se remplit par les passes et la
  file de la demande, jamais en relisant l'epoch `1`, qui porte l'ancienne règle.
- **Repli vers le SSR, sans bruit.** Toute réponse du stockage autre qu'un objet (`404` pour
  une page absente, `403` pour une politique ou une adresse en cause, `429`, `5xx`, connexion
  au-delà d'1 s, lecture au-delà de 3 s) renvoie à `@lodb_pinned_ssr`, la voie épinglée
  d'avant, à l'identique ; le corps XML de Scaleway n'atteint jamais un client
  (`proxy_intercept_errors on`). Une politique du bucket fautive ou une adresse du VPS changée
  ne produit donc **aucune erreur** : seulement une part de `X-LoDb-Source: ssr` qui reste haute
  après une passe complète, puis le retour des 503 du budget épinglé. La passe, elle, continue
  d'écrire (sa clé ne dépend pas de l'adresse). Un `418` dans `pages.conf` n'est qu'une route
  interne vers le SSR (chemin hors grammaire, query, méthode autre que `GET`/`HEAD`, dernier
  patch épinglé dans le chemin).
- **Le dernier patch épinglé (`/en/16.21.1/…`) ne passe jamais par le stockage.** Aucune passe
  ne l'écrit et le SSR le redirige en `301` vers l'URL courte ; sans règle, nginx essuierait
  d'abord une `404` de Scaleway (150 à 300 ms). nginx ne connaît le dernier patch par aucune
  variable : une promotion le change entre deux déploiements. Il le lit donc à l'exécution :
  le module njs de l'image de base relit `latest` de `GET /api/meta` toutes les 30 s
  (`njs/latest-version.js`, `@lodb_latest_version` dans `pages.conf`, `js_periodic` sur un seul
  worker, dès son démarrage) et le garde dans une zone partagée ; `$lodb_latest_pinned`
  (`sites/site.conf`) compare la version du chemin, entière, par référence arrière. Voulu : tant
  qu'aucune lecture n'a abouti (API injoignable au démarrage), la variable est vide et nginx
  interroge le stockage comme avant ; une lecture qui échoue garde la dernière valeur, sans
  ligne de log. Juste après une promotion, l'ancien dernier patch peut encore aller au SSR
  jusqu'à 30 s, sans plus de mal qu'un rendu ; le SSR lui-même le redirige parfois plus
  longtemps (sa copie de `/api/meta` vit jusqu'à 60 s, `LODB_API_META_TTL_SECONDS`). La lecture ne coûte qu'aux
  requêtes que le stockage aurait reçues (second `if`, après celui de `$lodb_store_page`) :
  jamais à une autre route, ni stockage coupé. L'API est jointe par son alias `lodb-api:8080`,
  jamais à travers le site.
- **Le 429 du stockage n'est pas celui de `limit_req`.** Scaleway freine en `429` ; la location
  du stockage refuse donc ses propres dépassements en `limit_req_status 420`, rendus au client
  en `429` avec `Retry-After` par `@lodb_rate_limited`, et un `429` du stockage mène au SSR.
  `error_page` ne s'hérite pas : chaque location du stockage répète les siens, et
  `recursive_error_pages on` laisse la location du SSR atteindre `@lodb_rate_limited` après une
  première redirection. `limit_req` ne compte une requête qu'une fois, quelle que soit la
  location qui la sert.
- **En-tête de précalcul effacé côté public.** `X-LoDb-Prerender: 1` fait passer un rendu dans
  la voie `prerender` du SSR (son budget, aucune limite par client, prioritaire sur la voie que
  donne l'URL) et ouvre `/_lodb/build-files`. nginx le pose vide sur toute requête relayée
  (`snippets/proxy-headers.conf`), comme `X-LoDb-Edge` : ne jamais retirer cette ligne. La passe
  appelle son SSR en direct (`http://lodb-web-ssr-prerender:4000`), jamais à travers nginx, qui effacerait
  l'en-tête, appliquerait `limit_req`, remplirait `lodb_archive` et compterait des vues. Un
  `curl -H 'X-LoDb-Prerender: 1'` sur le port publié du SSR (18082) consomme le budget de la
  passe. La voie n'écrit pas de ligne `http.request.served` par page, sauf sur une `5xx` :
  voulu (4 millions de lignes sinon).
- **Hôte public envoyé par la passe.** Chaque rendu part avec `Host` et `X-Forwarded-Host` =
  `LoDb__Prerender__PublicHost` et `X-Forwarded-Proto: https`, comme nginx les pose ; le SSR
  croit les deux derniers (`LODB_TRUST_PROXY_HEADERS`) et refuse en `400` tout `Host` hors de
  `LODB_ALLOWED_HOSTS`, son propre nom (`lodb-web-ssr:4000`) compris. La canonique et
  les `hreflang` en viennent : un hôte faux et chaque page stockée nomme le mauvais site, pour
  un an. `test.league-of-data-base.com` au staging, `localhost` en local (canoniques en
  `https://localhost/…`, voulu). La passe demande aussi `Accept-Encoding: gzip`, sans quoi le
  SSR rend du HTML en clair qu'elle refuse d'écrire, et son client ne décompresse rien.
- **En-têtes de la page en métadonnées, sous 2 Ko.** `Content-Type`, `Content-Encoding` et
  `Cache-Control` sont des en-têtes de l'objet ; la CSP, la CSP en rapport seul,
  `Reporting-Endpoints`, `Link` et `lodb-build` sont des `x-amz-meta-*`, 2 048 octets au plus
  en tout (préfixes compris). Au-delà, l'API compte la page en échec, sans nouvel essai : une
  CSP qui grossit (plus de scripts inline hachés) se voit en pages `failed`, jamais en en-tête
  tronqué. `Vary` et `Content-Length` ne sont jamais stockés. nginx masque chaque `x-amz-*` par
  son nom, jamais par motif (`snippets/pages-store/answer.conf`) : une métadonnée ajoutée par la
  passe s'ajoute là, et se repose dans `archive-cache.conf`, sinon elle sort chez le client.
  Une réponse du stockage, reconnue à sa métadonnée `lodb-build`, reçoit de nginx le type
  `text/html`, la `Cache-Control` de sa classe (`$lodb_pinned_cache_control`, une `map` sur
  `lodb-build` et la `Cache-Control` de l'objet : `revisable` pour un objet qui porte
  exactement sa valeur, `archived` pour tout autre) et, si son objet n'a pas de CSP, une
  politique stricte : jamais aucune. La valeur `revisable` s'écrit deux fois dans
  `sites/site.conf` (reconnue et donnée) et doit rester celle de `cache-control.ts`
  (`tools/nginx/test/archived-cache-control.test.mjs`) : une valeur changée d'un seul côté
  fait servir un an une page révisable. `ETag`, `Last-Modified` et `Accept-Ranges` décrivent
  l'objet gzip, pas la réponse (un client sans gzip reçoit d'autres octets) : masqués aussi,
  voulu. Une requête conditionnelle reçoit donc la page en `200`, jamais un `304`, et un `Range`
  la page entière ; les bundles hachés de `/build/`, servis sous le même fichier, perdent aussi
  ceux du SSR (immuables, jamais revalidés). Ne pas les « rétablir ».
- **Clé du stockage tirée d'une `map` stricte, jamais de l'URI brute.** `$lodb_store_page`
  (`sites/site.conf`) se construit sur `$lodb_request_path`, le chemin tel qu'envoyé, par une
  expression qui ne laisse passer que les 4 listes, la page de patch
  (`/{locale}/{version}/changes`, rien au-dessous) et les fiches d'une version, en `GET` ou
  `HEAD`, sans query ; tout le reste va au SSR sans interroger le stockage. Un `proxy_pass` à
  variable ne normalise pas l'URI : ni `$uri` ni `$request_uri` n'y entrent. Un nouveau type
  de page de la grammaire (`src/LoDb.Domain/Paths`) s'ajoute aussi à cette `map`, sinon ses
  pages restent servies par le SSR, sans erreur. Même règle pour `/build/`
  (`$lodb_store_bundle`), cherché au stockage sur une `404` du SSR seulement.
- **Rien du client ne part au stockage** : `proxy_pass_request_headers off` et
  `proxy_pass_request_body off`, seuls `Host` (celui du stockage) et `Connection ""` partent
  (`snippets/pages-store/request.conf`). Ne jamais y inclure `proxy-headers.conf` : un
  `Authorization`, un cookie ou un `x-amz-*` du client atteindrait Scaleway. Le certificat est
  vérifié sur le magasin de l'image (`proxy_ssl_verify`, profondeur `3` pour la racine
  croisée) : la CSP d'une page vient de son objet, une réponse d'un tiers pourrait tout
  exécuter sur l'origine du site.
- **L'upstream du stockage** est dans `sites/site.conf`, gabarit (`nginx.conf` n'en est pas
  un), avec `resolve` : nginx démarre même quand l'hôte ne se résout pas (CI, réseau coupé). Le
  stockage est coupé par défaut (`LODB_PAGES_STORE_ENABLED=0`, `compose.yaml` et `Dockerfile`),
  mais **allumé dans la pile locale** (`compose.override.yaml`), sur le stockage simulé
  `pages-store` : moto, en mémoire, sans vérification de signature, vidé à chaque redémarrage
  alors que `prerender_build` garde les builds pour copiés (sans effet en local, le SSR sert
  encore leurs bundles). La limite mémoire de nginx sur un hôte (512 Mo) tient compte de la
  zone de 64 Mo de clés : à mesurer avant de la baisser.
- **Une liste rendue avant ses images n'est pas stockée.** Tant que les images d'une version
  ne sont pas ingérées, une liste se rend avec des emplacements vides et la classe `transient`
  (`inject-catalogue-list.ts`) : la passe la compte `skipped` et la page reste servie par le
  SSR jusqu'à une passe suivante. Les fiches ne dépendent pas des images. En prod, les versions
  épinglées sont ingérées en entier avant la passe ; en local, une première passe sur une
  version neuve saute ses listes.
- **`prerender unit`** (CLI de l'API) ouvre une passe à lui, refusée tant qu'une autre est
  active, et l'annule s'il la laisse inachevée. Son hôte ne démarre aucune tâche de fond : il
  ingère d'abord le catalogue de l'unité en l'attendant, sans quoi l'ingestion mise en file ne
  partirait jamais. Une version qui vient de quitter le dernier rang attend encore quelques
  minutes (cache des versions de l'API et du SSR), dans la limite de `--wait`. Une spec E2E qui attend `X-LoDb-Source: store`
  retire d'abord la copie que nginx a pu garder d'un passage précédent
  (`specs/prerender/stored-page.spec.ts`) : sinon `cache`.
- **Au staging, les compteurs ne disent pas ce qui reste.** Les pages y expirent après 14 jours
  (règle `expire-pages`) alors que les unités restent faites : ces pages retombent sans bruit
  sur le SSR, et seule une nouvelle passe les remet. Une `401`, une `403` ou une `404` sur un
  `PUT` est une erreur de configuration (clé tournée, politique sans l'application d'écriture,
  bucket absent) : la passe se met en pause, la page n'est comptée nulle part. La sonde
  `verifier` lit en IPv4 seulement (`curl -4`) : l'hôte joint aussi Scaleway en IPv6, d'où la
  politique répond `403`.
- *Les deux points suivants valent pour le CDN, prévu mais pas encore construit (lot 4 de la
  [spécification](../produit/pre-rendu-catalogue-cdn.md)) : rien de Cloudflare n'est dans le
  dépôt au 2026-10-22.*
- **`/` jamais en cache au CDN** (lot 4) : sa `302` dépend d'`Accept-Language` et du cookie
  `lod_prefs`, et Cloudflare ne distingue pas les réponses selon `Accept-Language`. La règle
  « jamais en cache » (`/`, `/api/`, `/v1/`, `/webhooks/`, `/admin`, `/b/`, `/u/`, `/healthz`,
  `/readyz`) se place **après** la règle des pages éligibles, pour l'emporter.
- **La fermeture de l'origine se coupe avant tout retour en « DNS only »** (lot 4) : allumée,
  elle répond `403` à tout pair hors des plages de Cloudflare, donc à **tout le monde** dès que
  les enregistrements repassent en gris. Ordre du retour arrière : fermeture coupée, puis
  enregistrements en gris. Elle ne s'allume que 48 h au moins après le changement de serveurs
  de noms (TTL de la délégation du `.com` : 172 800 s), et ne touche ni `api.*` ni `stats.*`.
- **Diagnostics de la passe sans contrainte.** `prerender_pass.paused_at`, `pause_reason`,
  `prerender_unit.started_at`, `ended_at`, `failure` et `pages_expected` (migration
  `PrerenderDiagnostics`) n'ont aucune contrainte de cohérence avec `state` : l'image précédente
  de la stack met des passes en pause sans les remplir, et un retour arrière doit rester possible.
  Une passe mise en pause avant la migration n'a pas de `paused_at` : le panneau dit « en pause »
  sans durée. Les unités finies avant la migration reçoivent `ended_at = updated_at` ; une unité
  finie par l'image précédente après un retour à elle n'en a pas, et manque à la liste des échecs
  du moniteur (la matrice, lue par l'état, la montre).
- **Pages révisables, recalculées page par page.** Une page d'ancien patch de l'état ouvert
  d'une entité désigne l'URL courte ; quand une promotion clôt cet état, sa canonique, son
  titre et son historique changent. Le SSR la marque `revisable` (une heure), la passe la
  stocke ainsi, et la file des états clos (`prerender_revision`) la recalcule après la
  promotion, dans l'epoch servie, dans celle de la passe active et dans toute epoch plus
  récente remplie par une passe terminée (en attente de sa bascule) : chaque version de chaque
  état clos (celle qui vient de quitter le dernier patch comprise), dans les 21 langues, due
  21 minutes après (`2 × LoDb:Catalog:VersionsLifetime` + 1 min, le temps que tous les
  processus lisent l'index qui la couvre). La copie révisable d'avant peut encore être servie
  environ deux heures après le nouveau rendu (`s-maxage`, puis `stale-while-revalidate`) :
  voulu. Une page recalculée qui revient `revisable` compte comme un essai raté : nouvel essai
  5 minutes après, échec définitif au troisième, montré au pupitre. Le recalcul suit la
  graphie du catalogue de chaque version (`FiddleSticks` puis `Fiddlesticks`), pas celle de
  l'ouvreur. Cette file ne dépend pas de `AutoEnqueueOnPromotion` : elle tourne dès que le
  stockage est configuré, staging compris.
- **Rien du précalcul ne part sur un index en retard.** Tant que l'index des états ne couvre
  pas le patch promu, ni l'unité du patch remplacé ni la file des états clos ne démarrent
  (`prerender.promotion.index_behind`, toutes les 30 min tant qu'il est en retard) : rendues
  sur cet index, leurs pages garderaient pour de bon l'état ouvert. `prerender.reconcile` les
  ajoute une fois l'index rattrapé. Une génération neuve de l'index (reconstruction) est de
  l'historique : ses clôtures ne sont pas mises en file (`prerender.revision.generation`),
  seules celles de la dernière promotion le sont. Une reconstruction qui passe la fin d'un
  index resté en retard date ce qu'elle y clôt après son `built_at` (les dates de l'ancienne
  génération sont gardées pour le reste) : sans quoi ces clôtures, datées comme l'historique,
  ne seraient jamais recalculées.
- **Ordre des pages d'une version figé** (`VersionPages.Of`) : les quatre listes, puis la page
  de patch (`changes`, toujours listée : une version sans page de patch répond 404, comptée
  `skipped`), puis les entités. Une passe reprend à son curseur dans cet ordre : un
  déploiement qui le change décale toute passe active, qu'on **annule au pupitre avant**. La
  page de patch n'est pas un type de page (`TypeMask` garde quatre bits) : toute unité qui
  couvre tous les types la rend, tout filtre de type l'écarte.
- **Passe « ouvreurs d'abord »** (`{"pages":"openers"}`) : chaque unité rend la page de patch
  et les ouvreurs de tout état (ouvert ou clos) à sa version, lus dans l'index. Elle ne
  complète jamais une epoch et aucune promotion ne la rejoint : les unités d'un patch promu
  pendant qu'elle tourne attendent sa fin (`prerender.promotion.deferred`) ; une passe entière
  `{}` doit suivre. La commande `prerender unit` ne la propose pas.
- **Le périmètre d'une passe reste lisible par l'image d'avant les listes.** `prerender_pass.scope`
  porte les listes (`versions`, `locales`, `pageTypes`) **et** les anciens champs qui peuvent les
  dire (`PassScopeJson`) : les bornes `oldest`/`newest` d'une liste de versions, `locale` et
  `pageType` quand la liste n'en compte qu'un. L'image précédente ignore les listes : sans ces
  champs, elle lirait `{}`, tout ancien patch, tout type, toute langue (curseur décalé sur
  l'unité en cours, couverture comptée complète, epoch annoncée prête). Ne pas les retirer tant
  qu'un retour à cette image reste possible. Ce qu'ils ne disent pas, elle le lit plus large :
  plusieurs langues ou plusieurs types sur les 21 et les 4, une liste de versions trouée (toute
  la plage entre ses bornes). Avant un tel retour, annuler la passe active si son périmètre
  est de cette forme. Même règle pour `pages: "openers"`, que l'image d'avant la règle par
  état ignore : elle lirait la passe entière, et la croirait capable de compléter l'epoch.
- **Pas d'index sur `prerender_unit.updated_at`.** Il est réécrit à chaque page : l'indexer
  couperait les mises à jour HOT de la table, quatre millions de fois par passe. Ce qui doit être
  lu par ordre de fin passe par `ended_at`, écrit une fois, et son index partiel.
- **Epoch prête = plus aucun manque.** `readyEpoch` ne regarde plus si une passe « entière » a
  fini, mais si chaque (ancien patch, langue) de l'epoch a une unité terminée sur les quatre types
  de pages, ou une unité en échec définitif (`f`), quelles que soient les passes. Ces échecs et
  les pages en échec des unités terminées restent servis par le SSR après la bascule : la bande
  les compte.

- **Ne jamais lire le `/metrics` d'un SSR depuis la stack.** `prom-client` remet à zéro
  l'histogramme du retard de la boucle d'événements à chaque collecte : un second lecteur
  (sonde, script, régulateur) tronque la série `nodejs_eventloop_lag_p99_seconds` que lit
  Grafana. Le régulateur de la passe lit `GET /load`, sur le même port 9464, tenu par un
  moniteur à lui ; une sonde ad hoc fait de même.
- **`LoDb__Prerender__SsrBaseUrl` ne repointe jamais sur `lodb-web-ssr`.** La passe a son SSR,
  `web-ssr-prerender` (`http://lodb-web-ssr-prerender:4000`), au plus bas poids CPU de l'hôte :
  le SSR des visiteurs ralentit dès le premier rendu de la passe en vol (+45 % de médiane à un,
  +250 % à huit). Sa voie `prerender` et ses `LODB_PRERENDER_*` restent, inutilisées : ce n'est
  pas un levier de retour arrière. L'interrupteur est `LODB_PRERENDER_PACE_MODE=fixed`, le
  levier immédiat la pause au pupitre.
- **Plusieurs moteurs de précalcul : un nom, plusieurs adresses.** `web-ssr-prerender` tourne
  en `LODB_PRERENDER_RENDERERS` répliques qui partagent l'alias `lodb-web-ssr-prerender`.
  L'API ne s'en remet pas à son pool de connexions (il collerait à la première réplique) : elle
  résout le nom toutes les 30 s et réécrit l'adresse de chaque requête, le `Host` public gardé.
  `LoDb__Prerender__Renderers` et `replicas` lisent la même ligne : à 1, l'API ne résout rien.
  Pendant un déploiement, deux répliques peuvent tourner deux builds : une page rendue par
  l'une puis un fichier de build demandé à l'autre échouent une fois et sont repris.
- **`LODB_PRERENDER_MAX_IN_FLIGHT` ne règle plus les pages en vol de la passe.** Il nourrissait
  aussi la voie des SSR, où une valeur au-dessus de leurs créneaux les arrête au démarrage :
  l'API lit `LODB_PRERENDER_PAGES_IN_FLIGHT` (24), la fenêtre suit le rythme, et chaque moteur
  admet 15 rendus simultanés. Le staging garde `1` partout.
- **Les `503` des anciens patchs ne freinent pas le mode manuel.** Seul un refus de la voie du
  dernier patch (`current`) est un signe que les visiteurs manquent de place ; la voie
  `pinned` refuse au-delà de son budget même sur un hôte au repos.

## Transition (jusqu'à la décommission)

- L'ancienne stack (projet local `lodb`) tourne depuis `legacy/` du dépôt principal
  (`docker compose --project-directory legacy …`, son `.env` est `legacy/.env`), jamais
  recréée depuis un autre dossier (label `com.docker.compose.project.working_dir`). Ses
  commandes console gardent `-u www-data`.
- **Répétition locale** : [runbook](../reecriture/bascule.md) § 3.1 tel qu'écrit,
  emplacement `lodb-dev-e2`, jamais pendant un build Android. Derrière `| tee`, lire
  `$pipestatus[1]` (zsh) ou `${PIPESTATUS[0]}` (bash). Après elle, `lodb_rehearsal` ne doit
  plus exister dans le Postgres de l'ancienne stack.
- **Parité** : `tools/parity/` collecte, `tests/LoDb.Parity` compare ; ce projet est hors de
  `LoDb.slnx` (ni la suite ni la CI ne le lancent), il se lance par
  `dotnet test --project tests/LoDb.Parity/LoDb.Parity.csproj`. Un écart nouveau
  reçoit une règle argumentée de `ParityRules.cs`, jamais un filtre. Parité des builds et
  agrégats analytics : copies dans le Postgres de l'ancienne stack, jamais sa base `lodb`.
- **Rapports générés** (`diff-seo.md`, `lighthouse.md`…) : écrits par leurs outils seuls ;
  un écart SEO nouveau reçoit une règle dans `tools/seo-diff/lib/rules.mjs`.
- Les outils de comparaison (`seo-diff`, `lighthouse`, `builds-parity`) appellent encore
  `next` le côté « nouvelle stack » : ils disparaissent à la décommission avec `legacy/`.
- **Documents historiques** (`docs/reecriture/{plan-*,heritage,implementation,rapports}`) :
  ils gardent les noms de leur époque (`lodb-next`, `tools/next/`…) ; on ne les réécrit pas.
  Les **ADR** ne sont pas historiques : ils font foi, et une décision qui change s'y ajoute
  en amendement daté, sans réécrire le texte d'origine.
