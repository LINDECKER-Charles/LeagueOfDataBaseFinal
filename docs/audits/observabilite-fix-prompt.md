# 🛠️ Observabilité — mission d'implémentation

> **Comment s'en servir.** Pointer un agent sur ce fichier :
> *« Suis `docs/audits/observabilite-fix-prompt.md`, lot 1 »*.
> Un lot par session. Ne jamais enchaîner deux lots sans avoir vérifié le premier :
> un échec devient sinon impossible à attribuer.
>
> Les **patches exacts** vivent dans
> [`observabilite-2026-08-23.md`](observabilite-2026-08-23.md) — les lire avant de
> toucher un fichier. Ce document-ci ne les répète pas : il donne la mission, les
> pièges qui cassent le lot, et la définition de « terminé ».

---

## Règles valables pour tous les lots

**Avant de commencer** — lire `CLAUDE.md` à la racine (invariants et conventions de
code), puis le lot correspondant dans `observabilite-2026-08-23.md`.

**Interdits absolus :**

- ⛔ Ne **jamais** ajouter `caddy.import: journal-acces`, ni aucune directive `log` par
  site. Le journal d'accès de l'edge est **global** : une directive par site pousserait
  tous les autres sites du VPS dans `skip_hosts`, servis mais invisibles, sans le
  moindre symptôme.
- ⛔ Ne **jamais** modifier le dépôt `infra-vps`. Tout correctif est côté LODB.
- ⛔ Ne **jamais** lancer `docker compose exec` sans `-u www-data` : la commande laisse
  des dossiers `root` sous `var/` que le pool FPM ne peut plus écrire, et l'application
  se met à rendre une erreur 500 collée en fin de HTML sur toutes les pages.
- ⛔ Ne pas élargir le périmètre du lot. Ce qui est trouvé en chemin se signale, ne se
  corrige pas.

**Garde-fous à faire passer avant de considérer un lot terminé :**

```bash
# Backend, dans le conteneur, comme la CI
docker compose exec -T -u www-data php php vendor/bin/phpunit tests/Unit

# Front, seulement si app/assets ou app/templates a été touché (depuis app/)
npm test && npm run typecheck && npm run build

# Go, en conteneur — Go n'est pas installé en local
docker run --rm -v "$PWD/go/fetcher:/src" -w /src golang:1.25 go test ./...
docker run --rm -v "$PWD/go/api:/src"     -w /src golang:1.25 go test ./...
```

> `tests/Functional/AdminAccessTest` échoue en conteneur `APP_ENV=dev` — **pré-existant**,
> vert en CI. La baseline qui fait foi est `tests/Unit`.

**Commits** — Conventional Commits en français, sujet à l'infinitif, ≤ 72 caractères,
scope pris dans la carte de `CLAUDE.md`. Aucun trailer de co-auteur, aucune mention
d'outil de génération.

**Changelog** — une entrée `docs/changelog/2026/AAAA-MM-JJ-slug.md` jointe au commit,
pour les lots **1b, 2, 3 et 4** (chacun porte un changement observable). Format :
`docs/changelog/TEMPLATE.md`. Le lot 1a en est dispensé : configuration interne, aucun
comportement visible ne change.

**Vérification finale de chaque lot** — le tableau en fin de
`observabilite-2026-08-23.md` donne la commande et l'attendu, lot par lot.

---

## Lot 1 — ouvrir le robinet

Config seule, aucune ligne de PHP ni de Go. **C'est le préalable à tout le reste :**
tant qu'il n'est pas livré, chaque log ajouté par les lots suivants est détruit avant
`stderr`.

**Périmètre**, rien de plus : `app/config/packages/monolog.yaml`,
`app/config/packages/framework.yaml`, `docker/php/php.ini`, `docker/php/php-dev.ini`,
`docker/php/php-fpm.conf`, `docker/nginx/nginx.conf`, `compose.yaml` (ancre
`x-logging`), `compose.deploy.yaml` (commentaire d'invariant uniquement).

**Pièges qui cassent ce lot :**

1. **`php-fpm.conf` est une fusion, pas un remplacement.** Le `[www]` existant garde
   `clear_env`, `catch_workers_output`, `decorate_workers_output`, `ping.path` et les
   six `pm.*`. Perdre `catch_workers_output = yes` annule **tout le lot** en silence ;
   perdre les `pm.*` empêche php-fpm de démarrer.
2. **Le bloc `framework.yaml` est obligatoire**, pas optionnel. Sans lui, le nouveau
   handler always-on écrit une ligne `ERROR` pour chaque 404 de bot, conservée 90 jours.
3. **`process_psr_3_messages: false` sur les quatre handlers `stream`** — monolog-bundle
   l'active par défaut, et il réécrirait en silence toute clé contenant un
   `{placeholder}`.
4. **nginx : écrire `access_log off;`**, ne pas supprimer la directive. Le défaut
   compilé de nginx est `combined` : retirer la ligne continuerait à journaliser dans un
   autre format.
5. L'ancre `x-logging` s'applique aussi à **`minio-init`** : son script boucle sans
   timeout si MinIO ne répond pas, c'est le producteur de logs le plus susceptible de
   s'emballer.

**Terminé quand :**

```bash
docker compose exec -T -u www-data php php bin/console debug:config monolog --env=prod --no-debug
# -> les handlers business / stderr / error_context* / deprecation apparaissent

docker compose exec nginx nginx -t          # syntax is ok / test is successful
docker compose up -d --build
curl -s -o /dev/null localhost:8080/
docker compose logs --since 2m php | grep -c '"GET /index.php"'   # -> 0
```

Les 8 lignes `NOTICE: fpm is running` du démarrage subsistent : c'est normal, pas un
échec.

---

## Lot 2 — honorer le contrat de l'infra

Limites mémoire, healthcheck php réel, `start_period`, Mailpit hors production.
Requiert le lot 1.

**Pièges :**

1. `RUN apk add --no-cache fcgi` va dans l'étage **`php_base`** du Dockerfile, **avant**
   `USER www-data`. Placé dans l'étage `app`, il échoue.
2. Les limites vont dans **`compose.deploy.yaml`**, jamais `compose.yaml` : une limite à
   1 Gio en dev transformerait un `composer install` en OOM.
3. **Mailpit derrière un profil casse l'envoi de mail en prod si on s'arrête là.**
   `MAILER_DSN` doit devenir obligatoire dans `compose.deploy.yaml`
   (`${MAILER_DSN:?...}`), et `COMPOSE_PROFILES=dev` doit être posé dans `.env` **et**
   dans `.env.example` — `.env` est git-ignoré, sinon tout nouveau clone perd Mailpit
   sans message.
4. `access.suppress_path[] = /ping` dans le `[www]`, sinon la nouvelle sonde
   réintroduit exactement le bruit que le lot 1 vient de supprimer.
5. **Pas de plafond `cpus`** : trop bas, il bride silencieusement et déclenche
   `ConteneurCpuBride` — des lenteurs qu'aucun graphe d'hôte ne montre.

**Terminé quand** `docker compose up -d --build` laisse `php` passer `healthy` en ~30 s
sans `unhealthy` transitoire, et que `docker compose ps | grep -i mailer` ne renvoie
rien avec `COMPOSE_PROFILES` vide.

---

## Lot 3 — poser les logs applicatifs PHP

Le chantier de fond. Requiert le lot 1.

**Lire d'abord [`../guides/logging.md`](../guides/logging.md) en entier** : c'est la
convention d'écriture, et elle prime sur l'intuition.

**Règles non négociables :**

1. **Le comportement fonctionnel reste inchangé partout.** On ajoute un log, on ne
   touche pas au flux de contrôle : une page en panne redirige toujours, une ingestion
   différée avale toujours son exception.
2. Clé d'événement pointée `domaine.sujet.résultat`, contexte PSR-3, exception sous la
   clé `exception` **en objet**. Jamais d'interpolation de chaîne.
3. **Aucune clé de contexte nommée `error`** — elle fait classer la ligne en
   `level=error` par Vector. Renommer les trois occurrences existantes au passage
   (`ApiBillingController:108`, `DonationController:85`, `StripeWebhookController:87`).
4. **Miroir d'audit : sans `ip` ni `meta.identifier`.** Ce dernier porte l'adresse
   e-mail saisie sur `user.login_failed`. `AuditOutcome` a **trois** cas : `Success` →
   `info`, `Failure` et `Denied` → `warning`.
5. **`#[WithMonologChannel]` n'est pas hérité.** Sur `AbstractResourceController` et
   toute base abstraite, passer par le nom de l'argument : `LoggerInterface
   $catalogLogger`.
6. Corriger au passage le contrat *best-effort* d'`AuditLogger` : son `try/catch`
   n'ouvre qu'après la résolution de l'acteur, qui peut lever pendant le traitement d'un
   échec d'authentification.

**Tests obligatoires** dans `app/tests/Unit`. Le plus important : *`AuditLogger`
n'émet jamais `ip` ni `meta.identifier` vers le logger* — c'est une garantie RGPD
qu'une relecture de code ne rattraperait pas.

**Terminé quand** `phpunit tests/Unit` est verte et qu'en arrêtant `minio`, charger une
page de ressource produit bien une ligne `catalog.page.unavailable` sur le flux du
conteneur.

Découper en plusieurs commits par domaine (`catalog` / `billing` / `mail` / `audit`),
chacun avec ses tests.

---

## Lot 4 — mettre `go-fetcher` au niveau de `go-api`

`log/slog` JSON, erreurs typées, `srv.ErrorLog`, filtrage de `/healthz`.

**Points de vigilance :**

1. `slog.NewJSONHandler` sur **`os.Stdout`**, pas stderr, et `slog.SetDefault` pour
   capturer aussi le paquet `log` de la stdlib. Aujourd'hui `go-fetcher` écrit *tout*
   sur stderr, ce qui rend un filtre `stream:stderr` inutilisable.
2. **Sentinelles + `errors.Is`** : le niveau d'un log ne doit jamais dépendre d'un
   matching de chaîne. Distinguer `ErrRedirectNotAllowed` — un hôte autorisé tente de
   nous relayer ailleurs — des refus au premier appel : ce n'est pas le même événement.
3. Un refus d'allowlist se logge en **`Error`**, clé `fetch.allowlist.refused`.
   Aujourd'hui l'erreur est rendue *in-band* dans une réponse **HTTP 200**.
4. **Une ligne de synthèse par batch**, jamais une par URL : amplification de logs sur
   le chemin chaud.
5. Durée **numérique en millisecondes**, pas `time.Duration.String()`.
6. **Échapper le chemin** avant de le journaliser : un saut de ligne y injecterait des
   lignes entières, et le mot `error` y pilote le champ `level` de Vector.
7. Côté `go-api` : les **5xx sont journalisés en `INFO`**, le niveau doit suivre le
   statut. Ajouter `api_key_id` sur la ligne d'accès — **jamais la clé elle-même**.
8. **Ajuster les tests existants** (`fetcher_test.go`, `handlers_test.go`,
   `middleware_test.go`) qui comparent des messages d'erreur devenus des sentinelles.

**Terminé quand** les deux suites Go passent en conteneur et qu'un appel `/fetch` avec
une URL hors allowlist produit une ligne JSON `fetch.allowlist.refused` sur `stdout`.

---

## Lot 5 — corrélation et métriques

**Pas maintenant.** `observabilite-2026-08-23.md` le décrit comme une spécification, pas
comme des patches testés, et il n'a de valeur qu'une fois les quatre premiers lots en
production. Le reprendre — et le porter au même niveau de détail que les autres — quand
les lots 1 à 4 auront tourné une semaine.
