# Documentation — LeagueOfDataBase

Index de la documentation du dépôt. Les conventions de code et les invariants
d'architecture font foi dans [`../CLAUDE.md`](../CLAUDE.md) ; le point d'entrée
contributeur est [`../CONTRIBUTING.md`](../CONTRIBUTING.md).

## Organisation

    docs/
      README.md          ← ce fichier
      contribution.md    ← guide contributeur détaillé (FR / EN / ES)
      architecture/      ← comment le système fonctionne
      guides/            ← comment l'installer, le configurer, l'exploiter
      audits/            ← constats datés et plans de correction
      briefs/            ← briefs de design
      produit/           ← veille, SEO, communication
      changelog/         ← journal technique interne (une entrée par livraison)
      assets/            ← images et icônes utilisées par le README

## `architecture/` — comment ça marche

| Doc | Contenu |
|---|---|
| [architecture.md](architecture/architecture.md) | Vue d'ensemble : services, flux, arborescence |
| [architecture-report.md](architecture/architecture-report.md) | Rapport d'architecture — état avant/après refacto (DRY / SOLID / KISS) |
| [analytics.md](architecture/analytics.md) | Analytics sans base de données (NDJSON local → agrégats MinIO) et panneau `/admin` |
| [api-publique.md](architecture/api-publique.md) | API REST v1 payante servie par le micro-service Go `go-api` |
| [responsive-mobile.md](architecture/responsive-mobile.md) | Stratégie responsive, breakpoints, composants mobile |

## `guides/` — comment l'exploiter

| Doc | Contenu |
|---|---|
| [setup.md](guides/setup.md) | Prérequis, installation détaillée, dépannage |
| [configuration.md](guides/configuration.md) | Variables d'environnement et paramètres applicatifs |
| [docker.md](guides/docker.md) | Référence des commandes de la stack Compose |
| [github-actions-secrets.md](guides/github-actions-secrets.md) | Secrets GitHub Actions / GHCR, flux de promotion CI/CD |
| [migration-edge-proxy.md](guides/migration-edge-proxy.md) | Passage à l'edge proxy partagé (VPS mono-hôte multi-projets) |
| [oauth-google-setup.md](guides/oauth-google-setup.md) | Configuration « Sign in with Google » côté Google Cloud Console |
| [legal-info.md](guides/legal-info.md) | Checklist des informations légales à trancher avant la prod |
| [packaging-apk.md](guides/packaging-apk.md) | Distribution Android (TWA / APK) à partir de la PWA |

## `audits/` — constats datés

| Doc | Contenu |
|---|---|
| [performance-audit.md](audits/performance-audit.md) | Audit de la chaîne de chargement DDragon et du cache |
| [performance-fix-prompt.md](audits/performance-fix-prompt.md) | Plan de correction dérivé de l'audit ci-dessus |
| [version-compatibility-audit.md](audits/version-compatibility-audit.md) | Compatibilité des versions Data Dragon — diagnostic et correctifs |

> `report/` (métriques de conformité aux règles chiffrées de `CLAUDE.md`) est
> **généré** par le skill `archi-report` et git-ignoré : ne pas l'éditer à la main.

## `briefs/` — briefs de design

| Doc | Contenu |
|---|---|
| [brief-design-pages.md](briefs/brief-design-pages.md) | Contenu attendu par page + charte « Hextech » |
| [brief-design-prompt.md](briefs/brief-design-prompt.md) | Prompt de mission accompagnant le brief |

## `produit/` — veille et diffusion

| Doc | Contenu |
|---|---|
| [analyse-concurrentielle.md](produit/analyse-concurrentielle.md) | Cartographie de l'écosystème data League of Legends |
| [seo-indexabilite.md](produit/seo-indexabilite.md) | État des lieux et feuille de route SEO technique |
| [seo-backlinking.md](produit/seo-backlinking.md) | Stratégie d'acquisition de liens |

## `changelog/` — journal technique

Une entrée par feature ou correctif livré, jointe au commit correspondant.
Format et workflow : [`changelog/README.md`](changelog/README.md), gabarit
[`changelog/TEMPLATE.md`](changelog/TEMPLATE.md).
