# Ancienne stack (archivée) — règles

Symfony 7.4 / PHP 8.5, passerelle Go, îlots Vue 3 : la version du site servie en production
jusqu'à la bascule vers la stack .NET + Angular, puis supprimée à la décommission
([runbook](../docs/reecriture/bascule.md) § 10). Le tag `archive/stack-php` fige `main`
avant l'archivage.

**En lecture seule.** On n'y touche que pour un correctif exigé par la prod avant la
bascule. Ses workflows sont archivés dans `.github/workflows/` de ce dossier et ne tournent
plus : un correctif se déploie depuis le tag, ou après avoir remis les workflows en place
(chemins préfixés de `legacy/`, runbook § 8.2). Tous les chemins ci-dessous sont relatifs à
`legacy/`, et les commandes Compose se lancent depuis ce dossier (`cd legacy`, ou
`docker compose --project-directory legacy …` depuis la racine), qui porte son `.env`.

## Stack

| Couche | Techno |
|---|---|
| Backend | Symfony 7.4 LTS / PHP 8.5 (`app/`) |
| Fetch upstream | micro-service Go `go/fetcher/` (passerelle thin, allowlist SSRF) |
| API publique | micro-service Go `go/api/` (`/v1`) |
| Stockage assets | volume `storage` (`/srv/storage`), adressé par contenu, écritures atomiques |
| Données utilisateur | PostgreSQL 17 + Doctrine ORM (comptes, favoris, builds) |
| Front | Twig + îlots Vite / Vue 3 / TS / PrimeVue, navigation Turbo Drive |
| Design system | « Hextech » dans `app/assets/styles/app.css` |

## Invariants

- **Tout l'egress Data Dragon / CommunityDragon passe par le Go gateway**
  (`GoFetcherClient`). Une nouvelle source d'asset s'ajoute à l'`ALLOWED_HOSTS` du
  go-fetcher (`compose.yaml`), conteneur recréé.
- **Écritures atomiques** par `AtomicWriteAdapter` (`.staging/` + `rename()`). nginx n'expose
  que `blobs/`. Le manifeste se met à jour en **read-merge-write**
  (`AbstractManager::saveManifest`) ; une entrée à `null` = absence définitive persistée.
- **Postgres = données utilisateur uniquement** ; les données Data Dragon restent sur le
  volume. Migrations : `docker compose exec -T -u www-data php php bin/console
  doctrine:migrations:migrate`. **Aucune nouvelle migration Doctrine** : `Baseline` de la
  nouvelle stack en est le miroir exact (runbook § 2).
- **État durable sous `var/state/`** (volume `app_state`) ; le reste de `var/` est recréé à
  chaque déploiement.
- **Objets et sorts indexés par id**, jamais par nom (jumeaux Classic,
  `App\Service\API\Edition`).
- Managers de ressource dérivés d'`AbstractManager`, contrôleurs d'`AbstractResourceController`,
  résolution version/langue par `PageContextResolver`, orchestration Vue hors des SFC.

## Règles par langage

- **PHP** : `declare(strict_types=1);` partout, classes `final`, typage strict, `readonly`
  pour l'injection ; absence upstream (403/404) = fallback persisté, transitoire (5xx) =
  exception.
- **TypeScript / Vue** : `<script setup lang="ts">`, `vue-tsc --noEmit` vert, pas de `any` ;
  orchestration en composables ; design system `app.css`.
- **Go** : testé en conteneur (`golang:1.26`) ; passerelle thin, pas d'ingestion métier.

## Garde-fous

```bash
docker compose exec -T -u www-data php php vendor/bin/phpunit tests/Unit   # TOUJOURS -u www-data
cd app && npm test && npm run typecheck && npm run build
```

`tests/Functional/AdminAccessTest` échoue en conteneur `APP_ENV=dev` : pré-existant.

## Pièges

- **`docker compose exec php …` tourne en root** en dev : sans `-u www-data`, des dossiers
  root sous `var/` cassent le pool FPM (500 collée en fin de HTML). Correctif :
  `chown -R` sur `var` et `/srv/storage`.
- Loader : pas de `<Transition>` + `v-show` pour l'overlay (classe CSS déterministe).
- Splash et skins servis en hotlink depuis le CDN Data Dragon, non ingérés.
- Chromas : CommunityDragon seul ; label couleur dérivé de la teinte.
- `/build/` est réservé aux assets Vite ; les builds partagés vivent sur `/b/{token}`.
- CSRF stateless : les POST curl de test exigent un en-tête `Origin`.
- La page `/changelog` est vide depuis l'archivage : les releases ont rejoint la nouvelle
  stack. La version affichée est `0.0.0`.
- `app:ddragon:warmup` sur l'échantillon de parité dépasse 256 Mo : `php -d
  memory_limit=2G bin/console …`.
