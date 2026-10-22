# Contribuer à League of Database

Merci de votre intérêt pour le projet ! Ce document est le point d'entrée **court et à jour** pour contribuer.

- **Conventions de code complètes** (DRY / KISS / SOLID, limites de taille, nommage, règles par langage, invariants d'architecture) : [`AGENTS.md`](AGENTS.md) — source unique, à respecter.
- **Vue d'ensemble du système** : [`docs/architecture/architecture.md`](docs/architecture/architecture.md) ; index de la documentation : [`docs/README.md`](docs/README.md).
- **Templates** d'issue et de PR : [`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/) et [`.github/PULL_REQUEST_TEMPLATE.md`](.github/PULL_REQUEST_TEMPLATE.md).

---

## Prérequis

- **.NET SDK 10.0.400** (`global.json`) — API, tests, desktop.
- **Node.js 24** (ou `^22.22.3`, `>= 26`) / **npm** — workspace Angular `src/LoDb.Web`, E2E.
- **Docker** + **Docker Compose** — stack locale `lodb-dev` et Testcontainers.
- **Git**.

> Le site tourne sur cette stack depuis le 2026-10-04. L'ancienne (Symfony + Go + Vue) est
> archivée sous [`legacy/`](legacy/README.md) : elle ne reçoit plus de contributions.

## Démarrer

```bash
git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal

npm ci --prefix src/LoDb.Web
docker compose -p lodb-dev -f compose.yaml -f compose.override.yaml up -d --build
# site :18080 · API :18081 · Mailpit :18025
```

Détails : [`docs/guides/developpement.md`](docs/guides/developpement.md).

## Workflow Git

> ⚠️ Les Pull Requests ciblent la branche **`dev`**, jamais `main`.

```bash
git checkout dev
git pull upstream dev
git checkout -b feat/ma-fonctionnalite      # ou fix/… docs/… refactor/… test/…
```

Ouvrez ensuite la PR **vers `dev`**.

## Commits

Format [Conventional Commits](https://www.conventionalcommits.org/) : `type(scope): description`.

Types : `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`… Le scope suit la carte
de [`AGENTS.md`](AGENTS.md) (`back/<module>`, `front/<feature>`, `i18n`, `infra`, …).

```bash
git commit -m "feat(front/catalogue): filtrer les champions par role"
git commit -m "fix(back/builds): refuser un build sans champion"
```

## Garde-fous avant d'ouvrir une PR

À faire passer **au vert** avant toute PR :

```bash
dotnet build LoDb.slnx -c Release            # 0 avertissement
dotnet test LoDb.slnx                        # Testcontainers : Docker démarré
npm --prefix src/LoDb.Web run lint
npm --prefix src/LoDb.Web run typecheck
npm --prefix src/LoDb.Web run test
npm --prefix src/LoDb.Web run build:web
npm --prefix src/LoDb.Web run build:shell
npm --prefix src/LoDb.Web run api:check      # le client généré suit le contrat OpenAPI
```

La CI joue aussi les tests des outils (`node --test`), `tools/routing/check-urls.sh` et les
E2E (`tests/LoDb.E2E`), qui tournent contre la stack locale : la liste complète est dans les
garde-fous d'[`AGENTS.md`](AGENTS.md), le détail dans
[`docs/guides/developpement.md`](docs/guides/developpement.md). Un changement visible par le
joueur ajoute son entrée de changelog (`docs/changelog/`).

## Standards de code

Ne dupliquez pas les règles ici : elles vivent dans [`AGENTS.md`](AGENTS.md). En résumé, ce qui bloque une revue :

- C# : classes `sealed`, `record` pour les DTO, `TimeProvider` injecté, `CancellationToken` partout, aucun avertissement.
- Angular : composants standalone, signals, `OnPush`, pas de `any` ; l'orchestration vit dans des services et des fonctions pures.
- Limites : fichier ≤ 300 lignes (400 max), fonction ≤ 30 lignes, ≤ 3 paramètres, imbrication ≤ 3, complexité ≤ 10, ligne ≤ 100.
- Un seul élément public par fichier, nommé comme le fichier. Pas de nombres/chaînes magiques.
- Commentaires en anglais, expliquant le **pourquoi**.

**Invariants d'architecture à préserver** (voir `AGENTS.md` § « Invariants d'architecture ») : un seul hôte `LoDb.Api` ; egress Data Dragon par le client `ddragon` et son allow-list ; blobs écrits de façon atomique ; objets et sorts indexés par id ; SSR sans cookie ni secret ; le front ne consomme que le client généré.

## Signaler un bug · proposer une fonctionnalité

Via les [GitHub Issues](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/issues). Les templates (bug, feature, PR) sont dans [`.github/`](.github/ISSUE_TEMPLATE/).

## Licence des contributions

En soumettant une contribution, vous acceptez qu'elle soit distribuée sous **CC BY-NC 4.0** (voir [`LICENSE`](LICENSE)). Rappel : les assets Riot Games (Data Dragon / CommunityDragon) ne sont **pas** couverts par cette licence et restent la propriété de Riot Games, Inc.
