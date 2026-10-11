# Stratégie de test

Ce guide fixe **où** tester chaque comportement, ce que chaque couche coûte, et les règles qui
gardent la suite rapide et sobre en mémoire. Les commandes sont dans
[`developpement.md`](developpement.md) ; les pièges des E2E dans
[`pieges.md`](../architecture/pieges.md).

## Principe : la couche la plus basse qui observe le comportement

Un comportement se teste **une fois**, dans la couche la moins chère capable de l'observer.
On ne remonte d'une couche que pour ce que la couche inférieure ne voit pas (le câblage, le
navigateur, la stack réelle). Un test qui redit ce qu'un test plus bas prouve déjà est un coût
sans signal : il ralentit la CI et casse deux fois pour la même raison.

| Couche | Projet | Ce qu'elle prouve | Ce qu'elle ne fait pas |
|---|---|---|---|
| Unitaire .NET | `LoDb.Domain.Tests`, parties pures d'`Infrastructure`/`Ingestion` | règles, calculs, formats, cas limites | I/O, hôte HTTP |
| Intégration .NET | `LoDb.Infrastructure.Tests`, `LoDb.Ingestion.Tests` | SQL et migrations sur un vrai Postgres, Data Dragon rejoué | contrat HTTP |
| API | `LoDb.Api.Tests` | contrat HTTP (codes, corps, en-têtes), sécurité (auth, XSRF/`Origin`, limites), câblage d'un module | une règle pure déjà testée plus bas |
| Composants | Vitest (`*.spec.ts` à côté du code) | rendu, état, services, SSR sans navigateur | mise en page réelle, contraste, service worker |
| Outils | `node --test` (`tools/<outil>/test`), image nginx (`tools/nginx/test`) | scripts, configuration nginx (routage, cache, en-têtes) | parcours utilisateur |
| E2E | Playwright (`tests/LoDb.E2E`) | **parcours critiques** sur la stack complète, et ce que seul un vrai navigateur voit | variations exhaustives, messages de validation, codes d'erreur |

### Ce qui justifie un test E2E

- un parcours critique de bout en bout : consulter le catalogue, compte (inscription,
  session, double facteur), builds (forge, partage), profil, accès à l'administration,
  entrée du don, contact ;
- ce que seule la stack réelle montre : SSR puis hydratation, routage et cache de nginx bout
  à bout, CSP sur les trois modes de rendu, service worker, accessibilité (contraste) et mise
  en page réelles sur un échantillon de pages ;
- un *smoke* par fonctionnalité.

Une variation (chaque langue, chaque entité, chaque message d'erreur) appartient à Vitest ou à
l'API. Une nouvelle spec E2E dit en commentaire pourquoi aucune couche plus basse ne suffit.
Les vérifications de requête (en-têtes, caches, anciennes URL, grammaire d'URL de l'ADR 0005)
vivent dans `tools/nginx/test` et `tools/routing/check-urls.sh`, que le job `e2e` de la CI lance
avant Playwright. Le sous-ensemble `@readonly` sert aussi de *smoke* du staging et de la prod
(`tools/cutover/readonly-e2e.sh`) : un test fusionné garde ce tag.

### Paramétrage

Un `[Theory]` ou un `it.each` couvre **une ligne par classe d'équivalence**, plus les bornes.
Dix lignes qui passent par la même branche ne prouvent rien de plus qu'une.

## Infrastructure des tests .NET

- **Un serveur Postgres par assembly** (`[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]`),
  sans durabilité (`fsync`, `synchronous_commit`, `full_page_writes` coupés). Une classe ne
  démarre jamais son propre conteneur ; une fixture de collection reçoit le serveur par son
  constructeur.
- **Une base par test, copiée d'un gabarit** : `CreateMigratedDatabaseAsync` copie une base
  migrée une fois par serveur (`CREATE DATABASE … TEMPLATE`) puis relance `migrate`, qui remet
  les droits sur la base et les réglages du rôle que la copie ne reprend pas.
  `CreateDatabaseAsync` (base vide) est réservé aux tests des migrations elles-mêmes.
- **`ApiFactory` est toujours disposé** (`await using`, ou `DisposeAsync` de la fixture). Il
  libère le limiteur de débit que le middleware d'ASP.NET Core ne libère jamais
  (`PipelineRateLimiters`) ; sans cela chaque hôte restait en mémoire jusqu'à la fin du run.
  `HostReleaseTests` échoue si un hôte disposé redevient atteignable : une fuite de ce genre se
  voit dans la CI, pas sur le poste d'un développeur.
- **Base injoignable** : `ApiFactory.UnreachablePostgres` vise un socket Unix inexistant, qui
  échoue tout de suite (une connexion TCP refusée met deux secondes sous Windows).
- Un hôte par test seulement quand son état compte (limites de débit en mémoire, horloge
  simulée) ; sinon un hôte par classe ou par collection, chaque test créant ses propres
  données.

## Budgets

Mesurés le 2026-10-11 sur le poste de référence (24 threads, SSD) et sur la CI. Un écart net
est une régression à expliquer, comme un test rouge.

| Suite | Local | CI |
|---|---|---|
| .NET (`dotnet test LoDb.slnx`, 5 projets, 3169 cas) | 2 min 55, pic 2,2 Go | job `dotnet` 5 min 50, build compris |
| Vitest (2758 tests) | ~10 s d'exécution, pic 1,9 Go | job `front` 2 min |
| E2E (65 tests, 2 workers) | 1 min 25 d'exécution, 3 min avec la reconstruction de la stack | job `e2e`, `check-urls` et image nginx compris |

Plafonds de parallélisme, valables partout : 4 classes xUnit à la fois
(`tests/LoDb.Api.Tests/xunit.runner.json`), 4 workers Vitest (`vitest-base.config.mts`),
4 workers Playwright en local et 2 en CI (`playwright.config.ts`).

## Sous Windows

- Un checkout CRLF fait échouer des instantanés .NET et Prettier que la CI ne voit pas :
  `core.autocrlf=false` et `core.eol=lf` sur le worktree.
- `api:check` peut signaler une dérive qui n'est que des `\r\n` dans les commentaires XML :
  le contrat n'a pas changé.
- Les sorties de build (`bin`, `obj`, `dist`) et les worktrees vont sur un SSD : un disque
  dur partagé sature pendant un `dotnet test` ou un `npm ci`.
