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
- La liaison des entrées de composant par le routeur reste coupée : une query forgée
  remplirait des entrées.
- `app.routes.server.spec.ts` passe par `ɵextractRoutesAndCreateRouteTree`, API privée
  qu'une montée de version peut déplacer.
- Points d'accroche de `app.config.ts` : chaque propriétaire remplit son dossier
  (`core/auth`, `core/analytics`, `core/update`) sans toucher `app.config.ts` ni `app.html`.
- Une page qui oublie `Seo.apply` n'a ni canonique ni hreflang. Une clé `about.*`, `api.*`
  ou `seo.*` n'est traduite que si un ancêtre fournit son scope (`provideTranslocoScope`) ;
  sinon la clé brute sort dans le HTML, sans erreur.
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

## API, données et comptes

- `/readyz` se lit sur l'API (18081 ou `api:8080`) ; demandé à nginx, il part au SSR (404).
- En local, les URLs absolues perdent le port (nginx transmet `Host: $host`) : l'origine
  canonique se fixe par `LoDb__Seo__CanonicalOrigin` et `LoDb__Accounts__SiteOrigin`.
- `/api/pickers/*` exige `version` et `lang` (400 `invalid-version` sinon).
- **E-mails** : le modèle d'un `EmailMessage` s'écrit avec `EmailModelKeys`, jamais en
  littéral (sinon `dead` dès le premier essai). L'outbox n'envoie rien sans
  `LoDb:Mail:Host` : voulu. En local, Mailpit sur http://localhost:18025.
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
- Comparer le chemin d'une réponse (`new URL(url).pathname`), jamais `url.endsWith(...)`.
  Limiter les comptes de titres ou de boutons à `main` ou au composant visé. Une spec qui
  compte les POST exclut `/api/analytics/`. Un parcours qui crée un compte le supprime aussi
  en cas d'échec.
- Angular laisse des espaces autour d'un texte entre balises : ancrer une regex sur le
  contenu, pas sur `^`/`$`.
- `disclosure.spec.ts` échoue parfois en suite complète, jamais seul : relancer n'est pas
  une correction.
- Les instantanés de `tests/LoDb.E2E/test-results/` sont écrasés au passage suivant.

## Infra et déploiement

- Variables Compose préfixées **`LODB_`**. En dev, les images s'appellent
  `<projet>-<service>` : un emplacement n'écrase pas les images de la stack locale.
- Sur l'hôte, la stack occupe le projet de l'ancienne (`lodb-staging`, `lodb-prod`) : elle
  reprend son volume `pgdata` et range ses blobs dans `ddragon`. Le volume `storage` de
  l'ancienne stack (propriété de `www-data`) n'est ni réutilisé ni supprimé : le nommer
  `storage` rendrait le stockage de l'API non inscriptible.
- nginx : sous-domaine reconnu par `server_name ~^api\.` ; CORP `same-origin` sauf
  `/cdn/blobs/` en `cross-origin` (coquille Android) ; `access_log off` voulu (l'edge
  journalise) ; `sites/*.conf` passe par `envsubst`, filtré sur `^LODB_`.
  `server.d/legacy-redirects.conf` envoie à `/api/legacy` les premiers segments de l'ancien
  site : tout nouveau chemin hors locale se vérifie contre cette liste. `server.d/seo.conf`
  possède `/sitemap.xml`, `/sitemaps/`, `/robots.txt` et `/llms.txt`.
- `gzip_types` de `docker/nginx/nginx.conf` doit lister `text/javascript` et
  `application/manifest+json` (sinon ~1,7 s perdues en 4G lente). Un budget Lighthouse ne
  se relâche jamais ; Lighthouse en local est pessimiste (cache de transfert du SSR rangé
  sans le port).
- SSR : `LODB_TRUST_PROXY_HEADERS` obligatoire derrière nginx, `LODB_ALLOWED_HOSTS` liste
  les hôtes acceptés ; `public/` ne contient ni `media/` ni script racine nommé comme un
  bundle (cache `immutable`).
- Constats de logs (lignes hors JSON, erreurs multi-lignes) : **à corriger à la source,
  jamais à filtrer**.

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
