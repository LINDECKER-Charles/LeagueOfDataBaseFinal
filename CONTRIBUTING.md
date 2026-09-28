# Contribuer à League of Database

Merci de votre intérêt pour le projet ! Ce document est le point d'entrée **court et à jour** pour contribuer.

- **Conventions de code complètes** (DRY / KISS / SOLID, limites de taille, nommage, règles par langage, invariants d'architecture) : [`CLAUDE.md`](CLAUDE.md) — source unique, à respecter.
- **Guide détaillé multilingue** (FR / EN / ES, templates d'issue & de PR) : [`docs/contribution.md`](docs/contribution.md).

---

## Prérequis

- **.NET SDK 10.0.400** (`global.json`) — API, tests, desktop.
- **Node.js 24** (ou `^22.22.3`, `>= 26`) / **npm** — workspace Angular `src/LoDb.Web`, E2E.
- **Docker** + **Docker Compose** — stack locale `lodb-dev` et Testcontainers.
- **Git**.

> L'ancienne stack (Symfony + Go + Vue) est archivée sous [`legacy/`](legacy/README.md) :
> elle ne reçoit plus de contributions.

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
de [`CLAUDE.md`](CLAUDE.md) (`back/<module>`, `front/<feature>`, `i18n`, `infra`, …).

```bash
git commit -m "feat(front/catalogue): filtrer les champions par role"
git commit -m "fix(back/builds): refuser un build sans champion"
```

## Garde-fous avant d'ouvrir une PR

À faire passer **au vert** avant toute PR (identique à la CI) :

```bash
dotnet build LoDb.slnx -c Release            # 0 avertissement
dotnet test LoDb.slnx                        # Testcontainers : Docker démarré
npm --prefix src/LoDb.Web run lint
npm --prefix src/LoDb.Web run typecheck
npm --prefix src/LoDb.Web run test
npm --prefix src/LoDb.Web run build:web
npm --prefix src/LoDb.Web run api:check      # le client généré suit le contrat OpenAPI
```

Les E2E (`tests/LoDb.E2E`) tournent contre la stack locale : voir
[`docs/guides/developpement.md`](docs/guides/developpement.md).

## Standards de code

Ne dupliquez pas les règles ici : elles vivent dans [`CLAUDE.md`](CLAUDE.md). En résumé, ce qui bloque une revue :

- C# : classes `sealed`, `record` pour les DTO, `TimeProvider` injecté, `CancellationToken` partout, aucun avertissement.
- Angular : composants standalone, signals, `OnPush`, pas de `any` ; l'orchestration vit dans des services et des fonctions pures.
- Limites : fichier ≤ 300 lignes (400 max), fonction ≤ 30 lignes, ≤ 3 paramètres, imbrication ≤ 3, complexité ≤ 10, ligne ≤ 100.
- Un seul élément public par fichier, nommé comme le fichier. Pas de nombres/chaînes magiques.
- Commentaires en anglais, expliquant le **pourquoi**.

**Invariants d'architecture à préserver** (voir `CLAUDE.md` § « Nouvelle stack ») : un seul hôte `LoDb.Api` ; egress Data Dragon par le client `ddragon` et son allow-list ; blobs écrits de façon atomique ; objets et sorts indexés par id ; SSR sans cookie ni secret ; le front ne consomme que le client généré.

## Signaler un bug · proposer une fonctionnalité

Via les [GitHub Issues](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/issues). Les templates (bug, feature, PR) sont dans [`docs/contribution.md`](docs/contribution.md).

## Licence des contributions

En soumettant une contribution, vous acceptez qu'elle soit distribuée sous **CC BY-NC 4.0** (voir [`LICENSE`](LICENSE)). Rappel : les assets Riot Games (Data Dragon / CommunityDragon) ne sont **pas** couverts par cette licence et restent la propriété de Riot Games, Inc.
