# Contribuer à League of Database

Merci de votre intérêt pour le projet ! Ce document est le point d'entrée **court et à jour** pour contribuer.

- **Conventions de code complètes** (DRY / KISS / SOLID, limites de taille, nommage, règles par langage, invariants d'architecture) : [`CLAUDE.md`](CLAUDE.md) — source unique, à respecter.
- **Guide détaillé multilingue** (FR / EN / ES, templates d'issue & de PR) : [`docs/contribution.md`](docs/contribution.md).

---

## Prérequis

- **Docker** + **Docker Compose** — la stack complète (PHP 8.4 / Symfony 7.4, Go 1.25, MinIO) tourne en conteneurs ; rien à installer en local côté backend.
- **Node.js 20+** / **npm** — uniquement pour le dev et les garde-fous front (hors conteneur, depuis `app/`).
- **Git**.

## Démarrer

```bash
git clone https://github.com/VOTRE_USERNAME/LeagueOfDataBaseFinal.git
cd LeagueOfDataBaseFinal

docker compose up -d --build
# app :8080 · MinIO console :9001 · Mailpit :8025 · go-fetcher :8085/healthz
```

Détails : [`docs/docker.md`](docs/docker.md), [`docs/configuration.md`](docs/configuration.md).

## Workflow Git

> ⚠️ Les Pull Requests ciblent la branche **`dev`**, jamais `main`.

```bash
git checkout dev
git pull upstream dev
git checkout -b feature/ma-fonctionnalite   # ou fix/… docs/… refactor/… test/…
```

Ouvrez ensuite la PR **vers `dev`**.

## Commits

Format [Conventional Commits](https://www.conventionalcommits.org/) : `type(scope): description`.

Types : `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`. Le scope reflète la zone touchée (`front`, `back`, `i18n`, `champion`, `docker`, …).

```bash
git commit -m "feat(champion): recherche par rôle"
git commit -m "fix(loader): course manifeste read-merge-write"
```

## Garde-fous avant d'ouvrir une PR

À faire passer **au vert** avant toute PR (identique à la CI) :

```bash
# Backend — dans le conteneur
docker compose exec -T php php vendor/bin/phpunit tests/Unit

# Front — depuis app/
npm test          # vitest
npm run typecheck # vue-tsc --noEmit
npm run build     # vite build
```

> `tests/Functional/AdminAccessTest` échoue en conteneur `APP_ENV=dev` (`framework.test` inactif) — **pré-existant**, vert en CI. La baseline backend est `tests/Unit`.

## Standards de code

Ne dupliquez pas les règles ici : elles vivent dans [`CLAUDE.md`](CLAUDE.md). En résumé, ce qui bloque une revue :

- `declare(strict_types=1);` en tête de chaque fichier PHP ; classes `final` par défaut ; typage strict partout.
- Limites : fichier ≤ 300 lignes (500 max), fonction ≤ 30 lignes, ≤ 4 paramètres, imbrication ≤ 3, complexité ≤ 10, ligne ≤ 120.
- Un seul élément public par fichier, nommé comme le fichier. Pas de nombres/chaînes magiques.
- Front : `<script setup lang="ts">`, pas de `any`, orchestration en composables, réutiliser le design system (`app.css`).
- Commentaires en anglais, expliquant le **pourquoi**.

**Invariants d'architecture à préserver** (voir `CLAUDE.md` § « Architecture ») : tout l'egress Data Dragon / CommunityDragon passe par le gateway Go ; stockage sans base de données (manifeste en read-merge-write) ; les ressources dérivent d'`AbstractManager` / `AbstractResourceController`. Prouvez l'équivalence de comportement (tests + rendu) sur un refacto.

## Signaler un bug · proposer une fonctionnalité

Via les [GitHub Issues](https://github.com/LINDECKER-Charles/LeagueOfDataBaseFinal/issues). Les templates (bug, feature, PR) sont dans [`docs/contribution.md`](docs/contribution.md).

## Licence des contributions

En soumettant une contribution, vous acceptez qu'elle soit distribuée sous **CC BY-NC 4.0** (voir [`LICENSE`](LICENSE)). Rappel : les assets Riot Games (Data Dragon / CommunityDragon) ne sont **pas** couverts par cette licence et restent la propriété de Riot Games, Inc.
