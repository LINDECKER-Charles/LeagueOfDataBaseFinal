<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/assets/hero-dark.svg">
    <img src="docs/assets/hero-light.svg" alt="League of Database — explorateur de données League of Legends" width="880">
  </picture>
</p>

<p align="center">
  <a href="https://creativecommons.org/licenses/by-nc/4.0/"><img src="https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg" alt="License"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET"></a>
  <a href="https://angular.dev/"><img src="https://img.shields.io/badge/Angular-22-DD0031.svg?logo=angular&logoColor=white" alt="Angular"></a>
  <a href="https://www.postgresql.org/"><img src="https://img.shields.io/badge/PostgreSQL-17-4169E1.svg?logo=postgresql&logoColor=white" alt="PostgreSQL"></a>
  <a href="https://www.docker.com/"><img src="https://img.shields.io/badge/Docker-Compose-2496ED.svg?logo=docker&logoColor=white" alt="Docker"></a>
</p>

<p align="center">
  <b>Explorateur des données <a href="https://leagueoflegends.com">League of Legends</a></b> : champions,
  objets, runes et sorts d'invocateur,<br>pour <b>chaque version</b> et <b>chaque langue</b> du jeu.
  Un seul front pour le web, le desktop et Android.
</p>

---

> [!NOTE]
> **Bascule en cours.** Ce dépôt porte la réécriture .NET 10 + Angular 22. Le site en
> production tourne encore sur l'ancienne stack (Symfony + Go + Vue), archivée sous
> [`legacy/`](legacy/README.md) jusqu'à la décommission ; la bascule suit le
> [runbook](docs/reecriture/bascule.md).

## Stack

| Couche | Techno |
|---|---|
| API et tâches de fond | ASP.NET Core 10, un seul hôte `LoDb.Api` (`/api`, `/v1`, `/webhooks`, ingestion Data Dragon) |
| Données | PostgreSQL 17 + EF Core ; blobs adressés par contenu sur le volume `storage` |
| Web | Angular 22 en SSR (Node 24), locale dans l'URL (`/fr/…`), 21 langues |
| Apps | Desktop Photino + Velopack (Windows, macOS) ; Android Capacitor 8 |
| Frontal | nginx (cache des pages, `/cdn/blobs/`) derrière l'edge Caddy du VPS |

Décisions et architecture : [`docs/reecriture/`](docs/reecriture/README.md) (ADR 0001 à 0010).

## Démarrage rapide

Prérequis : .NET SDK 10.0.400, Node 24 (ou `^22.22.3`, `>= 26`), Docker. Aucun `.env`
n'est nécessaire en local.

```bash
npm ci --prefix src/LoDb.Web
docker compose -p lodb-next -f compose.next.yaml -f compose.next.override.yaml up -d --build
# site http://localhost:18080/en/ · API :18081 · Mailpit :18025
```

Build et tests :

```bash
dotnet build LoDb.slnx -c Release   # 0 avertissement
dotnet test LoDb.slnx               # Testcontainers : Docker démarré
npm --prefix src/LoDb.Web run lint
npm --prefix src/LoDb.Web run test
```

Ports, emplacements, E2E et dépannage : [`docs/guides/dev-next.md`](docs/guides/dev-next.md).

## Arborescence

```
src/LoDb.Domain/          règles pures : versions, langues, éditions
src/LoDb.Ingestion/       egress Data Dragon, pipeline, catalogue
src/LoDb.Infrastructure/  persistance, stockage, tâches, outbox, analytics
src/LoDb.Api/             hôte unique, modules, CLI, workers
src/LoDb.Web/             workspace Angular (web, desktop, Android)
src/LoDb.Desktop/         coquille desktop
tests/                    tests .NET, E2E Playwright, fixtures
docker/next/  compose.next*.yaml   images et stack Compose
tools/next/               outils : contrat, i18n, parité, bascule, releases
legacy/                   ancienne stack, archivée
```

## Documentation

| Doc | Contenu |
|---|---|
| [`docs/reecriture/README.md`](docs/reecriture/README.md) | Pourquoi la réécriture, décisions, architecture cible |
| [`docs/guides/dev-next.md`](docs/guides/dev-next.md) | Développer : commandes, stack locale, E2E |
| [`docs/guides/configuration.md`](docs/guides/configuration.md) | Tous les secrets et variables à configurer (`next`, prod, apps) |
| [`docs/guides/github-actions-secrets.md`](docs/guides/github-actions-secrets.md) | Pipeline : CI, images, déploiement de `next`, promotion en prod |
| [`docs/reecriture/bascule.md`](docs/reecriture/bascule.md) | Runbook de la bascule et du retour arrière |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Contribuer : branches, commits, garde-fous |
| [`docs/README.md`](docs/README.md) | Index complet de la documentation |

## Licence

**Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0)**, voir
[`LICENSE`](LICENSE). Les assets de Riot Games (Data Dragon, CommunityDragon) ne sont pas
couverts par cette licence et restent la propriété de Riot Games, Inc.
